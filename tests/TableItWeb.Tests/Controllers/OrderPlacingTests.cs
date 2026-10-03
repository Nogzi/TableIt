using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TableItShared.Models;
using TableItWeb.Controllers;
using TableItWeb.Tests.Infrastructure;

namespace TableItWeb.Tests.Controllers;

public class OrderPlacingTests : IDisposable
{
    private readonly TestDb _db = new();
    private readonly FakeHubContext _hub = new();
    private readonly OrderController _controller;

    public OrderPlacingTests() => _controller = new OrderController(_db.Context, _hub);

    public void Dispose() => _db.Dispose();

    private static CreateOrderRequest Request(int tableId, params CreateOrderLine[] lines) =>
        new CreateOrderRequest(tableId, null, lines.ToList());

    private void AssertNothingHappened()
    {
        Assert.Empty(_hub.Sent);
        using var ctx = _db.NewContext();
        Assert.Empty(ctx.Orders.ToList());
        Assert.Empty(ctx.OrderLines.ToList());
    }

    [Fact]
    public async Task CreateOrder_ValidRequest_ReturnsCreatedAtActionWithNewOrder()
    {
        var table = _db.AddTable(7);
        var item = _db.AddMenuItem("Burger", 120m);
        var before = DateTime.UtcNow;

        var result = await _controller.CreateOrder(
            new CreateOrderRequest(table.Id, "no onions", new() { new CreateOrderLine(item.Id, 2, "well done") }));

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(201, created.StatusCode);
        Assert.Equal(nameof(OrderController.GetOrder), created.ActionName);
        var order = Assert.IsType<Order>(created.Value);
        Assert.Equal(order.Id, created.RouteValues!["id"]);
        Assert.True(order.Id > 0);
        Assert.Equal(OrderStatus.New, order.Status);
        Assert.Equal(table.Id, order.TableId);
        Assert.Equal("no onions", order.Note);
        Assert.Equal(DateTimeKind.Utc, order.CreatedAt.Kind);
        Assert.InRange(order.CreatedAt, before, DateTime.UtcNow);
        Assert.Equal(order.CreatedAt, order.UpdatedAt);
    }

    [Fact]
    public async Task CreateOrder_ValidRequest_SnapshotsTableNumberAndLineNameAndPrice()
    {
        var table = _db.AddTable(12);
        var item = _db.AddMenuItem("Burger", 120.50m);

        var result = await _controller.CreateOrder(Request(table.Id, new CreateOrderLine(item.Id, 3, "rare")));

        var order = Assert.IsType<CreatedAtActionResult>(result.Result).Value as Order;
        Assert.NotNull(order);
        Assert.Equal(12, order!.TableNumber);
        var line = Assert.Single(order.Lines);
        Assert.Equal(item.Id, line.MenuItemId);
        Assert.Equal("Burger", line.Name);
        Assert.Equal(120.50m, line.UnitPrice);
        Assert.Equal(3, line.Quantity);
        Assert.Equal("rare", line.Note);

        using var ctx = _db.NewContext();
        var saved = ctx.Orders.Include(o => o.Lines).Single();
        Assert.Equal(12, saved.TableNumber);
        Assert.Equal("Burger", saved.Lines[0].Name);
        Assert.Equal(120.50m, saved.Lines[0].UnitPrice);
        Assert.Equal(OrderStatus.New, saved.Status);
    }

    [Fact]
    public async Task CreateOrder_ValidRequest_BroadcastsOrderCreatedExactlyOnce()
    {
        var table = _db.AddTable(1);
        var item = _db.AddMenuItem("Soup");

        var result = await _controller.CreateOrder(Request(table.Id, new CreateOrderLine(item.Id, 1, null)));

        var order = Assert.IsType<CreatedAtActionResult>(result.Result).Value;
        var sent = Assert.Single(_hub.Sent);
        Assert.Equal("OrderCreated", sent.Method);
        var arg = Assert.Single(sent.Args);
        Assert.Same(order, arg);
    }

