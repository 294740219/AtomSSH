using System.Collections.Concurrent;
using AtomSSH.Core.Network;
using AtomSSH.Core.Ports;
using AtomSSH.Core.Profiles;
using AtomSSH.Core.Results;
using AtomSSH.Core.Terminal;
using AtomSSH.Core.ValueObjects;

namespace AtomSSH.Session.Connections;

public sealed class FakeSshSessionRuntime : ISshSessionRuntime, ISshSessionFactory
{
    private readonly ConcurrentDictionary<SshSessionInstanceId, FakeSessionState> _sessions = new();

    public Task<OperationResult<SshSessionInstanceId>> OpenTerminalAsync(
        SshProfile profile,
        ConnectionRoute route,
        CancellationToken cancellationToken)
    {
        return OpenAsync(profile, route, cancellationToken);
    }

    public Task<OperationResult<SshSessionInstanceId>> OpenAsync(
        SshProfile profile,
        ConnectionRoute route,
        CancellationToken cancellationToken)
    {
        var sessionId = SshSessionInstanceId.New();
        _sessions[sessionId] = new FakeSessionState(profile.Id, route, new FakeTerminalChannel(sessionId));
        return Task.FromResult(OperationResult<SshSessionInstanceId>.Success(sessionId));
    }

    public Task<OperationResult<ITerminalChannel>> GetTerminalChannelAsync(
        SshSessionInstanceId sessionId,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(_sessions.TryGetValue(sessionId, out var state)
            ? OperationResult<ITerminalChannel>.Success(state.TerminalChannel)
            : OperationResult<ITerminalChannel>.Failure(new SshError(
                SshErrorKind.Validation,
                "SSH session was not found.",
                sessionId.Value.ToString())));
    }

    public Task<OperationResult> CloseAsync(SshSessionInstanceId sessionId, CancellationToken cancellationToken)
    {
        _sessions.TryRemove(sessionId, out _);
        return Task.FromResult(OperationResult.Success());
    }

    private sealed record FakeSessionState(
        SshProfileId ProfileId,
        ConnectionRoute Route,
        FakeTerminalChannel TerminalChannel);

    public sealed class FakeTerminalChannel : ITerminalChannel
    {
        private readonly List<byte> _input = new();

        public FakeTerminalChannel(SshSessionInstanceId sessionId)
        {
            SessionId = sessionId;
        }

        public SshSessionInstanceId SessionId { get; }

        public TerminalSize LastSize { get; private set; } = new(80, 24);

        public IReadOnlyList<byte> Input => _input;

        public Task<OperationResult<int>> ReadAsync(Memory<byte> output, CancellationToken cancellationToken)
        {
            var bytes = "AtomSSH fake terminal ready.\n"u8.ToArray();
            var count = Math.Min(bytes.Length, output.Length);
            bytes.AsMemory(0, count).CopyTo(output);
            return Task.FromResult(OperationResult<int>.Success(count));
        }

        public Task<OperationResult> SendAsync(ReadOnlyMemory<byte> input, CancellationToken cancellationToken)
        {
            _input.AddRange(input.ToArray());
            return Task.FromResult(OperationResult.Success());
        }

        public Task<OperationResult> ResizeAsync(TerminalSize size, CancellationToken cancellationToken)
        {
            LastSize = size;
            return Task.FromResult(OperationResult.Success());
        }
    }
}
