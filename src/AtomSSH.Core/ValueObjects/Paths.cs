namespace AtomSSH.Core.ValueObjects;

public readonly record struct RemotePath(string Value)
{
    public override string ToString() => Value;
}

public readonly record struct LocalPath(string Value)
{
    public override string ToString() => Value;
}

public readonly record struct HostName(string Value)
{
    public override string ToString() => Value;
}
