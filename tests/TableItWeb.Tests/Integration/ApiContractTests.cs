using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using TableItWeb.Tests.Infrastructure;

namespace TableItWeb.Tests.Integration;

public sealed class ApiContractTests : IDisposable
{
    private readonly TableItFactory _factory = new();
    private readonly HttpClient _client;

    public ApiContractTests() => _client = _factory.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");

    private async Task<JsonElement> GetJson(string url)
    {
        var response = await _client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    }

    private async Task<(int TableId, int MenuItemId)> FirstTableAndMenuItem()
    {
        var tables = await GetJson("/api/tables");
        var menu = await GetJson("/api/menu");
        return (tables[0].GetProperty("id").GetInt32(), menu[0].GetProperty("id").GetInt32());
    }

    private async Task<HttpResponseMessage> PostOrder(int tableId, int menuItemId, int quantity = 2) =>
        await _client.PostAsync("/api/orders", Json(
            $$"""{"tableId":{{tableId}},"note":"hi","lines":[{"menuItemId":{{menuItemId}},"quantity":{{quantity}},"note":null}]}"""));

    [Fact]
    public async Task GetTables_AfterStartup_ReturnsSeededTablesAsCamelCaseJson()
    {
        var tables = await GetJson("/api/tables");

        Assert.Equal(JsonValueKind.Array, tables.ValueKind);
        Assert.True(tables.GetArrayLength() >= 1);
        var first = tables[0];
        foreach (var name in new[] { "id", "number", "seats", "shape", "x", "y", "width", "height", "rotation" })
            Assert.True(first.TryGetProperty(name, out _), $"missing camelCase property '{name}'");
        Assert.False(first.TryGetProperty("Number", out _));
        Assert.Equal(JsonValueKind.String, first.GetProperty("shape").ValueKind);
        Assert.Contains(first.GetProperty("shape").GetString(), new[] { "Round", "Rect" });
        var numbers = tables.EnumerateArray().Select(t => t.GetProperty("number").GetInt32()).ToList();
        Assert.Equal(numbers.OrderBy(n => n), numbers);
    }

    [Fact]
    public async Task GetMenu_AfterStartup_ReturnsSeededItemsAsCamelCaseJson()
    {
        var menu = await GetJson("/api/menu");

        Assert.True(menu.GetArrayLength() >= 1);
        var first = menu[0];
        foreach (var name in new[] { "id", "name", "description", "category", "price", "isAvailable" })
            Assert.True(first.TryGetProperty(name, out _), $"missing camelCase property '{name}'");
        Assert.Equal(JsonValueKind.Number, first.GetProperty("price").ValueKind);
        Assert.Equal(JsonValueKind.True, first.GetProperty("isAvailable").ValueKind);
    }

    [Fact]
    public async Task PostOrder_ValidRequest_Returns201WithLocationHeaderAndStringEnumStatus()
    {
        var (tableId, menuItemId) = await FirstTableAndMenuItem();

        var response = await PostOrder(tableId, menuItemId);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var raw = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"status\":\"New\"", raw);
        var order = JsonDocument.Parse(raw).RootElement;
        var id = order.GetProperty("id").GetInt32();
        Assert.EndsWith($"/api/orders/{id}", response.Headers.Location!.AbsolutePath);
        Assert.Equal(tableId, order.GetProperty("tableId").GetInt32());
        Assert.True(order.GetProperty("createdAt").GetString()!.EndsWith('Z'));
        var line = order.GetProperty("lines")[0];
        Assert.Equal(2, line.GetProperty("quantity").GetInt32());
        Assert.Equal(JsonValueKind.String, line.GetProperty("name").ValueKind);
        Assert.Equal(JsonValueKind.Number, line.GetProperty("unitPrice").ValueKind);

        var fetched = await _client.GetAsync(response.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
    }

    [Fact]
    public async Task GetOrders_AfterPosting_ListsOrderAndAcceptsStringStatusFilter()
    {
        var (tableId, menuItemId) = await FirstTableAndMenuItem();
        await PostOrder(tableId, menuItemId);

        var all = await GetJson("/api/orders");
        var filtered = await GetJson("/api/orders?status=New");
        var none = await GetJson("/api/orders?status=Served");

        Assert.Equal(1, all.GetArrayLength());
        Assert.Equal(1, filtered.GetArrayLength());
        Assert.Equal(0, none.GetArrayLength());
    }

