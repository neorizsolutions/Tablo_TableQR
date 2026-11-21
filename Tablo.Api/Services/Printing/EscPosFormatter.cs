using System.Text;
using System.Text.Json;
using Tablo.Api.DTOs;

namespace Tablo.Api.Services.Printing;

public static class EscPosFormatter
{
    private static readonly TimeZoneInfo IstZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");

    public static string FormatIstDateTime(DateTime utcTime)
    {
        try
        {
            var ist = TimeZoneInfo.ConvertTimeFromUtc(utcTime, IstZone);
            return ist.ToString("dd-MMM-yyyy, hh:mm tt") + " IST";
        }
        catch
        {
            // Fallback if system tz id differs
            var ist = utcTime.AddMinutes(330);
            return ist.ToString("dd-MMM-yyyy, hh:mm tt") + " IST";
        }
    }

    public static (byte[] EscPosBytes, string ReadableText) FormatKotTicket(
        string restaurantName,
        string stationName,
        string ticketNumber,
        int orderNumber,
        int tableNumber,
        string? groupLabel,
        string ticketType,
        bool isReprint,
        int reprintCount,
        string? reprintReason,
        DateTime timestampUtc,
        List<KotTicketItemSnapshotDto> items,
        string? notes,
        int paperWidthMm = 80,
        string orderChannel = "DineIn",
        int? dailyTokenNumber = null,
        string? customerName = null,
        string? customerPhone = null,
        string? vehiclePlate = null,
        string? vehicleModel = null,
        string? parkingBay = null,
        bool isScheduled = false,
        DateTime? scheduledPickupTimeUtc = null,
        string? deliveryProvider = null,
        string? externalOrderReference = null)
    {
        int colWidth = paperWidthMm == 58 ? 32 : 48;
        string timestampIst = FormatIstDateTime(timestampUtc);

        // 1. Build Readable Plaintext
        var textBuilder = new StringBuilder();
        string divider = new string('-', colWidth);
        string doubleDivider = new string('=', colWidth);

        textBuilder.AppendLine(CenterText(restaurantName.ToUpperInvariant(), colWidth));
        textBuilder.AppendLine(CenterText($"*** {stationName.ToUpperInvariant()} ***", colWidth));

        if (!string.IsNullOrWhiteSpace(deliveryProvider))
        {
            textBuilder.AppendLine(CenterText($"*** {deliveryProvider.ToUpperInvariant()} DELIVERY ***", colWidth));
            if (!string.IsNullOrWhiteSpace(externalOrderReference))
            {
                textBuilder.AppendLine(CenterText($"EXT REF: {externalOrderReference}", colWidth));
            }
            if (dailyTokenNumber.HasValue)
            {
                textBuilder.AppendLine(CenterText($"TOKEN: #{dailyTokenNumber.Value:D3}", colWidth));
            }
        }
        else if (orderChannel == "ParcelTakeaway")
        {
            textBuilder.AppendLine(CenterText("*** PARCEL ORDER ***", colWidth));
            if (dailyTokenNumber.HasValue)
            {
                textBuilder.AppendLine(CenterText($"TOKEN: #{dailyTokenNumber.Value:D3}", colWidth));
            }
        }
        else if (orderChannel == "CarParking")
        {
            textBuilder.AppendLine(CenterText("*** CAR ORDER ***", colWidth));
            if (!string.IsNullOrWhiteSpace(vehiclePlate))
            {
                string vehText = $"VEHICLE: {vehiclePlate}" + (!string.IsNullOrWhiteSpace(vehicleModel) ? $" ({vehicleModel})" : "");
                textBuilder.AppendLine(CenterText(vehText, colWidth));
            }
            if (!string.IsNullOrWhiteSpace(parkingBay))
            {
                textBuilder.AppendLine(CenterText($"BAY: {parkingBay}", colWidth));
            }
        }

        if (isScheduled && scheduledPickupTimeUtc.HasValue)
        {
            textBuilder.AppendLine(CenterText($"*** PICKUP: {FormatIstDateTime(scheduledPickupTimeUtc.Value)} ***", colWidth));
        }

        if (isReprint)
        {
            string reprintHeader = reprintCount > 1 ? $"*** REPRINT #{reprintCount} ***" : "*** REPRINT ***";
            textBuilder.AppendLine(CenterText(reprintHeader, colWidth));
            if (!string.IsNullOrWhiteSpace(reprintReason))
            {
                textBuilder.AppendLine(CenterText($"Reason: {reprintReason}", colWidth));
            }
        }
        else if (ticketType == "Incremental")
        {
            textBuilder.AppendLine(CenterText("*** NEW ADDITION ***", colWidth));
        }
        else if (ticketType == "CourseFire")
        {
            textBuilder.AppendLine(CenterText("*** COURSE RELEASED ***", colWidth));
        }
        else if (ticketType == "Refill")
        {
            textBuilder.AppendLine(CenterText("*** FREE REFILL ***", colWidth));
        }
        else if (ticketType == "CancellationNotice")
        {
            textBuilder.AppendLine(CenterText("*** CANCELLATION NOTICE ***", colWidth));
            textBuilder.AppendLine(CenterText("*** STOP PREPARATION ***", colWidth));
        }

        textBuilder.AppendLine(doubleDivider);

        string locationStr = !string.IsNullOrWhiteSpace(deliveryProvider)
            ? $"{deliveryProvider.ToUpperInvariant()} #{dailyTokenNumber?.ToString("D3") ?? (externalOrderReference ?? "DELIVERY")}"
            : orderChannel == "ParcelTakeaway"
                ? (dailyTokenNumber.HasValue ? $"PARCEL #{dailyTokenNumber.Value:D3}" : "PARCEL COUNTER")
                : (orderChannel == "CarParking"
                    ? (!string.IsNullOrWhiteSpace(parkingBay) ? $"BAY: {parkingBay}" : "CAR-SIDE")
                    : $"Table {tableNumber}" + (!string.IsNullOrWhiteSpace(groupLabel) ? $" ({groupLabel})" : ""));

        textBuilder.AppendLine(PadSides($"Ticket: {ticketNumber}", $"Order: ORD-{orderNumber:D4}", colWidth));
        textBuilder.AppendLine(PadSides(locationStr, timestampIst, colWidth));

        if (!string.IsNullOrWhiteSpace(customerName) || !string.IsNullOrWhiteSpace(customerPhone))
        {
            string custInfo = $"Customer: {customerName}" + (!string.IsNullOrWhiteSpace(customerPhone) ? $" ({customerPhone})" : "");
            textBuilder.AppendLine(WrapText(custInfo, colWidth));
        }

        textBuilder.AppendLine(divider);

        bool isHybridDineIn = orderChannel == "DineIn" && items.Any(it => it.PackingIntent == "Takeaway");

        int totalQty = 0;
        foreach (var item in items)
        {
            totalQty += item.Quantity;
            string itemHeader = $"{item.Quantity}x {item.Name}";
            textBuilder.AppendLine(WrapText(itemHeader, colWidth));

            if (item.PackingIntent == "Takeaway")
            {
                textBuilder.AppendLine(WrapText("   [PACK AS PARCEL]", colWidth));
            }
            else if (isHybridDineIn)
            {
                textBuilder.AppendLine(WrapText("   [DINE-IN]", colWidth));
            }

            if (!string.IsNullOrWhiteSpace(item.CourseName))
            {
                textBuilder.AppendLine(WrapText($"   [Course: {item.CourseName}]", colWidth));
            }

            if (!string.IsNullOrWhiteSpace(item.ModifiersJson))
            {
                try
                {
                    var mods = JsonSerializer.Deserialize<List<SelectedModifierSnapshotDto>>(item.ModifiersJson);
                    if (mods != null)
                    {
                        foreach (var m in mods)
                        {
                            string delta = m.PriceDelta > 0 ? $" (+Rs.{m.PriceDelta:F2})" : (m.PriceDelta < 0 ? $" (-Rs.{Math.Abs(m.PriceDelta):F2})" : "");
                            textBuilder.AppendLine(WrapText($"   + {m.OptionName}{delta}", colWidth));
                        }
                    }
                }
                catch { }
            }

            if (item.IsRefill)
            {
                textBuilder.AppendLine(WrapText("   [FREE REFILL - ZERO CHARGE]", colWidth));
            }

            if (!string.IsNullOrWhiteSpace(item.SpecialInstructions))
            {
                textBuilder.AppendLine(WrapText($"   * Note: {item.SpecialInstructions}", colWidth));
            }
        }

        textBuilder.AppendLine(divider);
        if (!string.IsNullOrWhiteSpace(notes))
        {
            textBuilder.AppendLine(WrapText($"Order Notes: {notes}", colWidth));
            textBuilder.AppendLine(divider);
        }
        textBuilder.AppendLine(PadSides($"Total Items: {totalQty}", ticketType.ToUpperInvariant(), colWidth));
        textBuilder.AppendLine(doubleDivider);

        string readableText = textBuilder.ToString();

        // 2. Build ESC/POS Binary Bytes
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms, Encoding.ASCII);

