using System.Collections.Concurrent;
using AtomSSH.Core.Network;
using AtomSSH.Core.Ports;
using AtomSSH.Core.Profiles;
using AtomSSH.Core.Results;
using AtomSSH.Core.Terminal;
using AtomSSH.Core.ValueObjects;
using AtomSSH.Session.Errors;
using AtomSSH.Session.HostKeys;
using AtomSSH.Session.Terminal;
using Renci.SshNet;

namespace AtomSSH.Session.Connections;

internal sealed class SshNetSessionRuntime : ISshSessionRuntime, ISshSessionFactory
{
    private readonly SshNetClientConnector _connector;
    private readonly ConcurrentDictionary<SshSessionInstanceId, SshNetSessionState> _sessions = new();

    public SshNetSessionRuntime(SshNetClientConnector connector)
    {
        _connector = connector;
    }

    public Task<OperationResult<SshSessionInstanceId>> OpenTerminalAsync(
        SshProfile profile,
        ConnectionRoute route,
        CancellationToken cancellationToken)
    {
        return OpenAsync(profile, route, cancellationToken);
    }

    public async Task<OperationResult<SshSessionInstanceId>> OpenAsync(
        SshProfile profile,
        ConnectionRoute route,
        CancellationToken cancellationToken)
    {
        if (profile.CredentialRef is null)
        {
            return OperationResult<SshSessionInstanceId>.Failure(new SshError(
                SshErrorKind.Validation,
                "SSH profile does not reference a credential."));
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var connectionResult = await _connector.ConnectSshClientAsync(profile, route, cancellationToken)
                .ConfigureAwait(false);
            if (!connectionResult.Succeeded || connectionResult.Value is null)
            {
                return OperationResult<SshSessionInstanceId>.Failure(connectionResult.Error!);
            }

            var sessionId = SshSessionInstanceId.New();
            var size = profile.TerminalProfile?.InitialSize ?? new TerminalSize(80, 24);
            var connection = connectionResult.Value;
            try
            {
                var shellStream = connection.Client.CreateShellStream(
                    "xterm-256color",
                    (uint)size.Columns,
                    (uint)size.Rows,
                    0,
                    0,
                    4096);
                var terminalChannel = new SshNetTerminalChannel(sessionId, shellStream);
                var state = new SshNetSessionState(connection, terminalChannel);

                if (!_sessions.TryAdd(sessionId, state))
                {
                    state.Dispose();
                    return OperationResult<SshSessionInstanceId>.Failure(new SshError(
                        SshErrorKind.Internal,
                        "SSH session could not be registered."));
                }

                return OperationResult<SshSessionInstanceId>.Success(sessionId);
            }
            catch
            {
                connection.Dispose();
                throw;
            }
        }
        catch (HostKeyRejectedException exception)
        {
            return OperationResult<SshSessionInstanceId>.Failure(exception.Error);
        }
        catch (Exception exception)
        {
            return OperationResult<SshSessionInstanceId>.Failure(SshNetErrorMapper.Map(exception));
        }
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
        if (!_sessions.TryRemove(sessionId, out var state))
        {
            return Task.FromResult(OperationResult.Success());
        }

        try
        {
            state.Dispose();
            return Task.FromResult(OperationResult.Success());
        }
        catch (Exception exception)
        {
            return Task.FromResult(OperationResult.Failure(SshNetErrorMapper.Map(exception)));
        }
    }

    private sealed class SshNetSessionState : IDisposable
    {
        public SshNetSessionState(
            SshNetClientConnection<SshClient> connection,
            SshNetTerminalChannel terminalChannel)
        {
            Connection = connection;
            TerminalChannel = terminalChannel;
        }

        public SshNetClientConnection<SshClient> Connection { get; }

        public SshNetTerminalChannel TerminalChannel { get; }

        public void Dispose()
        {
            TerminalChannel.Dispose();
            Connection.Dispose();
        }
    }
}
