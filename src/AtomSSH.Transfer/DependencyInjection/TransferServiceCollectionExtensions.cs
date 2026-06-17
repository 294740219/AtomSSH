using Microsoft.Extensions.DependencyInjection;
using AtomSSH.Core.Ports;
using AtomSSH.Transfer.Scheduling;

namespace AtomSSH.Transfer.DependencyInjection;

public static class TransferServiceCollectionExtensions
{
    public static IServiceCollection AddAtomSSHTransfer(this IServiceCollection services)
    {
        return services.AddAtomSSHRealTransfer();
    }

    public static IServiceCollection AddAtomSSHRealTransfer(this IServiceCollection services)
    {
        services.AddSingleton<ITransferTaskScheduler, SftpTransferTaskScheduler>();

        return services;
    }

    public static IServiceCollection AddAtomSSHFakeTransfer(this IServiceCollection services)
    {
        services.AddSingleton<ITransferTaskScheduler, FakeTransferTaskScheduler>();

        return services;
    }
}
