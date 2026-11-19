using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Tablo.Api.Data;
using Tablo.Api.DTOs;
using Tablo.Api.Entities;

namespace Tablo.Api.Services;

public partial class InventoryService
{
    // ==========================================
    // 1. SUPPLIER MANAGEMENT
    // ==========================================

    public async Task<List<SupplierDto>> GetSuppliersAsync(Guid restaurantId, bool includeInactive = true)
    {
        var query = db.Suppliers.AsNoTracking()
            .Where(s => s.RestaurantId == restaurantId);

        if (!includeInactive)
            query = query.Where(s => s.IsActive);

        var suppliers = await query
            .Include(s => s.SupplierIngredients)
            .Include(s => s.PurchaseInvoices)
            .OrderBy(s => s.Name)
            .ToListAsync();

        return suppliers.Select(s => new SupplierDto(
            s.Id,
            s.Name,
            s.ContactPerson,
            s.Email,
            s.Phone,
            s.Address,
            s.TaxId,
            s.IsActive,
            s.SupplierIngredients.Count,
            s.PurchaseInvoices.Count,
            s.CreatedAtUtc
        )).ToList();
    }

    public async Task<SupplierDto> CreateSupplierAsync(Guid restaurantId, CreateSupplierDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new ArgumentException("Supplier name is required.");

        var exists = await db.Suppliers.AnyAsync(s =>
            s.RestaurantId == restaurantId && s.Name.ToLower() == dto.Name.Trim().ToLower());
        if (exists)
            throw new ServiceConflictException($"Supplier '{dto.Name.Trim()}' already exists.");

        var supplier = new Supplier
        {
            RestaurantId = restaurantId,
            Name = dto.Name.Trim(),
            ContactPerson = dto.ContactPerson?.Trim(),
            Email = dto.Email?.Trim(),
            Phone = dto.Phone?.Trim(),
            Address = dto.Address?.Trim(),
            TaxId = dto.TaxId?.Trim(),
            IsActive = true
        };

        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync();

        return new SupplierDto(
            supplier.Id,
            supplier.Name,
            supplier.ContactPerson,
            supplier.Email,
            supplier.Phone,
            supplier.Address,
            supplier.TaxId,
            supplier.IsActive,
            0,
            0,
            supplier.CreatedAtUtc
        );
    }

    public async Task<SupplierDto> UpdateSupplierAsync(Guid restaurantId, Guid id, UpdateSupplierDto dto)
    {
        var supplier = await db.Suppliers
            .Include(s => s.SupplierIngredients)
            .Include(s => s.PurchaseInvoices)
            .SingleOrDefaultAsync(s => s.RestaurantId == restaurantId && s.Id == id);

        if (supplier == null)
            throw new KeyNotFoundException("Supplier not found.");

        if (!string.IsNullOrWhiteSpace(dto.Name))
            supplier.Name = dto.Name.Trim();

        if (dto.ContactPerson != null) supplier.ContactPerson = dto.ContactPerson.Trim();
        if (dto.Email != null) supplier.Email = dto.Email.Trim();
        if (dto.Phone != null) supplier.Phone = dto.Phone.Trim();
        if (dto.Address != null) supplier.Address = dto.Address.Trim();
        if (dto.TaxId != null) supplier.TaxId = dto.TaxId.Trim();
        if (dto.IsActive.HasValue) supplier.IsActive = dto.IsActive.Value;
        supplier.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync();

        return new SupplierDto(
            supplier.Id,
            supplier.Name,
            supplier.ContactPerson,
            supplier.Email,
            supplier.Phone,
            supplier.Address,
            supplier.TaxId,
            supplier.IsActive,
            supplier.SupplierIngredients.Count,
            supplier.PurchaseInvoices.Count,
            supplier.CreatedAtUtc
        );
    }

    public async Task<List<SupplierIngredientDto>> GetSupplierIngredientsAsync(Guid restaurantId, Guid supplierId)
    {
        var items = await db.SupplierIngredients.AsNoTracking()
            .Include(si => si.Supplier)
            .Include(si => si.Ingredient)
            .Where(si => si.RestaurantId == restaurantId && si.SupplierId == supplierId)
            .OrderBy(si => si.Ingredient.Name)
            .ToListAsync();

        return items.Select(si => new SupplierIngredientDto(
            si.Id,
            si.SupplierId,
            si.Supplier.Name,
            si.IngredientId,
            si.Ingredient.Name,
            si.Ingredient.BaseUnit,
            si.PackUnit,
            si.PackQuantity,
            si.PackUnitType,
            si.ConversionFactor,
            si.LastPurchasePricePerPack,
            si.NormalizedPricePerBaseUnit
        )).ToList();
    }

    public async Task<SupplierIngredientDto> SetSupplierIngredientAsync(Guid restaurantId, SetSupplierIngredientDto dto)
    {
        var supplier = await db.Suppliers.SingleOrDefaultAsync(s => s.RestaurantId == restaurantId && s.Id == dto.SupplierId);
        if (supplier == null) throw new KeyNotFoundException("Supplier not found.");

        var ingredient = await db.Ingredients.SingleOrDefaultAsync(i => i.RestaurantId == restaurantId && i.Id == dto.IngredientId);
        if (ingredient == null) throw new KeyNotFoundException("Ingredient not found.");

        var conversionFactor = dto.ConversionFactor > 0
            ? dto.ConversionFactor
            : UnitConverter.NormalizeToBase(dto.PackQuantity, dto.PackUnitType, ingredient.BaseUnit);

        if (conversionFactor <= 0) conversionFactor = 1m;

        var normalizedPrice = conversionFactor > 0
            ? Math.Round(dto.LastPurchasePricePerPack / conversionFactor, 6)
            : 0m;

        var existing = await db.SupplierIngredients
            .SingleOrDefaultAsync(si => si.RestaurantId == restaurantId && si.SupplierId == dto.SupplierId && si.IngredientId == dto.IngredientId);

        if (existing != null)
        {
            existing.PackUnit = dto.PackUnit.Trim();
            existing.PackQuantity = dto.PackQuantity;
            existing.PackUnitType = dto.PackUnitType;
            existing.ConversionFactor = conversionFactor;
            existing.LastPurchasePricePerPack = dto.LastPurchasePricePerPack;
            existing.NormalizedPricePerBaseUnit = normalizedPrice;
            existing.UpdatedAtUtc = DateTime.UtcNow;
        }
        else
        {
            existing = new SupplierIngredient
            {
                RestaurantId = restaurantId,
                SupplierId = dto.SupplierId,
                IngredientId = dto.IngredientId,
                PackUnit = dto.PackUnit.Trim(),
                PackQuantity = dto.PackQuantity,
                PackUnitType = dto.PackUnitType,
                ConversionFactor = conversionFactor,
                LastPurchasePricePerPack = dto.LastPurchasePricePerPack,
                NormalizedPricePerBaseUnit = normalizedPrice,
                UpdatedAtUtc = DateTime.UtcNow
            };
            db.SupplierIngredients.Add(existing);
        }

        await db.SaveChangesAsync();

        return new SupplierIngredientDto(
            existing.Id,
            existing.SupplierId,
            supplier.Name,
            existing.IngredientId,
            ingredient.Name,
            ingredient.BaseUnit,
            existing.PackUnit,
            existing.PackQuantity,
            existing.PackUnitType,
            existing.ConversionFactor,
            existing.LastPurchasePricePerPack,
            existing.NormalizedPricePerBaseUnit
        );
    }

    public async Task<IngredientPriceComparisonDto> CompareIngredientPricesAsync(Guid restaurantId, Guid ingredientId)
    {
        var ingredient = await db.Ingredients.AsNoTracking()
            .SingleOrDefaultAsync(i => i.RestaurantId == restaurantId && i.Id == ingredientId);
        if (ingredient == null) throw new KeyNotFoundException("Ingredient not found.");

        var records = await db.SupplierIngredients.AsNoTracking()
            .Include(si => si.Supplier)
            .Where(si => si.RestaurantId == restaurantId && si.IngredientId == ingredientId && si.Supplier.IsActive)
            .OrderBy(si => si.NormalizedPricePerBaseUnit)
            .ToListAsync();

        var list = records.Select(r => new SupplierPriceComparisonItemDto(
            r.SupplierId,
            r.Supplier.Name,
            r.PackUnit,
            r.PackQuantity,
            r.LastPurchasePricePerPack,
            r.NormalizedPricePerBaseUnit,
            r.UpdatedAtUtc
        )).ToList();

        return new IngredientPriceComparisonDto(
            ingredient.Id,
            ingredient.Name,
            ingredient.BaseUnit,
            list
        );
    }

