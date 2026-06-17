using AtomSSH.Core.Profiles;
using AtomSSH.Core.Results;
using AtomSSH.Core.ValueObjects;

namespace AtomSSH.Core.PortForwarding;

public sealed record PortForwardProfile(
    Guid Id,
    string Name,
    SshProfileId ProfileId,
    PortForwardKind Kind,
    PortForwardEndpoint Local,
    PortForwardEndpoint? Remote);

public sealed record PortForwardEndpoint(string Host, int Port);

public sealed record PortForwardStatus(
    PortForwardInstanceId InstanceId,
    Guid ProfileId,
    PortForwardState State,
    SshError? LastError = null);

public enum PortForwardKind
{
    Local,
    Remote,
    DynamicSocks
}

public enum PortForwardState
{
    Starting,
    Running,
    Stopping,
    Stopped,
    Failed
}
