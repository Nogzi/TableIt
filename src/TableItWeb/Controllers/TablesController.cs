using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TableItShared.Models;
using TableItWeb.Data;
using TableItWeb.Hubs;
using TableItWeb.Services;

namespace TableItWeb.Controllers;

[ApiController]
[Route("api/tables")]
public class TablesController : ControllerBase
{
    private readonly TableItDbContext _db;
    private readonly IHubContext<RestaurantHub> _hub;
    private readonly IConfiguration? _config;

    public TablesController(TableItDbContext db, IHubContext<RestaurantHub> hub, IConfiguration? config = null)
    {
        _db = db;
        _hub = hub;
        _config = config;
    }

    private ModelStateDictionary? DeferredErrors() =>
        HttpContext?.Items[DeferModelErrorsAttribute.ItemKey] as ModelStateDictionary;

    [HttpGet]
    public async Task<ActionResult<List<Table>>> GetTables() =>
        await _db.Tables.OrderBy(t => t.Number).ToListAsync();

    [HttpPut]
    [DeferModelErrors]
    public async Task<ActionResult<List<Table>>> SaveLayout([FromBody] List<Table> tables)
    {
        if (tables is null)
            return DeferredErrors() is { IsValid: false } bad
                ? ValidationProblem(bad)
                : BadRequest("Body must be a list of tables.");
        if (tables.GroupBy(t => t.Number).Any(g => g.Count() > 1))
            return BadRequest("Table numbers must be unique.");
        if (tables.Any(t => t.Seats < 1))
            return BadRequest("Seats must be at least 1.");

        // Annotation errors (ranges, lengths) are reported after the basic checks above, so those keep their messages.
        if (DeferredErrors() is { IsValid: false } deferred)
            return ValidationProblem(deferred);

        var existing = await _db.Tables.ToListAsync();
        var byId = existing.ToDictionary(t => t.Id);
        var keepIds = tables.Where(t => t.Id != 0).Select(t => t.Id).ToHashSet();

        var removed = existing.Where(t => !keepIds.Contains(t.Id)).ToList();
        if (removed.Count > 0)
        {
            var removedIds = removed.Select(t => t.Id).ToList();
            var start = ServiceDay.StartUtc(_config);
            var openStatuses = new[] { OrderStatus.New, OrderStatus.InProgress, OrderStatus.Ready };
            var blocking = await _db.Orders
                .Where(o => removedIds.Contains(o.TableId) && o.CreatedAt >= start && openStatuses.Contains(o.Status))
                .Select(o => o.TableNumber)
                .Distinct()
                .ToListAsync();
            if (blocking.Count > 0)
                return BadRequest("Cannot remove table(s) " + string.Join(", ", blocking.OrderBy(n => n)) +
                                  " because they have open orders (New, In progress or Ready).");
        }

        _db.Tables.RemoveRange(removed);

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

/// <summary>
/// Moves model validation errors out of ModelState before the [ApiController] automatic 400 runs, so the action
/// can apply its own (plain text) checks first and then report the annotation errors as a ValidationProblem.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class DeferModelErrorsAttribute : Attribute, IActionFilter, IOrderedFilter
{
    public const string ItemKey = "DeferredModelErrors";

    public int Order => int.MinValue;

    public void OnActionExecuting(ActionExecutingContext context)
    {
        context.HttpContext.Items[ItemKey] = new ModelStateDictionary(context.ModelState);
        context.ModelState.Clear();
    }

    public void OnActionExecuted(ActionExecutedContext context) { }
}
