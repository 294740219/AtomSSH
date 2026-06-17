using AtomSSH.Core.Network;
using AtomSSH.Core.Ports;
using AtomSSH.Core.Results;
using AtomSSH.Infrastructure.Configuration;
using AtomSSH.Infrastructure.Storage;

namespace AtomSSH.Infrastructure.NetworkInventory;

public sealed class JsonNetworkInventoryStore : INetworkInventoryStore
{
    private readonly JsonFileStore<NetworkInventoryEnvelope> _store;

    public JsonNetworkInventoryStore(AtomSshDataDirectory directory)
    {
        _store = new JsonFileStore<NetworkInventoryEnvelope>(directory.NetworkInventoryFile);
    }

    public async Task<OperationResult<IReadOnlyList<NetworkSpace>>> ListSpacesAsync(CancellationToken cancellationToken)
    {
        var envelope = await ReadEnvelopeAsync(cancellationToken).ConfigureAwait(false);
        return !envelope.Succeeded
            ? OperationResult<IReadOnlyList<NetworkSpace>>.Failure(envelope.Error!)
            : OperationResult<IReadOnlyList<NetworkSpace>>.Success(envelope.Value!.Spaces);
    }

    public async Task<OperationResult<IReadOnlyList<NetworkNode>>> ListNodesAsync(Guid networkSpaceId, CancellationToken cancellationToken)
    {
        var envelope = await ReadEnvelopeAsync(cancellationToken).ConfigureAwait(false);
        return !envelope.Succeeded
            ? OperationResult<IReadOnlyList<NetworkNode>>.Failure(envelope.Error!)
            : OperationResult<IReadOnlyList<NetworkNode>>.Success(
                envelope.Value!.Nodes.Where(node => node.NetworkSpaceId == networkSpaceId).ToArray());
    }

    public async Task<OperationResult> SaveSpaceAsync(NetworkSpace space, CancellationToken cancellationToken)
    {
        var envelope = await ReadEnvelopeAsync(cancellationToken).ConfigureAwait(false);
        if (!envelope.Succeeded || envelope.Value is null)
        {
            return OperationResult.Failure(envelope.Error!);
        }

        envelope.Value.Spaces.RemoveAll(existing => existing.Id == space.Id);
        envelope.Value.Spaces.Add(space);
        return await _store.WriteAsync(envelope.Value, cancellationToken).ConfigureAwait(false);
    }

    public async Task<OperationResult> SaveNodeAsync(NetworkNode node, CancellationToken cancellationToken)
    {
        var envelope = await ReadEnvelopeAsync(cancellationToken).ConfigureAwait(false);
        if (!envelope.Succeeded || envelope.Value is null)
        {
            return OperationResult.Failure(envelope.Error!);
        }

        envelope.Value.Nodes.RemoveAll(existing => existing.Id == node.Id);
        envelope.Value.Nodes.Add(node);
        return await _store.WriteAsync(envelope.Value, cancellationToken).ConfigureAwait(false);
    }

    private Task<OperationResult<NetworkInventoryEnvelope>> ReadEnvelopeAsync(CancellationToken cancellationToken)
    {
        return _store.ReadAsync(new NetworkInventoryEnvelope(), cancellationToken);
    }
}

public sealed record NetworkInventoryEnvelope
{
    public List<NetworkSpace> Spaces { get; init; } = new();

    public List<NetworkNode> Nodes { get; init; } = new();
}
