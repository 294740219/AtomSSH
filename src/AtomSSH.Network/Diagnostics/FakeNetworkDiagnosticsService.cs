using AtomSSH.Core.Network;
using AtomSSH.Core.Ports;
using AtomSSH.Core.Results;

namespace AtomSSH.Network.Diagnostics;

public sealed class FakeNetworkDiagnosticsService : INetworkDiagnosticsService
{
    public Task<OperationResult<NetworkDiagnosticResult>> DiagnoseAsync(
        ConnectionRoute route,
        CancellationToken cancellationToken)
    {
        var result = new NetworkDiagnosticResult(route, Array.Empty<SshError>());
        return Task.FromResult(OperationResult<NetworkDiagnosticResult>.Success(result));
    }
}
