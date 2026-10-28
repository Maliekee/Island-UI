using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI
{
    // Feature: each recipe button shows how many times it can be crafted from currently
    // reachable materials ("x12"), written into the button's unused "stack" text child
    // (same prefab as inventory slots; vanilla leaves it empty on recipe buttons).
    // Rides on CraftableCheckSlot, so the number refreshes exactly when craftability does —
    // menu open, after crafting, inventory changes, and PlayerMoved-triggered re-scans.
    [HarmonyPatch(typeof(InventoryManager))]
    internal static class CraftCountDisplay
    {
        private static readonly AccessTools.FieldRef<InventoryManager, ManagersScript> MnRef =
            AccessTools.FieldRefAccess<InventoryManager, ManagersScript>("mn");

        // transform.Find + GetComponent per slot per sweep added up (500 buttons,
        // re-swept on every inventory change) — cache the Text refs; fake-null check
        // survives scene reloads, which destroy the cached components
        private static readonly System.Collections.Generic.Dictionary<int, Text> StackTexts =
            new System.Collections.Generic.Dictionary<int, Text>();

        [HarmonyPatch(nameof(InventoryManager.CraftableCheckSlot))]
        [HarmonyPostfix]
        private static void CraftableCheckSlot_Postfix(InventoryManager __instance, int slotID)
        {
            if (!StackTexts.TryGetValue(slotID, out Text text) || text == null)
            {
                Transform stackText = __instance.itemSlot[100 + slotID].transform.Find("stack");
                text = stackText != null ? stackText.GetComponent<Text>() : null;
                StackTexts[slotID] = text;
            }
            if (text == null)
            {
                return;
            }

            ManagersScript mn = MnRef(__instance);
            if (mn == null || mn.gameMN.creative)
            {
                text.text = "";
                return;
            }
            bool bench = __instance.craftPanelID != 0;
            if (bench && __instance.tmpSubInventory == null)
            {
                text.text = "";
                return;
            }

            int crafts = CraftableTimes(__instance, mn.craftMN.craftData[__instance.craftPanelID].craftInfo[slotID], bench);
            text.text = crafts <= 0 ? "" : (crafts > 999 ? "x999+" : $"x{crafts}");
        }

        internal static int CraftableTimes(InventoryManager inv, CraftInfo craftInfo, bool bench)
        {
            if (craftInfo.required.Length == 0)
            {
                return 0;
            }
            int crafts = int.MaxValue;
            for (int i = 0; i < craftInfo.required.Length; i++)
            {
                // a free recipe lists a placeholder "none" x0, which vanilla's check passes
                if (craftInfo.required[i].count <= 0f) continue;
                float available = CraftingPatches.CountAvailable(inv, craftInfo.required[i].itemData.name, bench);
                // Fractional recipe costs consume 1 item per craft (vanilla CraftConsume
                // clamps count up to 1 and yields multiple outputs instead)
                float perCraft = Mathf.Max(1f, craftInfo.required[i].count);
                crafts = Mathf.Min(crafts, (int)(available / perCraft));
            }
            return crafts == int.MaxValue ? 999 : crafts;   // nothing to pay: as creative counts
        }
    }
}
