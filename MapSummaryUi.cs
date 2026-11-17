using System.Numerics;
using Hexa.NET.ImGui;
using MapProfit.Core;

namespace MapProfit;

public sealed partial class MapProfitPlugin
{
    private MapExitSummary? _exitSummary;
    private long _exitSummaryExpires;
    private Vector2 _mainPanelPosition;
    private Vector2 _mainPanelSize;

    private void QueueExitSummary(MapRun run)
    {
        if (!Settings.ShowExitSummary || run.ActiveSeconds <= 0) return;
        _exitSummary = MapExitSummary.From(run);
        _exitSummaryExpires = _clock.ElapsedMilliseconds + Settings.ExitSummarySeconds.Value * 1000L;
    }

    private void DrawExitSummary()
    {
        if (!Settings.ShowExitSummary || _clock.ElapsedMilliseconds >= _exitSummaryExpires)
        {
            _exitSummary = null;
            return;
        }
        if (_exitSummary == null) return;
        var summary = _exitSummary;
        float fade = Math.Clamp((_exitSummaryExpires - _clock.ElapsedMilliseconds) / 1000f, 0, 1);
        ImGui.PushFont(ImGui.GetFont(), ImGui.GetFontSize() * Math.Min(Settings.FontScale.Value, 1f));
        var screen = ImGui.GetIO().DisplaySize;
        float width = Math.Max(100, Math.Min(250, screen.X - 16));
        float height = 20 + (3 + Math.Max(1, summary.TopLoot.Length)) * ImGui.GetTextLineHeightWithSpacing();
        const float gap = 8;
        float x = _mainPanelPosition.X + _mainPanelSize.X + gap;
        float y = _mainPanelPosition.Y;
        if (x + width > screen.X - gap)
        {
            x = _mainPanelPosition.X - width - gap;
            if (x < gap)
            {
                x = _mainPanelPosition.X;
                y += _mainPanelSize.Y + gap;
            }
        }
        x = Math.Clamp(x, 0, Math.Max(0, screen.X - width));
        y = Math.Clamp(y, 0, Math.Max(0, screen.Y - height));
        ImGui.SetNextWindowPos(new Vector2(x, y), ImGuiCond.Always);
        ImGui.SetNextWindowSize(new Vector2(width, height), ImGuiCond.Always);
        ImGui.SetNextWindowBgAlpha(Math.Clamp(Settings.Opacity.Value, 0.45f, 0.95f));
        ImGui.PushStyleVar(ImGuiStyleVar.Alpha, fade);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(8, 8));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 5f);
        ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0.055f, 0.065f, 0.08f, 1));
        var flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoMove |
            ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav;
        if (ImGui.Begin("Map Profit exit summary###MapProfitExitSummary", flags))
        {
            ImGui.TextColored(Gold, Fit(summary.Name, ImGui.GetContentRegionAvail().X));
            ImGui.TextColored(summary.Profit >= 0 ? Green : Red, Signed(summary.Profit) + " c  |  " + Duration(summary.Seconds));
            ImGui.Separator();
            if (summary.TopLoot.Length == 0) ImGui.TextColored(Muted, "No priced loot");
            else if (ImGui.BeginTable("ExitTopLoot", 2, ImGuiTableFlags.SizingStretchProp))
            {
                ImGui.TableSetupColumn("Item", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("Chaos", ImGuiTableColumnFlags.WidthFixed, 58);
                foreach (var item in summary.TopLoot)
                {
                    ImGui.TableNextRow(); ImGui.TableNextColumn();
                    ImGui.TextUnformatted(Fit(item.Name + " x" + item.Quantity, ImGui.GetContentRegionAvail().X));
                    ImGui.TableNextColumn();
                    string value = Money(item.Chaos);
                    ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Math.Max(0, ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize(value).X));
                    ImGui.TextColored(Green, value);
                }
                ImGui.EndTable();
            }
        }
        ImGui.End();
        ImGui.PopStyleColor();
        ImGui.PopStyleVar(3);
        ImGui.PopFont();
    }
}
