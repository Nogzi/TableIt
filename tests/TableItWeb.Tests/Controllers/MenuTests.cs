using Microsoft.AspNetCore.Mvc;
using TableItShared.Models;
using TableItWeb.Controllers;
using TableItWeb.Tests.Infrastructure;

namespace TableItWeb.Tests.Controllers;

public class MenuTests : IDisposable
{
    private readonly TestDb _db = new();
    private readonly FakeHubContext _hub = new();
    private readonly MenuController _controller;

    public MenuTests() => _controller = new MenuController(_db.Context, _hub);

    public void Dispose() => _db.Dispose();

    private static MenuItem Body(string name, decimal price = 10m, string category = "Mains", bool available = true, int id = 0) =>
        new() { Id = id, Name = name, Description = "desc", Category = category, Price = price, IsAvailable = available };

    private void AssertSingleMenuChangedBroadcast(params string[] expectedNamesInOrder)
    {
        var sent = Assert.Single(_hub.Sent);
        Assert.Equal("MenuChanged", sent.Method);
        var list = Assert.IsType<List<MenuItem>>(Assert.Single(sent.Args));
        Assert.Equal(expectedNamesInOrder, list.Select(m => m.Name));
    }

    [Fact]
    public async Task GetMenu_MixedItems_SortedByCategoryThenNameAndIncludesUnavailable()
    {
        _db.AddMenuItem("Zebra cake", category: "Desserts");
        _db.AddMenuItem("Coffee", category: "Drinks");
        _db.AddMenuItem("Beer", category: "Drinks", available: false);
        _db.AddMenuItem("Steak", category: "Mains");

        var result = (await _controller.GetMenu()).ValueOrFail();

        Assert.Equal(new[] { "Zebra cake", "Beer", "Coffee", "Steak" }, result.Select(m => m.Name));
        Assert.Contains(result, m => !m.IsAvailable);
    }

    [Fact]
    public async Task Create_ClientSuppliedId_IsIgnoredAndRealIdAssigned()
    {
        _db.AddMenuItem("Existing"); // occupies id 1

        var result = await _controller.Create(Body("New", id: 1));

        var created = Assert.IsType<CreatedResult>(result.Result);
        Assert.Equal(201, created.StatusCode);
        var item = Assert.IsType<MenuItem>(created.Value);
        Assert.NotEqual(1, item.Id);
        Assert.True(item.Id > 0);
        Assert.Equal($"/api/menu/{item.Id}", created.Location);
        using var ctx = _db.NewContext();
        Assert.Equal(2, ctx.MenuItems.Count());
        Assert.Equal("Existing", ctx.MenuItems.Single(m => m.Id == 1).Name);
    }

    [Fact]
    public async Task Create_ValidItem_PersistsAllFieldsAndBroadcastsFullList()
    {
        _db.AddMenuItem("Apple", category: "Desserts");

        await _controller.Create(Body("Burger", 99.5m, "Mains", false));

        using var ctx = _db.NewContext();
        var saved = ctx.MenuItems.Single(m => m.Name == "Burger");
        Assert.Equal((99.5m, "Mains", "desc", false), (saved.Price, saved.Category, saved.Description, saved.IsAvailable));
        AssertSingleMenuChangedBroadcast("Apple", "Burger");
    }

    [Fact]
    public async Task Update_ExistingItem_UpdatesAllFieldsAndBroadcastsFullList()
    {
        var other = _db.AddMenuItem("Other", category: "A");
        var item = _db.AddMenuItem("Old", 10m, "Mains");

        var result = await _controller.Update(item.Id, Body("New", 25m, "Drinks", false));

        var updated = result.ValueOrFail();
        Assert.Equal((item.Id, "New", 25m, "Drinks", false), (updated.Id, updated.Name, updated.Price, updated.Category, updated.IsAvailable));
        using var ctx = _db.NewContext();
        var saved = ctx.MenuItems.Single(m => m.Id == item.Id);
        Assert.Equal(("New", 25m, "Drinks", false), (saved.Name, saved.Price, saved.Category, saved.IsAvailable));
        Assert.Equal("Other", ctx.MenuItems.Single(m => m.Id == other.Id).Name);
        AssertSingleMenuChangedBroadcast("Other", "New");
    }

    [Fact]
    public async Task Update_BodyIdDiffersFromRouteId_UsesRouteId()
    {
        var a = _db.AddMenuItem("A");
        var b = _db.AddMenuItem("B");

        await _controller.Update(a.Id, Body("Renamed", id: b.Id));

        using var ctx = _db.NewContext();
        Assert.Equal("Renamed", ctx.MenuItems.Single(m => m.Id == a.Id).Name);
        Assert.Equal("B", ctx.MenuItems.Single(m => m.Id == b.Id).Name);
    }

    [Fact]
    public async Task Update_UnknownId_ReturnsNotFoundWithoutBroadcast()
    {
        var result = await _controller.Update(42, Body("X"));

        Assert.IsType<NotFoundResult>(result.Result);
        Assert.Empty(_hub.Sent);
    }

    [Fact]
    public async Task Delete_ExistingItem_ReturnsNoContentRemovesItAndBroadcastsRemainingList()
    {
        var gone = _db.AddMenuItem("Gone");
        _db.AddMenuItem("Stays");

        var result = await _controller.Delete(gone.Id);

        Assert.IsType<NoContentResult>(result);
        using var ctx = _db.NewContext();
        Assert.Equal("Stays", Assert.Single(ctx.MenuItems.ToList()).Name);
        AssertSingleMenuChangedBroadcast("Stays");
    }

    [Fact]
    public async Task Delete_UnknownId_ReturnsNotFoundWithoutBroadcast()
    {
        var result = await _controller.Delete(42);

        Assert.IsType<NotFoundResult>(result);
        Assert.Empty(_hub.Sent);
    }

    [Fact]
    public async Task Delete_ItemUsedByPastOrder_LeavesOrderSnapshotLinesIntact()
    {
        var table = _db.AddTable(1);
        var item = _db.AddMenuItem("Burger", 120m);
        var orders = new OrderController(_db.Context, new FakeHubContext());
        var created = await orders.CreateOrder(new CreateOrderRequest(table.Id, null, new() { new(item.Id, 2, null) }));
        var orderId = Assert.IsType<Order>(Assert.IsType<CreatedAtActionResult>(created.Result).Value).Id;

        var result = await _controller.Delete(item.Id);

        Assert.IsType<NoContentResult>(result);
        using var ctx = _db.NewContext();
        var readController = new OrderController(ctx, new FakeHubContext());
        var order = (await readController.GetOrder(orderId)).ValueOrFail();
        var line = Assert.Single(order.Lines);
        Assert.Equal(("Burger", 120m, 2, item.Id), (line.Name, line.UnitPrice, line.Quantity, line.MenuItemId));
    }
}
