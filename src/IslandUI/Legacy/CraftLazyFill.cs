using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using IslandUI.UI;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI
{
    // Feature: stutter-free crafting menu. Three mechanisms:
    //
    // 1. Hidden-open gate. The game calls CraftOpen(0) in places where the craft
    //    panel isn't even visible (most notably: closing ANY chest). When the panel
    //    is hidden, every participant in the CraftOpen pipeline stands down (each
    //    checks ShouldSkip) and only vanilla's lasting side effect (craftPanelID)
    //    is kept — LeftPanelState(1) re-runs CraftOpen when the panel really opens.
    //
    // 2. Fill replacement. This prefix REPLACES vanilla CraftOpen's fill loop +
    //    CraftableCheck() call: button active states (which determine layout and
    //    section placement) are set synchronously, the top screenful gets content
    //    immediately, and a button already showing its recipe is not re-filled.
    //
    // 3. Window virtualization (the actual stutter kill). Frame-time measurements
    //    showed the cost is Unity's canvas geometry rebuild — ~500 buttons' worth
    //    of UI mesh regenerated in one frame whenever the panel content changes.
    //    That work is engine-side and main-thread-bound; the only winning move is
    //    to have less of it. So: only buttons near the viewport keep their Graphic
    //    components enabled (a disabled Graphic contributes zero geometry while
    //    the GridLayoutGroup still reserves its cell). Maintain(), called every
    //    frame, watches the scroll position and enables/fills rows approaching the
    //    view (time-budgeted) while disabling rows far outside it. The canvas
    //    never carries more than ~2 screens of geometry no matter how long the
    //    aggregated list gets.
    [HarmonyPatch(typeof(InventoryManager))]
    internal static class CraftLazyFill
    {
        private static readonly AccessTools.FieldRef<InventoryManager, ManagersScript> MnRef =
            AccessTools.FieldRefAccess<InventoryManager, ManagersScript>("mn");

        // ---------- maintainer state (set by the fill prefix) ----------
        private static InventoryManager _inv;
        private static ManagersScript _mn;
        private static ItemData[] _items;
        private static int _panelId;
        private static float _lastScrollY = float.MinValue;
        private static bool _pending;

        // Panel-hidden check shared by the whole CraftOpen prefix/postfix pipeline.
        // Only innate opens are gated: bench opens (id != 0) happen while the bench UI
        // is coming up and their ordering with craftPanel activation is the game's own.
        // Also true during the panel-show frame of a two-stage open (below).
        internal static bool ShouldSkip(ManagersScript mn, int id)
        {
            return id == 0
                && (mn == null || mn.uiMN == null || mn.uiMN.craftPanel == null
                    || !mn.uiMN.craftPanel.activeSelf
                    || Time.frameCount == _deferOpenFrame);
        }

        // ---------- two-stage open ----------
        // Opening the menu NEAR benches paid panel activation AND the full aggregated
        // list rebuild in one frame — while opening away and walking into range
        // spreads those costs across frames and feels smooth (verified in play). So a
        // fresh panel-show skips the CraftOpen pipeline entirely (the panel appears
        // instantly with its previous content) and the normal full open runs two
        // frames later — the exact walk-into-range sequence, every time.
        private static int _deferOpenFrame = -1;
        private static int _refreshFrame;

        [HarmonyPatch(typeof(UIManager), nameof(UIManager.LeftPanelState))]
        [HarmonyPrefix]
        private static void LeftPanelState_Prefix(UIManager __instance, int state)
        {
            // the two-stage open exists to tame the VANILLA grid's rebuild; the
            // custom view opens cheaply and must not flash stale content
            if (state == 1 && Settings.DeferAggregationOnOpen.Value && !Settings.CustomGrid.Value
                && __instance.craftPanel != null && !__instance.craftPanel.activeSelf)
            {
                _deferOpenFrame = Time.frameCount;
                _refreshFrame = Time.frameCount + 2;
            }
        }

        // called from Plugin.Update: stage 2 — the real open, panel already on screen
        internal static void TryPostOpenRefresh(ManagersScript mn)
        {
            if (_refreshFrame == 0 || Time.frameCount < _refreshFrame)
            {
                return;
            }
            _refreshFrame = 0;
            if (mn == null || mn.inventory == null || mn.uiMN == null
                || mn.uiMN.craftPanel == null || !mn.uiMN.craftPanel.activeSelf
                || mn.inventory.craftPanelID != 0)
            {
                return;
            }
            mn.inventory.CraftOpen(0);
        }

        // ---------- unchanged-list fast path ----------
        // On a repeat open at the same spot the pipeline reproduces an IDENTICAL
        // list — the 500-iteration state pass, the section rebuild and the layout
        // aftermath (measured 16ms + a 53ms frame) all for a no-op. Remember what
        // was last built; when the new array matches element-for-element, skip
        // straight to the availability refresh. Invalidated when a collection
        // unlocks (CraftListCheck results change without the array changing) and
        // when layout/grouping config changes.
        private static CraftInfo[] _lastList;
        private static InventoryManager _lastInv;
        private static int _lastId = -1;
        internal static bool UnchangedOpen; // read by the Sections postfix

        internal static void InvalidateUnchangedCache()
        {
            _lastList = null;
        }

        [HarmonyPatch(nameof(InventoryManager.GetCollection))]
        [HarmonyPrefix]
        private static void GetCollection_Prefix()
        {
            // a fresh unlock changes CraftListCheck results, not the recipe array
            InvalidateUnchangedCache();
        }

        private static bool ListUnchanged(InventoryManager inv, int id, CraftInfo[] recipes)
        {
            if (_lastList == null || _items == null || _lastId != id
                || !ReferenceEquals(_lastInv, inv) || _lastList.Length != recipes.Length)
            {
                return false;
            }
            for (int i = 0; i < recipes.Length; i++)
            {
                if (!ReferenceEquals(_lastList[i], recipes[i]))
                {
                    return false;
                }
            }
            return true;
        }

        [HarmonyPatch(nameof(InventoryManager.CraftOpen))]
        [HarmonyPrefix]
        [HarmonyPriority(Priority.Last)]
        private static bool CraftOpen_FillPrefix(InventoryManager __instance, int id)
        {
            UnchangedOpen = false;
            ManagersScript mn = MnRef(__instance);
            if (mn == null)
            {
                return true; // vanilla fallback
            }
            __instance.craftPanelID = id; // vanilla's first statement — always preserved

            if (ShouldSkip(mn, id))
            {
                return false;
            }
            CanvasIsolation.Isolate(mn.uiMN.craftPanel);

            // the custom view takes over the innate menu entirely — the data
            // pipeline (higher-priority prefixes) has already produced craftData[0]
            BrowserShell.HandledThisOpen = false;
            if (id == 0 && Settings.CustomGrid.Value)
            {
                if (BrowserShell.TryHandleOpen(__instance, mn))
                {
                    BrowserShell.HandledThisOpen = true;
                    _items = null; // vanilla-grid maintainer stands down
                    __instance.StartCoroutine(LogFrameSpikes(id));
                    return false;
                }
            }
            else
            {
                BrowserShell.OnVanillaOpen(); // bench panel or feature off: mask back
            }

            var timer = System.Diagnostics.Stopwatch.StartNew();
            CraftInfo[] recipes = mn.craftMN.craftData[id].craftInfo;
            int count = __instance.craftCount;
            int syncCount = count;
            bool lazy = Settings.LazyFill.Value;
            if (lazy)
            {
                syncCount = CraftPanelLayout.GridColumns * (CraftPanelLayout.GridRows + 2);
            }

            UnchangedOpen = ListUnchanged(__instance, id, recipes);
            ItemData[] items;
            long passOneMs;
            if (UnchangedOpen)
            {
                // buttons, sections and graphics parking are already exactly right;
                // only availability might have changed
                items = _items;
                passOneMs = 0;
            }
            else
            {
            // Pass 1 (sync): resolve each slot's item + unlock state, set button
            // ACTIVE flags (final layout — the sections postfix depends on it), and
            // park every off-screen button's graphics. The itemKey is deliberately
            // NOT consulted for the parking decision: leaving stale-but-correct
            // buttons enabled is what made every open rebatch the full 500.
            items = new ItemData[count];
            for (int i = 0; i < count; i++)
            {
                ItemData item = null;
                if (i < recipes.Length && recipes[i] != null)
                {
                    item = mn.itemMN.FindItem(recipes[i].name); // game-cached lookup
                    if (item != null && !__instance.CraftListCheck(item))
                    {
                        item = null;
                    }
                }
                items[i] = item;
                ItemSlot button = __instance.itemSlot[100 + i];
                if (button.gameObject.activeSelf != (item != null))
                {
                    button.gameObject.SetActive(item != null);
                }
                if (lazy && item != null && i >= syncCount)
                {
                    SetGraphicsEnabled(i, button, false);
                }
            }
            _lastList = (CraftInfo[])recipes.Clone();
            _lastInv = __instance;
            _lastId = id;
            passOneMs = timer.ElapsedMilliseconds;
            }

            // Pass 2: chest scan once (vanilla rhythm: CraftableCheck(refFind:true)
            // on open), then content + craftability for the top screenful.
            __instance.FindCraftReference();
            long refScanMs = timer.ElapsedMilliseconds - passOneMs;
            int filled = 0;
            CraftingPatches.BeginSweepWindow();
            for (; filled < count && filled < syncCount; filled++)
            {
                FillOne(__instance, mn, items[filled], filled);
            }
            CraftingPatches.EndSweepWindow();

            // hand the rest to the per-frame maintainer
            _inv = __instance;
            _mn = mn;
            _items = items;
            _panelId = id;
            _lastScrollY = float.MinValue;
            _pending = true;

            timer.Stop();
            if (timer.ElapsedMilliseconds > 15)
            {
                Plugin.Log.LogDebug($"CraftOpen({id}) frame {Time.frameCount} sync: {timer.ElapsedMilliseconds}ms " +
                    $"(states {passOneMs}ms, chest scan {refScanMs}ms, " +
                    $"fill[{filled}] {timer.ElapsedMilliseconds - passOneMs - refScanMs}ms, unchanged={UnchangedOpen})");
            }
            __instance.StartCoroutine(LogFrameSpikes(id));
            return false; // vanilla fill loop + its CraftableCheck() fully replaced
        }

        // ---------- the per-frame visibility maintainer ----------

        // Called from Plugin.Update every frame. Cheap when idle: one scroll-position
        // compare. On scroll (or with fills outstanding) it walks the buttons,
        // enabling + filling those near the viewport within a time budget and
        // parking those far outside it.
        internal static void Maintain()
        {
            if (_items == null)
            {
                return;
            }
            if (!Settings.LazyFill.Value)
            {
                return;
            }
            if (_inv == null || _mn == null || _mn.uiMN == null) // scene reload
            {
                _items = null;
                return;
            }
            GameObject panel = _mn.uiMN.craftPanel;
            if (panel == null || !panel.activeSelf || _inv.craftPanelID != _panelId)
            {
                return;
            }
            RectTransform mask = panel.transform.Find("mask") as RectTransform;
            RectTransform grid = panel.transform.Find("mask/grid") as RectTransform;
            if (mask == null || grid == null)
            {
                return;
            }
            float top = grid.anchoredPosition.y; // pixels scrolled down
            if (!_pending && Mathf.Abs(top - _lastScrollY) < 1f)
            {
                return;
            }
            _lastScrollY = top;
            float maskH = mask.rect.height;
            float enableMargin = maskH;      // pre-build one screen each way
            float disableMargin = maskH * 2; // hysteresis: park only when well out
            float budget = Mathf.Clamp(Settings.LazyFillBudgetMs.Value, 0.5f, 12f);
            var sw = System.Diagnostics.Stopwatch.StartNew();

            bool pending = false;
            CraftingPatches.BeginSweepWindow();
            for (int i = 0; i < _items.Length; i++)
            {
                ItemData item = _items[i];
                if (item == null)
                {
                    continue;
                }
                ItemSlot button = _inv.itemSlot[100 + i];
                float y = -((RectTransform)button.transform).anchoredPosition.y;
                bool nearView = y + 30f >= top - enableMargin && y - 10f <= top + maskH + enableMargin;
                if (nearView)
                {
                    if (!IsShown(i, button, item))
                    {
                        // strictly-visible rows may exceed the budget a bit — blank
                        // cells in view are worse than one slightly longer frame
                        bool strictlyVisible = y + 30f >= top && y - 10f <= top + maskH;
                        double cap = strictlyVisible ? budget * 3f : budget;
                        if (sw.Elapsed.TotalMilliseconds >= cap)
                        {
                            pending = true;
                            continue;
                        }
                        FillOne(_inv, _mn, item, i);
                    }
                }
                else if (y + 30f < top - disableMargin || y - 10f > top + maskH + disableMargin)
                {
                    SetGraphicsEnabled(i, button, false); // no-op when already parked
                }
            }
            CraftingPatches.EndSweepWindow();
            _pending = pending;
        }

        private static bool IsShown(int i, ItemSlot button, ItemData item)
        {
            Graphic[] graphics = GraphicsOf(i, button);
            return graphics.Length > 0 && graphics[0] != null && graphics[0].enabled
                && button.itemKey == item.name;
        }

        private static void FillOne(InventoryManager inv, ManagersScript mn, ItemData item, int i)
        {
            if (item == null)
            {
                return;
            }
            ItemSlot button = inv.itemSlot[100 + i];
            SetGraphicsEnabled(i, button, true);
            if (button.itemKey != item.name)
            {
                mn.itemMN.ItemToSlot(item, button, 1);
            }
            // snapshot-backed while a sweep window is open; also drives the
            // craftable-count display via its postfix
            inv.CraftableCheckSlot(i);
        }

        // ---------- per-button Graphic sets ----------

        // cached; fake-null on the first entry detects scene reloads
        private static readonly Dictionary<int, Graphic[]> GraphicsCache = new Dictionary<int, Graphic[]>();

        private static Graphic[] GraphicsOf(int i, ItemSlot button)
        {
            if (!GraphicsCache.TryGetValue(i, out Graphic[] graphics)
                || graphics.Length == 0 || graphics[0] == null)
            {
                graphics = button.GetComponentsInChildren<Graphic>(true);
                GraphicsCache[i] = graphics;
            }
            return graphics;
        }

        private static void SetGraphicsEnabled(int i, ItemSlot button, bool enabled)
        {
            Graphic[] graphics = GraphicsOf(i, button);
            if (graphics.Length > 0 && graphics[0].enabled == enabled)
            {
                return; // whole set toggles together — first entry tells the state
            }
            for (int g = 0; g < graphics.Length; g++)
            {
                if (graphics[g] != null)
                {
                    graphics[g].enabled = enabled;
                }
            }
        }

        private static IEnumerator LogFrameSpikes(int id)
        {
            for (int f = 1; f <= 5; f++)
            {
                yield return null;
                float ms = Time.unscaledDeltaTime * 1000f;
                if (ms > 30f)
                {
                    Plugin.Log.LogDebug($"CraftOpen({id}) frame +{f} (abs {Time.frameCount}): {ms:0}ms frame time");
                }
            }
        }
    }

}
