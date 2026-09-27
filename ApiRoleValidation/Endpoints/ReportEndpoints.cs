using ApiRoleValidation.Data;
using ApiRoleValidation.Models;
using Microsoft.EntityFrameworkCore;

namespace ApiRoleValidation.Endpoints;

public static class ReportEndpoints
{
    public static RouteGroupBuilder MapReportEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/reports").WithTags("Reports");

        group.MapGet("/orders", async (AppDbContext db) =>
        {
            var orders = await db.Orders
                .Include(o => o.Items)
                .AsNoTracking()
                .ToListAsync();

            var totalOrders = orders.Count;
            var totalRevenue = orders.Sum(o => o.TotalAmount);
            var averageOrderValue = totalOrders > 0 ? Math.Round(totalRevenue / totalOrders, 2) : 0;

            var topProducts = orders
                .SelectMany(o => o.Items)
                .GroupBy(i => i.ProductName)
                .Select(g => new TopProductDto(
                    ProductName: g.Key,
                    UnitsSold: g.Sum(x => x.Quantity),
                    Revenue: g.Sum(x => x.TotalPrice)
                ))
                .OrderByDescending(p => p.Revenue)
                .Take(5)
                .ToList();

            var report = new OrderReport
            {
                TotalOrders = totalOrders,
                TotalRevenue = totalRevenue,
                AverageOrderValue = averageOrderValue,
                TopSellingProducts = topProducts,
                GeneratedAt = DateTime.UtcNow
            };

            return Results.Ok(report);
        })
        .RequireAuthorization(policy => policy.RequireRole(Roles.Admin))
        .WithName("GetOrderReports")
        .WithSummary("Get aggregate order reports (Accessible ONLY by Admin)");

        return group;
    }
}

