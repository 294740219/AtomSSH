using System.Text.Json.Serialization;

namespace AtomSSH.Core.ValueObjects;

public readonly record struct SshProfileId
{
    [JsonConstructor]
    public SshProfileId(Guid Value)
    {
        if (Value == Guid.Empty)
        {
            throw new ArgumentException("SSH profile id cannot be empty.", nameof(Value));
        }

        this.Value = Value;
    }

    public Guid Value { get; }

    public static SshProfileId New() => new(Guid.NewGuid());
}

public readonly record struct CredentialRef
{
    [JsonConstructor]
    public CredentialRef(Guid Value)
    {
        if (Value == Guid.Empty)
        {
            throw new ArgumentException("Credential reference cannot be empty.", nameof(Value));
        }

        this.Value = Value;
    }

    public Guid Value { get; }

    public static CredentialRef New() => new(Guid.NewGuid());
}

public readonly record struct SshSessionInstanceId
{
    [JsonConstructor]
    public SshSessionInstanceId(Guid Value)
    {
        if (Value == Guid.Empty)
        {
            throw new ArgumentException("SSH session instance id cannot be empty.", nameof(Value));
        }

        this.Value = Value;
    }

    public Guid Value { get; }

    public static SshSessionInstanceId New() => new(Guid.NewGuid());
}

public readonly record struct TransferTaskId
{
    [JsonConstructor]
    public TransferTaskId(Guid Value)
    {
        if (Value == Guid.Empty)
        {
            throw new ArgumentException("Transfer task id cannot be empty.", nameof(Value));
        }

        this.Value = Value;
    }

    public Guid Value { get; }

    public static TransferTaskId New() => new(Guid.NewGuid());
}

public readonly record struct NetworkNodeId
{
    [JsonConstructor]
    public NetworkNodeId(Guid Value)
    {
        if (Value == Guid.Empty)
        {
            throw new ArgumentException("Network node id cannot be empty.", nameof(Value));
        }

        this.Value = Value;
    }

    public Guid Value { get; }

    public static NetworkNodeId New() => new(Guid.NewGuid());
}

public readonly record struct CommandSnippetId
{
    [JsonConstructor]
    public CommandSnippetId(Guid Value)
    {
        if (Value == Guid.Empty)
        {
            throw new ArgumentException("Command snippet id cannot be empty.", nameof(Value));
        }

        this.Value = Value;
    }

    public Guid Value { get; }

    public static CommandSnippetId New() => new(Guid.NewGuid());
}

public readonly record struct PortForwardInstanceId
{
    [JsonConstructor]
    public PortForwardInstanceId(Guid Value)
    {
        if (Value == Guid.Empty)
        {
            throw new ArgumentException("Port forward instance id cannot be empty.", nameof(Value));
        }

        this.Value = Value;
    }

    public Guid Value { get; }

    public static PortForwardInstanceId New() => new(Guid.NewGuid());
}
