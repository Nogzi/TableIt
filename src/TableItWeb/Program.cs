using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using TableItWeb.Data;
using TableItWeb.Hubs;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSignalR()
    .AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddDbContext<TableItDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TableItDbContext>();
    // WAL lets readers and the writer overlap; the journal mode is persisted in the database file.
    db.Database.OpenConnection();
    db.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
    db.Database.CloseConnection();
    db.Database.Migrate();
    if (app.Configuration.GetValue<bool>("Seed:DemoData"))
        SeedData.Seed(db);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();
app.MapHub<RestaurantHub>("/hubs/restaurant");

app.Run();

public partial class Program { }
