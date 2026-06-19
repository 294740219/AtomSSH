using System.Text.Json.Serialization;

namespace AtomSSH.Core.ValueObjects;

public readonly record struct RemotePath
{
    [JsonConstructor]
    public RemotePath(string Value)
    {
        if (string.IsNullOrWhiteSpace(Value))
        {
            throw new ArgumentException("Remote path is required.", nameof(Value));
        }

        this.Value = Value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct LocalPath
{
    [JsonConstructor]
    public LocalPath(string Value)
    {
        if (string.IsNullOrWhiteSpace(Value))
        {
            throw new ArgumentException("Local path is required.", nameof(Value));
        }

        this.Value = Value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct HostName
{
    [JsonConstructor]
    public HostName(string Value)
    {
        if (string.IsNullOrWhiteSpace(Value))
        {
            throw new ArgumentException("Host name is required.", nameof(Value));
        }

        this.Value = Value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}
