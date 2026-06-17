using System.Text.RegularExpressions;

namespace AtomSSH.Core.Results;

public static partial class SshErrorRedactor
{
    public static SshError Redact(SshError error)
    {
        return error with { Detail = RedactDetail(error.Detail) };
    }

    public static string? RedactDetail(string? detail)
    {
        if (string.IsNullOrWhiteSpace(detail))
        {
            return detail;
        }

        var redacted = PrivateKeyBlockPattern().Replace(detail, "[redacted-private-key]");
        redacted = SecretAssignmentPattern().Replace(redacted, match => $"{match.Groups[1].Value}=<redacted>");
        redacted = UriCredentialPattern().Replace(redacted, "://<redacted>@");
        return redacted;
    }

    [GeneratedRegex(
        "-----BEGIN [^-]*PRIVATE KEY-----.*?-----END [^-]*PRIVATE KEY-----",
        RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex PrivateKeyBlockPattern();

    [GeneratedRegex(
        "(password|passphrase|token|secret)\\s*[:=]\\s*[^;\\s]+",
        RegexOptions.IgnoreCase)]
    private static partial Regex SecretAssignmentPattern();

    [GeneratedRegex(
        "://[^/\\s:@]+:[^@\\s/]+@",
        RegexOptions.IgnoreCase)]
    private static partial Regex UriCredentialPattern();
}
