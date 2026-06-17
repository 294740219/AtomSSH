namespace AtomSSH.Core.Results;

public sealed record OperationResult
{
    private OperationResult(bool succeeded, SshError? error)
    {
        Succeeded = succeeded;
        Error = error;
    }

    public bool Succeeded { get; }

    public SshError? Error { get; }

    public static OperationResult Success() => new(true, null);

    public static OperationResult Failure(SshError error) => new(false, error);
}

public sealed record OperationResult<T>
{
    private OperationResult(bool succeeded, T? value, SshError? error)
    {
        Succeeded = succeeded;
        Value = value;
        Error = error;
    }

    public bool Succeeded { get; }

    public T? Value { get; }

    public SshError? Error { get; }

    public static OperationResult<T> Success(T value) => new(true, value, null);

    public static OperationResult<T> Failure(SshError error) => new(false, default, error);
}
