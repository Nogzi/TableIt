using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TableItShared.Models;
using TableItWeb.Data;

namespace TableItWeb.Tests.Infrastructure;

/// <summary>
/// An EMPTY (unseeded) in-memory SQLite database. The connection stays open for the lifetime of the instance.
/// </summary>
public sealed class TestDb : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<TableItDbContext> _options;

    public TableItDbContext Context { get; }

    public TestDb()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<TableItDbContext>().UseSqlite(_connection).Options;
        Context = new TableItDbContext(_options);
        Context.Database.EnsureCreated();
    }

    /// <summary>A fresh context on the same database, with an empty change tracker (for verifying persisted state).</summary>
    public TableItDbContext NewContext() => new(_options);

    public Table AddTable(int number, int seats = 4, TableShape shape = TableShape.Round)
    {
        var t = new Table { Number = number, Seats = seats, Shape = shape, X = 100, Y = 100, Width = 90, Height = 90 };
        Context.Tables.Add(t);
        Context.SaveChanges();
        return t;
    }

    public MenuItem AddMenuItem(string name, decimal price = 100m, string category = "Mains", bool available = true)
    {
        var m = new MenuItem { Name = name, Category = category, Price = price, Description = "", IsAvailable = available };
        Context.MenuItems.Add(m);
        Context.SaveChanges();
        return m;
    }

    public void Dispose()
    {
        Context.Dispose();
        _connection.Dispose();
    }
}