        // ESC @: Initialize printer
        bw.Write(new byte[] { 0x1B, 0x40 });

        // Center Align: ESC a 1
        bw.Write(new byte[] { 0x1B, 0x61, 0x01 });

        // Double Height/Width for Restaurant Name: GS ! 0x11
        bw.Write(new byte[] { 0x1D, 0x21, 0x11 });
        bw.Write(Encoding.ASCII.GetBytes(restaurantName.ToUpperInvariant() + "\n"));

        // Bold for Station Name: ESC E 1
        bw.Write(new byte[] { 0x1D, 0x21, 0x00 }); // Normal size
        bw.Write(new byte[] { 0x1B, 0x45, 0x01 }); // Bold on
        bw.Write(Encoding.ASCII.GetBytes($"*** {stationName.ToUpperInvariant()} ***\n"));

        if (!string.IsNullOrWhiteSpace(deliveryProvider))
        {
            bw.Write(new byte[] { 0x1D, 0x21, 0x01 }); // Double height
            bw.Write(Encoding.ASCII.GetBytes($"*** {deliveryProvider.ToUpperInvariant()} DELIVERY ***\n"));
            bw.Write(new byte[] { 0x1D, 0x21, 0x00 });
            if (!string.IsNullOrWhiteSpace(externalOrderReference))
            {
                bw.Write(Encoding.ASCII.GetBytes($"EXT REF: {externalOrderReference}\n"));
            }
            if (dailyTokenNumber.HasValue)
            {
                bw.Write(new byte[] { 0x1D, 0x21, 0x11 }); // Double height & width for token
                bw.Write(Encoding.ASCII.GetBytes($"TOKEN: #{dailyTokenNumber.Value:D3}\n"));
                bw.Write(new byte[] { 0x1D, 0x21, 0x00 });
            }
        }
        else if (orderChannel == "ParcelTakeaway")
        {
            bw.Write(Encoding.ASCII.GetBytes("*** PARCEL ORDER ***\n"));
            if (dailyTokenNumber.HasValue)
            {
                bw.Write(new byte[] { 0x1D, 0x21, 0x11 }); // Double height & width for token
                bw.Write(Encoding.ASCII.GetBytes($"TOKEN: #{dailyTokenNumber.Value:D3}\n"));
                bw.Write(new byte[] { 0x1D, 0x21, 0x00 }); // Normal size
            }
        }
        else if (orderChannel == "CarParking")
        {
            bw.Write(Encoding.ASCII.GetBytes("*** CAR ORDER ***\n"));
            if (!string.IsNullOrWhiteSpace(vehiclePlate))
            {
                string vehText = $"VEHICLE: {vehiclePlate}" + (!string.IsNullOrWhiteSpace(vehicleModel) ? $" ({vehicleModel})" : "");
                bw.Write(Encoding.ASCII.GetBytes(vehText + "\n"));
            }
            if (!string.IsNullOrWhiteSpace(parkingBay))
            {
                bw.Write(Encoding.ASCII.GetBytes($"BAY: {parkingBay}\n"));
            }
        }

