namespace ApiRoleValidation.Models;

public class OrderReport
{
    public int TotalOrders { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal AverageOrderValue { get; set; }
    public List<TopProductDto> TopSellingProducts { get; set; } = new();
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

public record TopProductDto(string ProductName, int UnitsSold, decimal Revenue);

