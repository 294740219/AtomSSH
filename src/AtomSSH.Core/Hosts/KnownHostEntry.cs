using AtomSSH.Core.ValueObjects;

namespace AtomSSH.Core.Hosts;

public sealed record KnownHostEntry(
    HostName Host,
    int Port,
    string KeyType,
    HostKeyFingerprint Fingerprint,
    DateTimeOffset FirstTrustedAt,
    DateTimeOffset LastSeenAt,
    HostKeyTrustDecision Decision);

public sealed record HostKeyFingerprint(string Algorithm, string Value);

public enum HostKeyTrustDecision
{
    Unknown,
    Trusted,
    Rejected,
    Changed
}
