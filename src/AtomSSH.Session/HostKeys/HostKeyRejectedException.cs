using AtomSSH.Core.Results;

namespace AtomSSH.Session.HostKeys;

internal sealed class HostKeyRejectedException : Exception
{
    public HostKeyRejectedException(SshError error)
        : base(error.Summary)
    {
        Error = error;
    }

    public SshError Error { get; }
}
