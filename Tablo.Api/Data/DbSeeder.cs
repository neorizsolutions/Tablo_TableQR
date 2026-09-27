using Tablo.Api.Entities;

namespace Tablo.Api.Data
{
    public static class DbSeeder
    {
        public static void Seed(ApplicationDbContext context, bool seedDummyData)
        {
            if (context.Roles.Any())
            {
                // Roles exist, check if we need to migrate passwords
                var existingRestaurant = context.Restaurants.FirstOrDefault();
                if (existingRestaurant != null)
                {
                    Console.WriteLine($"[INFO] RestaurantId: {existingRestaurant.Id}");
                }
                
                // Migrate passwords if they are plain text (not starting with $2a$ or $2b$ or $2y$)
                var employees = context.Employees.ToList();
                bool passwordsUpdated = false;
                foreach(var emp in employees)
                {
                    if (!emp.PasswordHash.StartsWith("$2"))
                    {
                        emp.PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123");
                        passwordsUpdated = true;
                    }
                }
                if (passwordsUpdated)
                {
                    context.SaveChanges();
                    Console.WriteLine("[INFO] Passwords migrated to BCrypt.");
                }
                
                return; // DB has been seeded
            }

            // 1. Roles (Always seeded)
            var adminRole = new Role { Id = Guid.NewGuid(), Name = "Admin", Description = "Administrator" };
            var kitchenRole = new Role { Id = Guid.NewGuid(), Name = "Kitchen", Description = "Kitchen Staff" };
            var waiterRole = new Role { Id = Guid.NewGuid(), Name = "Waiter", Description = "Waiter Staff" };
            context.Roles.AddRange(adminRole, kitchenRole, waiterRole);
            context.SaveChanges();

            if (!seedDummyData)
            {
                Console.WriteLine("[INFO] SeedDummyData is false. Only roles created. Skipping demo data.");
                return;
            }

            // 2. Restaurant
            var restaurant = new Restaurant
            {
                Id = Guid.NewGuid(),
                Name = "Tablo Demo Restaurant",
                Description = "A wonderful place to eat.",
                Address = "123 Main St, Food City",
                ContactEmail = "contact@tablodemo.com",
                ContactPhone = "555-0199"
            };
            context.Restaurants.Add(restaurant);

            // 3. Employees
            var adminUser = new Employee
            {
                Id = Guid.NewGuid(),
                RestaurantId = restaurant.Id,
                RoleId = adminRole.Id,
                FirstName = "Admin",
                LastName = "User",
                Email = "admin@tablodemo.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123")
            };
            var kitchenUser = new Employee
            {
                Id = Guid.NewGuid(),
                RestaurantId = restaurant.Id,
                RoleId = kitchenRole.Id,
                FirstName = "Chef",
                LastName = "Gordon",
                Email = "kitchen@tablodemo.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123")
            };
            var waiterUser = new Employee
            {
                Id = Guid.NewGuid(),
                RestaurantId = restaurant.Id,
                RoleId = waiterRole.Id,
                FirstName = "John",
                LastName = "Doe",
                Email = "waiter@tablodemo.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123")
            };
            context.Employees.AddRange(adminUser, kitchenUser, waiterUser);

            // 4. Tables
            for (int i = 1; i <= 10; i++)
            {
                context.RestaurantTables.Add(new RestaurantTable
                {
                    Id = Guid.NewGuid(),
                    RestaurantId = restaurant.Id,
                    TableNumber = i,
                    QRToken = Guid.NewGuid().ToString("N")
                });
            }

            // 5. Categories
            var starters = new Category { Id = Guid.NewGuid(), RestaurantId = restaurant.Id, Name = "Starters", DisplayOrder = 1 };
            var mains = new Category { Id = Guid.NewGuid(), RestaurantId = restaurant.Id, Name = "Main Course", DisplayOrder = 2 };
            var desserts = new Category { Id = Guid.NewGuid(), RestaurantId = restaurant.Id, Name = "Desserts", DisplayOrder = 3 };
            var beverages = new Category { Id = Guid.NewGuid(), RestaurantId = restaurant.Id, Name = "Beverages", DisplayOrder = 4 };
            context.Categories.AddRange(starters, mains, desserts, beverages);

            // 6. Menu Items (20 items)
            var menuItems = new List<MenuItem>
            {
                // Starters
                new MenuItem { Id = Guid.NewGuid(), RestaurantId = restaurant.Id, CategoryId = starters.Id, Name = "Garlic Bread", Description = "Toasted bread with garlic butter", Price = 4.99m, IsAvailable = true },
                new MenuItem { Id = Guid.NewGuid(), RestaurantId = restaurant.Id, CategoryId = starters.Id, Name = "Bruschetta", Description = "Grilled bread with tomatoes and basil", Price = 6.99m, IsAvailable = true },
                new MenuItem { Id = Guid.NewGuid(), RestaurantId = restaurant.Id, CategoryId = starters.Id, Name = "Calamari", Description = "Fried squid with marinara sauce", Price = 8.99m, IsAvailable = true },
                new MenuItem { Id = Guid.NewGuid(), RestaurantId = restaurant.Id, CategoryId = starters.Id, Name = "Mozzarella Sticks", Description = "Fried cheese sticks", Price = 5.99m, IsAvailable = true },
                new MenuItem { Id = Guid.NewGuid(), RestaurantId = restaurant.Id, CategoryId = starters.Id, Name = "Stuffed Mushrooms", Description = "Mushrooms stuffed with cheese and herbs", Price = 7.99m, IsAvailable = true },
                
                // Mains
                new MenuItem { Id = Guid.NewGuid(), RestaurantId = restaurant.Id, CategoryId = mains.Id, Name = "Grilled Salmon", Description = "Fresh salmon with asparagus", Price = 18.99m, IsAvailable = true },
                new MenuItem { Id = Guid.NewGuid(), RestaurantId = restaurant.Id, CategoryId = mains.Id, Name = "Ribeye Steak", Description = "12oz ribeye with mashed potatoes", Price = 24.99m, IsAvailable = true },
                new MenuItem { Id = Guid.NewGuid(), RestaurantId = restaurant.Id, CategoryId = mains.Id, Name = "Chicken Parmesan", Description = "Breaded chicken with pasta", Price = 16.99m, IsAvailable = true },
                new MenuItem { Id = Guid.NewGuid(), RestaurantId = restaurant.Id, CategoryId = mains.Id, Name = "Margherita Pizza", Description = "Classic cheese and tomato pizza", Price = 12.99m, IsAvailable = true },
                new MenuItem { Id = Guid.NewGuid(), RestaurantId = restaurant.Id, CategoryId = mains.Id, Name = "Pasta Carbonara", Description = "Creamy pasta with pancetta", Price = 14.99m, IsAvailable = true },
                new MenuItem { Id = Guid.NewGuid(), RestaurantId = restaurant.Id, CategoryId = mains.Id, Name = "Beef Burger", Description = "Classic burger with fries", Price = 13.99m, IsAvailable = true },
                
                // Desserts
                new MenuItem { Id = Guid.NewGuid(), RestaurantId = restaurant.Id, CategoryId = desserts.Id, Name = "Tiramisu", Description = "Coffee-flavored Italian dessert", Price = 6.99m, IsAvailable = true },
                new MenuItem { Id = Guid.NewGuid(), RestaurantId = restaurant.Id, CategoryId = desserts.Id, Name = "Cheesecake", Description = "New York style cheesecake", Price = 7.99m, IsAvailable = true },
                new MenuItem { Id = Guid.NewGuid(), RestaurantId = restaurant.Id, CategoryId = desserts.Id, Name = "Chocolate Lava Cake", Description = "Warm chocolate cake", Price = 8.99m, IsAvailable = true },
                new MenuItem { Id = Guid.NewGuid(), RestaurantId = restaurant.Id, CategoryId = desserts.Id, Name = "Ice Cream Sundae", Description = "Vanilla ice cream with chocolate syrup", Price = 5.99m, IsAvailable = true },
                
                // Beverages
                new MenuItem { Id = Guid.NewGuid(), RestaurantId = restaurant.Id, CategoryId = beverages.Id, Name = "Coca Cola", Description = "Chilled soda", Price = 2.50m, IsAvailable = true },
                new MenuItem { Id = Guid.NewGuid(), RestaurantId = restaurant.Id, CategoryId = beverages.Id, Name = "Lemonade", Description = "Freshly squeezed", Price = 3.50m, IsAvailable = true },
                new MenuItem { Id = Guid.NewGuid(), RestaurantId = restaurant.Id, CategoryId = beverages.Id, Name = "Iced Tea", Description = "Sweetened or unsweetened", Price = 2.50m, IsAvailable = true },
                new MenuItem { Id = Guid.NewGuid(), RestaurantId = restaurant.Id, CategoryId = beverages.Id, Name = "Espresso", Description = "Strong Italian coffee", Price = 3.00m, IsAvailable = true },
                new MenuItem { Id = Guid.NewGuid(), RestaurantId = restaurant.Id, CategoryId = beverages.Id, Name = "Craft Beer", Description = "Local IPA", Price = 6.00m, IsAvailable = true }
            };
            
            context.MenuItems.AddRange(menuItems);

            context.SaveChanges();
            Console.WriteLine("Database seeded successfully with 1 restaurant, 3 roles, 3 employees, 10 tables, 4 categories, and 20 menu items.");
        }
    }
}
