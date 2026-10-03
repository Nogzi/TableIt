using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TableItShared.Models;
using TableItWeb.Data;
using TableItWeb.Hubs;

namespace TableItWeb.Controllers;

[ApiController]
[Route("api/tables")]
public class TablesController : ControllerBase
{
    private readonly TableItDbContext _db;
    private readonly IHubContext<RestaurantHub> _hub;

    public TablesController(TableItDbContext db, IHubContext<RestaurantHub> hub)
    {
        _db = db;
        _hub = hub;
    }

    [HttpGet]
    public async Task<ActionResult<List<Table>>> GetTables() =>
        await _db.Tables.OrderBy(t => t.Number).ToListAsync();

    [HttpPut]
    public async Task<ActionResult<List<Table>>> SaveLayout([FromBody] List<Table> tables)
    {
        if (tables is null)
            return BadRequest("Body must be a list of tables.");
        if (tables.GroupBy(t => t.Number).Any(g => g.Count() > 1))
            return BadRequest("Table numbers must be unique.");
        if (tables.Any(t => t.Seats < 1))
            return BadRequest("Seats must be at least 1.");

        var existing = await _db.Tables.ToListAsync();
        var byId = existing.ToDictionary(t => t.Id);
        var keepIds = tables.Where(t => t.Id != 0).Select(t => t.Id).ToHashSet();

        _db.Tables.RemoveRange(existing.Where(t => !keepIds.Contains(t.Id)));

        foreach (var t in tables)
        {
            if (t.Id == 0)
            {
                _db.Tables.Add(new Table
                {
                    Number = t.Number, Seats = t.Seats, Shape = t.Shape, X = t.X, Y = t.Y,
                    Width = t.Width, Height = t.Height, Rotation = t.Rotation
                });
            }
            else if (byId.TryGetValue(t.Id, out var e))
            {
                e.Number = t.Number;
                e.Seats = t.Seats;
                e.Shape = t.Shape;
                e.X = t.X;
                e.Y = t.Y;
                e.Width = t.Width;
                e.Height = t.Height;
                e.Rotation = t.Rotation;
            }
            else
            {
                return BadRequest($"Table {t.Id} does not exist.");
            }
        }

        await _db.SaveChangesAsync();
        var result = await _db.Tables.OrderBy(t => t.Number).ToListAsync();
        await _hub.Clients.All.SendAsync("TablesChanged", result);
        return result;
    }
}
