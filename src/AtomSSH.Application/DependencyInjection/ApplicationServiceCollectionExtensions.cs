using Microsoft.Extensions.DependencyInjection;
using AtomSSH.Application.CommandSnippets;
using AtomSSH.Application.ImportExport;
using AtomSSH.Application.Network;
using AtomSSH.Application.PortForwarding;
using AtomSSH.Application.Profiles;
using AtomSSH.Application.Sessions;
using AtomSSH.Application.Settings;
using AtomSSH.Application.Sftp;
using AtomSSH.Application.Transfers;

namespace AtomSSH.Application.DependencyInjection;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddAtomSSHApplication(this IServiceCollection services)
    {
        services.AddSingleton<ProfileAppService>();
        services.AddSingleton<SessionAppService>();
        services.AddSingleton<SftpAppService>();
        services.AddSingleton<TransferAppService>();
        services.AddSingleton<NetworkInventoryAppService>();
        services.AddSingleton<PortForwardAppService>();
        services.AddSingleton<CommandSnippetAppService>();
        services.AddSingleton<SettingsAppService>();
        services.AddSingleton<ImportExportAppService>();

        return services;
    }
}
