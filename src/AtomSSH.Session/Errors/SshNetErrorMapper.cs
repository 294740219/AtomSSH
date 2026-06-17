using AtomSSH.Core.Results;
using Renci.SshNet.Common;

namespace AtomSSH.Session.Errors;

internal static class SshNetErrorMapper
{
    public static SshError Map(Exception exception)
    {
        var error = exception switch
        {
            SshAuthenticationException => new SshError(
                SshErrorKind.Authentication,
                "SSH authentication failed.",
                exception.Message,
                IsRetryable: true),
            SshConnectionException => new SshError(
                SshErrorKind.Network,
                "SSH connection failed.",
                exception.Message,
                IsRetryable: true),
            SshOperationTimeoutException => new SshError(
                SshErrorKind.Network,
                "SSH operation timed out.",
                exception.Message,
                IsRetryable: true),
            SshException => new SshError(
                SshErrorKind.Protocol,
                "SSH protocol operation failed.",
                exception.Message,
                IsRetryable: true),
            OperationCanceledException => new SshError(
                SshErrorKind.Cancelled,
                "SSH operation was cancelled.",
                exception.Message),
            _ => new SshError(
                SshErrorKind.Internal,
                "SSH runtime operation failed.",
                exception.Message)
        };

        return SshErrorRedactor.Redact(error);
    }
}
