using ApiRoleValidation.Data;
using ApiRoleValidation.Models;
using Microsoft.EntityFrameworkCore;

namespace ApiRoleValidation.Endpoints;

public static class OrderEndpoints
{
    public static RouteGroupBuilder MapOrderEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/orders").WithTags("Orders");

        group.MapGet("/", async (AppDbContext db) =>
        {
            var orders = await db.Orders
                .Include(o => o.Items)
                .AsNoTracking()
                .ToListAsync();

            return Results.Ok(orders);
        })
        .RequireAuthorization(policy => policy.RequireRole(Roles.Admin, Roles.User))
        .WithName("GetOrders")
        .WithSummary("Get list of orders (Accessible by Admin and User)");

        group.MapGet("/{id:int}", async (int id, AppDbContext db) =>
        {
            var order = await db.Orders
                .Include(o => o.Items)
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
            {
                return Results.NotFound(new { Message = $"Order with ID {id} was not found." });
            }

            return Results.Ok(order);
        })
        .RequireAuthorization(policy => policy.RequireRole(Roles.Admin, Roles.User))
        .WithName("GetOrderById")
        .WithSummary("Get order details by ID (Accessible by Admin and User)");

        return group;
    }
}

