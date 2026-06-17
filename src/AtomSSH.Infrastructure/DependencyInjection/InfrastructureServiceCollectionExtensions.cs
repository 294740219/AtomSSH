using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using AtomSSH.Core.Ports;
using AtomSSH.Infrastructure.CommandSnippets;
using AtomSSH.Infrastructure.Configuration;
using AtomSSH.Infrastructure.Credentials;
using AtomSSH.Infrastructure.HostKeys;
using AtomSSH.Infrastructure.ImportExport;
using AtomSSH.Infrastructure.NetworkInventory;
using AtomSSH.Infrastructure.PortForwarding;
using AtomSSH.Infrastructure.Profiles;
using AtomSSH.Infrastructure.Transfers;

namespace AtomSSH.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddAtomSSHInfrastructure(this IServiceCollection services)
    {
        services.TryAddSingleton(AtomSshDataDirectory.CreateDefault());
        services.AddSingleton<ISshProfileRepository, JsonSshProfileRepository>();
        services.AddSingleton<IApplicationSettingsRepository, JsonApplicationSettingsRepository>();
        services.AddSingleton<IHostKeyTrustStore, JsonHostKeyTrustStore>();
        services.AddSingleton<ITransferTaskStore, JsonTransferTaskStore>();
        services.AddSingleton<ITransferStateStore, JsonTransferStateStore>();
        services.AddSingleton<INetworkInventoryStore, JsonNetworkInventoryStore>();
        services.AddSingleton<ICommandSnippetRepository, JsonCommandSnippetRepository>();
        services.AddSingleton<IPortForwardProfileRepository, JsonPortForwardProfileRepository>();
        services.AddSingleton<LocalCredentialStore>();
        services.AddSingleton<ICredentialStore>(provider => provider.GetRequiredService<LocalCredentialStore>());
        services.AddSingleton<ICredentialResolver>(provider => provider.GetRequiredService<LocalCredentialStore>());
        services.AddSingleton<IImportExportService, LocalImportExportService>();

        return services;
    }
}
