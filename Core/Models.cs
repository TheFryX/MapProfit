using System.Text.Json.Serialization;

namespace MapProfit.Core;

public sealed record ItemSnapshot(string Key, string Name, int Quantity, double? UnitChaos, bool IsEquipped = false);

public sealed class LootEntry
{
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public int Quantity { get; set; }
    public double? UnitChaos { get; set; }
    public double Chaos { get; set; }
    public int UnpricedQuantity { get; set; }
    [JsonIgnore] public DateTimeOffset LastPickupUtc { get; set; }
}

public sealed class MapRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public DateTimeOffset Started { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastVisited { get; set; } = DateTimeOffset.UtcNow;
    public List<string> Areas { get; set; } = [];
    public double ActiveSeconds { get; set; }
    public double EntryCostChaos { get; set; }
    public double? RateProfitChaos { get; set; }
    public double RateActiveSeconds { get; set; }
    public Dictionary<string, LootEntry> Loot { get; set; } = [];
    [JsonIgnore] public double LootChaos => Loot.Values.Sum(x => x.Chaos);
    [JsonIgnore] public double ProfitChaos => LootChaos - EntryCostChaos;
    [JsonIgnore] public double ChaosPerHour => ActiveSeconds >= 1 ? ProfitChaos * 3600 / ActiveSeconds : 0;
    public void CheckpointRate()
    {
        if (ActiveSeconds <= 0) return;
        RateProfitChaos = ProfitChaos;
        RateActiveSeconds = ActiveSeconds;
    }
}

public sealed class ProfitSession
{
    public int Version { get; set; } = 1;
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Character { get; set; } = "";
    public DateTimeOffset Started { get; set; } = DateTimeOffset.UtcNow;
    public Guid? CurrentRunId { get; set; }
    public List<MapRun> Runs { get; set; } = [];
    [JsonIgnore] public MapRun? Current => Runs.Find(x => x.Id == CurrentRunId);
    [JsonIgnore] public double ProfitChaos => Runs.Sum(x => x.ProfitChaos);
    [JsonIgnore] public double ActiveSeconds => Runs.Sum(x => x.ActiveSeconds);
    [JsonIgnore] public int RateMapCount => Runs.Count(x => x.RateProfitChaos.HasValue && x.RateActiveSeconds > 0);
    [JsonIgnore] public double RateSeconds => Runs.Where(x => x.RateProfitChaos.HasValue).Sum(x => x.RateActiveSeconds);
    [JsonIgnore] public bool HasReliableRate => RateMapCount > 0 && RateSeconds >= 120;
    [JsonIgnore] public double ChaosPerHour => HasReliableRate
        ? Runs.Where(x => x.RateProfitChaos.HasValue).Sum(x => x.RateProfitChaos!.Value) * 3600 / RateSeconds : 0;
}

public static class PriceMath
{
    public static double? PerUnit(double total, int count) =>
        count > 0 && double.IsFinite(total) && total > 0 ? total / count : null;
}
