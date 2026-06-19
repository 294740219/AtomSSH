using AtomSSH.Core.Profiles;
using AtomSSH.Core.Results;
using AtomSSH.Core.ValueObjects;

namespace AtomSSH.Core.Network;

public sealed record NetworkSpace(Guid Id, string Name);

public sealed record NetworkNode(
    NetworkNodeId Id,
    Guid NetworkSpaceId,
    string Name,
    NetworkAddress Address,
    SshProfileId? ProfileId,
    NetworkNodeRole Role);

public sealed record NetworkAddress(string Host, int Port = 22);

public sealed record SubnetRoute(string Cidr, SshProfileId? JumpHostProfileId);

public sealed record ConnectionRoute(ConnectionRouteKind Kind, SshEndpoint Target, IReadOnlyList<JumpHostRoute> JumpHosts);

public sealed record JumpHostRoute(SshProfileId ProfileId, SshEndpoint Endpoint);

public sealed record ProxyJumpChain(IReadOnlyList<JumpHostRoute> Hops);

public sealed record ConnectionRoutePlanningRequest(
    SshProfile TargetProfile,
    Guid? NetworkSpaceId = null,
    SshProfileId? PreferredJumpHostProfileId = null,
    bool AllowInventoryFallback = true);

public sealed record NetworkReachabilitySnapshot(NetworkNodeId NodeId, bool IsReachable, SshError? Error);

public sealed record NetworkDiagnosticResult(ConnectionRoute? Route, IReadOnlyList<SshError> Errors);

public sealed record RoutePlanningError(SshError Error);

public enum NetworkNodeRole
{
    Target,
    JumpHost,
    Gateway
}

public enum ConnectionRouteKind
{
    Direct,
    JumpHost,
    ProxyJumpChain
}
