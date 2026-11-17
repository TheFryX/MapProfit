namespace MapProfit.Core;

public sealed class InventoryLedger
{
    private Dictionary<string, ItemSnapshot>? _previous;
    private readonly Dictionary<string, int> _pendingRemovals = [];
    private readonly HashSet<string> _equippedOwnership = [];
    public bool HasBaseline => _previous != null;
    public void Forget() { _previous = null; _pendingRemovals.Clear(); _equippedOwnership.Clear(); }
    public void Rebase(IReadOnlyDictionary<string, ItemSnapshot> snapshot)
    {
        _previous = new(snapshot);
        _pendingRemovals.Clear();
        RememberEquipment(snapshot);
    }

    private void RememberEquipment(IReadOnlyDictionary<string, ItemSnapshot> snapshot)
    {
        foreach (var item in snapshot.Values.Where(x => x.IsEquipped)) _equippedOwnership.Add(item.Key);
    }

    public void Apply(IReadOnlyDictionary<string, ItemSnapshot> snapshot, MapRun run)
    {
        RememberEquipment(snapshot);
        foreach (var item in snapshot.Values)
        {
            if (item.UnitChaos is > 0 && run.Loot.TryGetValue(item.Key, out var pending) && pending.UnpricedQuantity != 0)
            {
                pending.UnitChaos = item.UnitChaos;
                pending.Chaos = pending.Quantity * item.UnitChaos.Value;
                pending.UnpricedQuantity = 0;
            }
        }
        if (_previous == null) { Rebase(snapshot); return; }
        foreach (var key in _previous.Keys.Union(snapshot.Keys).ToArray())
        {
            _previous.TryGetValue(key, out var before);
            snapshot.TryGetValue(key, out var after);
            if (_equippedOwnership.Contains(key))
            {
                _pendingRemovals.Remove(key);
                if (after == null) _previous.Remove(key);
                else _previous[key] = after;
                continue;
            }
            int delta = (after?.Quantity ?? 0) - (before?.Quantity ?? 0);
            if (delta == 0)
            {
                _pendingRemovals.Remove(key);
                if (after != null) _previous[key] = after;
                continue;
            }
            if (delta < 0)
            {
                int remaining = after?.Quantity ?? 0;
                if (!_pendingRemovals.TryGetValue(key, out var candidate) || candidate != remaining)
                {
                    _pendingRemovals[key] = remaining;
                    continue;
                }
            }
            _pendingRemovals.Remove(key);
            var item = after ?? before!;
            var quote = after?.UnitChaos ?? before?.UnitChaos;
            if (!run.Loot.TryGetValue(key, out var entry))
                run.Loot[key] = entry = new LootEntry { Key = key, Name = item.Name };
            entry.Quantity += delta;
            entry.UnitChaos ??= quote;
            entry.Chaos = entry.UnitChaos is > 0 ? entry.Quantity * entry.UnitChaos.Value : 0;
            entry.UnpricedQuantity = entry.UnitChaos is > 0 ? 0 : entry.Quantity;
            if (delta > 0) entry.LastPickupUtc = DateTimeOffset.UtcNow;
            if (after == null) _previous.Remove(key);
            else _previous[key] = after;
        }
    }
}

public sealed class SnapshotGate
{
    private Dictionary<string, ItemSnapshot>? _candidate;
    public void Reset() => _candidate = null;
    public bool Accept(IReadOnlyDictionary<string, ItemSnapshot> snapshot)
    {
        bool matches = _candidate != null && _candidate.Count == snapshot.Count &&
            snapshot.All(x => _candidate.TryGetValue(x.Key, out var previous) && previous.Quantity == x.Value.Quantity);
        _candidate = new(snapshot);
        return matches;
    }
}