    [Fact]
    public async Task PatchOrderStatus_WithStringStatus_UpdatesOrder()
    {
        var (tableId, menuItemId) = await FirstTableAndMenuItem();
        var created = await PostOrder(tableId, menuItemId);
        var id = JsonDocument.Parse(await created.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetInt32();

        var response = await _client.PatchAsync($"/api/orders/{id}/status", Json("""{"status":"Ready"}"""));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"status\":\"Ready\"", await response.Content.ReadAsStringAsync());
        var reread = await GetJson($"/api/orders/{id}");
        Assert.Equal("Ready", reread.GetProperty("status").GetString());
    }

    [Fact]
    public async Task PatchOrderStatus_UnknownOrder_Returns404()
    {
        var response = await _client.PatchAsync("/api/orders/99999/status", Json("""{"status":"Ready"}"""));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostOrder_UnknownTable_Returns400WithTextMessage()
    {
        var (_, menuItemId) = await FirstTableAndMenuItem();

        var response = await PostOrder(tableId: 99999, menuItemId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.StartsWith("text/plain", response.Content.Headers.ContentType?.MediaType + "");
        var body = await response.Content.ReadAsStringAsync();
        Assert.False(string.IsNullOrWhiteSpace(body));
        Assert.DoesNotContain("{", body);
    }

    [Fact]
    public async Task PostOrder_EmptyLines_Returns400WithTextMessage()
    {
        var (tableId, _) = await FirstTableAndMenuItem();

        var response = await _client.PostAsync("/api/orders", Json($$"""{"tableId":{{tableId}},"lines":[]}"""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.StartsWith("text/plain", response.Content.Headers.ContentType?.MediaType + "");
        Assert.False(string.IsNullOrWhiteSpace(await response.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task PutTables_ValidLayoutWithCamelCaseJsonAndStringShape_RoundTrips()
    {
        var response = await _client.PutAsync("/api/tables", Json(
            """[{"id":0,"number":42,"seats":5,"shape":"Rect","x":10.5,"y":20,"width":100,"height":50,"rotation":90}]"""));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var tables = await GetJson("/api/tables");
        var only = Assert.Single(tables.EnumerateArray().ToList());
        Assert.Equal(42, only.GetProperty("number").GetInt32());
        Assert.Equal("Rect", only.GetProperty("shape").GetString());
        Assert.Equal(90, only.GetProperty("rotation").GetDouble());
    }

    [Fact]
    public async Task PutTables_DuplicateNumbers_Returns400WithTextMessage()
    {
        var response = await _client.PutAsync("/api/tables", Json(
            """[{"id":0,"number":1,"seats":2,"shape":"Round"},{"id":0,"number":1,"seats":2,"shape":"Round"}]"""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.StartsWith("text/plain", response.Content.Headers.ContentType?.MediaType + "");
    }

    [Fact]
    public async Task MenuCrud_CreateUpdateDeleteOverHttp_FollowsContract()
    {
        var create = await _client.PostAsJsonAsync("/api/menu",
            new { name = "Test dish", description = "d", category = "Zzz", price = 12.5, isAvailable = true });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var id = JsonDocument.Parse(await create.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetInt32();

        var update = await _client.PutAsJsonAsync($"/api/menu/{id}",
            new { name = "Test dish 2", description = "d", category = "Zzz", price = 13, isAvailable = false });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var menu = await GetJson("/api/menu");
        var last = menu.EnumerateArray().Last();
        Assert.Equal("Test dish 2", last.GetProperty("name").GetString());
        Assert.Equal(JsonValueKind.False, last.GetProperty("isAvailable").ValueKind);

        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync($"/api/menu/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.DeleteAsync($"/api/menu/{id}")).StatusCode);
    }

    [Fact]
    public async Task NegotiateHub_PostWithNegotiateVersion1_Returns200()
    {
        var response = await _client.PostAsync("/hubs/restaurant/negotiate?negotiateVersion=1", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("connectionToken", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("/Staff")]
    [InlineData("/Kitchen")]
    [InlineData("/Kitchen/Planner")]
    [InlineData("/Kitchen/Menu")]
    public async Task GetPage_RazorPage_Returns200(string url)
    {
        var response = await _client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.StartsWith("text/html", response.Content.Headers.ContentType?.MediaType + "");
    }
}
