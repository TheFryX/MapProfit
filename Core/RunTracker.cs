namespace MapProfit.Core;

public sealed class RunTracker(ProfitSession session)
{
    public ProfitSession Session { get; } = session;
    public bool InCombatArea { get; private set; }
    private string? _area;

    public void Enter(string areaKey, string name, bool peaceful, bool groupConnected, double entryCost, bool forceNew = false)
    {
        if (_area == areaKey && !forceNew) return;
        bool wasCombat = InCombatArea;
        _area = areaKey;
        InCombatArea = !peaceful;
        if (peaceful)
        {
            Session.Current?.CheckpointRate();
            return;
        }
        var run = forceNew ? null : Session.Runs.LastOrDefault(r => r.Areas.Contains(areaKey));
        if (run == null && !forceNew && wasCombat && groupConnected)
            run = Session.Current;
        if (run == null)
        {
            Session.Current?.CheckpointRate();
            run = new MapRun { Name = name, EntryCostChaos = Math.Max(0, entryCost) };
            Session.Runs.Add(run);
        }
        else if (run.Id != Session.CurrentRunId) Session.Current?.CheckpointRate();
        if (!run.Areas.Contains(areaKey)) run.Areas.Add(areaKey);
        run.LastVisited = DateTimeOffset.UtcNow;
        Session.CurrentRunId = run.Id;
    }

    public void AddTime(double seconds)
    {
        if (InCombatArea && Session.Current is { } run && double.IsFinite(seconds) && seconds > 0)
            run.ActiveSeconds += seconds;
    }
}
