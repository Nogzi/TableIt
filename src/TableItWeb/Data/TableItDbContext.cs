using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using TableItShared.Models;

namespace TableItWeb.Data;

public class TableItDbContext : DbContext
{
    public TableItDbContext(DbContextOptions<TableItDbContext> options) : base(options)
    {
    }

    public DbSet<Table> Tables => Set<Table>();
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderLine> OrderLines => Set<OrderLine>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Table>().Property(t => t.Shape).HasConversion<string>();

        modelBuilder.Entity<MenuItem>().Property(m => m.Price).HasConversion<double>();

        modelBuilder.Entity<Order>(e =>
        {
            e.Property(o => o.Status).HasConversion<string>();
            e.HasIndex(o => o.ClientRequestId).IsUnique();
            e.HasMany(o => o.Lines)
                .WithOne()
                .HasForeignKey(l => l.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OrderLine>().Property(l => l.UnitPrice).HasConversion<double>();

        // SQLite drops DateTimeKind; make values come back as UTC so JSON gets a "Z" suffix.
        var utcConverter = new ValueConverter<DateTime, DateTime>(
            v => v.Kind == DateTimeKind.Local ? v.ToUniversalTime() : v,
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        modelBuilder.Entity<Order>().Property(o => o.CreatedAt).HasConversion(utcConverter);
        modelBuilder.Entity<Order>().Property(o => o.UpdatedAt).HasConversion(utcConverter);
    }
}
