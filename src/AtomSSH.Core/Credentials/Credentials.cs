using AtomSSH.Core.ValueObjects;

namespace AtomSSH.Core.Credentials;

public sealed record CredentialMetadata(CredentialRef Ref, CredentialKind Kind, string Name);

public sealed record CredentialLease(
    CredentialRef Ref,
    CredentialMaterial Material,
    DateTimeOffset AcquiredAt,
    Func<ValueTask>? ReleaseAsync = null) : IAsyncDisposable
{
    public CredentialKind Kind => Material.Kind;

    public ValueTask DisposeAsync()
    {
        return ReleaseAsync?.Invoke() ?? ValueTask.CompletedTask;
    }
}

public abstract record CredentialMaterial
{
    public abstract CredentialKind Kind { get; }
}

public sealed record PasswordCredentialMaterial(string Password) : CredentialMaterial
{
    public override CredentialKind Kind => CredentialKind.Password;
}

public sealed record PrivateKeyCredentialMaterial(string PrivateKeyPem, string? Passphrase) : CredentialMaterial
{
    public override CredentialKind Kind => string.IsNullOrEmpty(Passphrase)
        ? CredentialKind.PrivateKey
        : CredentialKind.PrivateKeyWithPassphrase;
}

public sealed record KeyboardInteractiveCredentialMaterial(
    IReadOnlyDictionary<string, string> Responses,
    string? DefaultResponse = null) : CredentialMaterial
{
    public override CredentialKind Kind => CredentialKind.KeyboardInteractive;
}

public sealed record AgentCredentialMaterial() : CredentialMaterial
{
    public override CredentialKind Kind => CredentialKind.Agent;
}

public enum CredentialKind
{
    Password,
    PrivateKey,
    PrivateKeyWithPassphrase,
    KeyboardInteractive,
    Agent
}
