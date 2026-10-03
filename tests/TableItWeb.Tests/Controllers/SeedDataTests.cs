using TableItWeb.Data;
using TableItWeb.Tests.Infrastructure;

namespace TableItWeb.Tests.Controllers;

public class SeedDataTests
{
    [Fact]
    public void Seed_EmptyDatabase_AddsTablesWithUniqueNumbersAndAtLeastOneSeat()
    {
        using var db = new TestDb();

        SeedData.Seed(db.Context);

        using var ctx = db.NewContext();
        var tables = ctx.Tables.ToList();
        Assert.NotEmpty(tables);
        Assert.Equal(tables.Count, tables.Select(t => t.Number).Distinct().Count());
        Assert.All(tables, t => Assert.True(t.Seats >= 1));
    }

    [Fact]
    public void Seed_EmptyDatabase_AddsOnlyAvailableMenuItemsWithPositivePrices()
    {
        using var db = new TestDb();

        SeedData.Seed(db.Context);

        using var ctx = db.NewContext();
        var items = ctx.MenuItems.ToList();
        Assert.NotEmpty(items);
        Assert.All(items, m =>
        {
            Assert.True(m.IsAvailable);
            Assert.False(string.IsNullOrWhiteSpace(m.Name));
            Assert.False(string.IsNullOrWhiteSpace(m.Category));
            Assert.True(m.Price > 0);
        });
    }

    [Fact]
    public void Seed_RunTwice_DoesNotDuplicateAnything()
    {
        using var db = new TestDb();
        SeedData.Seed(db.Context);
        var tables = db.Context.Tables.Count();
        var items = db.Context.MenuItems.Count();

        SeedData.Seed(db.Context);

        using var ctx = db.NewContext();
        Assert.Equal(tables, ctx.Tables.Count());
        Assert.Equal(items, ctx.MenuItems.Count());
    }

    [Fact]
    public void Seed_TablesAlreadyExist_DoesNotAddMoreTables()
    {
        using var db = new TestDb();
        db.AddTable(99);

        SeedData.Seed(db.Context);

        using var ctx = db.NewContext();
        Assert.Equal(99, Assert.Single(ctx.Tables.ToList()).Number);
    }
}
