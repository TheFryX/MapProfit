using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Enums;
using MapProfit.Core;

namespace MapProfit;

internal sealed class InventoryReader(GameController controller, NinjaPriceBridge prices)
{
    private static readonly HashSet<InventorySlotE> OwnedSlots =
    [
        InventorySlotE.MainInventory1, InventorySlotE.ExpandedMainInventory1,
        InventorySlotE.Cursor1, InventorySlotE.BodyArmour1, InventorySlotE.Weapon1,
        InventorySlotE.Weapon2, InventorySlotE.Offhand1, InventorySlotE.Offhand2,
        InventorySlotE.Helm1, InventorySlotE.Amulet1, InventorySlotE.Ring1,
        InventorySlotE.Ring2, InventorySlotE.Ring3, InventorySlotE.Gloves1,
        InventorySlotE.Boots1, InventorySlotE.Belt1, InventorySlotE.Flask1,
        InventorySlotE.PassiveJewels1, InventorySlotE.Trinket1
    ];

    public bool TryRead(long now, out Dictionary<string, ItemSnapshot> result, out string reason)
    {
        result = [];
        reason = "Waiting for inventory";
        try
        {
            var holders = controller.IngameState.ServerData.PlayerInventories;
            if (holders == null || holders.Count == 0) return false;
            bool mainFound = false;
            var seen = new HashSet<long>();
            var inventories = new HashSet<long>();
            foreach (var holder in holders)
            {
                var inv = holder.Inventory;
                if (inv == null || inv.Address == 0 || !OwnedSlots.Contains(inv.InventSlot)) continue;
                if (!inventories.Add(inv.Address)) continue;
                if (inv.InventSlot == InventorySlotE.MainInventory1)
                {
                    if (inv.Columns <= 0 || inv.Rows <= 0) return false;
                    mainFound = true;
                }
                var countBefore = inv.ItemCount;
                var items = inv.Items;
                if (items == null || countBefore < 0 || countBefore != items.Count) return false;
                bool equipped = inv.InventSlot is not (InventorySlotE.MainInventory1 or
                    InventorySlotE.ExpandedMainInventory1 or InventorySlotE.Cursor1);
                foreach (var item in items)
                    if (!ReadItem(item, now, result, seen, equipped)) return false;
                if (inv.ItemCount != countBefore) return false;
            }
            reason = mainFound ? "Inventory ready" : "Main inventory unavailable";
            return mainFound;
        }
        catch (Exception ex) { reason = "Inventory read pending: " + ex.GetType().Name; return false; }
    }

    private bool ReadItem(Entity? entity, long now, Dictionary<string, ItemSnapshot> result, HashSet<long> seen, bool equipped)
    {
        if (entity == null || entity.Address == 0 || !entity.IsValid || string.IsNullOrEmpty(entity.Path)) return false;
        if (!seen.Add(entity.Address))
        {
            string duplicateKey = "inventory:" + entity.Address + ":" + entity.Path;
            if (equipped && result.TryGetValue(duplicateKey, out var duplicate))
                result[duplicateKey] = duplicate with { IsEquipped = true };
            return true;
        }
        var stack = entity.GetComponent<Stack>();
        int quantity = stack?.Size ?? 1;
        if (quantity < 1 || quantity > 10000000) return false;
        var baseType = controller.Files.BaseItemTypes.Translate(entity.Path);
        string name = baseType?.BaseName ?? entity.GetComponent<Base>()?.Name ?? entity.Path.Split('/').Last();
        var mods = entity.GetComponent<Mods>();
        if (mods?.ItemRarity == ItemRarity.Unique)
            name = string.IsNullOrWhiteSpace(mods.UniqueName) ? name + " (unique)" : mods.UniqueName;
        int tier = entity.GetComponent<MapKey>()?.Tier ?? 0;
        if (tier > 0) name += $" (T{tier})";
        var gem = entity.GetComponent<SkillGem>();
        if (gem != null)
            name += $" (L{gem.Level}/Q{entity.GetComponent<Quality>()?.ItemQuality ?? 0})";
        if (entity.GetComponent<Base>()?.isCorrupted == true) name += " [corrupted]";

        bool fungible = stack?.Info?.MaxStackSize > 1;
        string key = fungible ? "stack:" + entity.Path + ":" + name
            : "inventory:" + entity.Address + ":" + entity.Path;
        double? unit = prices.GetUnitPrice(entity, key + ":" + name, quantity, now);
        if (result.TryGetValue(key, out var current))
        {
            if (!fungible) return false;
            result[key] = current with { Quantity = checked(current.Quantity + quantity), UnitChaos = unit ?? current.UnitChaos,
                IsEquipped = current.IsEquipped || equipped };
        }
        else result[key] = new ItemSnapshot(key, name, quantity, unit, equipped);

        var sockets = entity.GetComponent<Sockets>();
        if (sockets != null)
            foreach (var socketed in sockets.SocketedGems)
                if (socketed.GemEntity != null && !ReadItem(socketed.GemEntity, now, result, seen, equipped)) return false;
        return true;
    }
}
