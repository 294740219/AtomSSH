using AtomSSH.Core.Hosts;
using AtomSSH.Core.Ports;
using AtomSSH.Core.Profiles;
using AtomSSH.Core.Results;
using Renci.SshNet.Common;

namespace AtomSSH.Session.HostKeys;

internal sealed class SshNetHostKeyVerifier
{
    private readonly IHostKeyTrustStore _trustStore;

    public SshNetHostKeyVerifier(IHostKeyTrustStore trustStore)
    {
        _trustStore = trustStore;
    }

    public void Verify(SshEndpoint endpoint, HostKeyEventArgs args)
    {
        var fingerprint = CreateFingerprint(args);
        var verification = Verify(endpoint, args.HostKeyName, fingerprint);
        if (!verification.Succeeded)
        {
            args.CanTrust = false;
            throw new HostKeyRejectedException(verification.Error!);
        }

        args.CanTrust = true;
    }

    internal OperationResult Verify(
        SshEndpoint endpoint,
        string keyType,
        HostKeyFingerprint fingerprint)
    {
        var entryResult = _trustStore.FindAsync(endpoint.Host, endpoint.Port, CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        if (!entryResult.Succeeded)
        {
            return OperationResult.Failure(entryResult.Error!);
        }

        var entry = entryResult.Value;
        if (entry is null)
        {
            return OperationResult.Failure(new SshError(
                SshErrorKind.HostKey,
                "SSH host key is unknown and requires a trust decision.",
                $"{endpoint.Host.Value}:{endpoint.Port} {keyType} {fingerprint.Value}"));
        }

        if (entry.Decision != HostKeyTrustDecision.Trusted)
        {
            return OperationResult.Failure(new SshError(
                SshErrorKind.HostKey,
                "SSH host key is not trusted.",
                $"{endpoint.Host.Value}:{endpoint.Port} decision={entry.Decision}"));
        }

        if (!string.Equals(entry.KeyType, keyType, StringComparison.Ordinal)
            || !string.Equals(entry.Fingerprint.Algorithm, fingerprint.Algorithm, StringComparison.Ordinal)
            || !string.Equals(entry.Fingerprint.Value, fingerprint.Value, StringComparison.Ordinal))
        {
            return OperationResult.Failure(new SshError(
                SshErrorKind.HostKey,
                "SSH host key changed and connection was blocked.",
                $"{endpoint.Host.Value}:{endpoint.Port} expected={entry.KeyType} {entry.Fingerprint.Algorithm}:{entry.Fingerprint.Value}; actual={keyType} {fingerprint.Algorithm}:{fingerprint.Value}"));
        }

        return OperationResult.Success();
    }

    private static HostKeyFingerprint CreateFingerprint(HostKeyEventArgs args)
    {
        if (!string.IsNullOrWhiteSpace(args.FingerPrintSHA256))
        {
            return new HostKeyFingerprint("SHA256", args.FingerPrintSHA256);
        }

        return new HostKeyFingerprint("HEX", Convert.ToHexString(args.FingerPrint));
    }
}
