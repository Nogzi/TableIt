using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using TableItWeb.Tests.Infrastructure;

namespace TableItWeb.Tests.Integration;

public class SignalRRoundTripTests
{
    private static HubConnection Connect(TableItFactory factory) =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, "hubs/restaurant"), o =>
            {
                o.Transports = HttpTransportType.LongPolling;
                o.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
            })
            .Build();

    [Fact]
    public async Task PostOrder_ConnectedKitchenClient_ReceivesOrderCreatedBroadcast()
    {
        using var factory = new TableItFactory();
        using var client = factory.CreateClient();
        await using var connection = Connect(factory);
        var received = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<JsonElement>("OrderCreated", o => received.TrySetResult(o));
        await connection.StartAsync();

        var tables = await client.GetFromJsonAsync<JsonElement>("/api/tables");
        var menu = await client.GetFromJsonAsync<JsonElement>("/api/menu");
        var tableId = tables[0].GetProperty("id").GetInt32();
        var menuItem = menu[0];
        var response = await client.PostAsJsonAsync("/api/orders", new
        {
            tableId,
            note = "from test",
            lines = new[] { new { menuItemId = menuItem.GetProperty("id").GetInt32(), quantity = 3, note = (string?)null } }
        });
        response.EnsureSuccessStatusCode();

        var order = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(tableId, order.GetProperty("tableId").GetInt32());
        Assert.Equal("New", order.GetProperty("status").GetString());
        Assert.Equal("from test", order.GetProperty("note").GetString());
        var line = order.GetProperty("lines")[0];
        Assert.Equal(3, line.GetProperty("quantity").GetInt32());
        Assert.Equal(menuItem.GetProperty("name").GetString(), line.GetProperty("name").GetString());
    }

    [Fact]
    public async Task PatchOrderStatus_ConnectedClient_ReceivesOrderUpdatedBroadcast()
    {
        using var factory = new TableItFactory();
        using var client = factory.CreateClient();
        await using var connection = Connect(factory);
        var received = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<JsonElement>("OrderUpdated", o => received.TrySetResult(o));
        await connection.StartAsync();
        var tables = await client.GetFromJsonAsync<JsonElement>("/api/tables");
        var menu = await client.GetFromJsonAsync<JsonElement>("/api/menu");
        var created = await client.PostAsJsonAsync("/api/orders", new
        {
            tableId = tables[0].GetProperty("id").GetInt32(),
            lines = new[] { new { menuItemId = menu[0].GetProperty("id").GetInt32(), quantity = 1 } }
        });
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        (await client.PatchAsJsonAsync($"/api/orders/{id}/status", new { status = "InProgress" })).EnsureSuccessStatusCode();

        var order = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(id, order.GetProperty("id").GetInt32());
        Assert.Equal("InProgress", order.GetProperty("status").GetString());
    }
}
