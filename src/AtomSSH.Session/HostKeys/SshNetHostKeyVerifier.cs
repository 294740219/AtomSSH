using System.Collections.Concurrent;
using AtomSSH.Core.Hosts;
using AtomSSH.Core.Ports;
using AtomSSH.Core.Profiles;
using AtomSSH.Core.Results;
using Renci.SshNet.Common;

namespace AtomSSH.Session.HostKeys;

internal sealed class SshNetHostKeyVerifier
{
    private readonly IHostKeyTrustStore _trustStore;
    private readonly ConcurrentDictionary<string, OperationResult<KnownHostEntry?>> _preparedEntries = new();

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

    public async Task<OperationResult> PrepareAsync(SshEndpoint endpoint, CancellationToken cancellationToken)
    {
        var entry = await _trustStore.FindAsync(endpoint.Host, endpoint.Port, cancellationToken)
            .ConfigureAwait(false);
        _preparedEntries[CreateKey(endpoint)] = entry;

        return entry.Succeeded
            ? OperationResult.Success()
            : OperationResult.Failure(entry.Error!);
    }

    internal OperationResult Verify(
        SshEndpoint endpoint,
        string keyType,
        HostKeyFingerprint fingerprint)
    {
        if (!_preparedEntries.TryGetValue(CreateKey(endpoint), out var preparedEntry))
        {
            return OperationResult.Failure(new SshError(
                SshErrorKind.Configuration,
                "SSH host key verifier was not prepared.",
                $"{endpoint.Host.Value}:{endpoint.Port}"));
        }

        if (!preparedEntry.Succeeded)
        {
            return OperationResult.Failure(preparedEntry.Error!);
        }

        var entry = preparedEntry.Value;
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

    private static string CreateKey(SshEndpoint endpoint)
    {
        return $"{endpoint.Host.Value}:{endpoint.Port}";
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