    // ==========================================
    // 2. PURCHASE ORDERS, INVOICES & RECEIVING
    // ==========================================

    public async Task<List<PurchaseOrderDto>> GetPurchaseOrdersAsync(Guid restaurantId, string? status = null)
    {
        var query = db.PurchaseOrders.AsNoTracking()
            .Include(po => po.Supplier)
            .Include(po => po.Items).ThenInclude(poi => poi.Ingredient)
            .Where(po => po.RestaurantId == restaurantId);

        if (!string.IsNullOrEmpty(status))
            query = query.Where(po => po.Status == status);

        var list = await query.OrderByDescending(po => po.OrderDateUtc).ToListAsync();

        return list.Select(ProjectPurchaseOrder).ToList();
    }

    public async Task<PurchaseOrderDto> CreatePurchaseOrderAsync(Guid restaurantId, Guid actorId, CreatePurchaseOrderDto dto)
    {
        if (dto.Items.Count == 0)
            throw new ArgumentException("Purchase order must contain at least one line item.");

        var supplier = await db.Suppliers.SingleOrDefaultAsync(s => s.RestaurantId == restaurantId && s.Id == dto.SupplierId);
        if (supplier == null) throw new KeyNotFoundException("Supplier not found.");

        var orderCount = await db.PurchaseOrders.CountAsync(po => po.RestaurantId == restaurantId);
        var orderNumber = $"PO-{DateTime.UtcNow:yyyyMM}-{(orderCount + 1):D4}";

        var po = new PurchaseOrder
        {
            RestaurantId = restaurantId,
            OrderNumber = orderNumber,
            SupplierId = dto.SupplierId,
            Status = "Draft",
            OrderDateUtc = DateTime.UtcNow,
            ExpectedDeliveryDateUtc = dto.ExpectedDeliveryDateUtc,
            CreatedByEmployeeId = actorId != Guid.Empty ? actorId : null,
            Notes = dto.Notes?.Trim()
        };

        decimal total = 0m;
        foreach (var item in dto.Items)
        {
            var lineTotal = Math.Round(item.OrderedPackQuantity * item.UnitPricePerPack, 2);
            total += lineTotal;
            po.Items.Add(new PurchaseOrderItem
            {
                IngredientId = item.IngredientId,
                PackUnit = item.PackUnit.Trim(),
                OrderedPackQuantity = item.OrderedPackQuantity,
                ReceivedPackQuantity = 0m,
                UnitPricePerPack = item.UnitPricePerPack,
                LineTotal = lineTotal,
                ConversionFactor = item.ConversionFactor > 0 ? item.ConversionFactor : 1m
            });
        }
        po.TotalAmount = total;

        db.PurchaseOrders.Add(po);
        await db.SaveChangesAsync();

        return await GetPurchaseOrderByIdAsync(restaurantId, po.Id);
    }

    public async Task<PurchaseOrderDto> UpdatePurchaseOrderStatusAsync(Guid restaurantId, Guid actorId, Guid id, string newStatus)
    {
        var po = await db.PurchaseOrders
            .Include(p => p.Items).ThenInclude(i => i.Ingredient)
            .Include(p => p.Supplier)
            .SingleOrDefaultAsync(p => p.RestaurantId == restaurantId && p.Id == id);

        if (po == null) throw new KeyNotFoundException("Purchase order not found.");

        po.Status = newStatus;
        if (newStatus == "Approved")
        {
            po.ApprovedAtUtc = DateTime.UtcNow;
            po.ApprovedByEmployeeId = actorId != Guid.Empty ? actorId : null;
        }
        po.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return ProjectPurchaseOrder(po);
    }

    public async Task<PurchaseOrderDto> GetPurchaseOrderByIdAsync(Guid restaurantId, Guid id)
    {
        var po = await db.PurchaseOrders.AsNoTracking()
            .Include(p => p.Supplier)
            .Include(p => p.Items).ThenInclude(i => i.Ingredient)
            .SingleOrDefaultAsync(p => p.RestaurantId == restaurantId && p.Id == id);

        if (po == null) throw new KeyNotFoundException("Purchase order not found.");
        return ProjectPurchaseOrder(po);
    }

    private static PurchaseOrderDto ProjectPurchaseOrder(PurchaseOrder po)
    {
        return new PurchaseOrderDto(
            po.Id,
            po.OrderNumber,
            po.SupplierId,
            po.Supplier?.Name ?? "Supplier",
            po.Status,
            po.OrderDateUtc,
            po.ExpectedDeliveryDateUtc,
            po.ApprovedAtUtc,
            po.TotalAmount,
            po.Notes,
            po.Items.Select(i => new PurchaseOrderItemDto(
                i.Id,
                i.IngredientId,
                i.Ingredient?.Name ?? "Ingredient",
                i.Ingredient?.BaseUnit ?? "g",
                i.PackUnit,
                i.OrderedPackQuantity,
                i.ReceivedPackQuantity,
                i.UnitPricePerPack,
                i.LineTotal,
                i.ConversionFactor
            )).ToList()
        );
    }