    [Fact]
    public async Task CreateOrder_MenuItemChangedAfterwards_SavedOrderKeepsOriginalNameAndPrice()
    {
        var table = _db.AddTable(1);
        var item = _db.AddMenuItem("Burger", 100m);
        await _controller.CreateOrder(Request(table.Id, new CreateOrderLine(item.Id, 1, null)));

        item.Price = 999m;
        item.Name = "Renamed";
        _db.Context.SaveChanges();

        using var ctx = _db.NewContext();
        var line = ctx.Orders.Include(o => o.Lines).Single().Lines.Single();
        Assert.Equal("Burger", line.Name);
        Assert.Equal(100m, line.UnitPrice);
    }

    [Fact]
    public async Task CreateOrder_UnknownTable_ReturnsBadRequestWithoutSavingOrBroadcasting()
    {
        var item = _db.AddMenuItem("Soup");

        var result = await _controller.CreateOrder(Request(999, new CreateOrderLine(item.Id, 1, null)));

        result.AssertBadRequest();
        AssertNothingHappened();
    }

    [Fact]
    public async Task CreateOrder_EmptyLines_ReturnsBadRequestWithoutSavingOrBroadcasting()
    {
        var table = _db.AddTable(1);

        var result = await _controller.CreateOrder(Request(table.Id));

        result.AssertBadRequest();
        AssertNothingHappened();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task CreateOrder_QuantityBelowOne_ReturnsBadRequestWithoutSavingOrBroadcasting(int quantity)
    {
        var table = _db.AddTable(1);
        var item = _db.AddMenuItem("Soup");

        var result = await _controller.CreateOrder(Request(table.Id, new CreateOrderLine(item.Id, quantity, null)));

        result.AssertBadRequest();
        AssertNothingHappened();
    }

    [Fact]
    public async Task CreateOrder_UnknownMenuItem_ReturnsBadRequestWithoutSavingOrBroadcasting()
    {
        var table = _db.AddTable(1);

        var result = await _controller.CreateOrder(Request(table.Id, new CreateOrderLine(12345, 1, null)));

        result.AssertBadRequest();
        AssertNothingHappened();
    }

    [Fact]
    public async Task CreateOrder_UnavailableMenuItem_ReturnsBadRequestWithoutSavingOrBroadcasting()
    {
        var table = _db.AddTable(1);
        var item = _db.AddMenuItem("Sold out", available: false);

        var result = await _controller.CreateOrder(Request(table.Id, new CreateOrderLine(item.Id, 1, null)));

        result.AssertBadRequest();
        AssertNothingHappened();
    }

    [Fact]
    public async Task CreateOrder_OneValidAndOneUnavailableLine_RejectsWholeOrder()
    {
        var table = _db.AddTable(1);
        var ok = _db.AddMenuItem("Soup");
        var gone = _db.AddMenuItem("Gone", available: false);

        var result = await _controller.CreateOrder(Request(table.Id, new CreateOrderLine(ok.Id, 1, null), new CreateOrderLine(gone.Id, 1, null)));

        result.AssertBadRequest();
        AssertNothingHappened();
    }

    [Fact]
    public async Task CreateOrder_SeveralLinesForSameMenuItem_SavesEveryLine()
    {
        var table = _db.AddTable(1);
        var item = _db.AddMenuItem("Beer", 50m);

        var result = await _controller.CreateOrder(
            Request(table.Id, new CreateOrderLine(item.Id, 1, "first"), new CreateOrderLine(item.Id, 2, "second")));

        Assert.IsType<CreatedAtActionResult>(result.Result);
        using var ctx = _db.NewContext();
        var lines = ctx.Orders.Include(o => o.Lines).Single().Lines.OrderBy(l => l.Id).ToList();
        Assert.Equal(2, lines.Count);
        Assert.All(lines, l => Assert.Equal("Beer", l.Name));
        Assert.Equal(new[] { 1, 2 }, lines.Select(l => l.Quantity));
        Assert.Equal(new[] { "first", "second" }, lines.Select(l => l.Note));
    }
}
