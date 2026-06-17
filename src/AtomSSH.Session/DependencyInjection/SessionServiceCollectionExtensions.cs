using Microsoft.Extensions.DependencyInjection;
using AtomSSH.Core.Ports;
using AtomSSH.Session.Connections;
using AtomSSH.Session.HostKeys;
using AtomSSH.Session.PortForwarding;
using AtomSSH.Session.Sftp;

namespace AtomSSH.Session.DependencyInjection;

public static class SessionServiceCollectionExtensions
{
    public static IServiceCollection AddAtomSSHSession(this IServiceCollection services)
    {
        return services.AddAtomSSHSshNetSession();
    }

    public static IServiceCollection AddAtomSSHFakeSession(this IServiceCollection services)
    {
        services.AddSingleton<FakeSshSessionRuntime>();
        services.AddSingleton<ISshSessionRuntime>(provider => provider.GetRequiredService<FakeSshSessionRuntime>());
        services.AddSingleton<ISshSessionFactory>(provider => provider.GetRequiredService<FakeSshSessionRuntime>());
        services.AddSingleton<ISftpBrowser, FakeSftpBrowser>();
        services.AddSingleton<IPortForwardRuntime, FakePortForwardRuntime>();

        return services;
    }

    public static IServiceCollection AddAtomSSHSshNetSession(this IServiceCollection services)
    {
        services.AddSingleton<ISshNetConnectionInfoFactory, SshNetConnectionInfoFactory>();
        services.AddSingleton<SshNetHostKeyVerifier>();
        services.AddSingleton<SshNetClientConnector>();
        services.AddSingleton<SshNetSftpClientFactory>();
        services.AddSingleton<SshNetSessionRuntime>();
        services.AddSingleton<ISshSessionRuntime>(provider => provider.GetRequiredService<SshNetSessionRuntime>());
        services.AddSingleton<ISshSessionFactory>(provider => provider.GetRequiredService<SshNetSessionRuntime>());
        services.AddSingleton<ISftpBrowser, SshNetSftpBrowser>();
        services.AddSingleton<ISftpFileTransfer, SshNetSftpFileTransfer>();
        services.AddSingleton<IPortForwardRuntime, SshNetPortForwardRuntime>();

        return services;
    }
}
