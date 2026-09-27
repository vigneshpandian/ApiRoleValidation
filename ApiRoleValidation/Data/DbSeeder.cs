using ApiRoleValidation.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ApiRoleValidation.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext context)
    {
        if (await context.Users.AnyAsync())
        {
            return;
        }

        var passwordHasher = new PasswordHasher<User>();

        var adminUser = new User
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Username = "admin",
            Role = Roles.Admin,
            SecurityStamp = Guid.NewGuid().ToString()
        };
        adminUser.PasswordHash = passwordHasher.HashPassword(adminUser, "Admin123!");

        var standardUser = new User
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Username = "user1",
            Role = Roles.User,
            SecurityStamp = Guid.NewGuid().ToString()
        };
        standardUser.PasswordHash = passwordHasher.HashPassword(standardUser, "User123!");

        await context.Users.AddRangeAsync(adminUser, standardUser);

        var orders = new List<Order>
        {
            new Order
            {
                Id = 1,
                OrderNumber = "ORD-1001",
                CustomerName = "Alice Smith",
                TotalAmount = 1225.00m,
                CreatedAt = DateTime.UtcNow.AddDays(-5),
                Status = "Completed",
                Items = new List<OrderItem>
                {
                    new OrderItem { Id = 1, OrderId = 1, ProductName = "Pro Laptop 16-inch", Quantity = 1, UnitPrice = 1200.00m },
                    new OrderItem { Id = 2, OrderId = 1, ProductName = "Wireless Mouse", Quantity = 1, UnitPrice = 25.00m }
                }
            },
            new Order
            {
                Id = 2,
                OrderNumber = "ORD-1002",
                CustomerName = "Bob Jones",
                TotalAmount = 450.00m,
                CreatedAt = DateTime.UtcNow.AddDays(-2),
                Status = "Completed",
                Items = new List<OrderItem>
                {
                    new OrderItem { Id = 3, OrderId = 2, ProductName = "Mechanical Keyboard", Quantity = 2, UnitPrice = 75.00m },
                    new OrderItem { Id = 4, OrderId = 2, ProductName = "27-inch 4K Monitor", Quantity = 1, UnitPrice = 300.00m }
                }
            },
            new Order
            {
                Id = 3,
                OrderNumber = "ORD-1003",
                CustomerName = "Charlie Brown",
                TotalAmount = 75.00m,
                CreatedAt = DateTime.UtcNow.AddHours(-6),
                Status = "Shipped",
                Items = new List<OrderItem>
                {
                    new OrderItem { Id = 5, OrderId = 3, ProductName = "Braided USB-C Cable", Quantity = 5, UnitPrice = 15.00m }
                }
            }
        };

        await context.Orders.AddRangeAsync(orders);
        await context.SaveChangesAsync();
    }
}