    public async Task<PurchaseInvoiceDto> ReceiveGoodsAsync(Guid restaurantId, Guid actorId, ReceiveGoodsDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.InvoiceNumber))
            throw new ArgumentException("Invoice number is required.");

        var trimmedInvoiceNumber = dto.InvoiceNumber.Trim();

        // Idempotency check 1: By OperationId
        if (dto.OperationId != Guid.Empty)
        {
            var existingByOp = await db.PurchaseInvoices.AsNoTracking()
                .Include(pi => pi.Supplier)
                .Include(pi => pi.PurchaseOrder)
                .Include(pi => pi.Items).ThenInclude(pii => pii.Ingredient)
                .Include(pi => pi.Items).ThenInclude(pii => pii.StockLot)
                .SingleOrDefaultAsync(pi => pi.RestaurantId == restaurantId && pi.OperationId == dto.OperationId);

            if (existingByOp != null)
                return ProjectPurchaseInvoice(existingByOp);
        }

        // Idempotency check 2: By (RestaurantId, SupplierId, InvoiceNumber)
        var existingByInvoice = await db.PurchaseInvoices.AsNoTracking()
            .Include(pi => pi.Supplier)
            .Include(pi => pi.PurchaseOrder)
            .Include(pi => pi.Items).ThenInclude(pii => pii.Ingredient)
            .Include(pi => pi.Items).ThenInclude(pii => pii.StockLot)
            .SingleOrDefaultAsync(pi => pi.RestaurantId == restaurantId && pi.SupplierId == dto.SupplierId && pi.InvoiceNumber == trimmedInvoiceNumber);

        if (existingByInvoice != null)
            return ProjectPurchaseInvoice(existingByInvoice);

        var supplier = await db.Suppliers.SingleOrDefaultAsync(s => s.RestaurantId == restaurantId && s.Id == dto.SupplierId);
        if (supplier == null) throw new KeyNotFoundException("Supplier not found.");

        PurchaseOrder? po = null;
        if (dto.PurchaseOrderId.HasValue)
        {
            po = await db.PurchaseOrders.Include(p => p.Items)
                .SingleOrDefaultAsync(p => p.RestaurantId == restaurantId && p.Id == dto.PurchaseOrderId.Value);
        }

        var invoice = new PurchaseInvoice
        {
            RestaurantId = restaurantId,
            SupplierId = dto.SupplierId,
            PurchaseOrderId = dto.PurchaseOrderId,
            InvoiceNumber = trimmedInvoiceNumber,
            InvoiceDate = dto.InvoiceDate,
            ReceivedAtUtc = DateTime.UtcNow,
            ReceivedByEmployeeId = actorId != Guid.Empty ? actorId : null,
            Subtotal = dto.Subtotal,
            DiscountAmount = dto.DiscountAmount,
            TaxAmount = dto.TaxAmount,
            ShippingCharges = dto.ShippingCharges,
            GrandTotal = dto.GrandTotal,
            Notes = dto.Notes?.Trim(),
            OperationId = dto.OperationId != Guid.Empty ? dto.OperationId : null
        };

        var ingredientIds = dto.Items.Select(x => x.IngredientId).Distinct().ToList();
        var ingredients = await db.Ingredients.Where(i => i.RestaurantId == restaurantId && ingredientIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id);

        // Calculate current on-hand quantities for moving average cost update
        var onHandBalances = await db.StockMovements.AsNoTracking()
            .Where(m => m.RestaurantId == restaurantId && ingredientIds.Contains(m.IngredientId))
            .GroupBy(m => m.IngredientId)
            .Select(g => new { IngredientId = g.Key, OnHand = g.Sum(m => m.QuantityChange) })
            .ToDictionaryAsync(x => x.IngredientId, x => x.OnHand);

        foreach (var item in dto.Items)
        {
            if (!ingredients.TryGetValue(item.IngredientId, out var ing))
                continue;

            var conversionFactor = item.ConversionFactor > 0 ? item.ConversionFactor : 1m;
            var baseQuantity = item.ReceivedPackQuantity * conversionFactor;
            var unitCostPerBaseUnit = baseQuantity > 0 ? Math.Round(item.TotalLineCost / baseQuantity, 6) : 0m;

            var lotCode = !string.IsNullOrWhiteSpace(item.LotCode)
                ? item.LotCode.Trim()
                : $"LOT-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}";

            var stockLot = new StockLot
            {
                RestaurantId = restaurantId,
                IngredientId = item.IngredientId,
                LotCode = lotCode,
                InitialQuantity = baseQuantity,
                AvailableQuantity = baseQuantity,
                UnitCost = unitCostPerBaseUnit,
                ReceivedOrProducedDateUtc = invoice.ReceivedAtUtc,
                ExpiryDate = item.ExpiryDate,
                Status = "Active",
                SourceType = "Purchase",
                SourceReferenceId = trimmedInvoiceNumber
            };
            db.StockLots.Add(stockLot);

            var invoiceItem = new PurchaseInvoiceItem
            {
                IngredientId = item.IngredientId,
                PurchaseOrderItemId = item.PurchaseOrderItemId,
                PackUnit = item.PackUnit.Trim(),
                ReceivedPackQuantity = item.ReceivedPackQuantity,
                ConversionFactor = conversionFactor,
                BaseQuantity = baseQuantity,
                PackPrice = item.PackPrice,
                LineDiscount = item.LineDiscount,
                LineTax = item.LineTax,
                TotalLineCost = item.TotalLineCost,
                UnitCostPerBaseUnit = unitCostPerBaseUnit,
                StockLot = stockLot
            };
            invoice.Items.Add(invoiceItem);

            // Post Stock Movement (Reason = "Purchase")
            var movement = new StockMovement
            {
                RestaurantId = restaurantId,
                IngredientId = item.IngredientId,
                ActorEmployeeId = actorId != Guid.Empty ? actorId : null,
                TimestampUtc = invoice.ReceivedAtUtc,
                QuantityChange = baseQuantity,
                UnitSnapshot = ing.BaseUnit,
                Reason = "Purchase",
                UnitCostSnapshot = unitCostPerBaseUnit,
                TotalCost = item.TotalLineCost,
                ReferenceType = "PurchaseInvoice",
                ReferenceId = invoice.Id.ToString(),
                StockLot = stockLot,
                Notes = $"Received invoice {trimmedInvoiceNumber} ({item.ReceivedPackQuantity} {item.PackUnit})"
            };
            db.StockMovements.Add(movement);

            // Recalculate moving weighted average acquisition cost
            var currentOnHand = onHandBalances.GetValueOrDefault(item.IngredientId, 0m);
            if (currentOnHand < 0) currentOnHand = 0m;

            var oldTotalValuation = currentOnHand * ing.CurrentAverageCost;
            var newTotalQuantity = currentOnHand + baseQuantity;
            var newTotalValuation = oldTotalValuation + item.TotalLineCost;

            ing.CurrentAverageCost = newTotalQuantity > 0 ? Math.Round(newTotalValuation / newTotalQuantity, 6) : unitCostPerBaseUnit;
            ing.UpdatedAtUtc = DateTime.UtcNow;

            // Update SupplierIngredient last price snapshot
            var supIng = await db.SupplierIngredients.SingleOrDefaultAsync(si =>
                si.RestaurantId == restaurantId && si.SupplierId == dto.SupplierId && si.IngredientId == item.IngredientId);
            if (supIng != null)
            {
                supIng.LastPurchasePricePerPack = item.PackPrice;
                supIng.NormalizedPricePerBaseUnit = unitCostPerBaseUnit;
                supIng.UpdatedAtUtc = DateTime.UtcNow;
            }

            // If PO line exists, update received pack quantity
            if (po != null && item.PurchaseOrderItemId.HasValue)
            {
                var poItem = po.Items.FirstOrDefault(i => i.Id == item.PurchaseOrderItemId.Value);
                if (poItem != null)
                {
                    poItem.ReceivedPackQuantity += item.ReceivedPackQuantity;
                }
            }
        }

        // Update PO status if applicable
        if (po != null)
        {
            var allComplete = po.Items.All(i => i.ReceivedPackQuantity >= i.OrderedPackQuantity);
            var anyReceived = po.Items.Any(i => i.ReceivedPackQuantity > 0);
            po.Status = allComplete ? "Received" : (anyReceived ? "PartiallyReceived" : po.Status);
            po.UpdatedAtUtc = DateTime.UtcNow;
        }

        db.PurchaseInvoices.Add(invoice);
        await db.SaveChangesAsync();

        return await GetInvoiceByIdAsync(restaurantId, invoice.Id);
    }

    public async Task<List<PurchaseInvoiceDto>> GetInvoicesAsync(Guid restaurantId, Guid? supplierId = null)
    {
        var query = db.PurchaseInvoices.AsNoTracking()
            .Include(pi => pi.Supplier)
            .Include(pi => pi.PurchaseOrder)
            .Include(pi => pi.Items).ThenInclude(pii => pii.Ingredient)
            .Include(pi => pi.Items).ThenInclude(pii => pii.StockLot)
            .Where(pi => pi.RestaurantId == restaurantId);

        if (supplierId.HasValue)
            query = query.Where(pi => pi.SupplierId == supplierId.Value);

        var list = await query.OrderByDescending(pi => pi.InvoiceDate).ToListAsync();
        return list.Select(ProjectPurchaseInvoice).ToList();
    }

    public async Task<PurchaseInvoiceDto> GetInvoiceByIdAsync(Guid restaurantId, Guid id)
    {
        var pi = await db.PurchaseInvoices.AsNoTracking()
            .Include(p => p.Supplier)
            .Include(p => p.PurchaseOrder)
            .Include(p => p.Items).ThenInclude(pii => pii.Ingredient)
            .Include(p => p.Items).ThenInclude(pii => pii.StockLot)
            .SingleOrDefaultAsync(p => p.RestaurantId == restaurantId && p.Id == id);

        if (pi == null) throw new KeyNotFoundException("Invoice not found.");
        return ProjectPurchaseInvoice(pi);
    }

    private static PurchaseInvoiceDto ProjectPurchaseInvoice(PurchaseInvoice pi)
    {
        return new PurchaseInvoiceDto(
            pi.Id,
            pi.InvoiceNumber,
            pi.SupplierId,
            pi.Supplier?.Name ?? "Supplier",
            pi.PurchaseOrderId,
            pi.PurchaseOrder?.OrderNumber,
            pi.InvoiceDate,
            pi.ReceivedAtUtc,
            pi.Subtotal,
            pi.DiscountAmount,
            pi.TaxAmount,
            pi.ShippingCharges,
            pi.GrandTotal,
            pi.Notes,
            pi.Items.Select(i => new PurchaseInvoiceItemDto(
                i.Id,
                i.IngredientId,
                i.Ingredient?.Name ?? "Ingredient",
                i.Ingredient?.BaseUnit ?? "g",
                i.PackUnit,
                i.ReceivedPackQuantity,
                i.ConversionFactor,
                i.BaseQuantity,
                i.PackPrice,
                i.LineDiscount,
                i.LineTax,
                i.TotalLineCost,
                i.UnitCostPerBaseUnit,
                i.StockLotId,
                i.StockLot?.LotCode,
                i.ReturnedBaseQuantity
            )).ToList()
        );
    }

    // ==========================================
    // 3. PURCHASE RETURNS
    // ==========================================

    public async Task<PurchaseReturnDto> CreatePurchaseReturnAsync(Guid restaurantId, Guid actorId, CreatePurchaseReturnDto dto)
    {
        if (dto.Items.Count == 0)
            throw new ArgumentException("Purchase return must specify at least one item.");

        if (dto.OperationId != Guid.Empty)
        {
            var existing = await db.PurchaseReturns.AsNoTracking()
                .Include(pr => pr.Supplier)
                .Include(pr => pr.PurchaseInvoice)
                .Include(pr => pr.Items).ThenInclude(pri => pri.Ingredient)
                .SingleOrDefaultAsync(pr => pr.RestaurantId == restaurantId && pr.OperationId == dto.OperationId);

            if (existing != null)
                return ProjectPurchaseReturn(existing);
        }

        var invoice = await db.PurchaseInvoices
            .Include(pi => pi.Items).ThenInclude(pii => pii.StockLot)
            .Include(pi => pi.Supplier)
            .SingleOrDefaultAsync(pi => pi.RestaurantId == restaurantId && pi.Id == dto.PurchaseInvoiceId);

        if (invoice == null) throw new KeyNotFoundException("Purchase invoice not found.");

        var returnCount = await db.PurchaseReturns.CountAsync(pr => pr.RestaurantId == restaurantId);
        var returnNumber = $"PR-{DateTime.UtcNow:yyyyMM}-{(returnCount + 1):D4}";

        var purchaseReturn = new PurchaseReturn
        {
            RestaurantId = restaurantId,
            SupplierId = invoice.SupplierId,
            PurchaseInvoiceId = invoice.Id,
            ReturnNumber = returnNumber,
            CreditNoteReference = dto.CreditNoteReference.Trim(),
            Reason = dto.Reason.Trim(),
            ReturnDateUtc = DateTime.UtcNow,
            ProcessedByEmployeeId = actorId != Guid.Empty ? actorId : null,
            Notes = dto.Notes?.Trim(),
            OperationId = dto.OperationId != Guid.Empty ? dto.OperationId : null
        };

        decimal totalCredit = 0m;
        foreach (var reqItem in dto.Items)
        {
            var invoiceItem = invoice.Items.FirstOrDefault(i => i.Id == reqItem.PurchaseInvoiceItemId);
            if (invoiceItem == null) throw new ArgumentException("Invoice line item not found.");

            var remainingReturnable = invoiceItem.BaseQuantity - invoiceItem.ReturnedBaseQuantity;
            if (reqItem.ReturnBaseQuantity > remainingReturnable)
                throw new ArgumentException($"Return quantity {reqItem.ReturnBaseQuantity} exceeds returnable quantity {remainingReturnable} for line item.");

            // Check lot availability if lot exists
            if (invoiceItem.StockLotId.HasValue)
            {
                var lot = await db.StockLots.SingleOrDefaultAsync(l => l.Id == invoiceItem.StockLotId.Value);
                if (lot != null && lot.AvailableQuantity < reqItem.ReturnBaseQuantity)
                    throw new ArgumentException($"Return quantity {reqItem.ReturnBaseQuantity} exceeds current available lot stock {lot.AvailableQuantity}.");

                if (lot != null)
                {
                    lot.AvailableQuantity -= reqItem.ReturnBaseQuantity;
                    if (lot.AvailableQuantity <= 0) lot.Status = "Exhausted";
                    lot.UpdatedAtUtc = DateTime.UtcNow;
                }
            }

            var lineCredit = Math.Round(reqItem.ReturnBaseQuantity * invoiceItem.UnitCostPerBaseUnit, 2);
            totalCredit += lineCredit;

            invoiceItem.ReturnedBaseQuantity += reqItem.ReturnBaseQuantity;

            purchaseReturn.Items.Add(new PurchaseReturnItem
            {
                PurchaseInvoiceItemId = invoiceItem.Id,
                IngredientId = invoiceItem.IngredientId,
                StockLotId = invoiceItem.StockLotId,
                ReturnQuantity = reqItem.ReturnBaseQuantity,
                UnitCostSnapshot = invoiceItem.UnitCostPerBaseUnit,
                TotalCredit = lineCredit,
                Reason = reqItem.Reason?.Trim() ?? dto.Reason
            });

            // Post reversing stock movement
            var ing = await db.Ingredients.SingleOrDefaultAsync(i => i.Id == invoiceItem.IngredientId);
            db.StockMovements.Add(new StockMovement
            {
                RestaurantId = restaurantId,
                IngredientId = invoiceItem.IngredientId,
                ActorEmployeeId = actorId != Guid.Empty ? actorId : null,
                TimestampUtc = purchaseReturn.ReturnDateUtc,
                QuantityChange = -reqItem.ReturnBaseQuantity,
                UnitSnapshot = ing?.BaseUnit ?? "g",
                Reason = "PurchaseReturn",
                UnitCostSnapshot = invoiceItem.UnitCostPerBaseUnit,
                TotalCost = lineCredit,
                ReferenceType = "PurchaseReturn",
                ReferenceId = purchaseReturn.Id.ToString(),
                StockLotId = invoiceItem.StockLotId,
                Notes = $"Purchase return {returnNumber} (Credit note: {dto.CreditNoteReference})"
            });
        }

        purchaseReturn.TotalCreditAmount = totalCredit;
        db.PurchaseReturns.Add(purchaseReturn);

        await db.SaveChangesAsync();

        return await GetPurchaseReturnByIdAsync(restaurantId, purchaseReturn.Id);
    }

    public async Task<List<PurchaseReturnDto>> GetPurchaseReturnsAsync(Guid restaurantId)
    {
        var list = await db.PurchaseReturns.AsNoTracking()
            .Include(pr => pr.Supplier)
            .Include(pr => pr.PurchaseInvoice)
            .Include(pr => pr.Items).ThenInclude(pri => pri.Ingredient)
            .Where(pr => pr.RestaurantId == restaurantId)
            .OrderByDescending(pr => pr.ReturnDateUtc)
            .ToListAsync();

        return list.Select(ProjectPurchaseReturn).ToList();
    }

    public async Task<PurchaseReturnDto> GetPurchaseReturnByIdAsync(Guid restaurantId, Guid id)
    {
        var pr = await db.PurchaseReturns.AsNoTracking()
            .Include(p => p.Supplier)
            .Include(p => p.PurchaseInvoice)
            .Include(p => p.Items).ThenInclude(pri => pri.Ingredient)
            .SingleOrDefaultAsync(p => p.RestaurantId == restaurantId && p.Id == id);

        if (pr == null) throw new KeyNotFoundException("Purchase return not found.");
        return ProjectPurchaseReturn(pr);
    }

    private static PurchaseReturnDto ProjectPurchaseReturn(PurchaseReturn pr)
    {
        return new PurchaseReturnDto(
            pr.Id,
            pr.ReturnNumber,
            pr.SupplierId,
            pr.Supplier?.Name ?? "Supplier",
            pr.PurchaseInvoiceId,
            pr.PurchaseInvoice?.InvoiceNumber ?? "Invoice",
            pr.CreditNoteReference,
            pr.Reason,
            pr.ReturnDateUtc,
            pr.TotalCreditAmount,
            pr.Notes,
            pr.Items.Select(i => new PurchaseReturnItemDto(
                i.Id,
                i.IngredientId,
                i.Ingredient?.Name ?? "Ingredient",
                i.Ingredient?.BaseUnit ?? "g",
                i.ReturnQuantity,
                i.UnitCostSnapshot,
                i.TotalCredit,
                i.Reason
            )).ToList()
        );
    }

    // ==========================================
    // 4. PREPARED INGREDIENTS & MULTI-STAGE RECIPES
    // ==========================================

    public async Task<List<PreparationRecipeDto>> GetPreparationRecipesAsync(Guid restaurantId)
    {
        var recipes = await db.PreparationRecipes.AsNoTracking()
            .Include(pr => pr.OutputIngredient)
            .Include(pr => pr.InputItems).ThenInclude(pi => pi.InputIngredient)
            .Where(pr => pr.RestaurantId == restaurantId)
            .OrderBy(pr => pr.OutputIngredient.Name)
            .ToListAsync();

        return recipes.Select(r => new PreparationRecipeDto(
            r.Id,
            r.OutputIngredientId,
            r.OutputIngredient.Name,
            r.OutputIngredient.BaseUnit,
            r.ExpectedYieldQuantity,
            r.Notes,
            r.InputItems.Select(i =>
            {
                var rawQty = UnitConverter.CalculateRawQuantity(i.Quantity, i.YieldPercent);
                var extCost = Math.Round(rawQty * i.InputIngredient.CurrentAverageCost, 4);
                return new PreparationRecipeInputDto(
                    i.Id,
                    i.InputIngredientId,
                    i.InputIngredient.Name,
                    i.InputIngredient.BaseUnit,
                    i.Quantity,
                    i.YieldPercent,
                    i.InputIngredient.CurrentAverageCost,
                    extCost
                );
            }).ToList()
        )).ToList();
    }

    public async Task<PreparationRecipeDto> SavePreparationRecipeAsync(Guid restaurantId, SavePreparationRecipeDto dto)
    {
        if (dto.Inputs.Count == 0)
            throw new ArgumentException("Preparation recipe must have at least one input ingredient.");

        if (dto.ExpectedYieldQuantity <= 0)
            throw new ArgumentException("Expected yield quantity must be greater than zero.");

        var output = await db.Ingredients.SingleOrDefaultAsync(i => i.RestaurantId == restaurantId && i.Id == dto.OutputIngredientId);
        if (output == null) throw new KeyNotFoundException("Output ingredient not found.");

        // Check for self-reference or circular reference
        if (dto.Inputs.Any(i => i.InputIngredientId == dto.OutputIngredientId))
            throw new ArgumentException("Circular recipe detected: an ingredient cannot be an input to its own preparation.");

        // Check transitive circular references (DFS)
        var allPrepRecipes = await db.PreparationRecipes.AsNoTracking()
            .Include(pr => pr.InputItems)
            .Where(pr => pr.RestaurantId == restaurantId && pr.OutputIngredientId != dto.OutputIngredientId)
            .ToListAsync();

        var graph = new Dictionary<Guid, List<Guid>>();
        foreach (var r in allPrepRecipes)
        {
            graph[r.OutputIngredientId] = r.InputItems.Select(x => x.InputIngredientId).ToList();
        }
        graph[dto.OutputIngredientId] = dto.Inputs.Select(x => x.InputIngredientId).ToList();

        if (HasCycle(graph, dto.OutputIngredientId, new HashSet<Guid>(), new HashSet<Guid>()))
            throw new ArgumentException("Circular recipe detected: circular dependency chain found among preparation recipes.");

        var recipe = await db.PreparationRecipes
            .Include(pr => pr.InputItems)
            .SingleOrDefaultAsync(pr => pr.RestaurantId == restaurantId && pr.OutputIngredientId == dto.OutputIngredientId);

        if (recipe == null)
        {
            recipe = new PreparationRecipe
            {
                RestaurantId = restaurantId,
                OutputIngredientId = dto.OutputIngredientId,
                ExpectedYieldQuantity = dto.ExpectedYieldQuantity,
                Notes = dto.Notes?.Trim()
            };
            db.PreparationRecipes.Add(recipe);
        }
        else
        {
            recipe.ExpectedYieldQuantity = dto.ExpectedYieldQuantity;
            recipe.Notes = dto.Notes?.Trim();
            recipe.UpdatedAtUtc = DateTime.UtcNow;
            db.PreparationRecipeItems.RemoveRange(recipe.InputItems);
        }

        output.IsPrepared = true;
        output.UpdatedAtUtc = DateTime.UtcNow;

        foreach (var inp in dto.Inputs)
        {
            recipe.InputItems.Add(new PreparationRecipeItem
            {
                InputIngredientId = inp.InputIngredientId,
                Quantity = inp.Quantity,
                YieldPercent = inp.YieldPercent > 0 ? inp.YieldPercent : 100m
            });
        }

        await db.SaveChangesAsync();

        var saved = await db.PreparationRecipes.AsNoTracking()
            .Include(pr => pr.OutputIngredient)
            .Include(pr => pr.InputItems).ThenInclude(pi => pi.InputIngredient)
            .SingleAsync(pr => pr.Id == recipe.Id);

        return new PreparationRecipeDto(
            saved.Id,
            saved.OutputIngredientId,
            saved.OutputIngredient.Name,
            saved.OutputIngredient.BaseUnit,
            saved.ExpectedYieldQuantity,
            saved.Notes,
            saved.InputItems.Select(i => new PreparationRecipeInputDto(
                i.Id,
                i.InputIngredientId,
                i.InputIngredient.Name,
                i.InputIngredient.BaseUnit,
                i.Quantity,
                i.YieldPercent,
                i.InputIngredient.CurrentAverageCost,
                Math.Round(UnitConverter.CalculateRawQuantity(i.Quantity, i.YieldPercent) * i.InputIngredient.CurrentAverageCost, 4)
            )).ToList()
        );
    }

    private static bool HasCycle(Dictionary<Guid, List<Guid>> graph, Guid current, HashSet<Guid> visited, HashSet<Guid> recursionStack)
    {
        if (recursionStack.Contains(current)) return true;
        if (visited.Contains(current)) return false;

        visited.Add(current);
        recursionStack.Add(current);

        if (graph.TryGetValue(current, out var neighbors))
        {
            foreach (var neighbor in neighbors)
            {
                if (HasCycle(graph, neighbor, visited, recursionStack))
                    return true;
            }
        }

        recursionStack.Remove(current);
        return false;
    }

    public async Task<PreparationBatchDto> RecordPreparationBatchAsync(Guid restaurantId, Guid actorId, RecordPreparationBatchDto dto)
    {
        if (dto.ActualYieldQuantity <= 0)
            throw new ArgumentException("Actual yield quantity must be greater than zero.");

        if (dto.OperationId != Guid.Empty)
        {
            var existing = await db.PreparationBatches.AsNoTracking()
                .Include(pb => pb.OutputIngredient)
                .Include(pb => pb.OutputStockLot)
                .SingleOrDefaultAsync(pb => pb.RestaurantId == restaurantId && pb.OperationId == dto.OperationId);

            if (existing != null)
                return ProjectPreparationBatch(existing);
        }

        var recipe = await db.PreparationRecipes
            .Include(pr => pr.OutputIngredient)
            .Include(pr => pr.InputItems).ThenInclude(pi => pi.InputIngredient)
            .SingleOrDefaultAsync(pr => pr.RestaurantId == restaurantId && pr.Id == dto.PreparationRecipeId);

        if (recipe == null) throw new KeyNotFoundException("Preparation recipe not found.");

        var multiplier = dto.Multiplier > 0 ? dto.Multiplier : 1m;
        var expectedYield = recipe.ExpectedYieldQuantity * multiplier;

        decimal totalInputCost = 0m;
        var now = DateTime.UtcNow;

        // Deduct raw input ingredients and capitalize their costs
        foreach (var inp in recipe.InputItems)
        {
            var rawNeeded = UnitConverter.CalculateRawQuantity(inp.Quantity, inp.YieldPercent) * multiplier;
            var extCost = Math.Round(rawNeeded * inp.InputIngredient.CurrentAverageCost, 4);
            totalInputCost += extCost;

            // Post deduction movement for raw ingredient
            db.StockMovements.Add(new StockMovement
            {
                RestaurantId = restaurantId,
                IngredientId = inp.InputIngredientId,
                ActorEmployeeId = actorId != Guid.Empty ? actorId : null,
                TimestampUtc = now,
                QuantityChange = -rawNeeded,
                UnitSnapshot = inp.InputIngredient.BaseUnit,
                Reason = "PreparationInput",
                UnitCostSnapshot = inp.InputIngredient.CurrentAverageCost,
                TotalCost = extCost,
                ReferenceType = "PreparationRecipe",
                ReferenceId = recipe.Id.ToString(),
                Notes = $"Batch preparation input for {recipe.OutputIngredient.Name}"
            });
        }

        var costPerBaseUnit = Math.Round(totalInputCost / dto.ActualYieldQuantity, 6);

        // Create stock lot for prepared ingredient
        var lotCode = $"PREP-{now:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}";
        var stockLot = new StockLot
        {
            RestaurantId = restaurantId,
            IngredientId = recipe.OutputIngredientId,
            LotCode = lotCode,
            InitialQuantity = dto.ActualYieldQuantity,
            AvailableQuantity = dto.ActualYieldQuantity,
            UnitCost = costPerBaseUnit,
            ReceivedOrProducedDateUtc = now,
            ExpiryDate = dto.ExpiryDate,
            Status = "Active",
            SourceType = "PreparationBatch"
        };
        db.StockLots.Add(stockLot);

        // Add prepared ingredient to stock
        db.StockMovements.Add(new StockMovement
        {
            RestaurantId = restaurantId,
            IngredientId = recipe.OutputIngredientId,
            ActorEmployeeId = actorId != Guid.Empty ? actorId : null,
            TimestampUtc = now,
            QuantityChange = dto.ActualYieldQuantity,
            UnitSnapshot = recipe.OutputIngredient.BaseUnit,
            Reason = "PreparationOutput",
            UnitCostSnapshot = costPerBaseUnit,
            TotalCost = totalInputCost,
            ReferenceType = "PreparationRecipe",
            ReferenceId = recipe.Id.ToString(),
            StockLot = stockLot,
            Notes = $"Batch produced {dto.ActualYieldQuantity} {recipe.OutputIngredient.BaseUnit}"
        });

        // Recalculate moving average cost of output ingredient
        var onHand = await db.StockMovements.AsNoTracking()
            .Where(m => m.RestaurantId == restaurantId && m.IngredientId == recipe.OutputIngredientId)
            .SumAsync(m => m.QuantityChange);
        if (onHand < 0) onHand = 0m;

        var oldVal = onHand * recipe.OutputIngredient.CurrentAverageCost;
        var newVal = oldVal + totalInputCost;
        var newQty = onHand + dto.ActualYieldQuantity;

        recipe.OutputIngredient.CurrentAverageCost = newQty > 0 ? Math.Round(newVal / newQty, 6) : costPerBaseUnit;
        recipe.OutputIngredient.UpdatedAtUtc = now;

        var batch = new PreparationBatch
        {
            RestaurantId = restaurantId,
            PreparationRecipeId = recipe.Id,
            OutputIngredientId = recipe.OutputIngredientId,
            PreparedAtUtc = now,
            PreparedByEmployeeId = actorId != Guid.Empty ? actorId : null,
            Multiplier = multiplier,
            ExpectedYieldQuantity = expectedYield,
            ActualYieldQuantity = dto.ActualYieldQuantity,
            WasteQuantity = dto.WasteQuantity,
            WasteReason = dto.WasteReason?.Trim(),
            TotalInputCost = totalInputCost,
            CostPerBaseUnit = costPerBaseUnit,
            OutputStockLot = stockLot,
            OperationId = dto.OperationId != Guid.Empty ? dto.OperationId : null,
            Notes = dto.Notes?.Trim()
        };

        db.PreparationBatches.Add(batch);
        await db.SaveChangesAsync();

        return ProjectPreparationBatch(batch);
    }

    public async Task<List<PreparationBatchDto>> GetPreparationBatchesAsync(Guid restaurantId)
    {
        var list = await db.PreparationBatches.AsNoTracking()
            .Include(pb => pb.OutputIngredient)
            .Include(pb => pb.OutputStockLot)
            .Where(pb => pb.RestaurantId == restaurantId)
            .OrderByDescending(pb => pb.PreparedAtUtc)
            .ToListAsync();

        return list.Select(ProjectPreparationBatch).ToList();
    }

    private static PreparationBatchDto ProjectPreparationBatch(PreparationBatch b)
    {
        return new PreparationBatchDto(
            b.Id,
            b.PreparationRecipeId,
            b.OutputIngredientId,
            b.OutputIngredient?.Name ?? "Prepared Ingredient",
            b.OutputIngredient?.BaseUnit ?? "g",
            b.PreparedAtUtc,
            b.Multiplier,
            b.ExpectedYieldQuantity,
            b.ActualYieldQuantity,
            b.WasteQuantity,
            b.WasteReason,
            b.TotalInputCost,
            b.CostPerBaseUnit,
            b.OutputStockLotId,
            b.OutputStockLot?.LotCode
        );
    }

    // ==========================================
    // 5. LOTS & EXPIRY
    // ==========================================

    public async Task<List<StockLotDto>> GetLotsAsync(Guid restaurantId, string? status = null, bool? expiringSoon = null)
    {
        var query = db.StockLots.AsNoTracking()
            .Include(l => l.Ingredient)
            .Where(l => l.RestaurantId == restaurantId);

        if (!string.IsNullOrEmpty(status))
            query = query.Where(l => l.Status == status);

        var now = DateTime.UtcNow;
        var soonThreshold = now.AddDays(3);

        if (expiringSoon == true)
        {
            query = query.Where(l => l.ExpiryDate.HasValue && l.ExpiryDate.Value <= soonThreshold && l.Status == "Active");
        }

        var lots = await query.OrderBy(l => l.ExpiryDate ?? DateTime.MaxValue).ThenBy(l => l.LotCode).ToListAsync();

        return lots.Select(l =>
        {
            var isExpired = l.ExpiryDate.HasValue && l.ExpiryDate.Value < now;
            var isSoon = l.ExpiryDate.HasValue && !isExpired && l.ExpiryDate.Value <= soonThreshold;
            return new StockLotDto(
                l.Id,
                l.IngredientId,
                l.Ingredient?.Name ?? "Ingredient",
                l.Ingredient?.BaseUnit ?? "g",
                l.LotCode,
                l.InitialQuantity,
                l.AvailableQuantity,
                l.UnitCost,
                l.ReceivedOrProducedDateUtc,
                l.ExpiryDate,
                l.Status,
                l.SourceType,
                l.SourceReferenceId,
                isSoon,
                isExpired
            );
        }).ToList();
    }

    public async Task<StockLotDto> QuarantineLotAsync(Guid restaurantId, Guid actorId, Guid lotId, QuarantineLotDto dto)
    {
        var lot = await db.StockLots.Include(l => l.Ingredient)
            .SingleOrDefaultAsync(l => l.RestaurantId == restaurantId && l.Id == lotId);
        if (lot == null) throw new KeyNotFoundException("Lot not found.");

        lot.Status = "Quarantined";
        lot.Notes = $"Quarantined: {dto.Reason}. {lot.Notes}".Trim();
        lot.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync();

        return new StockLotDto(
            lot.Id,
            lot.IngredientId,
            lot.Ingredient?.Name ?? "Ingredient",
            lot.Ingredient?.BaseUnit ?? "g",
            lot.LotCode,
            lot.InitialQuantity,
            lot.AvailableQuantity,
            lot.UnitCost,
            lot.ReceivedOrProducedDateUtc,
            lot.ExpiryDate,
            lot.Status,
            lot.SourceType,
            lot.SourceReferenceId,
            false,
            lot.ExpiryDate.HasValue && lot.ExpiryDate.Value < DateTime.UtcNow
        );
    }

    public async Task<StockLotDto> WriteOffLotAsync(Guid restaurantId, Guid actorId, Guid lotId, WriteOffLotDto dto)
    {
        var lot = await db.StockLots.Include(l => l.Ingredient)
            .SingleOrDefaultAsync(l => l.RestaurantId == restaurantId && l.Id == lotId);
        if (lot == null) throw new KeyNotFoundException("Lot not found.");

        var writeOffQty = dto.Quantity > 0 ? Math.Min(dto.Quantity, lot.AvailableQuantity) : lot.AvailableQuantity;
        if (writeOffQty <= 0)
            throw new ArgumentException("Write off quantity must be greater than zero.");

        lot.AvailableQuantity -= writeOffQty;
        if (lot.AvailableQuantity <= 0)
        {
            lot.AvailableQuantity = 0;
            lot.Status = "WrittenOff";
        }
        lot.UpdatedAtUtc = DateTime.UtcNow;

        var totalLoss = Math.Round(writeOffQty * lot.UnitCost, 4);

        // Record stock loss in ledger
        db.StockMovements.Add(new StockMovement
        {
            RestaurantId = restaurantId,
            IngredientId = lot.IngredientId,
            ActorEmployeeId = actorId != Guid.Empty ? actorId : null,
            TimestampUtc = DateTime.UtcNow,
            QuantityChange = -writeOffQty,
            UnitSnapshot = lot.Ingredient?.BaseUnit ?? "g",
            Reason = "WriteOff",
            UnitCostSnapshot = lot.UnitCost,
            TotalCost = totalLoss,
            ReferenceType = "StockLot",
            ReferenceId = lot.Id.ToString(),
            StockLotId = lot.Id,
            Notes = $"Written off lot {lot.LotCode}: {dto.Reason}. {dto.Notes}".Trim()
        });

        await db.SaveChangesAsync();

        return new StockLotDto(
            lot.Id,
            lot.IngredientId,
            lot.Ingredient?.Name ?? "Ingredient",
            lot.Ingredient?.BaseUnit ?? "g",
            lot.LotCode,
            lot.InitialQuantity,
            lot.AvailableQuantity,
            lot.UnitCost,
            lot.ReceivedOrProducedDateUtc,
            lot.ExpiryDate,
            lot.Status,
            lot.SourceType,
            lot.SourceReferenceId,
            false,
            lot.ExpiryDate.HasValue && lot.ExpiryDate.Value < DateTime.UtcNow
        );
    }

    // ==========================================
    // 6. COSTING CONFIGURATION & SETTINGS
    // ==========================================

    public async Task<InventorySettingsDto> GetInventorySettingsAsync(Guid restaurantId)
    {
        var settings = await db.RestaurantSettings.SingleOrDefaultAsync(s => s.RestaurantId == restaurantId);
        return new InventorySettingsDto(
            settings?.CostingMethod ?? "WeightedAverage",
            settings?.AutoManageAvailability ?? false
        );
    }

    public async Task<InventorySettingsDto> UpdateInventorySettingsAsync(Guid restaurantId, UpdateInventorySettingsDto dto)
    {
        var settings = await db.RestaurantSettings.SingleOrDefaultAsync(s => s.RestaurantId == restaurantId);
        if (settings == null)
        {
            settings = new RestaurantSettings
            {
                RestaurantId = restaurantId,
                CostingMethod = dto.CostingMethod,
                AutoManageAvailability = dto.AutoManageAvailability
            };
            db.RestaurantSettings.Add(settings);
        }
        else
        {
            settings.CostingMethod = dto.CostingMethod;
            settings.AutoManageAvailability = dto.AutoManageAvailability;
        }

        await db.SaveChangesAsync();
        return new InventorySettingsDto(settings.CostingMethod, settings.AutoManageAvailability);
    }

    // ==========================================
    // 7. OUTLETS & TRANSFERS
    // ==========================================

    public async Task<List<OutletDto>> GetOutletsAsync(Guid restaurantId)
    {
        var outlets = await db.Outlets.AsNoTracking()
            .Where(o => o.RestaurantId == restaurantId)
            .OrderBy(o => o.Name)
            .ToListAsync();

        if (outlets.Count == 0)
        {
            // Auto-seed default main kitchen outlet for transparent single-outlet operation
            var defaultOutlet = new Outlet
            {
                RestaurantId = restaurantId,
                Name = "Main Kitchen",
                Code = "MAIN",
                IsCentralKitchen = false,
                IsActive = true
            };
            db.Outlets.Add(defaultOutlet);
            await db.SaveChangesAsync();
            outlets.Add(defaultOutlet);
        }

        return outlets.Select(o => new OutletDto(o.Id, o.Name, o.Code, o.IsCentralKitchen, o.IsActive, o.Address)).ToList();
    }

    public async Task<OutletDto> CreateOutletAsync(Guid restaurantId, CreateOutletDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.Code))
            throw new ArgumentException("Outlet name and code are required.");

        var exists = await db.Outlets.AnyAsync(o => o.RestaurantId == restaurantId && o.Code.ToLower() == dto.Code.Trim().ToLower());
        if (exists) throw new ServiceConflictException($"Outlet with code '{dto.Code.Trim()}' already exists.");

        var outlet = new Outlet
        {
            RestaurantId = restaurantId,
            Name = dto.Name.Trim(),
            Code = dto.Code.Trim().ToUpper(),
            IsCentralKitchen = dto.IsCentralKitchen,
            Address = dto.Address?.Trim(),
            IsActive = true
        };

        db.Outlets.Add(outlet);
        await db.SaveChangesAsync();

        return new OutletDto(outlet.Id, outlet.Name, outlet.Code, outlet.IsCentralKitchen, outlet.IsActive, outlet.Address);
    }

    public async Task<List<StockTransferDto>> GetTransfersAsync(Guid restaurantId)
    {
        var list = await db.StockTransfers.AsNoTracking()
            .Include(t => t.SourceOutlet)
            .Include(t => t.DestinationOutlet)
            .Include(t => t.Items).ThenInclude(i => i.Ingredient)
            .Where(t => t.RestaurantId == restaurantId)
            .OrderByDescending(t => t.CreatedAtUtc)
            .ToListAsync();

        return list.Select(ProjectStockTransfer).ToList();
    }

    public async Task<StockTransferDto> CreateTransferAsync(Guid restaurantId, Guid actorId, CreateStockTransferDto dto)
    {
        if (dto.SourceOutletId == dto.DestinationOutletId)
            throw new ArgumentException("Source and destination outlets must be different.");

        if (dto.Items.Count == 0)
            throw new ArgumentException("Transfer must contain at least one line item.");

        var transferCount = await db.StockTransfers.CountAsync(t => t.RestaurantId == restaurantId);
        var transferNumber = $"TR-{DateTime.UtcNow:yyyyMM}-{(transferCount + 1):D4}";

        var transfer = new StockTransfer
        {
            RestaurantId = restaurantId,
            TransferNumber = transferNumber,
            SourceOutletId = dto.SourceOutletId,
            DestinationOutletId = dto.DestinationOutletId,
            Status = "Requested",
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByEmployeeId = actorId != Guid.Empty ? actorId : null,
            Notes = dto.Notes?.Trim()
        };

        var ingIds = dto.Items.Select(x => x.IngredientId).ToList();
        var ingredients = await db.Ingredients.Where(i => i.RestaurantId == restaurantId && ingIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id);

        foreach (var item in dto.Items)
        {
            var unitCost = ingredients.TryGetValue(item.IngredientId, out var ing) ? ing.CurrentAverageCost : 0m;
            transfer.Items.Add(new StockTransferItem
            {
                IngredientId = item.IngredientId,
                RequestedQuantity = item.RequestedQuantity,
                DispatchedQuantity = 0m,
                ReceivedQuantity = 0m,
                DiscrepancyQuantity = 0m,
                UnitCostSnapshot = unitCost
            });
        }

        db.StockTransfers.Add(transfer);
        await db.SaveChangesAsync();

        return await GetTransferByIdAsync(restaurantId, transfer.Id);
    }

    public async Task<StockTransferDto> DispatchTransferAsync(Guid restaurantId, Guid actorId, Guid transferId, DispatchStockTransferDto dto)
    {
        var transfer = await db.StockTransfers
            .Include(t => t.Items).ThenInclude(i => i.Ingredient)
            .SingleOrDefaultAsync(t => t.RestaurantId == restaurantId && t.Id == transferId);

        if (transfer == null) throw new KeyNotFoundException("Transfer not found.");
        if (transfer.Status != "Requested" && transfer.Status != "Approved")
            throw new ArgumentException($"Cannot dispatch transfer with status '{transfer.Status}'.");

        var now = DateTime.UtcNow;
        transfer.Status = "Dispatched";
        transfer.DispatchedAtUtc = now;
        transfer.DispatchedByEmployeeId = actorId != Guid.Empty ? actorId : null;

        var dispatchMap = dto.Items.ToDictionary(x => x.ItemId, x => x.DispatchedQuantity);

        foreach (var item in transfer.Items)
        {
            var qty = dispatchMap.GetValueOrDefault(item.Id, item.RequestedQuantity);
            item.DispatchedQuantity = qty;

            // Deduct from Source Outlet
            db.StockMovements.Add(new StockMovement
            {
                RestaurantId = restaurantId,
                IngredientId = item.IngredientId,
                OutletId = transfer.SourceOutletId,
                ActorEmployeeId = actorId != Guid.Empty ? actorId : null,
                TimestampUtc = now,
                QuantityChange = -qty,
                UnitSnapshot = item.Ingredient?.BaseUnit ?? "g",
                Reason = "TransferOut",
                UnitCostSnapshot = item.UnitCostSnapshot,
                TotalCost = Math.Round(qty * item.UnitCostSnapshot, 4),
                ReferenceType = "StockTransfer",
                ReferenceId = transfer.Id.ToString(),
                Notes = $"Transferred out to outlet {transfer.DestinationOutletId}"
            });
        }

        await db.SaveChangesAsync();
        return await GetTransferByIdAsync(restaurantId, transfer.Id);
    }

    public async Task<StockTransferDto> ReceiveTransferAsync(Guid restaurantId, Guid actorId, Guid transferId, ReceiveStockTransferDto dto)
    {
        var transfer = await db.StockTransfers
            .Include(t => t.Items).ThenInclude(i => i.Ingredient)
            .SingleOrDefaultAsync(t => t.RestaurantId == restaurantId && t.Id == transferId);

        if (transfer == null) throw new KeyNotFoundException("Transfer not found.");
        if (transfer.Status != "Dispatched")
            throw new ArgumentException($"Cannot receive transfer with status '{transfer.Status}'.");

        var now = DateTime.UtcNow;
        transfer.Status = "Received";
        transfer.ReceivedAtUtc = now;
        transfer.ReceivedByEmployeeId = actorId != Guid.Empty ? actorId : null;

        var receiveMap = dto.Items.ToDictionary(x => x.ItemId);

        foreach (var item in transfer.Items)
        {
            receiveMap.TryGetValue(item.Id, out var rec);
            var receivedQty = rec != null ? rec.ReceivedQuantity : item.DispatchedQuantity;
            item.ReceivedQuantity = receivedQty;

            var discrepancy = item.DispatchedQuantity - receivedQty;
            item.DiscrepancyQuantity = discrepancy > 0 ? discrepancy : 0m;
            item.DiscrepancyReason = rec?.DiscrepancyReason?.Trim();

            // Add received stock to Destination Outlet
            db.StockMovements.Add(new StockMovement
            {
                RestaurantId = restaurantId,
                IngredientId = item.IngredientId,
                OutletId = transfer.DestinationOutletId,
                ActorEmployeeId = actorId != Guid.Empty ? actorId : null,
                TimestampUtc = now,
                QuantityChange = receivedQty,
                UnitSnapshot = item.Ingredient?.BaseUnit ?? "g",
                Reason = "TransferIn",
                UnitCostSnapshot = item.UnitCostSnapshot,
                TotalCost = Math.Round(receivedQty * item.UnitCostSnapshot, 4),
                ReferenceType = "StockTransferIn",
                ReferenceId = transfer.Id.ToString(),
                Notes = $"Received from outlet {transfer.SourceOutletId}"
            });

            // If discrepancy loss occurred in transit, record discrepancy movement
            if (item.DiscrepancyQuantity > 0)
            {
                db.StockMovements.Add(new StockMovement
                {
                    RestaurantId = restaurantId,
                    IngredientId = item.IngredientId,
                    OutletId = transfer.SourceOutletId,
                    ActorEmployeeId = actorId != Guid.Empty ? actorId : null,
                    TimestampUtc = now,
                    QuantityChange = -item.DiscrepancyQuantity,
                    UnitSnapshot = item.Ingredient?.BaseUnit ?? "g",
                    Reason = "TransferDiscrepancyLoss",
                    UnitCostSnapshot = item.UnitCostSnapshot,
                    TotalCost = Math.Round(item.DiscrepancyQuantity * item.UnitCostSnapshot, 4),
                    ReferenceType = "StockTransferDiscrepancy",
                    ReferenceId = transfer.Id.ToString(),
                    Notes = $"Transfer discrepancy loss: {item.DiscrepancyReason ?? "Damaged or lost in transit"}"
                });
            }
        }

        await db.SaveChangesAsync();
        return await GetTransferByIdAsync(restaurantId, transfer.Id);
    }

    public async Task<StockTransferDto> GetTransferByIdAsync(Guid restaurantId, Guid id)
    {
        var t = await db.StockTransfers.AsNoTracking()
            .Include(x => x.SourceOutlet)
            .Include(x => x.DestinationOutlet)
            .Include(x => x.Items).ThenInclude(i => i.Ingredient)
            .SingleOrDefaultAsync(x => x.RestaurantId == restaurantId && x.Id == id);

        if (t == null) throw new KeyNotFoundException("Transfer not found.");
        return ProjectStockTransfer(t);
    }

    private static StockTransferDto ProjectStockTransfer(StockTransfer t)
    {
        return new StockTransferDto(
            t.Id,
            t.TransferNumber,
            t.SourceOutletId,
            t.SourceOutlet?.Name ?? "Source Outlet",
            t.DestinationOutletId,
            t.DestinationOutlet?.Name ?? "Destination Outlet",
            t.Status,
            t.CreatedAtUtc,
            t.DispatchedAtUtc,
            t.ReceivedAtUtc,
            t.Notes,
            t.Items.Select(i => new StockTransferItemDto(
                i.Id,
                i.IngredientId,
                i.Ingredient?.Name ?? "Ingredient",
                i.Ingredient?.BaseUnit ?? "g",
                i.RequestedQuantity,
                i.DispatchedQuantity,
                i.ReceivedQuantity,
                i.DiscrepancyQuantity,
                i.DiscrepancyReason,
                i.UnitCostSnapshot
            )).ToList()
        );
    }

    // ==========================================
    // 8. MENU AVAILABILITY & STOCK RESERVATIONS
    // ==========================================

    public async Task<List<MenuItemAvailabilityDto>> GetMenuAvailabilityAsync(Guid restaurantId)
    {
        var menuItems = await db.MenuItems.AsNoTracking()
            .Where(m => m.RestaurantId == restaurantId && !m.IsDeleted)
            .OrderBy(m => m.Name)
            .ToListAsync();

        var recipes = await db.RecipeItems.AsNoTracking()
            .Include(r => r.Ingredient)
            .Where(r => r.RestaurantId == restaurantId)
            .ToListAsync();

        // Calculate on-hand stock
        var onHands = await db.StockMovements.AsNoTracking()
            .Where(m => m.RestaurantId == restaurantId)
            .GroupBy(m => m.IngredientId)
            .Select(g => new { IngredientId = g.Key, Qty = g.Sum(m => m.QuantityChange) })
            .ToDictionaryAsync(x => x.IngredientId, x => x.Qty);

        // Calculate active reservations
        var reservations = await db.StockReservations.AsNoTracking()
            .Where(r => r.RestaurantId == restaurantId && r.Status == "Active")
            .GroupBy(r => r.IngredientId)
            .Select(g => new { IngredientId = g.Key, Reserved = g.Sum(r => r.ReservedQuantity) })
            .ToDictionaryAsync(x => x.IngredientId, x => x.Reserved);

        var result = new List<MenuItemAvailabilityDto>();

        foreach (var mi in menuItems)
        {
            var itemRecipes = recipes.Where(r => r.MenuItemId == mi.Id).ToList();
            if (itemRecipes.Count == 0)
            {
                result.Add(new MenuItemAvailabilityDto(mi.Id, mi.Name, false, mi.IsAvailable, 999, new List<IngredientAvailabilityDto>()));
                continue;
            }

            int minPortions = int.MaxValue;
            var ingredientAvailabilities = new List<IngredientAvailabilityDto>();

            foreach (var r in itemRecipes)
            {
                var rawPerPortion = UnitConverter.CalculateRawQuantity(r.UsableQuantity, r.YieldPercent);
                var onHand = onHands.GetValueOrDefault(r.IngredientId, 0m);
                var reserved = reservations.GetValueOrDefault(r.IngredientId, 0m);
                var effectiveAvailable = onHand - reserved;
                if (effectiveAvailable < 0) effectiveAvailable = 0m;

                var portions = rawPerPortion > 0 ? (int)Math.Floor(effectiveAvailable / rawPerPortion) : 999;
                if (portions < minPortions) minPortions = portions;

                ingredientAvailabilities.Add(new IngredientAvailabilityDto(
                    r.IngredientId,
                    r.Ingredient?.Name ?? "Ingredient",
                    r.Ingredient?.BaseUnit ?? "g",
                    rawPerPortion,
                    effectiveAvailable,
                    portions
                ));
            }

            if (minPortions == int.MaxValue) minPortions = 0;
            var isAvailable = minPortions > 0 && mi.IsAvailable;

            result.Add(new MenuItemAvailabilityDto(
                mi.Id,
                mi.Name,
                true,
                isAvailable,
                minPortions,
                ingredientAvailabilities
            ));
        }

        return result;
    }
}
