using Microsoft.AspNetCore.Mvc;
using TableItShared.Models;
using TableItWeb.Controllers;
using TableItWeb.Tests.Infrastructure;

namespace TableItWeb.Tests.Controllers;

public class OrderStatusTests : IDisposable
{
    private readonly TestDb _db = new();
    private readonly FakeHubContext _hub = new();
    private readonly OrderController _controller;

    public OrderStatusTests() => _controller = new OrderController(_db.Context, _hub);

    public void Dispose() => _db.Dispose();

    private Order AddOrder()
    {
        var table = _db.AddTable(1);
        var past = DateTime.UtcNow.AddMinutes(-10);
        var order = new Order
        {
            TableId = table.Id, TableNumber = 1, CreatedAt = past, UpdatedAt = past, Status = OrderStatus.New,
            Lines = { new OrderLine { MenuItemId = 1, Name = "X", UnitPrice = 1m, Quantity = 1 } }
        };
        _db.Context.Orders.Add(order);
        _db.Context.SaveChanges();
        return order;
    }

    [Fact]
    public async Task UpdateStatus_WalkingThroughWorkflow_PersistsEachStatusAndBroadcastsEachStep()
    {
        var order = AddOrder();
        var previousUpdatedAt = order.UpdatedAt;
        var steps = new[] { OrderStatus.InProgress, OrderStatus.Ready, OrderStatus.Served };

        for (var i = 0; i < steps.Length; i++)
        {
            var result = await _controller.UpdateStatus(order.Id, new UpdateOrderStatusRequest(steps[i]));

            var returned = result.ValueOrFail();
            Assert.Equal(steps[i], returned.Status);
            Assert.True(returned.UpdatedAt > previousUpdatedAt);
            previousUpdatedAt = returned.UpdatedAt;

            using var ctx = _db.NewContext();
            Assert.Equal(steps[i], ctx.Orders.Single().Status);

            Assert.Equal(i + 1, _hub.Sent.Count);
            Assert.Equal("OrderUpdated", _hub.Sent[i].Method);
            var broadcast = Assert.IsType<Order>(Assert.Single(_hub.Sent[i].Args));
            Assert.Equal(steps[i], broadcast.Status);
        }
    }

    [Fact]
    public async Task UpdateStatus_Cancelled_SetsStatusAndBroadcasts()
    {
        var order = AddOrder();

        var result = await _controller.UpdateStatus(order.Id, new UpdateOrderStatusRequest(OrderStatus.Cancelled));

        Assert.Equal(OrderStatus.Cancelled, result.ValueOrFail().Status);
        using var ctx = _db.NewContext();
        Assert.Equal(OrderStatus.Cancelled, ctx.Orders.Single().Status);
        Assert.Equal("OrderUpdated", Assert.Single(_hub.Sent).Method);
    }

    [Fact]
    public async Task UpdateStatus_UpdatedAtIsRefreshedButCreatedAtIsNot()
    {
        var order = AddOrder();
        var createdAt = order.CreatedAt;

        var result = await _controller.UpdateStatus(order.Id, new UpdateOrderStatusRequest(OrderStatus.InProgress));

        var returned = result.ValueOrFail();
        Assert.Equal(createdAt, returned.CreatedAt);
        Assert.Equal(DateTimeKind.Utc, returned.UpdatedAt.Kind);
        Assert.True((DateTime.UtcNow - returned.UpdatedAt) < TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task UpdateStatus_ReturnedOrderStillContainsLines()
    {
        var order = AddOrder();

        var result = await _controller.UpdateStatus(order.Id, new UpdateOrderStatusRequest(OrderStatus.InProgress));

        Assert.Single(result.ValueOrFail().Lines);
    }

    [Fact]
    public async Task UpdateStatus_UnknownId_ReturnsNotFoundWithoutBroadcast()
    {
        var result = await _controller.UpdateStatus(777, new UpdateOrderStatusRequest(OrderStatus.Ready));

        Assert.IsType<NotFoundResult>(result.Result);
        Assert.Empty(_hub.Sent);
    }

    private Order AddOrderWithStatus(OrderStatus status)
    {
        var order = AddOrder();
        order.Status = status;
        _db.Context.SaveChanges();
        return order;
    }

    public static IEnumerable<object[]> Transitions()
    {
        var allowed = new Dictionary<OrderStatus, OrderStatus[]>
        {
            [OrderStatus.New] = new[] { OrderStatus.InProgress, OrderStatus.Cancelled },
            [OrderStatus.InProgress] = new[] { OrderStatus.Ready, OrderStatus.New, OrderStatus.Cancelled },
            [OrderStatus.Ready] = new[] { OrderStatus.Served, OrderStatus.InProgress, OrderStatus.Cancelled },
            [OrderStatus.Served] = new[] { OrderStatus.Ready },
            [OrderStatus.Cancelled] = Array.Empty<OrderStatus>()
        };
        foreach (var from in Enum.GetValues<OrderStatus>())
            foreach (var to in Enum.GetValues<OrderStatus>())
                if (from != to)
                    yield return new object[] { from, to, allowed[from].Contains(to) };
    }

    [Theory]
    [MemberData(nameof(Transitions))]
    public async Task UpdateStatus_TransitionTable_AllowsOrRejects(OrderStatus from, OrderStatus to, bool allowed)
    {
        var order = AddOrderWithStatus(from);

        var result = await _controller.UpdateStatus(order.Id, new UpdateOrderStatusRequest(to));

        using var ctx = _db.NewContext();
        if (allowed)
        {
            Assert.Equal(to, result.ValueOrFail().Status);
            Assert.Equal(to, ctx.Orders.Single().Status);
            Assert.Single(_hub.Sent);
        }
        else
        {
            var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
            Assert.Equal(409, conflict.StatusCode);
            Assert.Contains(from.ToString(), (string)conflict.Value!);
            Assert.Equal(from, ctx.Orders.Single().Status);
            Assert.Empty(_hub.Sent);
        }
    }

    [Theory]
    [InlineData(OrderStatus.New)]
    [InlineData(OrderStatus.Served)]
    [InlineData(OrderStatus.Cancelled)]
    public async Task UpdateStatus_SameStatus_IsNoOpOkWithoutBroadcast(OrderStatus status)
    {
        var order = AddOrderWithStatus(status);
        var updatedAt = order.UpdatedAt;

        var result = await _controller.UpdateStatus(order.Id, new UpdateOrderStatusRequest(status));

        Assert.Equal(status, result.ValueOrFail().Status);
        Assert.Empty(_hub.Sent);
        using var ctx = _db.NewContext();
        Assert.Equal(updatedAt, ctx.Orders.Single().UpdatedAt);
    }
}
