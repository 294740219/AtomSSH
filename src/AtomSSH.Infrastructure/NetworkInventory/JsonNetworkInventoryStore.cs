using AtomSSH.Core.Network;
using AtomSSH.Core.Ports;
using AtomSSH.Core.Results;
using AtomSSH.Core.ValueObjects;
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
        return await _store.UpdateAsync(
            new NetworkInventoryEnvelope(),
            envelope =>
            {
                envelope.Spaces.RemoveAll(existing => existing.Id == space.Id);
                envelope.Spaces.Add(space);
                return OperationResult<NetworkInventoryEnvelope>.Success(envelope);
            },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<OperationResult> SaveNodeAsync(NetworkNode node, CancellationToken cancellationToken)
    {
        return await _store.UpdateAsync(
            new NetworkInventoryEnvelope(),
            envelope =>
            {
                envelope.Nodes.RemoveAll(existing => existing.Id == node.Id);
                envelope.Nodes.Add(node);
                return OperationResult<NetworkInventoryEnvelope>.Success(envelope);
            },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<OperationResult> DeleteSpaceAsync(Guid networkSpaceId, CancellationToken cancellationToken)
    {
        return await _store.UpdateAsync(
            new NetworkInventoryEnvelope(),
            envelope =>
            {
                envelope.Spaces.RemoveAll(existing => existing.Id == networkSpaceId);
                envelope.Nodes.RemoveAll(existing => existing.NetworkSpaceId == networkSpaceId);
                return OperationResult<NetworkInventoryEnvelope>.Success(envelope);
            },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<OperationResult> DeleteNodeAsync(NetworkNodeId nodeId, CancellationToken cancellationToken)
    {
        return await _store.UpdateAsync(
            new NetworkInventoryEnvelope(),
            envelope =>
            {
                envelope.Nodes.RemoveAll(existing => existing.Id == nodeId);
                return OperationResult<NetworkInventoryEnvelope>.Success(envelope);
            },
            cancellationToken).ConfigureAwait(false);
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
