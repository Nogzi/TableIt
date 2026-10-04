using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using TableItShared.Models;
using TableItWeb.Controllers;
using TableItWeb.Tests.Infrastructure;

namespace TableItWeb.Tests.Controllers;

public sealed class ValidationTests : IDisposable
{
    private readonly TableItFactory _factory = new();
    private readonly HttpClient _client;

    public ValidationTests() => _client = _factory.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");

    private async Task<JsonElement> Get(string url) =>
        JsonDocument.Parse(await _client.GetStringAsync(url)).RootElement.Clone();

    private static string MenuJson(string name = "Soup", string category = "Starters", string description = "Hot",
        string price = "50") =>
        $$"""{"name":{{JsonSerializer.Serialize(name)}},"category":{{JsonSerializer.Serialize(category)}},"description":{{JsonSerializer.Serialize(description)}},"price":{{price}},"isAvailable":true}""";

    private async Task AssertBadRequestWithField(HttpResponseMessage response, string field)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(field, body, StringComparison.OrdinalIgnoreCase);
    }

    // ---- Menu ----

    [Fact]
    public async Task CreateMenuItem_Valid_Returns201()
    {
        var response = await _client.PostAsync("/api/menu", Json(MenuJson()));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Theory]
    [InlineData("", "Starters", "Hot", "50", "name")]
    [InlineData("   ", "Starters", "Hot", "50", "name")]
    [InlineData("Soup", "", "Hot", "50", "category")]
    [InlineData("Soup", "   ", "Hot", "50", "category")]
    [InlineData("Soup", "Starters", "Hot", "-1", "price")]
    [InlineData("Soup", "Starters", "Hot", "100001", "price")]
    public async Task CreateMenuItem_Invalid_Returns400WithFieldError(
        string name, string category, string description, string price, string field)
    {
        var response = await _client.PostAsync("/api/menu", Json(MenuJson(name, category, description, price)));
        await AssertBadRequestWithField(response, field);
    }

    [Fact]
    public async Task CreateMenuItem_NameTooLong_Returns400()
    {
        var response = await _client.PostAsync("/api/menu", Json(MenuJson(name: new string('a', 101))));
        await AssertBadRequestWithField(response, "name");
    }

    [Fact]
    public async Task CreateMenuItem_CategoryTooLong_Returns400()
    {
        var response = await _client.PostAsync("/api/menu", Json(MenuJson(category: new string('a', 51))));
        await AssertBadRequestWithField(response, "category");
    }

    [Fact]
    public async Task CreateMenuItem_DescriptionTooLong_Returns400()
    {
        var response = await _client.PostAsync("/api/menu", Json(MenuJson(description: new string('a', 501))));
        await AssertBadRequestWithField(response, "description");
    }

    [Fact]
    public async Task CreateMenuItem_BoundaryValues_Accepted()
    {
        var response = await _client.PostAsync("/api/menu", Json(
            MenuJson(new string('a', 100), new string('b', 50), new string('c', 500), "100000")));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var response0 = await _client.PostAsync("/api/menu", Json(MenuJson(price: "0")));
        Assert.Equal(HttpStatusCode.Created, response0.StatusCode);
    }

    [Fact]
    public async Task CreateMenuItem_PaddedName_IsTrimmed()
    {
        var response = await _client.PostAsync("/api/menu", Json(MenuJson("  Soup  ", " Starters ")));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var item = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Soup", item.GetProperty("name").GetString());
        Assert.Equal("Starters", item.GetProperty("category").GetString());
    }

    [Fact]
    public async Task UpdateMenuItem_Invalid_Returns400()
    {
        var id = (await Get("/api/menu"))[0].GetProperty("id").GetInt32();
        var response = await _client.PutAsync($"/api/menu/{id}", Json(MenuJson(price: "-5")));
        await AssertBadRequestWithField(response, "price");
        response = await _client.PutAsync($"/api/menu/{id}", Json(MenuJson(name: "")));
        await AssertBadRequestWithField(response, "name");
    }

    [Fact]
    public async Task UpdateMenuItem_Valid_TrimsName()
    {
        var id = (await Get("/api/menu"))[0].GetProperty("id").GetInt32();
        var response = await _client.PutAsync($"/api/menu/{id}", Json(MenuJson("  Renamed ")));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var item = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Renamed", item.GetProperty("name").GetString());
    }

    // ---- Orders ----

    private async Task<(int TableId, int MenuItemId)> FirstTableAndItem() =>
        ((await Get("/api/tables"))[0].GetProperty("id").GetInt32(), (await Get("/api/menu"))[0].GetProperty("id").GetInt32());

    private static string OrderJson(int tableId, string lines, string? note = "hi") =>
        $$"""{"tableId":{{tableId}},"note":{{JsonSerializer.Serialize(note)}},"lines":{{lines}}}""";

    private static string Line(int itemId, int quantity = 1, string? note = null) =>
        $$"""{"menuItemId":{{itemId}},"quantity":{{quantity}},"note":{{JsonSerializer.Serialize(note)}}}""";

    [Fact]
    public async Task CreateOrder_Valid_Returns201()
    {
        var (t, m) = await FirstTableAndItem();
        var response = await _client.PostAsync("/api/orders", Json(OrderJson(t, $"[{Line(m, 99, new string('n', 500))}]", new string('n', 500))));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    [InlineData(-3)]
    public async Task CreateOrder_QuantityOutOfRange_Returns400(int quantity)
    {
        var (t, m) = await FirstTableAndItem();
        var response = await _client.PostAsync("/api/orders", Json(OrderJson(t, $"[{Line(m, quantity)}]")));
        await AssertBadRequestWithField(response, "quantity");
    }

    [Fact]
    public async Task CreateOrder_OrderNoteTooLong_Returns400()
    {
        var (t, m) = await FirstTableAndItem();
        var response = await _client.PostAsync("/api/orders", Json(OrderJson(t, $"[{Line(m)}]", new string('n', 501))));
        await AssertBadRequestWithField(response, "note");
    }

    [Fact]
    public async Task CreateOrder_LineNoteTooLong_Returns400()
    {
        var (t, m) = await FirstTableAndItem();
        var response = await _client.PostAsync("/api/orders", Json(OrderJson(t, $"[{Line(m, 1, new string('n', 501))}]")));
        await AssertBadRequestWithField(response, "note");
    }

    [Fact]
    public async Task CreateOrder_NoLines_Returns400()
    {
        var (t, _) = await FirstTableAndItem();
        var response = await _client.PostAsync("/api/orders", Json(OrderJson(t, "[]")));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateOrder_TooManyLines_Returns400AndFiftyIsAccepted()
    {
        var (t, m) = await FirstTableAndItem();
        string Lines(int n) => "[" + string.Join(",", Enumerable.Range(0, n).Select(_ => Line(m))) + "]";

        var tooMany = await _client.PostAsync("/api/orders", Json(OrderJson(t, Lines(51))));
        await AssertBadRequestWithField(tooMany, "lines");

        var ok = await _client.PostAsync("/api/orders", Json(OrderJson(t, Lines(50))));
        Assert.Equal(HttpStatusCode.Created, ok.StatusCode);
    }

    // ---- Tables ----

    private static string TableJson(int id = 0, int number = 50, int seats = 4, double x = 100, double y = 100,
        double width = 90, double height = 90, double rotation = 0) =>
        $$"""{"id":{{id}},"number":{{number}},"seats":{{seats}},"shape":"Round","x":{{x}},"y":{{y}},"width":{{width}},"height":{{height}},"rotation":{{rotation}}}""";

    private async Task<string> ExistingLayoutPlus(string extraTable)
    {
        var tables = (await Get("/api/tables")).EnumerateArray().Select(t => t.GetRawText());
        return "[" + string.Join(",", tables.Append(extraTable)) + "]";
    }

    [Fact]
    public async Task SaveLayout_ValidNewTable_Returns200()
    {
        var response = await _client.PutAsync("/api/tables", Json(await ExistingLayoutPlus(TableJson())));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SaveLayout_BoundaryValues_Accepted()
    {
        var response = await _client.PutAsync("/api/tables",
            Json(await ExistingLayoutPlus(TableJson(number: 999, seats: 50, x: 1000, y: 700, width: 1000, height: 20, rotation: 360))));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("number", 0)]
    [InlineData("number", 1000)]
    [InlineData("seats", 0)]
    [InlineData("seats", 51)]
    [InlineData("width", 19)]
    [InlineData("width", 1001)]
    [InlineData("height", 19)]
    [InlineData("height", 1001)]
    [InlineData("x", -1)]
    [InlineData("x", 1001)]
    [InlineData("y", -1)]
    [InlineData("y", 701)]
    [InlineData("rotation", -1)]
    [InlineData("rotation", 361)]
    public async Task SaveLayout_OutOfRange_Returns400WithFieldError(string field, double value)
    {
        var table = field switch
        {
            "number" => TableJson(number: (int)value),
            "seats" => TableJson(seats: (int)value),
            "width" => TableJson(width: value),
            "height" => TableJson(height: value),
            "x" => TableJson(x: value),
            "y" => TableJson(y: value),
            _ => TableJson(rotation: value)
        };
        var response = await _client.PutAsync("/api/tables", Json(await ExistingLayoutPlus(table)));
        await AssertBadRequestWithField(response, field);
    }

    [Fact]
    public async Task SaveLayout_RemovingTableWithOpenOrder_Returns400AndKeepsTable()
    {
        foreach (var status in new[] { OrderStatus.New, OrderStatus.InProgress, OrderStatus.Ready })
        {
            using var factory = new TableItFactory();
            using var client = factory.CreateClient();
            var tables = JsonDocument.Parse(await client.GetStringAsync("/api/tables")).RootElement.Clone();
            var menu = JsonDocument.Parse(await client.GetStringAsync("/api/menu")).RootElement.Clone();
            var victim = tables[0].GetProperty("id").GetInt32();
            var item = menu[0].GetProperty("id").GetInt32();

            var created = await client.PostAsync("/api/orders", Json(OrderJson(victim, $"[{Line(item)}]")));
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            var orderId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
            if (status != OrderStatus.New)
            {
                var patch = await client.PatchAsync($"/api/orders/{orderId}/status", Json($$"""{"status":"{{status}}"}"""));
                if (status == OrderStatus.Ready)
                {
                    await client.PatchAsync($"/api/orders/{orderId}/status", Json("""{"status":"InProgress"}"""));
                    patch = await client.PatchAsync($"/api/orders/{orderId}/status", Json("""{"status":"Ready"}"""));
                }
                Assert.Equal(HttpStatusCode.OK, patch.StatusCode);
            }

            var remaining = "[" + string.Join(",", tables.EnumerateArray().Skip(1).Select(t => t.GetRawText())) + "]";
            var response = await client.PutAsync("/api/tables", Json(remaining));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("open orders", await response.Content.ReadAsStringAsync());
            Assert.Equal(tables.GetArrayLength(),
                JsonDocument.Parse(await client.GetStringAsync("/api/tables")).RootElement.GetArrayLength());
        }
    }

    [Theory]
    [InlineData(OrderStatus.Served)]
    [InlineData(OrderStatus.Cancelled)]
    public async Task SaveLayout_RemovingTableWithOnlyClosedOrders_Succeeds(OrderStatus status)
    {
        var tables = await Get("/api/tables");
        var victim = tables[0].GetProperty("id").GetInt32();
        var item = (await Get("/api/menu"))[0].GetProperty("id").GetInt32();
        var created = await _client.PostAsync("/api/orders", Json(OrderJson(victim, $"[{Line(item)}]")));
        var orderId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        if (status == OrderStatus.Served)
        {
            await _client.PatchAsync($"/api/orders/{orderId}/status", Json("""{"status":"InProgress"}"""));
            await _client.PatchAsync($"/api/orders/{orderId}/status", Json("""{"status":"Ready"}"""));
        }
        var patch = await _client.PatchAsync($"/api/orders/{orderId}/status", Json($$"""{"status":"{{status}}"}"""));
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);

        var remaining = "[" + string.Join(",", tables.EnumerateArray().Skip(1).Select(t => t.GetRawText())) + "]";
        var response = await _client.PutAsync("/api/tables", Json(remaining));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
