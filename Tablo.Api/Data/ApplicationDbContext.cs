using Microsoft.EntityFrameworkCore;
using Tablo.Api.Entities;

namespace Tablo.Api.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<Restaurant> Restaurants { get; set; } = null!;
        public DbSet<RestaurantSettings> RestaurantSettings { get; set; } = null!;
        public DbSet<RestaurantTable> RestaurantTables { get; set; } = null!;
        public DbSet<Category> Categories { get; set; } = null!;
        public DbSet<MenuItem> MenuItems { get; set; } = null!;
        public DbSet<Order> Orders { get; set; } = null!;
        public DbSet<TableBill> TableBills { get; set; } = null!;
        public DbSet<FloorOperation> FloorOperations { get; set; } = null!;
        public DbSet<DiningGroup> DiningGroups { get; set; } = null!;
        public DbSet<BillPayment> BillPayments { get; set; } = null!;
        public DbSet<StaffOrderSubmission> StaffOrderSubmissions { get; set; } = null!;
        public DbSet<RestaurantOrderSequence> RestaurantOrderSequences { get; set; } = null!;
        public DbSet<OrderItem> OrderItems { get; set; } = null!;
        public DbSet<Employee> Employees { get; set; } = null!;
        public DbSet<Role> Roles { get; set; } = null!;
        public DbSet<ServiceRequest> ServiceRequests { get; set; } = null!;
        public DbSet<Ingredient> Ingredients { get; set; } = null!;
        public DbSet<RecipeItem> RecipeItems { get; set; } = null!;
        public DbSet<StockMovement> StockMovements { get; set; } = null!;
        public DbSet<Stocktake> Stocktakes { get; set; } = null!;
        public DbSet<StocktakeItem> StocktakeItems { get; set; } = null!;
        public DbSet<SettledBillItemCost> SettledBillItemCosts { get; set; } = null!;
        public DbSet<SettledBillItemIngredientCost> SettledBillItemIngredientCosts { get; set; } = null!;
        public DbSet<Supplier> Suppliers { get; set; } = null!;
        public DbSet<SupplierIngredient> SupplierIngredients { get; set; } = null!;
        public DbSet<Outlet> Outlets { get; set; } = null!;
        public DbSet<StockLot> StockLots { get; set; } = null!;
        public DbSet<PurchaseOrder> PurchaseOrders { get; set; } = null!;
        public DbSet<PurchaseOrderItem> PurchaseOrderItems { get; set; } = null!;
        public DbSet<PurchaseInvoice> PurchaseInvoices { get; set; } = null!;
        public DbSet<PurchaseInvoiceItem> PurchaseInvoiceItems { get; set; } = null!;
        public DbSet<PurchaseReturn> PurchaseReturns { get; set; } = null!;
        public DbSet<PurchaseReturnItem> PurchaseReturnItems { get; set; } = null!;
        public DbSet<PreparationRecipe> PreparationRecipes { get; set; } = null!;
        public DbSet<PreparationRecipeItem> PreparationRecipeItems { get; set; } = null!;
        public DbSet<PreparationBatch> PreparationBatches { get; set; } = null!;
        public DbSet<StockTransfer> StockTransfers { get; set; } = null!;
        public DbSet<StockTransferItem> StockTransferItems { get; set; } = null!;
        public DbSet<StockReservation> StockReservations { get; set; } = null!;
        public DbSet<Plan> Plans { get; set; } = null!;
        public DbSet<Subscription> Subscriptions { get; set; } = null!;
        public DbSet<SubscriptionAuditLog> SubscriptionAuditLogs { get; set; } = null!;
        public DbSet<KitchenStation> KitchenStations { get; set; } = null!;
        public DbSet<ThermalPrinter> ThermalPrinters { get; set; } = null!;
        public DbSet<PrintAgentRegistration> PrintAgentRegistrations { get; set; } = null!;
        public DbSet<KotBatch> KotBatches { get; set; } = null!;
        public DbSet<KotTicket> KotTickets { get; set; } = null!;
        public DbSet<PrintJob> PrintJobs { get; set; } = null!;
        public DbSet<DailyTokenSequence> DailyTokenSequences { get; set; } = null!;
        public DbSet<ModifierGroup> ModifierGroups { get; set; } = null!;
        public DbSet<ModifierOption> ModifierOptions { get; set; } = null!;
        public DbSet<MenuItemModifierGroup> MenuItemModifierGroups { get; set; } = null!;
        public DbSet<ItemCancellationLog> ItemCancellationLogs { get; set; } = null!;
        public DbSet<OrderProductionOperation> OrderProductionOperations { get; set; } = null!;
        public DbSet<ManagerPinRateLimit> ManagerPinRateLimits { get; set; } = null!;
        public DbSet<MarketplacePayout> MarketplacePayouts { get; set; } = null!;
        public DbSet<CustomerProfile> Customers { get; set; } = null!;
        public DbSet<LoyaltyLedgerEntry> LoyaltyLedgerEntries { get; set; } = null!;
        public DbSet<CustomerFeedback> CustomerFeedbacks { get; set; } = null!;
        public DbSet<MarketplaceOutletMapping> MarketplaceOutletMappings { get; set; } = null!;
        public DbSet<MarketplaceItemMapping> MarketplaceItemMappings { get; set; } = null!;
        public DbSet<MarketplaceInboundEvent> MarketplaceInboundEvents { get; set; } = null!;
        public DbSet<MarketplaceOutboundUpdate> MarketplaceOutboundUpdates { get; set; } = null!;
        public DbSet<SupportConversation> SupportConversations { get; set; } = null!;
        public DbSet<SupportMessage> SupportMessages { get; set; } = null!;
        public DbSet<SupportEscalationLog> SupportEscalationLogs { get; set; } = null!;
        public DbSet<SupportAuditLog> SupportAuditLogs { get; set; } = null!;
        public DbSet<EmployeeAuditLog> EmployeeAuditLogs { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            // Apply all configurations from the current assembly
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        }
    }
}
