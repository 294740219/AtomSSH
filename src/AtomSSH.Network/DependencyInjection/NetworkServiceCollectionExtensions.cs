using Microsoft.Extensions.DependencyInjection;
using AtomSSH.Core.Ports;
using AtomSSH.Network.Diagnostics;
using AtomSSH.Network.Routes;

namespace AtomSSH.Network.DependencyInjection;

public static class NetworkServiceCollectionExtensions
{
    public static IServiceCollection AddAtomSSHNetwork(this IServiceCollection services)
    {
        services.AddSingleton<IConnectionRoutePlanner, ConnectionRoutePlanner>();
        services.AddSingleton<INetworkDiagnosticsService, TcpNetworkDiagnosticsService>();

        return services;
    }

    public static IServiceCollection AddAtomSSHFakeNetwork(this IServiceCollection services)
    {
        services.AddSingleton<IConnectionRoutePlanner, FakeConnectionRoutePlanner>();
        services.AddSingleton<INetworkDiagnosticsService, FakeNetworkDiagnosticsService>();

        return services;
    }
}
