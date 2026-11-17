using Hexa.NET.ImGui;
using MapProfit.Core;

namespace MapProfit;

public sealed partial class MapProfitPlugin
{
    private sealed record ArchivedSession(string Path, DateTime ModifiedUtc, ProfitSession Data);
    private readonly List<ArchivedSession> _archives = [];
    private bool _showArchives;
    private ArchivedSession? _archivePreview;
    private string? _archiveMessage;

    private void RefreshArchives()
    {
        _archives.Clear();
        _archiveMessage = null;
        if (_character == null) return;
        try
        {
            string folder = Path.Combine(ConfigDirectory, "Sessions", "Archive");
            if (!Directory.Exists(folder)) return;
            int unreadable = 0;
            foreach (string path in Directory.EnumerateFiles(folder, "*.json"))
            {
                try
                {
                    var data = SessionStore.Load(path);
                    if (data.Character != _character || data.Id == _tracker.Session.Id) continue;
                    foreach (var run in data.Runs) run.CheckpointRate();
                    _archives.Add(new ArchivedSession(path, File.GetLastWriteTimeUtc(path), data));
                }
                catch { unreadable++; }
            }
            _archives.Sort((a, b) => b.ModifiedUtc.CompareTo(a.ModifiedUtc));
            if (unreadable > 0) _archiveMessage = $"Skipped {unreadable} unreadable archive(s); files were preserved.";
        }
        catch (Exception ex) { _archiveMessage = "Could not read archives: " + ex.Message; }
    }

    private void RestorePreviousSession()
    {
        RefreshArchives();
        var previous = _archives.FirstOrDefault();
        if (previous == null) { _notice = "No previous session for this character / league"; return; }
        RestoreSession(previous.Path);
    }

    private void RestoreSession(string path)
    {
        if (_sessionPath == null || _character == null) return;
        try
        {
            var restored = SessionStore.Load(path);
            if (restored.Character != _character || restored.Id == _tracker.Session.Id)
                throw new InvalidDataException("Select a different session for this character and league.");
            foreach (var run in restored.Runs) run.CheckpointRate();
            _tracker.Session.Current?.CheckpointRate();
            string archive = Path.Combine(ConfigDirectory, "Sessions", "Archive", _tracker.Session.Id + ".json");
            SessionStore.Save(archive, _tracker.Session);
            SessionStore.Save(_sessionPath, restored);
            _tracker = new RunTracker(restored);
            _exitSummary = null;
            _areaKey = null;
            _selectedRun = null;
            _archivePreview = null;
            _ledger.Forget(); _gate.Reset(); _prices.Clear(); _timing = false;
            _settleUntil = _clock.ElapsedMilliseconds + Settings.SettleMs.Value;
            _storageError = null;
            _notice = $"Restored session from {restored.Started.ToLocalTime():yyyy-MM-dd HH:mm}. Previous active session archived.";
            RefreshArchives();
        }
        catch (Exception ex) { _storageError = "Restore failed (active session kept): " + ex.Message; }
    }

    private void DrawSessionArchives()
    {
        if (!_showArchives) return;
        ImGui.Separator();
        ImGui.TextUnformatted("Archived sessions for this character / league");
        if (ImGui.SmallButton("Refresh archives")) RefreshArchives();
        ImGui.SameLine();
        if (ImGui.SmallButton("Show active session")) { _archivePreview = null; _selectedRun = null; }
        if (_archives.Count == 0) ImGui.TextDisabled("No archived sessions yet");
        if (ImGui.BeginTable("SessionArchives", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY, new System.Numerics.Vector2(0, 150)))
        {
            ImGui.TableSetupColumn("Started"); ImGui.TableSetupColumn("Maps");
            ImGui.TableSetupColumn("Profit (c)"); ImGui.TableSetupColumn("Map time");
            ImGui.TableSetupScrollFreeze(0, 1); ImGui.TableHeadersRow();
            foreach (var archive in _archives)
            {
                var session = archive.Data;
                ImGui.TableNextRow(); ImGui.TableNextColumn();
                if (ImGui.Selectable(session.Started.ToLocalTime().ToString("yyyy-MM-dd HH:mm") + "##" + session.Id,
                    _archivePreview?.Data.Id == session.Id, ImGuiSelectableFlags.SpanAllColumns))
                {
                    _archivePreview = archive; _selectedRun = null;
                }
                ImGui.TableNextColumn(); ImGui.TextUnformatted(session.Runs.Count.ToString());
                ImGui.TableNextColumn(); ImGui.TextUnformatted(Signed(session.ProfitChaos));
                ImGui.TableNextColumn(); ImGui.TextUnformatted(Duration(session.ActiveSeconds));
            }
            ImGui.EndTable();
        }
        if (_archivePreview != null)
        {
            ImGui.TextColored(Gold, "Archive preview. Current session continues recording.");
            if (ImGui.Button("Restore selected session")) RestoreSession(_archivePreview.Path);
        }
        if (_archiveMessage != null) ImGui.TextWrapped(_archiveMessage);
    }
}
