using System.Globalization;
using System.Numerics;
using Hexa.NET.ImGui;
using MapProfit.Core;

namespace MapProfit;

public sealed partial class MapProfitPlugin
{
    private static readonly Vector4 Green = new(0.36f, 1f, 0.28f, 1f);
    private static readonly Vector4 Gold = new(1f, 0.79f, 0.25f, 1f);
    private static readonly Vector4 Muted = new(0.56f, 0.60f, 0.63f, 1f);
    private static readonly Vector4 Red = new(1f, 0.40f, 0.36f, 1f);

    public override void Render()
    {
        if (!Settings.Enable || !GameController.InGame || GameController.IsLoading) return;
        if (Settings.ShowHistory) DrawHistory();
        if (!Settings.ShowOverlay) return;
        if (Settings.HideWithPanels && GameController.IngameState.IngameUi.LargePanels.Any(x => x.IsVisible)) return;
        if (!Settings.ShowInTown && !_tracker.InCombatArea)
        {
            _mainPanelPosition = new Vector2(Settings.PanelX.Value, Settings.PanelY.Value);
            _mainPanelSize = new Vector2(Settings.PanelWidth.Value, 0);
            DrawExitSummary();
            return;
        }
        var flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.AlwaysAutoResize |
                    ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoSavedSettings;
        flags |= ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoMove;
        ImGui.SetNextWindowPos(new Vector2(Settings.PanelX.Value, Settings.PanelY.Value), ImGuiCond.Always);
        ImGui.SetNextWindowSize(new Vector2(Settings.PanelWidth.Value, 0), ImGuiCond.Always);
        ImGui.SetNextWindowBgAlpha(Settings.Opacity.Value);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 7f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(12, 10));
        ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0.055f, 0.065f, 0.08f, 1));
        ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(0.23f, 0.26f, 0.30f, 0.85f));
        bool visible = ImGui.Begin("Map Profit###MapProfitOverlay", flags);
        if (visible)
        {
            ImGui.PushFont(ImGui.GetFont(), ImGui.GetFontSize() * Settings.FontScale.Value);
            var run = _tracker.Session.Current;
            ImGui.Dummy(new Vector2(0, ImGui.GetFontSize() + 4));
            if (run == null)
            {
                ImGui.TextDisabled("Enter a map to start recording");
                ImGui.TextDisabled(_prices.Status);
            }
            else
            {
                if (!Settings.Collapsed)
                {
                    ImGui.TextUnformatted("Time: " + Duration(run.ActiveSeconds));
                    if (Settings.Pause || !_tracker.InCombatArea)
                    {
                        ImGui.SameLine(); ImGui.TextColored(Muted, Settings.Pause ? "[paused]" : "[in town]");
                    }
                }
                ImGui.TextColored(run.ProfitChaos >= 0 ? Green : Red,
                    Money(run.ChaosPerHour) + " c/h (actualy map)");
                ImGui.TextColored(Green, SessionRate(_tracker.Session) + " (session)");
                ImGui.SameLine(); ImGui.TextColored(Muted, $"[{_tracker.Session.Runs.Count} maps]");
                ImGui.Separator();
                ImGui.TextColored(run.ProfitChaos >= 0 ? Green : Red, "Total: " + Signed(run.ProfitChaos) + " c");
                if (!Settings.Collapsed)
                {
                    ImGui.Separator();
                    DrawLoot(run, Settings.MaxRows.Value, true);
                    DrawCompactStatistics();
                    if (Settings.ShowSession)
                    {
                        ImGui.Separator();
                        ImGui.TextColored(Muted, "Session: " + Signed(_tracker.Session.ProfitChaos) + " c  /  " + Duration(_tracker.Session.ActiveSeconds));
                    }
                }
                if (!_prices.Status.Contains("connected", StringComparison.Ordinal))
                    ImGui.TextColored(Gold, _prices.Status);
            }
            if (_storageError != null) ImGui.TextColored(Red, "History save failed - see plugin settings");
            _mainPanelPosition = ImGui.GetWindowPos();
            _mainPanelSize = ImGui.GetWindowSize();
            ImGui.PopFont();
        }
        ImGui.End();
        ImGui.PopStyleColor(2);
        ImGui.PopStyleVar(2);
        DrawOverlayHeader();
        DrawExitSummary();
    }

    private void DrawLoot(MapRun run, int maximum, bool compact)
    {
        var rows = run.Loot.Values.Where(x => x.Quantity != 0)
            .GroupBy(x => (x.Name, Unknown: x.UnpricedQuantity != 0))
            .Select(g => new { g.Key.Name, g.Key.Unknown, Quantity = g.Sum(x => x.Quantity), Chaos = g.Sum(x => x.Chaos), LastPickup = g.Max(x => x.LastPickupUtc), Keys = g.Select(x => x.Key).ToArray() })
            .Where(x => x.Quantity != 0 || Math.Abs(x.Chaos) > 0.000001).ToList();
        var priced = rows.Where(x => !x.Unknown).OrderByDescending(x => x.Chaos).ToList();
        if (rows.Count == 0) ImGui.TextColored(Muted, "No loot recorded yet");
        bool canCorrect = !compact && _tracker.Session.Runs.Contains(run);
        if (ImGui.BeginTable("Loot" + run.Id, canCorrect ? 3 : 2, ImGuiTableFlags.SizingStretchProp))
        {
            ImGui.TableSetupColumn("Item", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Chaos", ImGuiTableColumnFlags.WidthFixed, 76 * Settings.FontScale.Value);
            if (canCorrect) ImGui.TableSetupColumn("Correct", ImGuiTableColumnFlags.WidthFixed, 28);
            foreach (var entry in priced.Take(maximum))
            {
                ImGui.TableNextRow(); ImGui.TableNextColumn();
                var color = LootColor(entry.Quantity, entry.LastPickup);
                string label = $"{(entry.Quantity >= 0 ? "+" : "-")} {entry.Name}  x{Math.Abs(entry.Quantity)}";
                ImGui.TextColored(color, compact ? Fit(label, ImGui.GetContentRegionAvail().X) : label);
                if (ImGui.IsItemHovered()) ImGui.SetTooltip(label);
                ImGui.TableNextColumn();
                string value = Money(entry.Chaos);
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Math.Max(0, ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize(value).X));
                ImGui.TextColored(color, value);
                if (canCorrect)
                {
                    ImGui.TableNextColumn();
                    DrawRemoveLootButton(run, entry.Keys);
                }
            }
            ImGui.EndTable();
        }
        if (priced.Count > maximum) ImGui.TextColored(Muted, $"+ {priced.Count - maximum} more rows (History)");
        var unpriced = rows.Where(x => x.Unknown).OrderByDescending(x => Math.Abs(x.Quantity)).ToList();
        if (Settings.ShowUnpriced && unpriced.Count > 0)
        {
            ImGui.TextColored(Muted, "-- No Price --");
            foreach (var entry in unpriced.Take(compact ? 4 : int.MaxValue))
            {
                if (canCorrect) { DrawRemoveLootButton(run, entry.Keys); ImGui.SameLine(); }
                string label = $"{(entry.Quantity >= 0 ? "+" : "-")} {entry.Name}  x{Math.Abs(entry.Quantity)}";
                ImGui.TextColored(LootColor(entry.Quantity, entry.LastPickup), compact ? Fit(label, ImGui.GetContentRegionAvail().X) : label);
                if (ImGui.IsItemHovered()) ImGui.SetTooltip(label + "\nNot included in the chaos total.");
            }
            if (compact && unpriced.Count > 4) ImGui.TextColored(Muted, $"+ {unpriced.Count - 4} unpriced rows (History)");
        }
    }

    private void DrawHistory()
    {
        bool open = Settings.ShowHistory.Value;
        ImGui.SetNextWindowSize(new Vector2(850, 600), ImGuiCond.FirstUseEver);
        if (ImGui.Begin("Map Profit - History", ref open))
        {
            ImGui.TextUnformatted(_character ?? "Waiting for character");
            if (ImGui.Button("Export CSV")) Export(_archivePreview?.Data);
            ImGui.SameLine();
            if (ImGui.Button("New session (archive current)")) NewSession();
            ImGui.SameLine();
            if (ImGui.Button("Restore previous session")) RestorePreviousSession();
            if (ImGui.Button(_showArchives ? "Hide archives" : "Browse archived sessions"))
            {
                _showArchives = !_showArchives;
                if (_showArchives) RefreshArchives();
                else { _archivePreview = null; _selectedRun = null; }
            }
            DrawSessionArchives();
            var displayed = _archivePreview?.Data ?? _tracker.Session;
            ImGui.TextColored(Green, $"{displayed.Runs.Count} maps | {Signed(displayed.ProfitChaos)} c | {SessionRate(displayed)}");
            ImGui.TextDisabled($"Rate: {displayed.RateMapCount} map checkpoints / {Duration(displayed.RateSeconds)}. Updates on map exit.");
            DrawStatisticsComparison(displayed);
            ImGui.Separator();
            if (ImGui.BeginTable("Runs", 5, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY, new Vector2(0, 230)))
            {
                ImGui.TableSetupColumn("Map"); ImGui.TableSetupColumn("Started");
                ImGui.TableSetupColumn("Time"); ImGui.TableSetupColumn("Profit (c)"); ImGui.TableSetupColumn("c/h");
                ImGui.TableSetupScrollFreeze(0, 1); ImGui.TableHeadersRow();
                foreach (var run in displayed.Runs.AsEnumerable().Reverse())
                {
                    ImGui.TableNextRow(); ImGui.TableNextColumn();
                    if (ImGui.Selectable(run.Name + "##" + run.Id, _selectedRun == run.Id, ImGuiSelectableFlags.SpanAllColumns)) _selectedRun = run.Id;
                    ImGui.TableNextColumn(); ImGui.TextUnformatted(run.Started.ToLocalTime().ToString("MM-dd HH:mm"));
                    ImGui.TableNextColumn(); ImGui.TextUnformatted(Duration(run.ActiveSeconds));
                    ImGui.TableNextColumn(); ImGui.TextColored(run.ProfitChaos >= 0 ? Green : Red, Signed(run.ProfitChaos));
                    ImGui.TableNextColumn(); ImGui.TextUnformatted(Money(run.ChaosPerHour));
                }
                ImGui.EndTable();
            }
            var selected = displayed.Runs.Find(r => r.Id == _selectedRun) ?? displayed.Current;
            if (selected != null)
            {
                ImGui.Separator();
                ImGui.TextColored(Gold, selected.Name);
                if (_archivePreview == null) ImGui.TextDisabled("X removes an incorrect row from this map; a backup is saved first.");
                DrawLoot(selected, int.MaxValue, false);
            }
            DrawStatus();
        }
        ImGui.End();
        Settings.ShowHistory.Value = open;
    }

    public override void DrawSettings()
    {
        ImGui.TextColored(Gold, "Map Profit 1.3.0");
        base.DrawSettings();
        ImGui.Separator();
        ImGui.TextWrapped("Tracks picked-up inventory changes. Spending / dropping in a map subtracts value; stash, trade and town changes do not. Prices come from NinjaPrice.GetValue. No Price items are excluded from totals.");
        ImGui.TextWrapped("Session c/h uses map-exit checkpoints, weighted by map time. The current unfinished segment is not extrapolated. First estimate requires at least 2 minutes across checkpointed maps. No preparation time or new entry investment is added.");
        if (ImGui.Button("Open history / full loot")) Settings.ShowHistory.Value = true;
        if (ImGui.Button("Start a new run in this area")) NewRun();
        if (ImGui.Button("New session (archive current)")) NewSession();
        if (ImGui.Button("Restore previous session")) RestorePreviousSession();
        if (ImGui.Button("Export CSV")) Export();
        if (ImGui.Button("Re-read inventory baseline")) { _ledger.Forget(); _gate.Reset(); }
        DrawStatus();
    }

    private void DrawStatus()
    {
        ImGui.TextWrapped("Tracking: " + _status);
        ImGui.TextWrapped("Prices: " + _prices.Status);
        if (_storageError != null) ImGui.TextColored(Red, _storageError);
        if (_notice != null) ImGui.TextWrapped(_notice);
    }
    private static string Money(double value) => value.ToString("0.00", CultureInfo.InvariantCulture);
    private static string SessionRate(ProfitSession session) => session.HasReliableRate ? Money(session.ChaosPerHour) + " c/h" : "-- c/h (collecting maps)";
    private Vector4 LootColor(int quantity, DateTimeOffset lastPickup)
    {
        if (quantity < 0) return Red;
        double age = (DateTimeOffset.UtcNow - lastPickup).TotalSeconds;
        return Settings.HighlightNewLoot && age is >= 0 and < 2
            ? Vector4.Lerp(Gold, Green, (float)(age / 2)) : Green;
    }
    private static string Signed(double value) => (value >= 0 ? "+" : "") + Money(value);
    private static string Duration(double seconds)
    {
        var time = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return time.TotalHours >= 1 ? $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}" : $"{(int)time.TotalMinutes:00}:{time.Seconds:00}";
    }
    private static string Fit(string text, float width)
    {
        if (ImGui.CalcTextSize(text).X <= width) return text;
        int length = text.Length;
        while (length > 0 && ImGui.CalcTextSize(text[..length] + "...").X > width) length--;
        return text[..length] + "...";
    }
}
