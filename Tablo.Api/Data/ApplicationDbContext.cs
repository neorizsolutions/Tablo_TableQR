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
        public DbSet<RestaurantTable> RestaurantTables { get; set; } = null!;
        public DbSet<Category> Categories { get; set; } = null!;
        public DbSet<MenuItem> MenuItems { get; set; } = null!;
        public DbSet<Order> Orders { get; set; } = null!;
        public DbSet<RestaurantOrderSequence> RestaurantOrderSequences { get; set; } = null!;
        public DbSet<OrderItem> OrderItems { get; set; } = null!;
        public DbSet<Employee> Employees { get; set; } = null!;
        public DbSet<Role> Roles { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            // Apply all configurations from the current assembly
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        }
    }
}
