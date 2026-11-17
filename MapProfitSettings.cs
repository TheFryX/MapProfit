using ExileCore.Shared.Attributes;
using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;

namespace MapProfit;

public sealed class MapProfitSettings : ISettings
{
    public int SettingsRevision { get; set; }
    public int RecentMapCount { get; set; } = 10;
    public ToggleNode Enable { get; set; } = new(true);
    [Menu("Show overlay")] public ToggleNode ShowOverlay { get; set; } = new(true);
    [Menu("Collapsed panel")] public ToggleNode Collapsed { get; set; } = new(false);
    [Menu("Show averages per map")] public ToggleNode ShowAverages { get; set; } = new(true);
    [Menu("Show recent maps comparison")] public ToggleNode ShowRecentMaps { get; set; } = new(true);
    [Menu("Show small summary after map exit")] public ToggleNode ShowExitSummary { get; set; } = new(true);
    [Menu("Map summary duration (seconds)")] public RangeNode<int> ExitSummarySeconds { get; set; } = new(8, 3, 30);
    [Menu("Pause recording", "Rebases inventory while paused; resuming never counts old items.")]
    public ToggleNode Pause { get; set; } = new(false);
    [Menu("Lock panel (click through body)")] public ToggleNode LockPanel { get; set; } = new(true);
    [Menu("Highlight new loot")] public ToggleNode HighlightNewLoot { get; set; } = new(true);
    [Menu("Panel X")] public RangeNode<int> PanelX { get; set; } = new(30, 0, 7680);
    [Menu("Panel Y")] public RangeNode<int> PanelY { get; set; } = new(230, 0, 4320);
    [Menu("Panel width")] public RangeNode<int> PanelWidth { get; set; } = new(320, 250, 800);
    [Menu("Font scale")] public RangeNode<float> FontScale { get; set; } = new(1f, 0.7f, 2f);
    [Menu("Background opacity (0 = invisible)")] public RangeNode<float> Opacity { get; set; } = new(0.65f, 0f, 1f);
    [Menu("Maximum priced rows")] public RangeNode<int> MaxRows { get; set; } = new(12, 3, 40);
    [Menu("Show items without price")] public ToggleNode ShowUnpriced { get; set; } = new(true);
    [Menu("Show session summary")] public ToggleNode ShowSession { get; set; } = new(true);
    [Menu("Show panel in hideout / town")] public ToggleNode ShowInTown { get; set; } = new(true);
    [Menu("Hide panel behind large game panels")] public ToggleNode HideWithPanels { get; set; } = new(false);
    [Menu("Group connected combat zones", "Direct transitions (boss arenas, Abyss, Harvest) belong to the current run. A new instance entered from town starts a new run.")]
    public ToggleNode GroupConnectedAreas { get; set; } = new(true);
    [Menu("Inventory poll interval (ms)")] public RangeNode<int> PollMs { get; set; } = new(100, 50, 1000);
    [Menu("Area loading settle time (ms)")] public RangeNode<int> SettleMs { get; set; } = new(1000, 300, 5000);
    [Menu("History / full loot")] public ToggleNode ShowHistory { get; set; } = new(false);
}
