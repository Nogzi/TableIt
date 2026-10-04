using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using TableItWeb.Data;

namespace TableItWeb.Tests.Controllers;

public class MigrationTests
{
    [Fact]
    public void Migrate_OnEmptyFile_CreatesSchemaMatchingTheModel()
    {
        var path = Path.Combine(Path.GetTempPath(), $"tableit-mig-{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<TableItDbContext>()
                .UseSqlite($"Data Source={path}").Options;
            using (var ctx = new TableItDbContext(options))
            {
                ctx.Database.Migrate();

                Assert.Empty(ctx.Database.GetPendingMigrations());
                var differ = ctx.GetService<IMigrationsModelDiffer>();
                var snapshotModel = ctx.GetService<IMigrationsAssembly>().ModelSnapshot!.Model;
                var designTime = ctx.GetService<IDesignTimeModel>().Model;
                Assert.False(differ.HasDifferences(
                    ctx.GetService<IModelRuntimeInitializer>().Initialize(snapshotModel).GetRelationalModel(),
                    designTime.GetRelationalModel()));
                Assert.Empty(ctx.Orders);
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            foreach (var suffix in new[] { "", "-wal", "-shm", "-journal" })
                try { File.Delete(path + suffix); } catch (IOException) { }
        }
    }
}
