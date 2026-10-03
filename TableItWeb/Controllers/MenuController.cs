using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TableItShared.Models;
using TableItWeb.Data;
using TableItWeb.Hubs;

namespace TableItWeb.Controllers;

[ApiController]
[Route("api/menu")]
public class MenuController : ControllerBase
{
    private readonly TableItDbContext _db;
    private readonly IHubContext<RestaurantHub> _hub;

    public MenuController(TableItDbContext db, IHubContext<RestaurantHub> hub)
    {
        _db = db;
        _hub = hub;
    }

    private Task<List<MenuItem>> AllItems() =>
        _db.MenuItems.OrderBy(m => m.Category).ThenBy(m => m.Name).ToListAsync();

    private async Task Broadcast() =>
        await _hub.Clients.All.SendAsync("MenuChanged", await AllItems());

    [HttpGet]
    public async Task<ActionResult<List<MenuItem>>> GetMenu() => await AllItems();

    [HttpPost]
    public async Task<ActionResult<MenuItem>> Create([FromBody] MenuItem item)
    {
        item.Id = 0;
        _db.MenuItems.Add(item);
        await _db.SaveChangesAsync();
        await Broadcast();
        return Created($"/api/menu/{item.Id}", item);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<MenuItem>> Update(int id, [FromBody] MenuItem item)
    {
        var existing = await _db.MenuItems.FindAsync(id);
        if (existing is null)
            return NotFound();

        existing.Name = item.Name;
        existing.Description = item.Description;
        existing.Category = item.Category;
        existing.Price = item.Price;
        existing.IsAvailable = item.IsAvailable;
        await _db.SaveChangesAsync();
        await Broadcast();
        return existing;
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var existing = await _db.MenuItems.FindAsync(id);
        if (existing is null)
            return NotFound();

        _db.MenuItems.Remove(existing);
        await _db.SaveChangesAsync();
        await Broadcast();
        return NoContent();
    }
}
