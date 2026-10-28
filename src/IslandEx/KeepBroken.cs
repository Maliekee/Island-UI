using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace IslandEx
{
    // A worn-out tool stays (Forgejo #164): at 0 durability it is broken, not gone,
    // so a repair kit can bring it back.
    //
    // Vanilla wears the held tool and the shield through one door,
    // PlayerMove.DurabilityChange (equip 1 on a swing or a dissection, equip 6 by
    // the damage a guard takes). When itemLife reaches 0 it shows the "broke"
    // caution, unequips and CLEARS the slot - the item is gone. The repair kit
    // (ItemManager.RepairItem) sets any tool below its full life back to full, a
    // tool at 0 included, so keeping the item is all a repair needs.
    //
    // Kept, the item cannot be used: at 0 it comes off the body the way
    // RandomCharacter.EquipItem takes an item off (UnEquip, into the first free bag
    // slot, the equip slot cleared, the stats recounted) - into the Shared Bag, or at
    // your feet when the bag is full - and EquipItem refuses it until it is mended.
    // Every equip goes through EquipItem: the vanilla click and drag, Island UI's
    // inventory, tool belt and action bar, an NPC's menu. Off, both patches step
    // aside and a broken tool equipped then breaks for good on its first use, as
    // vanilla would.
    [HarmonyPatch]
    internal static class KeepBroken
    {
        private static ConfigEntry<bool> _enabled;
        private static ManagersScript _mn;

        internal static void Bind(ConfigFile cfg)
        {
            _enabled = cfg.Bind("Tools", "Keep Broken Tools", false,
                "A tool, weapon or shield worn to 0 durability stays as a broken item that cannot be equipped until a repair kit mends it. Off = it vanishes, as in the unmodded game. Applies live.");
        }

        private static bool On
        {
            get { return _enabled != null && _enabled.Value && Plugin.Enabled.Value; }
        }

        // Found by name, as the game does: PlayerMove's own mn is a property, which
        // Harmony cannot inject as a mn field.
        private static ManagersScript Mn()
        {
            if (_mn == null)
            {
                GameObject managers = GameObject.Find("Managers");
                _mn = managers != null ? managers.GetComponent<ManagersScript>() : null;
            }
            return _mn;
        }

        // The tool types that wear and that a repair kit mends: RepairItem's list.
        private static bool Wears(ItemData.ToolType type)
        {
            switch (type)
            {
                case ItemData.ToolType.Axe:
                case ItemData.ToolType.Pickax:
                case ItemData.ToolType.FishingRod:
                case ItemData.ToolType.Shovel:
                case ItemData.ToolType.Bow:
                case ItemData.ToolType.Shield:
                case ItemData.ToolType.Pickax2:
                case ItemData.ToolType.Stun:
                case ItemData.ToolType.Knife:
                case ItemData.ToolType.Weapon:
                case ItemData.ToolType.Net:
                case ItemData.ToolType.Axe2:
                case ItemData.ToolType.Picknaxe:
                case ItemData.ToolType.Throwing:
                    return true;
                default:
                    return false;
            }
        }

        // Broken: a wearing tool at 0 whose kind has a life to wear (a kind at 0 in
        // the item table never wore down, so its 0 means nothing).
        private static bool Broken(ManagersScript mn, ItemSlot slot, out ItemData data)
        {
            data = null;
            if (slot == null || string.IsNullOrEmpty(slot.itemKey) || slot.itemLife > 0f || !Wears(slot.toolType))
            {
                return false;
            }
            data = mn.itemMN.FindItem(slot.itemKey);
            return data != null && data.itemLife > 0f;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlayerMove), nameof(PlayerMove.DurabilityChange))]
        private static bool Wear(PlayerMove __instance, int equipID, int value)
        {
            ManagersScript mn = On ? Mn() : null;
            CommonStates common = __instance.common;
            if (!On || mn == null || common == null || common.equip == null
                || equipID < 0 || equipID >= common.equip.Length)
            {
                return true;
            }
            ItemSlot eq = common.equip[equipID];
            if (eq == null || string.IsNullOrEmpty(eq.itemKey) || !Wears(eq.toolType) || eq.itemLife + value > 0f)
            {
                return true;
            }
            ItemData data = mn.itemMN.FindItem(eq.itemKey);
            if (data == null || data.itemLife <= 0f)
            {
                return true;
            }
            InventoryManager inv = mn.inventory;
            eq.itemLife = 0f;
            int free = inv.EmptySlotCheck();
            mn.eventMN.StartCoroutine(mn.eventMN.GoCautionSt(free >= 0
                ? Shared.Loc.Get("keep_broken.broke", "{0} is broken. It waits in your bag for a repair kit.", data.GetItemName())
                : Shared.Loc.Get("keep_broken.broke_dropped", "{0} is broken. No room in your bag, so it lies at your feet.", data.GetItemName())));
            mn.randChar.UnEquip(common, eq);
            inv.SlotToSlot(eq, free >= 0 ? inv.itemSlot[free] : inv.MakeDropItem());
            inv.SlotClear(eq);
            if (mn.gameMN.menuNPC == null)
            {
                inv.SlotClear(inv.itemSlot[40 + equipID]);
            }
            if (free >= 0)
            {
                inv.StackTextChange(free);
            }
            mn.randChar.EquipEffectCheck(common, equipID);
            mn.uiMN.StatusChange(common);
            __instance.target = null;
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(RandomCharacter), nameof(RandomCharacter.EquipItem))]
        private static bool Refuse(ItemSlot slot)
        {
            ManagersScript mn = On ? Mn() : null;
            // taking a worn item OFF is the same call on an equip slot: always allowed
            if (!On || mn == null || slot == null || mn.inventory.IsEquipSlot(slot.slotID))
            {
                return true;
            }
            ItemData data;
            if (!Broken(mn, slot, out data))
            {
                return true;
            }
            mn.eventMN.StartCoroutine(mn.eventMN.GoCautionSt(
                Shared.Loc.Get("keep_broken.refused", "{0} is broken. A repair kit mends it.", data.GetItemName())));
            return false;
        }
    }
}
