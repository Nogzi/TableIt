using Microsoft.AspNetCore.Mvc;
using TableItShared.Models;
using TableItWeb.Controllers;
using TableItWeb.Tests.Infrastructure;

namespace TableItWeb.Tests.Controllers;

public class TablesFloorPlannerTests : IDisposable
{
    private readonly TestDb _db = new();
    private readonly FakeHubContext _hub = new();
    private readonly TablesController _controller;

    public TablesFloorPlannerTests() => _controller = new TablesController(_db.Context, _hub);

    public void Dispose() => _db.Dispose();

    private static Table Copy(Table t) => new()
    {
        Id = t.Id, Number = t.Number, Seats = t.Seats, Shape = t.Shape, X = t.X, Y = t.Y,
        Width = t.Width, Height = t.Height, Rotation = t.Rotation
    };

    private static Table NewTable(int number, int seats = 2) =>
        new() { Id = 0, Number = number, Seats = seats, Shape = TableShape.Rect, X = 1, Y = 2, Width = 3, Height = 4, Rotation = 5 };

    private List<Table> Snapshot()
    {
        using var ctx = _db.NewContext();
        return ctx.Tables.OrderBy(t => t.Id).ToList();
    }

    private static void AssertSameTables(List<Table> expected, List<Table> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            var (e, a) = (expected[i], actual[i]);
            Assert.Equal((e.Id, e.Number, e.Seats, e.Shape, e.X, e.Y, e.Width, e.Height, e.Rotation),
                         (a.Id, a.Number, a.Seats, a.Shape, a.X, a.Y, a.Width, a.Height, a.Rotation));
        }
    }

    [Fact]
    public async Task GetTables_UnorderedInsertion_ReturnsSortedByNumber()
    {
        _db.AddTable(5);
        _db.AddTable(1);
        _db.AddTable(3);

        var result = (await _controller.GetTables()).ValueOrFail();

        Assert.Equal(new[] { 1, 3, 5 }, result.Select(t => t.Number));
    }

    [Fact]
    public async Task SaveLayout_NewTablesWithIdZero_AreInsertedWithRealIds()
    {
        var result = (await _controller.SaveLayout(new() { NewTable(10), NewTable(11, 6) })).ValueOrFail();

        Assert.Equal(2, result.Count);
        Assert.All(result, t => Assert.True(t.Id > 0));
        Assert.Equal(2, Snapshot().Count);
        var t11 = result.Single(t => t.Number == 11);
        Assert.Equal((6, TableShape.Rect, 1d, 2d, 3d, 4d, 5d), (t11.Seats, t11.Shape, t11.X, t11.Y, t11.Width, t11.Height, t11.Rotation));
    }

    [Fact]
    public async Task SaveLayout_ExistingTables_AreUpdatedWithAllFieldsPersisted()
    {
        var a = _db.AddTable(1, seats: 2, shape: TableShape.Round);
        var b = _db.AddTable(2);
        var edited = new Table
        {
            Id = a.Id, Number = 9, Seats = 8, Shape = TableShape.Rect, X = 321.5, Y = 123.25,
            Width = 200, Height = 80, Rotation = 45
        };

        await _controller.SaveLayout(new() { edited, Copy(b) });

        var saved = Snapshot().Single(t => t.Id == a.Id);
        Assert.Equal((9, 8, TableShape.Rect, 321.5, 123.25, 200d, 80d, 45d),
            (saved.Number, saved.Seats, saved.Shape, saved.X, saved.Y, saved.Width, saved.Height, saved.Rotation));
        Assert.Equal(2, Snapshot().Count);
    }

    [Fact]
    public async Task SaveLayout_OmittedTables_AreDeleted()
    {
        var keep = _db.AddTable(1);
        _db.AddTable(2);
        _db.AddTable(3);

        var result = (await _controller.SaveLayout(new() { Copy(keep) })).ValueOrFail();

        Assert.Equal(keep.Id, Assert.Single(result).Id);
        Assert.Equal(keep.Id, Assert.Single(Snapshot()).Id);
    }

    [Fact]
    public async Task SaveLayout_EmptyList_DeletesAllTables()
    {
        _db.AddTable(1);
        _db.AddTable(2);

        var result = (await _controller.SaveLayout(new())).ValueOrFail();

        Assert.Empty(result);
        Assert.Empty(Snapshot());
    }

    [Fact]
    public async Task SaveLayout_MixedInsertUpdateDelete_ReturnsSortedListAndBroadcastsTablesChangedOnce()
    {
        var keep = _db.AddTable(4);
        _db.AddTable(2); // will be deleted
        var update = Copy(keep);
        update.Number = 7;

        var result = (await _controller.SaveLayout(new() { update, NewTable(3) })).ValueOrFail();

        Assert.Equal(new[] { 3, 7 }, result.Select(t => t.Number));
        var sent = Assert.Single(_hub.Sent);
        Assert.Equal("TablesChanged", sent.Method);
        var broadcast = Assert.IsType<List<Table>>(Assert.Single(sent.Args));
        Assert.Equal(new[] { 3, 7 }, broadcast.Select(t => t.Number));
    }

    [Fact]
    public async Task SaveLayout_DuplicateNumbers_ReturnsBadRequestAndLeavesDbUnchangedWithoutBroadcast()
    {
        var a = _db.AddTable(1);
        _db.AddTable(2);
        var before = Snapshot();

        var result = await _controller.SaveLayout(new() { Copy(a), NewTable(1) });

        result.AssertBadRequest();
        AssertSameTables(before, Snapshot());
        Assert.Empty(_hub.Sent);
    }

    [Fact]
    public async Task SaveLayout_SeatsZero_ReturnsBadRequestAndLeavesDbUnchangedWithoutBroadcast()
    {
        var a = _db.AddTable(1);
        var before = Snapshot();
        var bad = Copy(a);
        bad.Seats = 0;

        var result = await _controller.SaveLayout(new() { bad, NewTable(2) });

        result.AssertBadRequest();
        AssertSameTables(before, Snapshot());
        Assert.Empty(_hub.Sent);
    }

    [Fact]
    public async Task SaveLayout_UnknownNonZeroId_ReturnsBadRequestAndLeavesDbUnchangedWithoutBroadcast()
    {
        var a = _db.AddTable(1);
        _db.AddTable(2);
        var before = Snapshot();
        var ghost = NewTable(3);
        ghost.Id = 9999;
        var moved = Copy(a);
        moved.Number = 50;

        var result = await _controller.SaveLayout(new() { moved, ghost });

        result.AssertBadRequest();
        AssertSameTables(before, Snapshot());
        Assert.Empty(_hub.Sent);
    }

    [Fact]
    public async Task SaveLayout_SwappingTwoTableNumbers_Succeeds()
    {
        var one = _db.AddTable(1, seats: 2);
        var two = _db.AddTable(2, seats: 6);
        var a = Copy(one);
        var b = Copy(two);
        a.Number = 2;
        b.Number = 1;

        var result = await _controller.SaveLayout(new() { a, b });

        Assert.IsNotType<BadRequestObjectResult>(result.Result);
        var saved = Snapshot();
        Assert.Equal(2, saved.Single(t => t.Id == one.Id).Number);
        Assert.Equal(1, saved.Single(t => t.Id == two.Id).Number);
        Assert.Equal(new[] { two.Id, one.Id }, result.ValueOrFail().Select(t => t.Id));
    }

    [Fact]
    public async Task SaveLayout_DeletedTable_LeavesItsOrdersReadableWithTableNumber()
    {
        var doomed = _db.AddTable(8);
        var keep = _db.AddTable(9);
        var item = _db.AddMenuItem("Soup");
        var orders = new OrderController(_db.Context, _hub);
        var created = await orders.CreateOrder(new CreateOrderRequest(doomed.Id, null, new() { new(item.Id, 1, null) }));
        var orderId = Assert.IsType<Order>(Assert.IsType<CreatedAtActionResult>(created.Result).Value).Id;

        await _controller.SaveLayout(new() { Copy(keep) });

        using var ctx = _db.NewContext();
        var readController = new OrderController(ctx, new FakeHubContext());
        var read = (await readController.GetOrder(orderId)).ValueOrFail();
        Assert.Equal(8, read.TableNumber);
        Assert.Equal(doomed.Id, read.TableId);
        Assert.Single(read.Lines);
    }
}
