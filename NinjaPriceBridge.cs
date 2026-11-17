using ExileCore;
using System.Diagnostics;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
using MapProfit.Core;

namespace MapProfit;

internal sealed class NinjaPriceBridge(GameController controller)
{
    private Func<Entity, double>? _getValue;
    private long _nextConnect;
    private readonly Dictionary<string, (double? Price, long Expires)> _cache = [];
    private readonly Queue<string> _queue = new();
    private readonly Dictionary<string, Entity> _pending = [];
    public string Status { get; private set; } = "Waiting for Ninja Price";

    public double? GetUnitPrice(Entity entity, string key, int stackSize, long now)
    {
        if (now >= _nextConnect)
        {
            _nextConnect = now + 3000;
            try
            {
                var method = controller.PluginBridge.GetMethod<Func<Entity, double>>("NinjaPrice.GetValue");
                if (method != _getValue) { Clear(); _getValue = method; }
                Status = _getValue == null ? "Enable Ninja Price to get prices" : "Ninja Price connected";
            }
            catch { _getValue = null; Status = "Ninja Price bridge unavailable"; }
        }
        if (_getValue == null) return null;
        if (_cache.TryGetValue(key, out var cached) && now < cached.Expires) return cached.Price;
        if (!_pending.ContainsKey(key)) _queue.Enqueue(key);
        _pending[key] = entity;
        return cached.Price;
    }

    public void Pump(long now)
    {
        if (_getValue == null) return;
        long start = Stopwatch.GetTimestamp();
        int calls = 0;
        while (_queue.TryDequeue(out var key))
        {
            if (!_pending.Remove(key, out var entity)) continue;
            try
            {
                if (!entity.IsValid || entity.Address == 0) continue;
                int count = entity.GetComponent<Stack>()?.Size ?? 1;
                double? price = PriceMath.PerUnit(_getValue(entity), count);
                _cache[key] = (price, now + (price.HasValue ? 30000 : 1500));
            }
            catch
            {
                Status = "Ninja Price not ready - retrying";
                _cache[key] = (null, now + 1500);
            }
            if (++calls >= 6 || Stopwatch.GetElapsedTime(start).TotalMilliseconds >= 3) break;
        }
    }
    public void Clear() { _cache.Clear(); _pending.Clear(); _queue.Clear(); }
}
