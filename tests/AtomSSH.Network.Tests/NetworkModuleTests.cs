using AtomSSH.Core.Credentials;
using AtomSSH.Core.Network;
using AtomSSH.Core.Ports;
using AtomSSH.Core.Profiles;
using AtomSSH.Core.Results;
using AtomSSH.Core.ValueObjects;
using AtomSSH.Network;
using AtomSSH.Network.Diagnostics;
using AtomSSH.Network.DependencyInjection;
using AtomSSH.Network.Routes;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Sockets;

namespace AtomSSH.Network.Tests;

public sealed class NetworkModuleTests
{
    [Fact]
    public void AddAtomSSHNetworkReturnsSameServiceCollection()
    {
        var services = new ServiceCollection();

        var result = services.AddAtomSSHNetwork();

        Assert.Same(services, result);
        Assert.Equal("AtomSSH.Network", NetworkModule.Name);
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(INetworkDiagnosticsService)
            && descriptor.ImplementationType == typeof(TcpNetworkDiagnosticsService));
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IConnectionRoutePlanner)
            && descriptor.ImplementationType == typeof(ConnectionRoutePlanner));
    }

    [Fact]
    public void AddAtomSSHFakeNetworkRegistersFakeDiagnostics()
    {
        var services = new ServiceCollection();

        services.AddAtomSSHFakeNetwork();

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IConnectionRoutePlanner)
            && descriptor.ImplementationType == typeof(FakeConnectionRoutePlanner));
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(INetworkDiagnosticsService)
            && descriptor.ImplementationType == typeof(FakeNetworkDiagnosticsService));
    }

    [Fact]
    public async Task RoutePlannerCreatesDirectRouteWhenNoJumpHostIsConfigured()
    {
        var profile = CreateProfile("target", "target.internal");
        var planner = new ConnectionRoutePlanner();

        var result = await planner.PlanAsync(profile, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(Core.Network.ConnectionRouteKind.Direct, result.Value!.Kind);
        Assert.Empty(result.Value.JumpHosts);
    }

    [Fact]
    public async Task RoutePlannerCreatesJumpHostRouteWhenProfileReferencesJumpHost()
    {
        var jumpHost = CreateProfile("jump", "jump.internal");
        var target = CreateProfile("target", "target.internal", jumpHost.Id);
        var planner = new ConnectionRoutePlanner(new StubProfileRepository(jumpHost));

        var result = await planner.PlanAsync(target, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(Core.Network.ConnectionRouteKind.JumpHost, result.Value!.Kind);
        Assert.Single(result.Value.JumpHosts);
        Assert.Equal(jumpHost.Id, result.Value.JumpHosts[0].ProfileId);
        Assert.Equal(jumpHost.Endpoint, result.Value.JumpHosts[0].Endpoint);
    }

    [Fact]
    public async Task RoutePlannerCreatesJumpHostRouteFromNetworkInventory()
    {
        var space = new NetworkSpace(Guid.NewGuid(), "prod-vpc");
        var jumpHost = CreateProfile("jump", "jump.internal");
        var target = CreateProfile("target", "target.internal");
        var inventory = new StubNetworkInventoryStore(
            [space],
            [
                new NetworkNode(
                    NetworkNodeId.New(),
                    space.Id,
                    "jump",
                    new NetworkAddress("10.0.0.10"),
                    jumpHost.Id,
                    NetworkNodeRole.JumpHost),
                new NetworkNode(
                    NetworkNodeId.New(),
                    space.Id,
                    "target",
                    new NetworkAddress("10.0.0.20"),
                    target.Id,
                    NetworkNodeRole.Target)
            ]);
        var planner = new ConnectionRoutePlanner(new StubProfileRepository(jumpHost, target), inventory);

        var result = await planner.PlanAsync(target, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(Core.Network.ConnectionRouteKind.JumpHost, result.Value!.Kind);
        Assert.Single(result.Value.JumpHosts);
        Assert.Equal(jumpHost.Id, result.Value.JumpHosts[0].ProfileId);
    }

    [Fact]
    public async Task TcpNetworkDiagnosticsReturnsNoErrorsForReachableEndpoint()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = new SshEndpoint(new HostName("127.0.0.1"), ((IPEndPoint)listener.LocalEndpoint).Port);
        var route = new Core.Network.ConnectionRoute(
            Core.Network.ConnectionRouteKind.Direct,
            endpoint,
            Array.Empty<Core.Network.JumpHostRoute>());
        var diagnostics = new TcpNetworkDiagnosticsService();

        var result = await diagnostics.DiagnoseAsync(route, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Empty(result.Value!.Errors);
    }

    [Fact]
    public async Task TcpNetworkDiagnosticsReturnsNetworkErrorForUnreachableEndpoint()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        var endpoint = new SshEndpoint(new HostName("127.0.0.1"), port);
        var route = new Core.Network.ConnectionRoute(
            Core.Network.ConnectionRouteKind.Direct,
            endpoint,
            Array.Empty<Core.Network.JumpHostRoute>());
        var diagnostics = new TcpNetworkDiagnosticsService();

        var result = await diagnostics.DiagnoseAsync(route, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Contains(result.Value!.Errors, error => error.Kind == SshErrorKind.Network);
    }

    private static SshProfile CreateProfile(
        string name,
        string host,
        SshProfileId? jumpHostProfileId = null)
    {
        return new SshProfile(
            SshProfileId.New(),
            name,
            new SshEndpoint(new HostName(host), 22),
            "ops",
            SshAuthMethod.Password,
            CredentialRef.New(),
            Group: null,
            TerminalProfile: null,
            jumpHostProfileId);
    }

    private sealed class StubProfileRepository : ISshProfileRepository
    {
        private readonly Dictionary<SshProfileId, SshProfile> _profiles;

        public StubProfileRepository(params SshProfile[] profiles)
        {
            _profiles = profiles.ToDictionary(profile => profile.Id);
        }

        public Task<OperationResult<SshProfile?>> GetAsync(SshProfileId id, CancellationToken cancellationToken)
        {
            _profiles.TryGetValue(id, out var profile);
            return Task.FromResult(OperationResult<SshProfile?>.Success(profile));
        }

        public Task<OperationResult<IReadOnlyList<SshProfile>>> ListAsync(CancellationToken cancellationToken)
        {
            IReadOnlyList<SshProfile> profiles = _profiles.Values.ToArray();
            return Task.FromResult(OperationResult<IReadOnlyList<SshProfile>>.Success(profiles));
        }

        public Task<OperationResult> SaveAsync(SshProfile profile, CancellationToken cancellationToken)
        {
            return Task.FromResult(OperationResult.Success());
        }

        public Task<OperationResult> DeleteAsync(SshProfileId id, CancellationToken cancellationToken)
        {
            return Task.FromResult(OperationResult.Success());
        }
    }

    private sealed class StubNetworkInventoryStore : INetworkInventoryStore
    {
        private readonly IReadOnlyList<NetworkSpace> _spaces;
        private readonly IReadOnlyList<NetworkNode> _nodes;

        public StubNetworkInventoryStore(IReadOnlyList<NetworkSpace> spaces, IReadOnlyList<NetworkNode> nodes)
        {
            _spaces = spaces;
            _nodes = nodes;
        }

        public Task<OperationResult<IReadOnlyList<NetworkSpace>>> ListSpacesAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(OperationResult<IReadOnlyList<NetworkSpace>>.Success(_spaces));
        }

        public Task<OperationResult<IReadOnlyList<NetworkNode>>> ListNodesAsync(Guid networkSpaceId, CancellationToken cancellationToken)
        {
            IReadOnlyList<NetworkNode> nodes = _nodes.Where(node => node.NetworkSpaceId == networkSpaceId).ToArray();
            return Task.FromResult(OperationResult<IReadOnlyList<NetworkNode>>.Success(nodes));
        }

        public Task<OperationResult> SaveSpaceAsync(NetworkSpace space, CancellationToken cancellationToken)
        {
            return Task.FromResult(OperationResult.Success());
        }

        public Task<OperationResult> SaveNodeAsync(NetworkNode node, CancellationToken cancellationToken)
        {
            return Task.FromResult(OperationResult.Success());
        }
    }
}
