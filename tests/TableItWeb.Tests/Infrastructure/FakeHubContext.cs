using Microsoft.AspNetCore.SignalR;
using TableItWeb.Hubs;

namespace TableItWeb.Tests.Infrastructure;

/// <summary>Hand-made fake that records every Clients.All.SendAsync call.</summary>
public sealed class FakeHubContext : IHubContext<RestaurantHub>
{
    private readonly FakeClients _clients = new();

    public IReadOnlyList<(string Method, object?[] Args)> Sent => _clients.All.Sent;

    public IHubClients Clients => _clients;
    public IGroupManager Groups => throw new NotSupportedException();

    private sealed class FakeClients : IHubClients
    {
        public readonly RecordingProxy All = new();

        IClientProxy IHubClients<IClientProxy>.All => All;
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();
        public IClientProxy Client(string connectionId) => throw new NotSupportedException();
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => throw new NotSupportedException();
        public IClientProxy Group(string groupName) => throw new NotSupportedException();
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => throw new NotSupportedException();
        public IClientProxy User(string userId) => throw new NotSupportedException();
        public IClientProxy Users(IReadOnlyList<string> userIds) => throw new NotSupportedException();
    }

    private sealed class RecordingProxy : IClientProxy
    {
        public readonly List<(string Method, object?[] Args)> Sent = new();

        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
        {
            Sent.Add((method, args));
            return Task.CompletedTask;
        }
    }
}
