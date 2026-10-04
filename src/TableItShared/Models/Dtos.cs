using System.ComponentModel.DataAnnotations;

namespace TableItShared.Models;

public record CreateOrderRequest(
    int TableId,
    [MaxLength(500)] string? Note,
    [MaxLength(50)] List<CreateOrderLine> Lines,
    Guid? ClientRequestId = null);

public record CreateOrderLine(
    int MenuItemId,
    [Range(1, 99)] int Quantity,
    [MaxLength(500)] string? Note);

public record UpdateOrderStatusRequest(OrderStatus Status);