        if (isScheduled && scheduledPickupTimeUtc.HasValue)
        {
            bw.Write(Encoding.ASCII.GetBytes($"*** PICKUP: {FormatIstDateTime(scheduledPickupTimeUtc.Value)} ***\n"));
        }

        if (isReprint)
        {
            string reprintHeader = reprintCount > 1 ? $"*** REPRINT #{reprintCount} ***\n" : "*** REPRINT ***\n";
            bw.Write(Encoding.ASCII.GetBytes(reprintHeader));
            if (!string.IsNullOrWhiteSpace(reprintReason))
            {
                bw.Write(Encoding.ASCII.GetBytes($"Reason: {reprintReason}\n"));
            }
        }
        else if (ticketType == "Incremental")
        {
            bw.Write(Encoding.ASCII.GetBytes("*** NEW ADDITION ***\n"));
        }
        else if (ticketType == "CourseFire")
        {
            bw.Write(Encoding.ASCII.GetBytes("*** COURSE RELEASED ***\n"));
        }
        else if (ticketType == "Refill")
        {
            bw.Write(Encoding.ASCII.GetBytes("*** FREE REFILL ***\n"));
        }
        else if (ticketType == "CancellationNotice")
        {
            bw.Write(Encoding.ASCII.GetBytes("*** CANCELLATION NOTICE ***\n"));
            bw.Write(Encoding.ASCII.GetBytes("*** STOP PREPARATION ***\n"));
        }

