namespace MapProfit.Core;

public sealed record MapStatistics(int Count, double Profit, double Seconds)
{
    public double AverageProfit => Count > 0 ? Profit / Count : 0;
    public double AverageSeconds => Count > 0 ? Seconds / Count : 0;
    public bool HasRate => Count > 0 && Seconds >= 120;
    public double ChaosPerHour => HasRate ? Profit * 3600 / Seconds : 0;

    public static MapStatistics From(ProfitSession session, int? limit = null)
    {
        var maps = session.Runs.Where(r => r.RateProfitChaos.HasValue && r.RateActiveSeconds > 0)
            .OrderByDescending(r => r.Started).AsEnumerable();
        if (limit.HasValue) maps = maps.Take(Math.Max(0, limit.Value));
        var selected = maps.ToArray();
        return new MapStatistics(selected.Length, selected.Sum(r => r.RateProfitChaos!.Value),
            selected.Sum(r => r.RateActiveSeconds));
    }
}

public sealed record SummaryLoot(string Name, int Quantity, double Chaos);

public sealed record MapExitSummary(string Name, double Profit, double Seconds, SummaryLoot[] TopLoot)
{
    public static MapExitSummary From(MapRun run) => new(run.Name, run.ProfitChaos, run.ActiveSeconds,
        run.Loot.Values.Where(e => e.Quantity > 0 && e.Chaos > 0 && e.UnpricedQuantity == 0)
            .GroupBy(e => e.Name)
            .Select(g => new SummaryLoot(g.Key, g.Sum(e => e.Quantity), g.Sum(e => e.Chaos)))
            .OrderByDescending(e => e.Chaos).ThenBy(e => e.Name).Take(3).ToArray());
}
