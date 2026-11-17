using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using ExileCore;
using ExileCore.PoEMemory.Components;
using MapProfit.Core;

namespace MapProfit;

public sealed partial class MapProfitPlugin : BaseSettingsPlugin<MapProfitSettings>
{
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly InventoryLedger _ledger = new();
    private readonly SnapshotGate _gate = new();
    private RunTracker _tracker = new(new ProfitSession());
    private NinjaPriceBridge _prices = null!;
    private InventoryReader _inventory = null!;
    private long _nextPoll, _settleUntil, _lastTick, _nextSave;
    private string? _areaKey, _character, _sessionPath;
    private string _gameLifetime = "";
    private int _processId;
    private string _status = "Waiting for game";
    private string? _storageError;
    private string? _notice;
    private bool _wasPaused, _timing, _wasSuppressed;
    private Guid? _selectedRun;

    public override bool Initialise()
    {
        Name = "Map Profit";
        CanUseMultiThreading = false;
        _prices = new NinjaPriceBridge(GameController);
        _inventory = new InventoryReader(GameController, _prices);
        if (Settings.SettingsRevision < 2)
        {
            if (Settings.PollMs.Value == 150) Settings.PollMs.Value = 100;
            Settings.SettingsRevision = 2;
            SavePanelSettings();
        }
        return true;
    }

    public override void AreaChange(AreaInstance area)
    {
        _timing = false;
        _ledger.Forget();
        _gate.Reset();
        _prices?.Clear();
        _settleUntil = _clock.ElapsedMilliseconds + Settings.SettleMs.Value;
        _nextPoll = 0;
    }

    public override Job Tick()
    {
        long now = _clock.ElapsedMilliseconds;
        double elapsed = _lastTick == 0 ? 0 : (now - _lastTick) / 1000.0;
        _lastTick = now;
        if (!Settings.Enable || !GameController.InGame || GameController.IsLoading || GameController.Player == null)
        {
            _timing = false;
            _ledger.Forget();
            _gate.Reset();
            _status = "Waiting for game / loading";
            return null!;
        }
        try
        {
            var area = GameController.Area.CurrentArea;
            if (area?.Area == null) return null!;
            var process = GameController.Window.Process;
            if (_processId != process.Id)
            {
                _exitSummary = null;
                _processId = process.Id;
                _gameLifetime = process.Id + ":" + process.StartTime.ToUniversalTime().Ticks;
                _areaKey = null;
                _tracker = new RunTracker(_tracker.Session);
            }
            var playerName = GameController.Player.GetComponent<Player>()?.PlayerName;
            var league = GameController.IngameState.ServerData.League;
            if (string.IsNullOrWhiteSpace(playerName) || string.IsNullOrWhiteSpace(league)) return null!;
            string character = league + " / " + playerName;
            if (_character != character && !LoadCharacter(character)) return null!;
            string key = _gameLifetime + ":" + area.Area.Id + ":" + area.Hash;
            bool peaceful = area.IsTown || area.IsHideout || area.IsPeaceful;
            if (_areaKey != key)
            {
                var previousRun = _tracker.Session.Current;
                bool wasInMap = _tracker.InCombatArea;
                _areaKey = key;
                _tracker.Enter(key, area.DisplayName, peaceful, Settings.GroupConnectedAreas.Value, 0);
                if (wasInMap && previousRun != null && (peaceful || _tracker.Session.Current?.Id != previousRun.Id))
                    QueueExitSummary(previousRun);
                AreaChange(area);
                SaveSession();
            }
            bool paused = Settings.Pause.Value;
            if (paused != _wasPaused)
            {
                _ledger.Forget(); _gate.Reset(); _timing = false; _wasPaused = paused;
            }
            if (_timing && !paused && !peaceful && elapsed <= 2)
                _tracker.AddTime(elapsed);
            _timing = !paused && !peaceful;
            if (now >= _nextSave) { SaveSession(); _nextSave = now + 10000; }
            _prices.Pump(now);
            if (now < _nextPoll || now < _settleUntil) return null!;
            _nextPoll = now + Settings.PollMs.Value;
            bool suppress = paused || peaceful || HasTransferPanel();
            if (suppress != _wasSuppressed)
            {
                _ledger.Forget(); _gate.Reset(); _wasSuppressed = suppress;
            }
            if (!_inventory.TryRead(now, out var snapshot, out var reason))
            {
                _gate.Reset(); _status = reason; return null!;
            }
            if (!_ledger.HasBaseline && !_gate.Accept(snapshot))
            {
                _status = "Validating initial inventory"; return null!;
            }
            if (suppress || _tracker.Session.Current == null)
            {
                _ledger.Rebase(snapshot);
                _status = paused ? "Paused" : peaceful ? "In town - map suspended" : "Stash / trade - not recording";
            }
            else
            {
                bool hadBaseline = _ledger.HasBaseline;
                _ledger.Apply(snapshot, _tracker.Session.Current);
                _status = hadBaseline ? "Recording pickups and spending" : "Inventory baseline ready";
            }
        }
        catch (Exception ex)
        {
            _gate.Reset();
            _status = "Waiting for a valid read: " + ex.GetType().Name;
        }
        return null!;
    }

