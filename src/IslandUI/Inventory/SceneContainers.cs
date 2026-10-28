using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace IslandUI
{
    // EVERY CONTAINER IN THE SCENE, without asking the engine for them. A sheet
    // that says where a carried person is (CarriedWhere) has to look in every
    // chest, cage and device, and FindObjectsOfType<InventorySlot> costs 40 ms
    // in a built base - the stutter of the Social page, which asked it once a
    // second for each carried name on it (Forgejo #12). Walking the slots of all
    // two hundred containers costs a tenth of a millisecond; finding them was
    // the whole price. So they are counted in as they start, the one find is
    // spent once to seed the list (a hot reload, or a container that started
    // before the patch), and the destroyed fall out on the next walk.
    [HarmonyPatch]
    internal static class SceneContainers
    {
        private static readonly List<InventorySlot> All = new List<InventorySlot>();
        private static readonly HashSet<InventorySlot> Known = new HashSet<InventorySlot>();
        private static bool _seeded;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(InventorySlot), nameof(InventorySlot.Start))]
        private static void Started(InventorySlot __instance)
        {
            if (__instance != null && Known.Add(__instance))
            {
                All.Add(__instance);
            }
        }

        // the live ones, as FindObjectsOfType would return them: active in the
        // scene, the destroyed dropped from the list as they are met
        internal static List<InventorySlot> Live(List<InventorySlot> into)
        {
            into.Clear();
            if (!_seeded)
            {
                _seeded = true;
                foreach (InventorySlot inv in Object.FindObjectsOfType<InventorySlot>())
                {
                    Started(inv);
                }
            }
            for (int i = All.Count - 1; i >= 0; i--)
            {
                InventorySlot inv = All[i];
                if (inv == null)
                {
                    Known.Remove(inv);
                    All.RemoveAt(i);
                    continue;
                }
                if (inv.gameObject.activeInHierarchy)
                {
                    into.Add(inv);
                }
            }
            into.Reverse();   // in the order they started
            return into;
        }
    }
}
