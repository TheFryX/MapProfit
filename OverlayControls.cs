using System.Numerics;
using Hexa.NET.ImGui;

namespace MapProfit;

public sealed partial class MapProfitPlugin
{
    private Vector2 _dragOrigin;
    private Vector2 _dragMouseOrigin;

    private void DrawOverlayHeader()
    {
        ImGui.PushFont(ImGui.GetFont(), ImGui.GetFontSize() * Settings.FontScale.Value);
        float height = ImGui.GetFontSize() + 4;
        ImGui.SetNextWindowPos(new Vector2(Settings.PanelX.Value, Settings.PanelY.Value), ImGuiCond.Always);
        ImGui.SetNextWindowSize(new Vector2(Settings.PanelWidth.Value, height + 20), ImGuiCond.Always);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(12, 10));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0);
        var flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoMove |
            ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav;
        if (ImGui.Begin("Map Profit controls###MapProfitHeader", flags))
        {
            var draw = ImGui.GetWindowDrawList();
            var titlePos = ImGui.GetCursorScreenPos();
            float titleWidth = Math.Max(40, ImGui.GetContentRegionAvail().X - 88);
            ImGui.InvisibleButton("DragMapProfit", new Vector2(titleWidth, height));
            if (ImGui.IsItemActivated())
            {
                _dragOrigin = new Vector2(Settings.PanelX.Value, Settings.PanelY.Value);
                _dragMouseOrigin = ImGui.GetIO().MousePos;
            }
            if (!Settings.LockPanel && ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
            {
                var target = _dragOrigin + ImGui.GetIO().MousePos - _dragMouseOrigin;
                var screen = ImGui.GetIO().DisplaySize;
                Settings.PanelX.Value = (int)Math.Clamp(target.X, 0, Math.Max(0, screen.X - Settings.PanelWidth.Value));
                Settings.PanelY.Value = (int)Math.Clamp(target.Y, 0, Math.Max(0, screen.Y - height - 20));
            }
            if (ImGui.IsItemDeactivated() && !Settings.LockPanel) SavePanelSettings();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(Settings.LockPanel ? "Panel locked. Click the padlock to unlock."
                    : "Drag the map name to move. Padlock fixes the position.\nSession c/h updates after map exit, using all map checkpoints.");
            draw.AddText(titlePos, ImGui.GetColorU32(Gold), Fit(_tracker.Session.Current?.Name ?? "Map Profit", titleWidth));
            ImGui.SameLine();
            if (ImGui.SmallButton(Settings.Collapsed ? "+" : "-"))
            {
                Settings.Collapsed.Value = !Settings.Collapsed.Value;
                SavePanelSettings();
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(Settings.Collapsed ? "Expand loot and statistics" : "Collapse to map, total and c/h");
            ImGui.SameLine();
            var lockPos = ImGui.GetCursorScreenPos();
            if (ImGui.InvisibleButton("MapProfitPadlock", new Vector2(22, height)))
            {
                Settings.LockPanel.Value = !Settings.LockPanel.Value;
                SavePanelSettings();
            }
            bool locked = Settings.LockPanel.Value;
            uint color = ImGui.GetColorU32(ImGui.IsItemHovered() ? Gold : locked ? Muted : Green);
            float top = lockPos.Y + Math.Max(0, (height - 17) / 2);
            draw.AddRectFilled(new Vector2(lockPos.X + 4, top + 8), new Vector2(lockPos.X + 18, top + 18), color, 2);
            draw.AddLine(new Vector2(lockPos.X + 7, top + 8), new Vector2(lockPos.X + 7, top + 2), color, 2);
            draw.AddLine(new Vector2(lockPos.X + 7, top + 2), new Vector2(lockPos.X + 15, top + 2), color, 2);
            draw.AddLine(new Vector2(lockPos.X + 15, top + 2), new Vector2(lockPos.X + 15, top + (locked ? 8 : 4)), color, 2);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(locked ? "Unlock panel" : "Lock panel (body stays click-through)");
            ImGui.SameLine();
            if (ImGui.SmallButton("...")) ImGui.OpenPopup("MapProfitPanelOptions");
            if (ImGui.BeginPopup("MapProfitPanelOptions"))
            {
                float opacity = Settings.Opacity.Value;
                if (ImGui.SliderFloat("Background opacity", ref opacity, 0, 1, "%.2f")) Settings.Opacity.Value = opacity;
                if (ImGui.IsItemDeactivatedAfterEdit()) SavePanelSettings();
                ImGui.TextDisabled("0 = invisible background; text stays readable");
                bool highlight = Settings.HighlightNewLoot.Value;
                if (ImGui.Checkbox("Highlight new loot", ref highlight)) { Settings.HighlightNewLoot.Value = highlight; SavePanelSettings(); }
                DrawStatisticsOptions();
                if (ImGui.Button("History / restore sessions")) { Settings.ShowHistory.Value = true; ImGui.CloseCurrentPopup(); }
                if (ImGui.Button(Settings.Pause ? "Resume recording" : "Pause recording")) Settings.Pause.Value = !Settings.Pause.Value;
                ImGui.EndPopup();
            }
        }
        ImGui.End();
        ImGui.PopStyleVar(2);
        ImGui.PopFont();
    }

    private void SavePanelSettings()
    {
        try { _SaveSettings(); }
        catch (Exception ex) { _notice = "Panel setting save failed: " + ex.Message; }
    }
}