        bw.Write(new byte[] { 0x1B, 0x45, 0x00 }); // Bold off
        bw.Write(Encoding.ASCII.GetBytes(doubleDivider + "\n"));

        // Left Align: ESC a 0
        bw.Write(new byte[] { 0x1B, 0x61, 0x00 });
        bw.Write(Encoding.ASCII.GetBytes(PadSides($"Ticket: {ticketNumber}", $"Order: ORD-{orderNumber:D4}", colWidth) + "\n"));
        bw.Write(Encoding.ASCII.GetBytes(PadSides(locationStr, timestampIst, colWidth) + "\n"));

        if (!string.IsNullOrWhiteSpace(customerName) || !string.IsNullOrWhiteSpace(customerPhone))
        {
            string custInfo = $"Customer: {customerName}" + (!string.IsNullOrWhiteSpace(customerPhone) ? $" ({customerPhone})" : "");
            bw.Write(Encoding.ASCII.GetBytes(WrapText(custInfo, colWidth) + "\n"));
        }

        bw.Write(Encoding.ASCII.GetBytes(divider + "\n"));

        // Items in Double-Height for kitchen readability
        foreach (var item in items)
        {
            bw.Write(new byte[] { 0x1B, 0x45, 0x01 }); // Bold
            bw.Write(new byte[] { 0x1D, 0x21, 0x01 }); // Double height
            bw.Write(Encoding.ASCII.GetBytes(WrapText($"{item.Quantity}x {item.Name}", colWidth) + "\n"));
            bw.Write(new byte[] { 0x1D, 0x21, 0x00 }); // Normal size
            bw.Write(new byte[] { 0x1B, 0x45, 0x00 }); // Bold off

            if (item.PackingIntent == "Takeaway")
            {
                bw.Write(Encoding.ASCII.GetBytes(WrapText("   [PACK AS PARCEL]", colWidth) + "\n"));
            }
            else if (isHybridDineIn)
            {
                bw.Write(Encoding.ASCII.GetBytes(WrapText("   [DINE-IN]", colWidth) + "\n"));
            }

            if (!string.IsNullOrWhiteSpace(item.CourseName))
            {
                bw.Write(Encoding.ASCII.GetBytes(WrapText($"   [Course: {item.CourseName}]", colWidth) + "\n"));
            }

            if (!string.IsNullOrWhiteSpace(item.ModifiersJson))
            {
                try
                {
                    var mods = JsonSerializer.Deserialize<List<SelectedModifierSnapshotDto>>(item.ModifiersJson);
                    if (mods != null)
                    {
                        foreach (var m in mods)
                        {
                            string delta = m.PriceDelta > 0 ? $" (+Rs.{m.PriceDelta:F2})" : (m.PriceDelta < 0 ? $" (-Rs.{Math.Abs(m.PriceDelta):F2})" : "");
                            bw.Write(Encoding.ASCII.GetBytes(WrapText($"   + {m.OptionName}{delta}", colWidth) + "\n"));
                        }
                    }
                }
                catch { }
            }

            if (item.IsRefill)
            {
                bw.Write(Encoding.ASCII.GetBytes(WrapText("   [FREE REFILL - ZERO CHARGE]", colWidth) + "\n"));
            }

            if (!string.IsNullOrWhiteSpace(item.SpecialInstructions))
            {
                bw.Write(Encoding.ASCII.GetBytes(WrapText($"   * Note: {item.SpecialInstructions}", colWidth) + "\n"));
            }
        }

