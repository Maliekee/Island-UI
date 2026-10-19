using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI
{
    // Feature: the crafting hover tooltip's material list shows current availability
    // next to each requirement ("8/5") and greys out materials you lack. Availability
    // uses the same reachable-source counting as craftability/count display
    // (CraftingPatches.CountAvailable: Shared Bag + bag + chests, bench slots at benches),
    // so the tooltip can never disagree with the button state.
    [HarmonyPatch(typeof(UIManager))]
    internal static class TooltipAvailability
    {
        private static readonly AccessTools.FieldRef<UIManager, ManagersScript> MnRef =
            AccessTools.FieldRefAccess<UIManager, ManagersScript>("mn");

        private static readonly Color InsufficientText = new Color(0.6f, 0.6f, 0.6f, 1f);
        private static readonly Color InsufficientIcon = new Color(1f, 1f, 1f, 0.4f);

        [HarmonyPatch(nameof(UIManager.ItemDescOpen))]
        [HarmonyPostfix]
        private static void ItemDescOpen_Postfix(UIManager __instance, int slotID)
        {
            // craft-recipe tooltips only (shop range >= 1000 keeps vanilla trade display)
            if (!Settings.TooltipAvailability.Value || slotID < 100 || slotID >= 1000)
            {
                return;
            }
            ManagersScript mn = MnRef(__instance);
            if (mn == null)
            {
                return;
            }
            InventoryManager inv = mn.inventory;
            CraftInfo[] recipes = mn.craftMN.craftData[inv.craftPanelID].craftInfo;
            if (slotID - 100 >= recipes.Length)
            {
                return;
            }
            CraftInfo craftInfo = recipes[slotID - 100];
            bool bench = inv.craftPanelID != 0 && inv.tmpSubInventory != null;

            for (int j = 0; j < craftInfo.required.Length && j < inv.needSlot.Length; j++)
            {
                string key = craftInfo.required[j].itemData.name;
                // fractional recipe costs consume 1 per craft (displayed as 1 by vanilla)
                float need = Mathf.Max(1f, craftInfo.required[j].count);
                float have = CraftingPatches.CountAvailable(inv, key, bench);
                bool enough = have >= need;

                ItemSlot row = inv.needSlot[j];
                row.transform.Find("stack").GetComponent<Text>().text = $"{(int)have}/{(int)need}";
                // widgets are reused between tooltips — always set colors both ways
                row.transform.Find("Text").GetComponent<TextMeshProUGUI>().color =
                    enough ? Color.white : InsufficientText;
                row.transform.Find("itemImage").GetComponent<Image>().color =
                    enough ? Color.white : InsufficientIcon;
            }
        }
    }
}
