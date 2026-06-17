using AtomSSH.Core.Results;

namespace AtomSSH.Application.Common;

internal static class ApplicationErrors
{
    public static SshError NotFound(string summary, string? detail = null)
    {
        return new SshError(SshErrorKind.Validation, summary, detail);
    }
}