        bw.Write(Encoding.ASCII.GetBytes(divider + "\n"));
        if (!string.IsNullOrWhiteSpace(notes))
        {
            bw.Write(Encoding.ASCII.GetBytes(WrapText($"Order Notes: {notes}", colWidth) + "\n"));
            bw.Write(Encoding.ASCII.GetBytes(divider + "\n"));
        }

        bw.Write(new byte[] { 0x1B, 0x45, 0x01 }); // Bold
        bw.Write(Encoding.ASCII.GetBytes(PadSides($"Total Items: {totalQty}", ticketType.ToUpperInvariant(), colWidth) + "\n"));
        bw.Write(new byte[] { 0x1B, 0x45, 0x00 }); // Bold off
        bw.Write(Encoding.ASCII.GetBytes(doubleDivider + "\n\n\n\n"));

        // Partial Cut with Feed: GS V 66 0
        bw.Write(new byte[] { 0x1D, 0x56, 0x42, 0x00 });

        return (ms.ToArray(), readableText);
    }

    public static (byte[] EscPosBytes, string ReadableText) FormatTestPrint(string restaurantName, string printerName, int paperWidthMm)
    {
        int colWidth = paperWidthMm == 58 ? 32 : 48;
        string timestampIst = FormatIstDateTime(DateTime.UtcNow);
        string divider = new string('-', colWidth);

        var textBuilder = new StringBuilder();
        textBuilder.AppendLine(CenterText(restaurantName.ToUpperInvariant(), colWidth));
        textBuilder.AppendLine(CenterText("*** TEST PRINT ***", colWidth));
        textBuilder.AppendLine(divider);
        textBuilder.AppendLine($"Printer: {printerName}");
        textBuilder.AppendLine($"Paper: {paperWidthMm}mm ({colWidth} cols)");
        textBuilder.AppendLine($"Time: {timestampIst}");
        textBuilder.AppendLine(divider);
        textBuilder.AppendLine(CenterText("THERMAL PRINTER READY", colWidth));
        textBuilder.AppendLine(divider);

        string readableText = textBuilder.ToString();

        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms, Encoding.ASCII);

        bw.Write(new byte[] { 0x1B, 0x40 }); // Init
        bw.Write(new byte[] { 0x1B, 0x61, 0x01 }); // Center
        bw.Write(new byte[] { 0x1D, 0x21, 0x11 }); // Double height & width
        bw.Write(Encoding.ASCII.GetBytes(restaurantName.ToUpperInvariant() + "\n"));
        bw.Write(new byte[] { 0x1D, 0x21, 0x00 }); // Normal
        bw.Write(new byte[] { 0x1B, 0x45, 0x01 }); // Bold
        bw.Write(Encoding.ASCII.GetBytes("*** TEST PRINT ***\n"));
        bw.Write(new byte[] { 0x1B, 0x45, 0x00 }); // Bold off
        bw.Write(Encoding.ASCII.GetBytes(divider + "\n"));

        bw.Write(new byte[] { 0x1B, 0x61, 0x00 }); // Left
        bw.Write(Encoding.ASCII.GetBytes($"Printer: {printerName}\n"));
        bw.Write(Encoding.ASCII.GetBytes($"Paper: {paperWidthMm}mm ({colWidth} cols)\n"));
        bw.Write(Encoding.ASCII.GetBytes($"Time: {timestampIst}\n"));
        bw.Write(Encoding.ASCII.GetBytes(divider + "\n"));

        bw.Write(new byte[] { 0x1B, 0x61, 0x01 }); // Center
        bw.Write(new byte[] { 0x1B, 0x45, 0x01 }); // Bold
        bw.Write(Encoding.ASCII.GetBytes("THERMAL PRINTER READY\n"));
        bw.Write(new byte[] { 0x1B, 0x45, 0x00 }); // Bold off
        bw.Write(Encoding.ASCII.GetBytes(divider + "\n\n\n\n"));

        bw.Write(new byte[] { 0x1D, 0x56, 0x42, 0x00 }); // Cut

        return (ms.ToArray(), readableText);
    }

    public static (byte[] EscPosBytes, string ReadableText) FormatParcelReceipt(
        string restaurantName,
        string billNumber,
        int orderNumber,
        int dailyTokenNumber,
        string? businessDate,
        DateTime timestampUtc,
        string? customerName,
        string? customerPhone,
        List<(string Name, int Quantity, decimal UnitPrice, decimal Total)> items,
        decimal foodSubtotal,
        decimal packagingCharge,
        string? packagingMode,
        decimal tax,
        decimal discount,
        decimal totalAmount,
        string? paymentMethod,
        bool isSettled,
        bool isReprint = false,
        string? reprintReason = null,
        int paperWidthMm = 80)
    {
        int colWidth = paperWidthMm == 58 ? 32 : 48;
        string timestampIst = FormatIstDateTime(timestampUtc);
        string divider = new string('-', colWidth);
        string doubleDivider = new string('=', colWidth);

        var textBuilder = new StringBuilder();
        textBuilder.AppendLine(CenterText(restaurantName.ToUpperInvariant(), colWidth));
        textBuilder.AppendLine(CenterText(isSettled ? "TAX INVOICE / RECEIPT" : "BILL ESTIMATE", colWidth));
        textBuilder.AppendLine(CenterText("*** PARCEL TAKEAWAY ***", colWidth));
        textBuilder.AppendLine(CenterText($"TOKEN: #{dailyTokenNumber:D3}", colWidth));

        if (isReprint)
        {
            textBuilder.AppendLine(CenterText("*** REPRINT ***", colWidth));
            if (!string.IsNullOrWhiteSpace(reprintReason))
            {
                textBuilder.AppendLine(CenterText($"Reason: {reprintReason}", colWidth));
            }
        }

        textBuilder.AppendLine(doubleDivider);
        textBuilder.AppendLine(PadSides($"Bill: {billNumber}", $"Order: ORD-{orderNumber:D4}", colWidth));
        textBuilder.AppendLine(PadSides($"Date: {businessDate ?? "Today"}", timestampIst, colWidth));

        if (!string.IsNullOrWhiteSpace(customerName) || !string.IsNullOrWhiteSpace(customerPhone))
        {
            string custInfo = $"Customer: {customerName}" + (!string.IsNullOrWhiteSpace(customerPhone) ? $" ({customerPhone})" : "");
            textBuilder.AppendLine(WrapText(custInfo, colWidth));
        }

        textBuilder.AppendLine(divider);

        foreach (var item in items)
        {
            string lineItem = $"{item.Quantity}x {item.Name}";
            string priceStr = item.Total.ToString("F2");
            textBuilder.AppendLine(PadSides(lineItem, priceStr, colWidth));
        }

        textBuilder.AppendLine(divider);
        textBuilder.AppendLine(PadSides("Food Subtotal:", foodSubtotal.ToString("F2"), colWidth));

        if (packagingCharge > 0)
        {
            string packLabel = !string.IsNullOrWhiteSpace(packagingMode) ? $"Packaging ({packagingMode}):" : "Packaging Charges:";
            textBuilder.AppendLine(PadSides(packLabel, packagingCharge.ToString("F2"), colWidth));
        }

        if (tax > 0)
        {
            textBuilder.AppendLine(PadSides("GST / Applicable Tax:", tax.ToString("F2"), colWidth));
        }

        if (discount > 0)
        {
            textBuilder.AppendLine(PadSides("Discount:", $"-{discount:F2}", colWidth));
        }

        textBuilder.AppendLine(doubleDivider);
        textBuilder.AppendLine(PadSides("TOTAL AMOUNT:", totalAmount.ToString("F2"), colWidth));

        if (isSettled && !string.IsNullOrWhiteSpace(paymentMethod))
        {
            textBuilder.AppendLine(PadSides($"Paid via {paymentMethod}:", totalAmount.ToString("F2"), colWidth));
            textBuilder.AppendLine(divider);
            textBuilder.AppendLine(CenterText("THANK YOU FOR YOUR VISIT!", colWidth));
        }
        else
        {
            textBuilder.AppendLine(PadSides("Balance Due:", totalAmount.ToString("F2"), colWidth));
        }
        textBuilder.AppendLine(doubleDivider);

        string readableText = textBuilder.ToString();

        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms, Encoding.ASCII);

        bw.Write(new byte[] { 0x1B, 0x40 }); // Init
        bw.Write(new byte[] { 0x1B, 0x61, 0x01 }); // Center
        bw.Write(new byte[] { 0x1D, 0x21, 0x11 }); // 2x
        bw.Write(Encoding.ASCII.GetBytes(restaurantName.ToUpperInvariant() + "\n"));
        bw.Write(new byte[] { 0x1D, 0x21, 0x00 }); // Normal
        bw.Write(new byte[] { 0x1B, 0x45, 0x01 }); // Bold
        bw.Write(Encoding.ASCII.GetBytes((isSettled ? "TAX INVOICE / RECEIPT" : "BILL ESTIMATE") + "\n"));
        bw.Write(Encoding.ASCII.GetBytes("*** PARCEL TAKEAWAY ***\n"));
        bw.Write(new byte[] { 0x1D, 0x21, 0x11 }); // 2x for token
        bw.Write(Encoding.ASCII.GetBytes($"TOKEN: #{dailyTokenNumber:D3}\n"));
        bw.Write(new byte[] { 0x1D, 0x21, 0x00 }); // Normal
        if (isReprint)
        {
            bw.Write(Encoding.ASCII.GetBytes("*** REPRINT ***\n"));
            if (!string.IsNullOrWhiteSpace(reprintReason))
            {
                bw.Write(Encoding.ASCII.GetBytes($"Reason: {reprintReason}\n"));
            }
        }
        bw.Write(new byte[] { 0x1B, 0x45, 0x00 }); // Bold off
        bw.Write(Encoding.ASCII.GetBytes(doubleDivider + "\n"));

        bw.Write(new byte[] { 0x1B, 0x61, 0x00 }); // Left
        bw.Write(Encoding.ASCII.GetBytes(PadSides($"Bill: {billNumber}", $"Order: ORD-{orderNumber:D4}", colWidth) + "\n"));
        bw.Write(Encoding.ASCII.GetBytes(PadSides($"Date: {businessDate ?? "Today"}", timestampIst, colWidth) + "\n"));

        if (!string.IsNullOrWhiteSpace(customerName) || !string.IsNullOrWhiteSpace(customerPhone))
        {
            string custInfo = $"Customer: {customerName}" + (!string.IsNullOrWhiteSpace(customerPhone) ? $" ({customerPhone})" : "");
            bw.Write(Encoding.ASCII.GetBytes(WrapText(custInfo, colWidth) + "\n"));
        }

        bw.Write(Encoding.ASCII.GetBytes(divider + "\n"));

        foreach (var item in items)
        {
            string lineItem = $"{item.Quantity}x {item.Name}";
            string priceStr = item.Total.ToString("F2");
            bw.Write(Encoding.ASCII.GetBytes(PadSides(lineItem, priceStr, colWidth) + "\n"));
        }

        bw.Write(Encoding.ASCII.GetBytes(divider + "\n"));
        bw.Write(Encoding.ASCII.GetBytes(PadSides("Food Subtotal:", foodSubtotal.ToString("F2"), colWidth) + "\n"));

        if (packagingCharge > 0)
        {
            string packLabel = !string.IsNullOrWhiteSpace(packagingMode) ? $"Packaging ({packagingMode}):" : "Packaging Charges:";
            bw.Write(Encoding.ASCII.GetBytes(PadSides(packLabel, packagingCharge.ToString("F2"), colWidth) + "\n"));
        }

        if (tax > 0)
        {
            bw.Write(Encoding.ASCII.GetBytes(PadSides("GST / Applicable Tax:", tax.ToString("F2"), colWidth) + "\n"));
        }

        if (discount > 0)
        {
            bw.Write(Encoding.ASCII.GetBytes(PadSides("Discount:", $"-{discount:F2}", colWidth) + "\n"));
        }

        bw.Write(new byte[] { 0x1B, 0x45, 0x01 }); // Bold
        bw.Write(Encoding.ASCII.GetBytes(doubleDivider + "\n"));
        bw.Write(Encoding.ASCII.GetBytes(PadSides("TOTAL AMOUNT:", totalAmount.ToString("F2"), colWidth) + "\n"));

        if (isSettled && !string.IsNullOrWhiteSpace(paymentMethod))
        {
            bw.Write(Encoding.ASCII.GetBytes(PadSides($"Paid via {paymentMethod}:", totalAmount.ToString("F2"), colWidth) + "\n"));
            bw.Write(new byte[] { 0x1B, 0x45, 0x00 });
            bw.Write(Encoding.ASCII.GetBytes(divider + "\n"));
            bw.Write(new byte[] { 0x1B, 0x61, 0x01 });
            bw.Write(Encoding.ASCII.GetBytes("THANK YOU FOR YOUR VISIT!\n"));
        }
        else
        {
            bw.Write(Encoding.ASCII.GetBytes(PadSides("Balance Due:", totalAmount.ToString("F2"), colWidth) + "\n"));
            bw.Write(new byte[] { 0x1B, 0x45, 0x00 });
        }
        bw.Write(Encoding.ASCII.GetBytes(doubleDivider + "\n\n\n\n"));
        bw.Write(new byte[] { 0x1D, 0x56, 0x42, 0x00 }); // Cut

        return (ms.ToArray(), readableText);
    }

    private static string WrapText(string text, int width)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= width) return text;

        var words = text.Split(' ');
        var sb = new StringBuilder();
        var line = new StringBuilder();

        foreach (var word in words)
        {
            if (line.Length + word.Length + 1 > width)
            {
                if (sb.Length > 0) sb.AppendLine();
                sb.Append(line.ToString());
                line.Clear();
            }
            if (line.Length > 0) line.Append(' ');
            line.Append(word);
        }

        if (line.Length > 0)
        {
            if (sb.Length > 0) sb.AppendLine();
            sb.Append(line.ToString());
        }

        return sb.ToString();
    }

    private static string CenterText(string text, int width)
    {
        if (text.Length >= width) return text.Substring(0, width);
        int leftPad = (width - text.Length) / 2;
        return new string(' ', leftPad) + text;
    }

    private static string PadSides(string left, string right, int width)
    {
        int spaces = width - left.Length - right.Length;
        if (spaces <= 0) return $"{left} {right}";
        return left + new string(' ', spaces) + right;
    }
}
