namespace AtomSSH.Core.ValueObjects;

public readonly record struct SshProfileId(Guid Value)
{
    public static SshProfileId New() => new(Guid.NewGuid());
}

public readonly record struct CredentialRef(Guid Value)
{
    public static CredentialRef New() => new(Guid.NewGuid());
}

public readonly record struct SshSessionInstanceId(Guid Value)
{
    public static SshSessionInstanceId New() => new(Guid.NewGuid());
}

public readonly record struct TransferTaskId(Guid Value)
{
    public static TransferTaskId New() => new(Guid.NewGuid());
}

public readonly record struct NetworkNodeId(Guid Value)
{
    public static NetworkNodeId New() => new(Guid.NewGuid());
}

public readonly record struct CommandSnippetId(Guid Value)
{
    public static CommandSnippetId New() => new(Guid.NewGuid());
}

public readonly record struct PortForwardInstanceId(Guid Value)
{
    public static PortForwardInstanceId New() => new(Guid.NewGuid());
}
