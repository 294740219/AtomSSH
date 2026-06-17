using AtomSSH.Application;
using AtomSSH.Application.CommandSnippets;
using AtomSSH.Application.DependencyInjection;
using AtomSSH.Application.Profiles;
using AtomSSH.Application.Sessions;
using AtomSSH.Application.Transfers;
using AtomSSH.Core.CommandSnippets;
using AtomSSH.Core.Network;
using AtomSSH.Core.Ports;
using AtomSSH.Core.Profiles;
using AtomSSH.Core.Results;
using AtomSSH.Core.Terminal;
using AtomSSH.Core.Transfers;
using AtomSSH.Core.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

namespace AtomSSH.Application.Tests;

public sealed class ApplicationModuleTests
{
    [Fact]
    public void AddAtomSSHApplicationReturnsSameServiceCollection()
    {
        var services = new ServiceCollection();

        var result = services.AddAtomSSHApplication();

        Assert.Same(services, result);
        Assert.Equal("AtomSSH.Application", ApplicationModule.Name);
    }

    [Fact]
    public void AddAtomSSHApplicationRegistersUseCaseServices()
    {
        var services = new ServiceCollection();

        services.AddAtomSSHApplication();

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ProfileAppService));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(SessionAppService));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(TransferAppService));
    }

    [Fact]
    public async Task CreateSftpTransferPlansRouteBeforeSubmittingScheduler()
    {
        var profile = CreateProfile();
        var profileRepository = new FakeProfileRepository(profile);
        var routePlanner = new FakeRoutePlanner(profile.Endpoint);
        var taskStore = new FakeTransferTaskStore();
        var stateStore = new FakeTransferStateStore();
        var scheduler = new CapturingTransferTaskScheduler();
        var appService = new TransferAppService(profileRepository, routePlanner, taskStore, stateStore, scheduler);
        var task = new SftpTransferTask(
            TransferTaskId.New(),
            profile.Id,
            TransferDirection.Download,
            new LocalPath("C:\\temp\\app.log"),
            new RemotePath("/var/log/app.log"),
            TransferOverwritePolicy.Overwrite,
            DateTimeOffset.UtcNow,
            TransferStatus.Pending);

        var result = await appService.CreateSftpTransferAsync(task, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(task.Id, result.Value);
        Assert.True(taskStore.SftpTaskSaved);
        Assert.NotNull(scheduler.SftpExecutionPlan);
        Assert.Equal(task.Id, scheduler.SftpExecutionPlan.TaskId);
        Assert.Equal(ConnectionRouteKind.Direct, scheduler.SftpExecutionPlan.SourceRoute.Kind);
    }

    [Fact]
    public async Task CommandSnippetSendWritesSnippetTextToTerminalChannel()
    {
        var snippet = new CommandSnippet(CommandSnippetId.New(), "uptime", "uptime");
        var repository = new FakeCommandSnippetRepository(snippet);
        var runtime = new CapturingSessionRuntime();
        var appService = new CommandSnippetAppService(repository, runtime);
        var sessionId = SshSessionInstanceId.New();

        var result = await appService.SendAsync(snippet.Id, sessionId, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(sessionId, runtime.Channel.SessionId);
        Assert.Equal("uptime\n", runtime.Channel.SentText);
    }

    private static SshProfile CreateProfile()
    {
        return new SshProfile(
            SshProfileId.New(),
            "prod",
            new SshEndpoint(new HostName("prod.internal"), 22),
            "ops",
            SshAuthMethod.Password,
            CredentialRef.New(),
            Group: null,
            TerminalProfile: null);
    }

    private sealed class FakeProfileRepository : ISshProfileRepository
    {
        private readonly SshProfile _profile;

        public FakeProfileRepository(SshProfile profile)
        {
            _profile = profile;
        }

        public Task<OperationResult<SshProfile?>> GetAsync(SshProfileId id, CancellationToken cancellationToken)
        {
            return Task.FromResult(OperationResult<SshProfile?>.Success(_profile.Id == id ? _profile : null));
        }

        public Task<OperationResult<IReadOnlyList<SshProfile>>> ListAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(OperationResult<IReadOnlyList<SshProfile>>.Success(new[] { _profile }));
        }

        public Task<OperationResult> SaveAsync(SshProfile profile, CancellationToken cancellationToken)
        {
            return Task.FromResult(OperationResult.Success());
        }

        public Task<OperationResult> DeleteAsync(SshProfileId id, CancellationToken cancellationToken)
        {
            return Task.FromResult(OperationResult.Success());
        }
    }

    private sealed class FakeRoutePlanner : IConnectionRoutePlanner
    {
        private readonly SshEndpoint _endpoint;

        public FakeRoutePlanner(SshEndpoint endpoint)
        {
            _endpoint = endpoint;
        }

        public Task<OperationResult<ConnectionRoute>> PlanAsync(SshProfile profile, CancellationToken cancellationToken)
        {
            return Task.FromResult(OperationResult<ConnectionRoute>.Success(
                new ConnectionRoute(ConnectionRouteKind.Direct, _endpoint, Array.Empty<JumpHostRoute>())));
        }
    }

    private sealed class FakeTransferTaskStore : ITransferTaskStore
    {
        public bool SftpTaskSaved { get; private set; }

        public Task<OperationResult> SaveAsync(SftpTransferTask task, CancellationToken cancellationToken)
        {
            SftpTaskSaved = true;
            return Task.FromResult(OperationResult.Success());
        }

        public Task<OperationResult> SaveAsync(RemoteCopyTask task, CancellationToken cancellationToken)
        {
            return Task.FromResult(OperationResult.Success());
        }
    }

    private sealed class FakeTransferStateStore : ITransferStateStore
    {
        public Task<OperationResult<IReadOnlyList<TransferProgress>>> ListAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(OperationResult<IReadOnlyList<TransferProgress>>.Success(Array.Empty<TransferProgress>()));
        }

        public Task<OperationResult> SaveAsync(TransferProgress progress, CancellationToken cancellationToken)
        {
            return Task.FromResult(OperationResult.Success());
        }
    }

    private sealed class CapturingTransferTaskScheduler : ITransferTaskScheduler
    {
        public TransferExecutionPlan? SftpExecutionPlan { get; private set; }

        public Task<OperationResult> SubmitAsync(
            SftpTransferTask task,
            TransferExecutionPlan executionPlan,
            CancellationToken cancellationToken)
        {
            SftpExecutionPlan = executionPlan;
            return Task.FromResult(OperationResult.Success());
        }

        public Task<OperationResult> SubmitAsync(
            RemoteCopyTask task,
            TransferExecutionPlan executionPlan,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(OperationResult.Success());
        }

        public Task<OperationResult> CancelAsync(TransferTaskId taskId, CancellationToken cancellationToken)
        {
            return Task.FromResult(OperationResult.Success());
        }
    }

    private sealed class FakeCommandSnippetRepository : ICommandSnippetRepository
    {
        private readonly CommandSnippet _snippet;

        public FakeCommandSnippetRepository(CommandSnippet snippet)
        {
            _snippet = snippet;
        }

        public Task<OperationResult<IReadOnlyList<CommandSnippet>>> ListAsync(CancellationToken cancellationToken)
        {
            IReadOnlyList<CommandSnippet> snippets = [_snippet];
            return Task.FromResult(OperationResult<IReadOnlyList<CommandSnippet>>.Success(snippets));
        }

        public Task<OperationResult> SaveAsync(CommandSnippet snippet, CancellationToken cancellationToken)
        {
            return Task.FromResult(OperationResult.Success());
        }
    }

    private sealed class CapturingSessionRuntime : ISshSessionRuntime
    {
        public CapturingTerminalChannel Channel { get; } = new(SshSessionInstanceId.New());

        public Task<OperationResult<SshSessionInstanceId>> OpenTerminalAsync(
            SshProfile profile,
            ConnectionRoute route,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(OperationResult<SshSessionInstanceId>.Success(Channel.SessionId));
        }

        public Task<OperationResult<ITerminalChannel>> GetTerminalChannelAsync(
            SshSessionInstanceId sessionId,
            CancellationToken cancellationToken)
        {
            Channel.SessionId = sessionId;
            return Task.FromResult(OperationResult<ITerminalChannel>.Success(Channel));
        }

        public Task<OperationResult> CloseAsync(SshSessionInstanceId sessionId, CancellationToken cancellationToken)
        {
            return Task.FromResult(OperationResult.Success());
        }
    }

    private sealed class CapturingTerminalChannel : ITerminalChannel
    {
        public CapturingTerminalChannel(SshSessionInstanceId sessionId)
        {
            SessionId = sessionId;
        }

        public SshSessionInstanceId SessionId { get; set; }

        public string? SentText { get; private set; }

        public Task<OperationResult<int>> ReadAsync(Memory<byte> output, CancellationToken cancellationToken)
        {
            return Task.FromResult(OperationResult<int>.Success(0));
        }

        public Task<OperationResult> SendAsync(ReadOnlyMemory<byte> input, CancellationToken cancellationToken)
        {
            SentText = System.Text.Encoding.UTF8.GetString(input.Span);
            return Task.FromResult(OperationResult.Success());
        }

        public Task<OperationResult> ResizeAsync(TerminalSize size, CancellationToken cancellationToken)
        {
            return Task.FromResult(OperationResult.Success());
        }
    }
}
