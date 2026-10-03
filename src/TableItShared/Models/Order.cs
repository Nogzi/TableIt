namespace TableItShared.Models;

public enum OrderStatus
{
    New,
    InProgress,
    Ready,
    Served,
    Cancelled
}

public class Order
{
    public int Id { get; set; }
    public int TableId { get; set; }
    /// <summary>Snapshot of the table number at creation.</summary>
    public int TableNumber { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public OrderStatus Status { get; set; }
    public string? Note { get; set; }
    public List<OrderLine> Lines { get; set; } = new();
}

public class OrderLine
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int MenuItemId { get; set; }
    public string Name { get; set; } = "";
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public string? Note { get; set; }
}
