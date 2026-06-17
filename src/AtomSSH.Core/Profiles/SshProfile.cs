using AtomSSH.Core.Credentials;
using AtomSSH.Core.Terminal;
using AtomSSH.Core.ValueObjects;

namespace AtomSSH.Core.Profiles;

public sealed record SshProfile(
    SshProfileId Id,
    string Name,
    SshEndpoint Endpoint,
    string UserName,
    SshAuthMethod AuthMethod,
    CredentialRef? CredentialRef,
    SshProfileGroup? Group,
    TerminalProfile? TerminalProfile,
    SshProfileId? JumpHostProfileId = null,
    string? Notes = null);

public sealed record SshEndpoint(HostName Host, int Port);

public sealed record SshProfileGroup(string Name);

public enum SshAuthMethod
{
    Password,
    PrivateKey,
    PrivateKeyWithPassphrase,
    KeyboardInteractive,
    Agent
}
