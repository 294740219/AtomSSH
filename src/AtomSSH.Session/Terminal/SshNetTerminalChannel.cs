using AtomSSH.Core.Ports;
using AtomSSH.Core.Results;
using AtomSSH.Core.Terminal;
using AtomSSH.Core.ValueObjects;
using AtomSSH.Session.Errors;
using Renci.SshNet;

namespace AtomSSH.Session.Terminal;

internal sealed class SshNetTerminalChannel : ITerminalChannel, IDisposable
{
    private readonly ShellStream _shellStream;

    public SshNetTerminalChannel(SshSessionInstanceId sessionId, ShellStream shellStream)
    {
        SessionId = sessionId;
        _shellStream = shellStream;
    }

    public SshSessionInstanceId SessionId { get; }

    public async Task<OperationResult<int>> ReadAsync(Memory<byte> output, CancellationToken cancellationToken)
    {
        try
        {
            var count = await _shellStream.ReadAsync(output, cancellationToken).ConfigureAwait(false);
            return OperationResult<int>.Success(count);
        }
        catch (Exception exception)
        {
            return OperationResult<int>.Failure(SshNetErrorMapper.Map(exception));
        }
    }

    public async Task<OperationResult> SendAsync(ReadOnlyMemory<byte> input, CancellationToken cancellationToken)
    {
        try
        {
            await _shellStream.WriteAsync(input, cancellationToken).ConfigureAwait(false);
            await _shellStream.FlushAsync(cancellationToken).ConfigureAwait(false);
            return OperationResult.Success();
        }
        catch (Exception exception)
        {
            return OperationResult.Failure(SshNetErrorMapper.Map(exception));
        }
    }

    public Task<OperationResult> ResizeAsync(TerminalSize size, CancellationToken cancellationToken)
    {
        try
        {
            _shellStream.ChangeWindowSize((uint)size.Columns, (uint)size.Rows, 0, 0);
            return Task.FromResult(OperationResult.Success());
        }
        catch (Exception exception)
        {
            return Task.FromResult(OperationResult.Failure(SshNetErrorMapper.Map(exception)));
        }
    }

    public void Dispose()
    {
        _shellStream.Dispose();
    }
}
