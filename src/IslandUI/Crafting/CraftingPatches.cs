using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI
{
    // Feature: crafting material sourcing — fixes both directions of the vanilla asymmetry:
    // - bench crafting (craftPanelID != 0) can also use the player's inventory
    //   (order: bench -> nearby chests -> player; pockets last, so behavior is
    //   unchanged whenever stored materials suffice)
    // - innate crafting (craftPanelID == 0) can also use nearby Material chests,
    //   scanned around the player since there is no bench
    //   (order: player -> chests; vanilla's source drains first)
    [HarmonyPatch(typeof(InventoryManager))]
    internal static class CraftingPatches
    {
        // InventoryManager.mn is private; everything else we need is public
        private static readonly AccessTools.FieldRef<InventoryManager, ManagersScript> MnRef =
            AccessTools.FieldRefAccess<InventoryManager, ManagersScript>("mn");

        private static bool UseBag => Settings.UseCharacterBag.Value;

        // The active character's bag: the per-character inventory, distinct from the
        // Shared Bag (itemSlot[0..baseCount]) that vanilla crafting counts.
        // IMPORTANT: while the VANILLA container UI owns a container, the game MOVES
        // its contents into the on-screen mirror slots (itemSlot[50+], see
        // SubInventoryLoad/UnLoad) and the real slots are empty — so when that
        // container IS the player's bag, the bag source resolves to the mirror slots
        // instead. `mirror` tells consumers to refresh the on-screen stack texts after
        // consuming. WI-4 intercepts those loads for plain storage (the bag included),
        // which is why the test is MirrorsLive and not the panel's activeSelf: an
        // intercepted panel is active but empty, and the real slots stay authoritative.
        internal static List<ItemSlot> PlayerBagSlots(InventoryManager inv, out bool mirror)
        {
            mirror = false;
            if (!UseBag)
            {
                return null;
            }
            ManagersScript mn = MnRef(inv);
            if (mn == null || mn.gameMN == null || mn.gameMN.pMove == null)
            {
                return null;
            }
            CommonStates common = mn.gameMN.pMove.common;
            if (common == null || common.bag == null)
            {
                return null;
            }
            var result = new List<ItemSlot>();
            if (VanillaContainerUI.MirrorsLive(inv) && inv.tmpSubInventory == common.bag)
            {
                mirror = true;
                for (int j = 0; j < common.bag.size && 50 + j < inv.itemSlot.Length; j++)
                {
                    result.Add(inv.itemSlot[50 + j]);
                }
            }
            else if (common.bag.slots != null)
            {
                result.AddRange(common.bag.slots);
            }
            return result;
        }

        // ---------- Scan side: give the innate menu a chest scan, centered on the player ----------

        // ---------- sweep coalescing ----------
        // Vanilla runs a FULL 500-recipe CraftableCheck sweep from StackTextChange —
        // i.e. once per slot that changes. Opening a large container (spider closet:
        // ~50-100 slots via SlotToSlot) fires 50-100 full sweeps in one frame,
        // measured at 130-190ms — the closet stutter, present in the unmodded game.
        // Only the LAST sweep's result can matter, so calls just mark a dirty flag
        // and ONE real sweep runs in LateUpdate (same frame, before rendering).
        // Bonus: the sweep is dropped entirely while the innate craft UI is hidden —
        // our CraftOpen replacement re-checks every shown button on open anyway.
        private static InventoryManager _sweepInv;
        private static bool _sweepDirty;
        private static bool _sweepRefFind;
        private static bool _sweepExecuting;

        // Also gives the innate menu its missing chest-scan trigger (vanilla only
        // scans for a bench menu), and opens the availability-snapshot window.
        [HarmonyPatch(nameof(InventoryManager.CraftableCheck))]
        [HarmonyPrefix]
        private static bool CraftableCheck_Prefix(InventoryManager __instance, bool refFind)
        {
            if (!_sweepExecuting && Settings.CoalesceCraftSweeps.Value)
            {
                _sweepInv = __instance;
                _sweepDirty = true;
                _sweepRefFind |= refFind;
                return false;
            }
            if (refFind && __instance.craftPanelID == 0)
            {
                __instance.FindCraftReference();
            }
            BeginSweepWindow();
            return true;
        }

        [HarmonyPatch(nameof(InventoryManager.CraftableCheck))]
        [HarmonyPostfix]
        private static void CraftableCheck_Postfix()
        {
            EndSweepWindow();
        }

        // called from Plugin.LateUpdate — one deferred sweep per frame at most
        internal static void RunCoalescedSweep()
        {
            if (!_sweepDirty)
            {
                return;
            }
            _sweepDirty = false;
            InventoryManager inv = _sweepInv;
            bool refFind = _sweepRefFind;
            _sweepInv = null;
            _sweepRefFind = false;
            if (inv == null) // scene reload took the instance
            {
                return;
            }
            ManagersScript mn = MnRef(inv);
            // custom view active: its bound cells are the only visible craftability
            // UI — refresh those instead of sweeping 500 hidden vanilla buttons
            if (inv.craftPanelID == 0 && BrowserShell.Active)
            {
                if (refFind)
                {
                    inv.FindCraftReference();
                }
                BrowserShell.RefreshAvailability(inv);
                return;
            }
            // innate menu hidden: nothing on screen reads the result, and CraftOpen
            // re-checks all shown buttons when the panel opens
            if (inv.craftPanelID == 0
                && (mn == null || mn.uiMN == null || mn.uiMN.craftPanel == null
                    || !mn.uiMN.craftPanel.activeSelf))
            {
                return;
            }
            _sweepExecuting = true;
            try
            {
                inv.CraftableCheck(refFind);
            }
            finally
            {
                _sweepExecuting = false;
            }
        }

        // Also used by CraftLazyFill, which replaces the vanilla open-sweep with its
        // own batched CraftableCheckSlot calls (one window per batch/frame).
        internal static void BeginSweepWindow()
        {
            _sweeping = true;
            _sweepFrame = Time.frameCount;
            _snapshot = null;
        }

        internal static void EndSweepWindow()
        {
            _sweeping = false;
            _snapshot = null;
        }

        // Vanilla FindCraftReference centers its 5m scan on the bench and bails without one.
        // In innate mode, replace the body with the identical scan centered on the player.
        // refInventory is always cleared first: it can still hold the previous bench's
        // chest list (harmless in vanilla, which ignores it in innate mode — but our
        // innate check/consume paths read it, and must never see a stale list).
        [HarmonyPatch(nameof(InventoryManager.FindCraftReference))]
        [HarmonyPrefix]
        private static bool FindCraftReference_Prefix(InventoryManager __instance)
        {
            if (__instance.craftPanelID != 0)
            {
                return true;
            }
            __instance.refInventory.Clear();
            ManagersScript mn = MnRef(__instance);
            PlayerMove pMove = (mn != null && mn.gameMN != null) ? mn.gameMN.pMove : null;
            if (pMove == null)
            {
                return false;
            }
            // The special storages the reach rule makes live (D145: 5 m) - the vicinity
            // window's own answer, so a storage it shows is one a recipe can use.
            Collider[] hits = Physics.OverlapSphere(pMove.transform.position, ReachRule.Widest(mn), LayerMask.GetMask("BG"));
            for (int i = 0; i < hits.Length; i++)
            {
                InventorySlot chest = hits[i].GetComponent<InventorySlot>();
                if (chest != null && chest.type == InventorySlot.Type.Material
                    && ReachRule.Live(mn, chest)
                    && !__instance.refInventory.Contains(chest))
                {
                    __instance.refInventory.Add(chest);
                }
            }
            // the last boss's bench: its parts are what the fight's recipe is made of
            InventorySlot boss = BossBench.Live(pMove.transform.position);
            if (boss != null && !__instance.refInventory.Contains(boss))
            {
                __instance.refInventory.Add(boss);
            }
            return false;
        }

        // ---------- Check side: light up recipes the combined sources can pay for ----------

        [HarmonyPatch(nameof(InventoryManager.CraftableCheckSlot))]
        [HarmonyPostfix]
        private static void CraftableCheckSlot_Postfix(InventoryManager __instance, int slotID, ref bool __result)
        {
            if (__result)
            {
                return;
            }
            bool bench = __instance.craftPanelID != 0;
            if (bench && __instance.tmpSubInventory == null)
            {
                return;
            }
            ManagersScript mn = MnRef(__instance);
            if (mn == null)
            {
                return;
            }
            CraftInfo craftInfo = mn.craftMN.craftData[__instance.craftPanelID].craftInfo[slotID];
            if (!RequirementsMet(__instance, craftInfo, bench))
            {
                return;
            }
            __result = true;
            __instance.itemSlot[100 + slotID].GetComponent<Button>().interactable = true;
            __instance.itemSlot[100 + slotID].GetComponent<Image>().color = new Color(1f, 1f, 1f, 1f);
        }

        internal static bool RequirementsMet(InventoryManager inv, CraftInfo craftInfo, bool bench)
        {
            for (int i = 0; i < craftInfo.required.Length; i++)
            {
                if (CountAvailable(inv, craftInfo.required[i].itemData.name, bench) < craftInfo.required[i].count)
                {
                    return false;
                }
            }
            return true;
        }

        // ---------- availability snapshot ----------
        // A CraftableCheck sweep runs CraftableCheckSlot for EVERY recipe, and each of
        // those counted every source slot per material — O(recipes × materials × slots).
        // In a dense base (hundreds of aggregated recipes × chest slots, re-triggered
        // while walking) that stacked frames until Windows declared the game hung.
        // Sources are static for the duration of one sweep, so on first use we build a
        // single itemKey→total map and answer every count from it. Scoped strictly to
        // the sweep (opened in the prefix, torn down in the postfix, frame-stamped
        // against a sweep aborted by an exception) — standalone calls, e.g. from the
        // CraftItem guard or tooltip hover, still count directly and can never go stale.
        private static Dictionary<string, float> _snapshot;
        private static bool _sweeping;
        private static int _sweepFrame;

        // Total reachable amount of an item under the active sourcing rules. Vanilla sources
        // are always counted; the sources this mod adds only when their toggle is on.
        internal static float CountAvailable(InventoryManager inv, string key, bool bench)
        {
            if (_sweeping && _sweepFrame == Time.frameCount)
            {
                if (_snapshot == null)
                {
                    _snapshot = BuildSnapshot(inv, bench);
                }
                return _snapshot.TryGetValue(key, out float total) ? total : 0f;
            }
            return CountDirect(inv, key, bench);
        }

        // The bench you are standing at, once WI-4 has claimed it.
        //
        // Vanilla crafted from a bench's own strip through the mirror slots, under
        // `craftPanelID != 0`. The bench redirect turned every bench open into a SCOPED innate
        // open, so craftPanelID is always 0, that branch stopped running, and
        // materials put into a bench quietly stopped counting for anything — the
        // strip became a box you could fill and never spend. Interception keeps it
        // in its real slots, so the bench simply joins the innate sources, and
        // FIRST, which is the order vanilla drew on it in.
        //
        // Only the OPEN bench, never benches merely in reach: this is the container
        // you are dealing with, not a widening of the sourcing invariant.
        private static InventorySlot FocusBench()
        {
            InventorySlot chest = VanillaContainerUI.Intercepted;
            return chest != null && chest.type == InventorySlot.Type.Bench
                && chest.slots != null ? chest : null;
        }

        // One pass over all active sources; same source selection as CountDirect.
        private static Dictionary<string, float> BuildSnapshot(InventoryManager inv, bool bench)
        {
            var totals = new Dictionary<string, float>();
            if (bench)
            {
                for (int j = 0; j < inv.tmpSubInventory.size; j++)
                {
                    AddStack(totals, inv.itemSlot[50 + j]);
                }
            }
            InventorySlot focus = bench ? null : FocusBench();
            if (focus != null)
            {
                for (int j = 0; j < focus.slots.Length; j++)
                {
                    AddStack(totals, focus.slots[j]);
                }
            }
            for (int c = 0; c < inv.refInventory.Count; c++)
            {
                ItemSlot[] slots = inv.refInventory[c].slots;
                for (int j = 0; j < slots.Length; j++)
                {
                    AddStack(totals, slots[j]);
                }
            }
            for (int j = 0; j < inv.baseCount; j++)
            {
                AddStack(totals, inv.itemSlot[j]);
            }
            List<ItemSlot> bag = PlayerBagSlots(inv, out _);
            if (bag != null)
            {
                for (int j = 0; j < bag.Count; j++)
                {
                    AddStack(totals, bag[j]);
                }
            }
            return totals;
        }

        private static void AddStack(Dictionary<string, float> totals, ItemSlot slot)
        {
            if (string.IsNullOrEmpty(slot.itemKey))
            {
                return;
            }
            totals.TryGetValue(slot.itemKey, out float total);
            totals[slot.itemKey] = total + slot.stack;
        }

        private static float CountDirect(InventoryManager inv, string key, bool bench)
        {
            float total = 0f;
            if (bench)
            {
                for (int j = 0; j < inv.tmpSubInventory.size; j++)
                {
                    total += StackOf(inv.itemSlot[50 + j], key);
                }
            }
            InventorySlot focus = bench ? null : FocusBench();
            if (focus != null)
            {
                for (int j = 0; j < focus.slots.Length; j++)
                {
                    total += StackOf(focus.slots[j], key);
                }
            }
            for (int c = 0; c < inv.refInventory.Count; c++)
            {
                ItemSlot[] slots = inv.refInventory[c].slots;
                for (int j = 0; j < slots.Length; j++)
                {
                    total += StackOf(slots[j], key);
                }
            }
            for (int j = 0; j < inv.baseCount; j++)
            {
                total += StackOf(inv.itemSlot[j], key);
            }
            List<ItemSlot> bag = PlayerBagSlots(inv, out _);
            if (bag != null)
            {
                for (int j = 0; j < bag.Count; j++)
                {
                    total += StackOf(bag[j], key);
                }
            }
            return total;
        }

        private static int StackOf(ItemSlot slot, string key)
        {
            return slot.itemKey == key ? slot.stack : 0;
        }

        // ---------- Consume side: reimplementation of CraftItem's consumption + new sources ----------

        [HarmonyPatch(nameof(InventoryManager.CraftItem))]
        [HarmonyPrefix]
        private static bool CraftItem_Prefix(InventoryManager __instance, int slotID, bool craftableCheck)
        {
            bool bench = __instance.craftPanelID != 0;
            // Vanilla handles: a bench without sub-inventory, creative mode
            if (bench && __instance.tmpSubInventory == null)
            {
                return true;
            }
            InventorySlot focus = bench ? null : FocusBench();
            ManagersScript mn = MnRef(__instance);
            if (mn == null || mn.gameMN.creative)
            {
                return true;
            }

            // Guard clause mirrors vanilla; CraftableCheckSlot includes our postfix, so
            // recipes funded by the new sources pass here too
            CraftInfo craftInfo = mn.craftMN.craftData[__instance.craftPanelID].craftInfo[slotID - 100];
            ItemData itemData = craftInfo != null ? craftInfo.GetComponent<ItemData>() : null;
            if (craftInfo == null || itemData == null || (craftableCheck && !__instance.CraftableCheckSlot(slotID - 100)))
            {
                return false;
            }

            // Consumption; loop structure kept identical to vanilla (including the
            // commonStates bookkeeping the cloth_109 variant selection depends on),
            // with the new source appended as each mode's last resort
            CommonStates commonStates = null;
            int getCount = 1;
            for (int i = 0; i < craftInfo.required.Length; i++)
            {
                string reqName = craftInfo.required[i].itemData.name;
                float count = craftInfo.required[i].count;

                if (bench)
                {
                    for (int j = 0; j < __instance.tmpSubInventory.slots.Length; j++)
                    {
                        commonStates = __instance.itemSlot[50 + j].common;
                        __instance.CraftConsume(reqName, itemData, __instance.itemSlot[50 + j], ref count, ref getCount);
                        if (count <= 0f)
                        {
                            break;
                        }
                    }
                }
                else
                {
                    // the bench you opened comes first, as it did in vanilla
                    if (focus != null)
                    {
                        for (int j = 0; j < focus.slots.Length; j++)
                        {
                            commonStates = focus.slots[j].common;
                            __instance.CraftConsume(reqName, itemData, focus.slots[j], ref count, ref getCount);
                            if (count <= 0f)
                            {
                                break;
                            }
                        }
                    }
                    if (count > 0f)
                    {
                        for (int j = 0; j < __instance.baseCount; j++)
                        {
                            commonStates = __instance.itemSlot[j].common;
                            __instance.CraftConsume(reqName, itemData, __instance.itemSlot[j], ref count, ref getCount);
                            if (count <= 0f)
                            {
                                break;
                            }
                        }
                    }
                    if (count > 0f)
                    {
                        ConsumeFromBag(__instance, reqName, itemData, ref count, ref getCount, ref commonStates);
                    }
                }
                if (count > 0f)
                {
                    for (int c = 0; c < __instance.refInventory.Count; c++)
                    {
                        for (int j = 0; j < __instance.refInventory[c].slots.Length; j++)
                        {
                            commonStates = __instance.refInventory[c].slots[j].common;
                            __instance.CraftConsume(reqName, itemData, __instance.refInventory[c].slots[j], ref count, ref getCount);
                            if (count <= 0f)
                            {
                                break;
                            }
                        }
                        if (count <= 0f)
                        {
                            break;
                        }
                    }
                }
                if (bench && count > 0f)
                {
                    for (int j = 0; j < __instance.baseCount; j++)
                    {
                        commonStates = __instance.itemSlot[j].common;
                        __instance.CraftConsume(reqName, itemData, __instance.itemSlot[j], ref count, ref getCount);
                        if (count <= 0f)
                        {
                            break;
                        }
                    }
                    if (count > 0f)
                    {
                        ConsumeFromBag(__instance, reqName, itemData, ref count, ref getCount, ref commonStates);
                    }
                }
            }

            // Refresh visible stack counters (bench UI slots only exist in bench mode;
            // player slots can be drained in both modes now)
            if (bench)
            {
                for (int j = 0; j < __instance.subCount; j++)
                {
                    __instance.StackTextChange(50 + j, craftableCheck: false);
                }
            }
            for (int j = 0; j < __instance.baseCount; j++)
            {
                __instance.StackTextChange(j, craftableCheck: false);
            }

            // Vanilla quirk kept: crafted cloth variant follows the last touched slot's owner
            if (itemData.name == "cloth_109" && commonStates != null)
            {
                switch (commonStates.parameters[6])
                {
                    case 0:
                    case 1:
                        itemData = mn.itemMN.FindItem("cloth_108");
                        break;
                    case 3:
                        itemData = mn.itemMN.FindItem("cloth_110");
                        break;
                }
            }

            mn.itemMN.GetItem(itemData, getCount);
            return false;
        }

        private static void ConsumeFromBag(InventoryManager inv, string reqName, ItemData itemData,
            ref float count, ref int getCount, ref CommonStates commonStates)
        {
            List<ItemSlot> bag = PlayerBagSlots(inv, out bool mirror);
            if (bag == null)
            {
                return;
            }
            for (int j = 0; j < bag.Count; j++)
            {
                commonStates = bag[j].common;
                inv.CraftConsume(reqName, itemData, bag[j], ref count, ref getCount);
                if (count <= 0f)
                {
                    break;
                }
            }
            if (mirror)
            {
                // consumed from the on-screen mirror slots — refresh their stack labels
                for (int j = 0; j < bag.Count; j++)
                {
                    inv.StackTextChange(bag[j].slotID, craftableCheck: false);
                }
            }
        }
    }
}
