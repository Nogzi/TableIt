using Microsoft.Extensions.Configuration;
using TableItWeb.Services;
using Microsoft.AspNetCore.Mvc;
using TableItShared.Models;
using TableItWeb.Controllers;
using TableItWeb.Tests.Infrastructure;

namespace TableItWeb.Tests.Controllers;

public class OrderReadingTests : IDisposable
{
    private readonly TestDb _db = new();
    private readonly OrderController _controller;

    public OrderReadingTests() => _controller = new OrderController(_db.Context, new FakeHubContext());

    public void Dispose() => _db.Dispose();

    private Order AddOrder(Table table, DateTime? createdAt = null, OrderStatus status = OrderStatus.New, int lines = 1)
    {
        var at = createdAt ?? DateTime.UtcNow;
        var order = new Order
        {
            TableId = table.Id, TableNumber = table.Number, CreatedAt = at, UpdatedAt = at, Status = status,
            Lines = Enumerable.Range(1, lines)
                .Select(i => new OrderLine { MenuItemId = i, Name = $"Item{i}", UnitPrice = 10m * i, Quantity = i })
                .ToList()
        };
        _db.Context.Orders.Add(order);
        _db.Context.SaveChanges();
        return order;
    }

    [Fact]
    public async Task GetOrder_ExistingId_ReturnsOrderWithLines()
    {
        var table = _db.AddTable(3);
        var order = AddOrder(table, lines: 2);

        var result = await _controller.GetOrder(order.Id);

        var found = result.ValueOrFail();
        Assert.Equal(order.Id, found.Id);
        Assert.Equal(3, found.TableNumber);
        Assert.Equal(2, found.Lines.Count);
    }

    [Fact]
    public async Task GetOrder_UnknownId_ReturnsNotFound()
    {
        var result = await _controller.GetOrder(404);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetOrders_FilterByTableId_ReturnsOnlyThatTablesOrders()
    {
        var t1 = _db.AddTable(1);
        var t2 = _db.AddTable(2);
        AddOrder(t1);
        var wanted = AddOrder(t2);

        var result = (await _controller.GetOrders(t2.Id, null)).ValueOrFail();

        Assert.Equal(wanted.Id, Assert.Single(result).Id);
    }

    [Fact]
    public async Task GetOrders_FilterByStatus_ReturnsOnlyMatchingStatus()
    {
        var t = _db.AddTable(1);
        AddOrder(t, status: OrderStatus.New);
        var ready = AddOrder(t, status: OrderStatus.Ready);
        AddOrder(t, status: OrderStatus.Served);

        var result = (await _controller.GetOrders(null, OrderStatus.Ready)).ValueOrFail();

        Assert.Equal(ready.Id, Assert.Single(result).Id);
    }

    [Fact]
    public async Task GetOrders_CombinedTableAndStatusFilter_AppliesBoth()
    {
        var t1 = _db.AddTable(1);
        var t2 = _db.AddTable(2);
        AddOrder(t1, status: OrderStatus.Ready);
        AddOrder(t2, status: OrderStatus.New);
        var wanted = AddOrder(t2, status: OrderStatus.Ready);

        var result = (await _controller.GetOrders(t2.Id, OrderStatus.Ready)).ValueOrFail();

        Assert.Equal(wanted.Id, Assert.Single(result).Id);
    }

    [Fact]
    public async Task GetOrders_UsesConfiguredServiceDayStart()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Restaurant:TimeZone"] = "Pacific/Auckland",
                ["Restaurant:ServiceDayStartHour"] = "9",
            }).Build();
        var controller = new OrderController(_db.Context, new FakeHubContext(), config);
        var start = ServiceDay.StartUtc(config);
        var t = _db.AddTable(1);
        var before = AddOrder(t, start.AddMinutes(-1));
        var after = AddOrder(t, start.AddMinutes(1));

        var result = (await controller.GetOrders(null, null)).ValueOrFail();

        Assert.Equal(after.Id, Assert.Single(result).Id);
        Assert.DoesNotContain(result, o => o.Id == before.Id);
    }

    [Fact]
    public async Task GetOrders_OrderFromTwoDaysAgo_ExcludedByDefault()
    {
        var t = _db.AddTable(1);
        var old = AddOrder(t, DateTime.UtcNow.AddDays(-2));
        var today = AddOrder(t);

        var result = (await _controller.GetOrders(null, null)).ValueOrFail();

        Assert.Equal(today.Id, Assert.Single(result).Id);
        Assert.DoesNotContain(result, o => o.Id == old.Id);
    }

    [Fact]
    public async Task GetOrders_OrderFromTwoDaysAgo_IncludedWhenAllIsTrue()
    {
        var t = _db.AddTable(1);
        var old = AddOrder(t, DateTime.UtcNow.AddDays(-2));
        var today = AddOrder(t);

        var result = (await _controller.GetOrders(null, null, all: true)).ValueOrFail();

        Assert.Equal(new[] { old.Id, today.Id }, result.Select(o => o.Id));
    }

    [Fact]
    public async Task GetOrders_MultipleOrders_OrderedByCreatedAtAscending()
    {
        var t = _db.AddTable(1);
        var now = DateTime.UtcNow;
        var latest = AddOrder(t, now);
        var earliest = AddOrder(t, now.AddMinutes(-30));
        var middle = AddOrder(t, now.AddMinutes(-10));

        // all: true so the result doesn't depend on whether "now - 30 min" falls before the 05:00 service-day cutoff.
        var result = (await _controller.GetOrders(null, null, all: true)).ValueOrFail();

        Assert.Equal(new[] { earliest.Id, middle.Id, latest.Id }, result.Select(o => o.Id));
    }

    [Fact]
    public async Task GetOrders_Results_IncludeLinesAndUtcTimestamps()
    {
        var t = _db.AddTable(1);
        AddOrder(t, lines: 3);

        var result = (await _controller.GetOrders(null, null)).ValueOrFail();

        var order = Assert.Single(result);
        Assert.Equal(3, order.Lines.Count);
        Assert.Equal(DateTimeKind.Utc, order.CreatedAt.Kind);
    }
}
