using System.Globalization;
using Hexa.NET.ImGui;
using MapProfit.Core;

namespace MapProfit;

public sealed partial class MapProfitPlugin
{
    private int RecentWindow => Settings.RecentMapCount is 5 or 10 or 20 ? Settings.RecentMapCount : 10;

    private void DrawCompactStatistics()
    {
        if (!Settings.ShowAverages && !Settings.ShowRecentMaps) return;
        var all = MapStatistics.From(_tracker.Session);
        ImGui.Separator();
        if (Settings.ShowAverages)
        {
            string average = all.Count > 0
                ? $"Avg: {Money(all.AverageProfit)} c/map | {Duration(all.AverageSeconds)}"
                : "Avg: -- c/map | --:--";
            ImGui.TextColored(Muted, Fit(average, ImGui.GetContentRegionAvail().X));
        }
        if (Settings.ShowRecentMaps)
        {
            var recent = MapStatistics.From(_tracker.Session, RecentWindow);
            string sample = recent.Count < RecentWindow ? $"{recent.Count}/{RecentWindow}" : RecentWindow.ToString();
            string rate = recent.HasRate ? Money(recent.ChaosPerHour) + " c/h" : "collecting maps";
            double? change = RelativeRate(recent, all);
            string comparison = change.HasValue ? $" ({Percent(change.Value)})" : "";
            ImGui.TextColored(Muted, Fit($"Last {sample}: {rate}{comparison}", ImGui.GetContentRegionAvail().X));
        }
    }

    private static double? RelativeRate(MapStatistics recent, MapStatistics all) =>
        recent.HasRate && all.HasRate && Math.Abs(all.ChaosPerHour) > 0.000001
            ? (recent.ChaosPerHour - all.ChaosPerHour) / Math.Abs(all.ChaosPerHour) * 100 : null;

    private static string Percent(double value) => (value >= 0 ? "+" : "") + value.ToString("0.0", CultureInfo.InvariantCulture) + "%";

    private void DrawStatisticsComparison(ProfitSession session)
    {
        var all = MapStatistics.From(session);
        if (!ImGui.BeginTable("MapAverages", 6, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp)) return;
        ImGui.TableSetupColumn("Range"); ImGui.TableSetupColumn("Maps");
        ImGui.TableSetupColumn("Avg c/map"); ImGui.TableSetupColumn("Avg time");
        ImGui.TableSetupColumn("c/h"); ImGui.TableSetupColumn("vs session");
        ImGui.TableHeadersRow();
        foreach (int limit in new[] { 0, 5, 10, 20 })
        {
            var sample = limit == 0 ? all : MapStatistics.From(session, limit);
            ImGui.TableNextRow(); ImGui.TableNextColumn();
            ImGui.TextUnformatted(limit == 0 ? "Session" : $"Last {limit}");
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(limit == 0 ? sample.Count.ToString() : $"{sample.Count}/{limit}");
            ImGui.TableNextColumn(); ImGui.TextUnformatted(sample.Count > 0 ? Money(sample.AverageProfit) : "--");
            ImGui.TableNextColumn(); ImGui.TextUnformatted(sample.Count > 0 ? Duration(sample.AverageSeconds) : "--:--");
            ImGui.TableNextColumn(); ImGui.TextUnformatted(sample.HasRate ? Money(sample.ChaosPerHour) : "--");
            ImGui.TableNextColumn();
            double? change = limit == 0 ? null : RelativeRate(sample, all);
            if (change.HasValue) ImGui.TextColored(change.Value >= 0 ? Green : Red, Percent(change.Value));
            else ImGui.TextDisabled("--");
        }
        ImGui.EndTable();
        ImGui.TextDisabled("Exit records only; returns update the same map. Recent maps are ordered by first entry.");
    }

    private void DrawStatisticsOptions()
    {
        ImGui.Separator();
        bool averages = Settings.ShowAverages.Value;
        if (ImGui.Checkbox("Average profit / time per map", ref averages))
        {
            Settings.ShowAverages.Value = averages; SavePanelSettings();
        }
        bool recent = Settings.ShowRecentMaps.Value;
        if (ImGui.Checkbox("Recent maps comparison", ref recent))
        {
            Settings.ShowRecentMaps.Value = recent; SavePanelSettings();
        }
        foreach (int count in new[] { 5, 10, 20 })
        {
            if (count != 5) ImGui.SameLine();
            if (ImGui.RadioButton(count + " maps##RecentWindow", RecentWindow == count))
            {
                Settings.RecentMapCount = count; SavePanelSettings();
            }
        }
        bool summary = Settings.ShowExitSummary.Value;
        if (ImGui.Checkbox("Small map-exit summary", ref summary))
        {
            Settings.ShowExitSummary.Value = summary; SavePanelSettings();
        }
        int seconds = Settings.ExitSummarySeconds.Value;
        if (ImGui.SliderInt("Summary seconds", ref seconds, 3, 30)) Settings.ExitSummarySeconds.Value = seconds;
        if (ImGui.IsItemDeactivatedAfterEdit()) SavePanelSettings();
        ImGui.Separator();
    }
}
