using Hexa.NET.ImGui;
using MapProfit.Core;

namespace MapProfit;

public sealed partial class MapProfitPlugin
{
    private void DrawRemoveLootButton(MapRun run, string[] keys)
    {
        if (ImGui.SmallButton("X##" + run.Id + ":" + keys[0])) RemoveLootRows(run, keys);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Remove this row from the selected map. Saves a full session copy in Sessions/Corrections first.");
    }

    private void RemoveLootRows(MapRun run, string[] keys)
    {
        if (!_tracker.Session.Runs.Contains(run)) return;
        var removed = keys.Where(run.Loot.ContainsKey).ToDictionary(key => key, key => run.Loot[key]);
        if (removed.Count == 0) return;
        double? rateBefore = run.RateProfitChaos;
        try
        {
            string backup = Path.Combine(ConfigDirectory, "Sessions", "Corrections",
                _tracker.Session.Id + "-" + DateTime.UtcNow.Ticks + ".json");
            SessionStore.Save(backup, _tracker.Session);
            foreach (var key in removed.Keys) run.Loot.Remove(key);
            if (run.RateProfitChaos.HasValue && Math.Abs(run.RateActiveSeconds - run.ActiveSeconds) < 0.001)
                run.RateProfitChaos = run.ProfitChaos;
            if (!SaveSession())
            {
                foreach (var pair in removed) run.Loot[pair.Key] = pair.Value;
                run.RateProfitChaos = rateBefore;
                return;
            }
            _notice = "Removed row from " + run.Name + ". Original session saved: " + backup;
        }
        catch (Exception ex)
        {
            foreach (var pair in removed) run.Loot[pair.Key] = pair.Value;
            run.RateProfitChaos = rateBefore;
            _storageError = "Correction failed: " + ex.Message;
        }
    }
}
