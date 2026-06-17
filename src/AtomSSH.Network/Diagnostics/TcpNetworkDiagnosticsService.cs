using System.Diagnostics;
using System.Net.Sockets;
using AtomSSH.Core.Network;
using AtomSSH.Core.Ports;
using AtomSSH.Core.Profiles;
using AtomSSH.Core.Results;

namespace AtomSSH.Network.Diagnostics;

public sealed class TcpNetworkDiagnosticsService : INetworkDiagnosticsService
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(3);

    public async Task<OperationResult<NetworkDiagnosticResult>> DiagnoseAsync(
        ConnectionRoute route,
        CancellationToken cancellationToken)
    {
        var errors = new List<SshError>();

        if (route.Kind is ConnectionRouteKind.JumpHost or ConnectionRouteKind.ProxyJumpChain)
        {
            foreach (var jumpHost in route.JumpHosts)
            {
                var jumpResult = await CheckEndpointAsync(
                    jumpHost.Endpoint,
                    "SSH jump host is not reachable.",
                    cancellationToken).ConfigureAwait(false);
                if (jumpResult is not null)
                {
                    errors.Add(jumpResult);
                }
            }
        }

        var targetResult = await CheckEndpointAsync(
            route.Target,
            "SSH target is not reachable.",
            cancellationToken).ConfigureAwait(false);
        if (targetResult is not null)
        {
            errors.Add(targetResult);
        }

        return OperationResult<NetworkDiagnosticResult>.Success(new NetworkDiagnosticResult(route, errors));
    }

    private static async Task<SshError?> CheckEndpointAsync(
        SshEndpoint endpoint,
        string summary,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(DefaultTimeout);

            using var client = new TcpClient();
            await client.ConnectAsync(endpoint.Host.Value, endpoint.Port, timeoutSource.Token)
                .ConfigureAwait(false);

            return null;
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            return new SshError(
                SshErrorKind.Network,
                summary,
                $"{endpoint.Host.Value}:{endpoint.Port} timed out after {DefaultTimeout.TotalSeconds:0.#}s. {exception.Message}",
                IsRetryable: true);
        }
        catch (Exception exception)
        {
            return new SshError(
                SshErrorKind.Network,
                summary,
                SshErrorRedactor.RedactDetail($"{endpoint.Host.Value}:{endpoint.Port} failed after {stopwatch.ElapsedMilliseconds}ms. {exception.Message}"),
                IsRetryable: true);
        }
    }
}
