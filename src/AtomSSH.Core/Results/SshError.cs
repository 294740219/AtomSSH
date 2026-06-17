namespace AtomSSH.Core.Results;

public sealed record SshError(
    SshErrorKind Kind,
    string Summary,
    string? Detail = null,
    bool IsRetryable = false,
    string? CorrelationId = null);

public enum SshErrorKind
{
    Authentication,
    Network,
    HostKey,
    Permission,
    Path,
    PortUnavailable,
    Protocol,
    Cancelled,
    Validation,
    Configuration,
    Internal
}
