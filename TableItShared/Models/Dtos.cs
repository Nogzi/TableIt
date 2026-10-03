namespace TableItShared.Models;

public record CreateOrderRequest(int TableId, string? Note, List<CreateOrderLine> Lines);

public record CreateOrderLine(int MenuItemId, int Quantity, string? Note);

public record UpdateOrderStatusRequest(OrderStatus Status);
