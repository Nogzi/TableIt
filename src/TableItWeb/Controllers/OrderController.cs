using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TableItShared.Models;
using TableItWeb.Data;
using TableItWeb.Hubs;
using TableItWeb.Services;

namespace TableItWeb.Controllers;

[ApiController]
[Route("api/orders")]
public class OrderController : ControllerBase
{
    private readonly TableItDbContext _db;
    private readonly IHubContext<RestaurantHub> _hub;
    private readonly IConfiguration? _config;

    public OrderController(TableItDbContext db, IHubContext<RestaurantHub> hub, IConfiguration? config = null)
    {
        _db = db;
        _hub = hub;
        _config = config;
    }

    [HttpGet]
    public async Task<ActionResult<List<Order>>> GetOrders(
        [FromQuery] int? tableId, [FromQuery] OrderStatus? status, [FromQuery] bool all = false)
    {
        var query = _db.Orders.Include(o => o.Lines).AsQueryable();

        if (!all)
        {
            var start = ServiceDay.StartUtc(_config);
            query = query.Where(o => o.CreatedAt >= start);
        }
        if (tableId.HasValue)
            query = query.Where(o => o.TableId == tableId.Value);
        if (status.HasValue)
            query = query.Where(o => o.Status == status.Value);

        return await query.OrderBy(o => o.CreatedAt).ToListAsync();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Order>> GetOrder(int id)
    {
        var order = await _db.Orders.Include(o => o.Lines).FirstOrDefaultAsync(o => o.Id == id);
        return order is null ? NotFound() : order;
    }

    private static readonly Dictionary<OrderStatus, OrderStatus[]> AllowedTransitions = new()
    {
        [OrderStatus.New] = new[] { OrderStatus.InProgress, OrderStatus.Cancelled },
        [OrderStatus.InProgress] = new[] { OrderStatus.Ready, OrderStatus.New, OrderStatus.Cancelled },
        [OrderStatus.Ready] = new[] { OrderStatus.Served, OrderStatus.InProgress, OrderStatus.Cancelled },
        [OrderStatus.Served] = new[] { OrderStatus.Ready },
        [OrderStatus.Cancelled] = Array.Empty<OrderStatus>()
    };

    private Task<Order?> FindByClientRequestId(Guid id) =>
        _db.Orders.Include(o => o.Lines).FirstOrDefaultAsync(o => o.ClientRequestId == id);

    [HttpPost]
    public async Task<ActionResult<Order>> CreateOrder([FromBody] CreateOrderRequest request)
    {
        // A retry of an already-placed order returns the original instead of creating a duplicate.
        if (request.ClientRequestId is { } requestId && await FindByClientRequestId(requestId) is { } existing)
            return Ok(existing);

        var table = await _db.Tables.FirstOrDefaultAsync(t => t.Id == request.TableId);
        if (table is null)
            return BadRequest("Table does not exist.");
        if (request.Lines is null || request.Lines.Count == 0)
            return BadRequest("Order must contain at least one line.");
        if (request.Lines.Any(l => l.Quantity < 1))
            return BadRequest("Quantity must be at least 1.");

        var ids = request.Lines.Select(l => l.MenuItemId).Distinct().ToList();
        var items = await _db.MenuItems.Where(m => ids.Contains(m.Id)).ToDictionaryAsync(m => m.Id);
        foreach (var id in ids)
        {
            if (!items.TryGetValue(id, out var mi))
                return BadRequest($"Menu item {id} does not exist.");
            if (!mi.IsAvailable)
                return BadRequest($"Menu item '{mi.Name}' is not available.");
        }

        var now = DateTime.UtcNow;
        var order = new Order
        {
            TableId = table.Id,
            TableNumber = table.Number,
            CreatedAt = now,
            UpdatedAt = now,
            Status = OrderStatus.New,
            Note = request.Note,
            ClientRequestId = request.ClientRequestId,
            Lines = request.Lines.Select(l => new OrderLine
            {
                MenuItemId = l.MenuItemId,
                Name = items[l.MenuItemId].Name,
                UnitPrice = items[l.MenuItemId].Price,
                Quantity = l.Quantity,
                Note = l.Note
            }).ToList()
        };

        _db.Orders.Add(order);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException) when (request.ClientRequestId.HasValue)
        {
            // A concurrent request with the same ID won the race; return its order.
            _db.ChangeTracker.Clear();
            if (await FindByClientRequestId(request.ClientRequestId.Value) is { } winner)
                return Ok(winner);
            throw;
        }
        await _hub.Clients.All.SendAsync("OrderCreated", order);

        return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, order);
    }


    [HttpPatch("{id:int}/status")]
    public async Task<ActionResult<Order>> UpdateStatus(int id, [FromBody] UpdateOrderStatusRequest request)
    {
        var order = await _db.Orders.Include(o => o.Lines).FirstOrDefaultAsync(o => o.Id == id);
        if (order is null)
            return NotFound();

        if (order.Status == request.Status)
            return order;
        if (!AllowedTransitions[order.Status].Contains(request.Status))
            return Conflict($"Cannot change an order from {order.Status} to {request.Status}.");

        order.Status = request.Status;
        order.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await _hub.Clients.All.SendAsync("OrderUpdated", order);

        return order;
    }
}