    private bool HasTransferPanel()
    {
        var ui = GameController.IngameState.IngameUi;
        return ui.StashElement?.IsVisible == true || ui.GuildStashElement?.IsVisible == true ||
            ui.TradeWindow?.IsVisible == true || ui.SellWindow?.IsVisible == true ||
            ui.PurchaseWindow?.IsVisible == true;
    }

    private bool LoadCharacter(string character)
    {
        if (!SaveSession()) return false;
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(character)))[..16];
        string sessionPath = Path.Combine(ConfigDirectory, "Sessions", hash + ".json");
        var session = new ProfitSession { Character = character };
        _storageError = null;
        if (File.Exists(sessionPath))
        {
            try { session = SessionStore.Load(sessionPath); }
            catch (Exception ex)
            {
                string corrupt = sessionPath + ".unreadable-" + DateTime.UtcNow.Ticks;
                try { File.Copy(sessionPath, corrupt); }
                catch (Exception io) { _storageError = "Could not preserve unreadable history: " + io.Message; return false; }
                if (File.Exists(sessionPath + ".bak"))
                {
                    try { session = SessionStore.Load(sessionPath + ".bak"); _notice = "Recovered session from backup"; }
                    catch { _notice = "Invalid history preserved; started a new session"; }
                }
                else _notice = "Invalid history preserved; started a new session";
                LogError("Map Profit preserved unreadable history: " + ex.Message);
            }
        }
        _character = character;
        _exitSummary = null;
        _sessionPath = sessionPath;
        _tracker = new RunTracker(session);
        _areaKey = null;
        _selectedRun = null;
        _archivePreview = null;
        _archives.Clear();
        _showArchives = false;
        _ledger.Forget(); _gate.Reset();
        return true;
    }

    private bool SaveSession()
    {
        if (_sessionPath == null) return true;
        try { SessionStore.Save(_sessionPath, _tracker.Session); _storageError = null; return true; }
        catch (Exception ex) { _storageError = "Save failed: " + ex.Message; return false; }
    }

    private void NewSession()
    {
        if (_sessionPath == null) return;
        try
        {
            _tracker.Session.Current?.CheckpointRate();
            string archive = Path.Combine(ConfigDirectory, "Sessions", "Archive", _tracker.Session.Id + ".json");
            SessionStore.Save(archive, _tracker.Session);
            var fresh = new ProfitSession { Character = _character! };
            SessionStore.Save(_sessionPath, fresh);
            _tracker = new RunTracker(fresh);
            _exitSummary = null;
            _areaKey = null;
            _selectedRun = null;
            _archivePreview = null;
            _ledger.Forget(); _gate.Reset(); _timing = false;
            RefreshArchives();
            _notice = "New session started; previous session archived";
        }
        catch (Exception ex) { _storageError = "Session reset failed: " + ex.Message; }
    }

    private void NewRun()
    {
        if (_areaKey == null || !_tracker.InCombatArea) return;
        var area = GameController.Area.CurrentArea;
        _tracker.Enter(_areaKey, area.DisplayName, false, false, 0, true);
        _ledger.Forget(); _gate.Reset();
        SaveSession();
    }

    private void Export(ProfitSession? session = null)
    {
        try
        {
            string path = Path.Combine(ConfigDirectory, "Exports", "MapProfit-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".csv");
            SessionStore.ExportCsv(path, session ?? _tracker.Session);
            _notice = "Exported: " + path;
        }
        catch (Exception ex) { _notice = "Export failed: " + ex.Message; }
    }

    public override void OnClose() => SaveSession();
    public override void OnUnload() => SaveSession();
    public override void OnPluginDestroyForHotReload() => SaveSession();
}
