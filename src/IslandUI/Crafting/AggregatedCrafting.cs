using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace IslandUI
{
    // Feature: the innate crafting menu also lists recipes of owned workbenches within
    // reach of the player.
    //
    // Mechanism: the innate recipe array (craftMN.craftData[0].craftInfo) is extended
    // in-place with the nearby benches' CraftInfo references just before CraftOpen(0)
    // fills the menu. Every downstream consumer — button fill, CraftableCheck(Slot),
    // the sourcing patches, craft-count display, tooltips, CraftItem — reads that same
    // array, so the aggregated recipes behave like native innate recipes everywhere.
    // Materials follow innate sourcing rules (Shared Bag + bag + player-nearby chests).
    // The pristine array is cached and restored whenever aggregation yields nothing.
    //
    // Safe because the only other craftData writers run before us and touch copies:
    // CraftManager.Start clones panel 0 into 24 (hand bench) at startup;
    // WorkshopItemManager builds panel 25 from workshop content.
    [HarmonyPatch(typeof(InventoryManager))]
    internal static class AggregatedCrafting
    {
        private static readonly AccessTools.FieldRef<InventoryManager, ManagersScript> MnRef =
            AccessTools.FieldRefAccess<InventoryManager, ManagersScript>("mn");

        // bench item name -> craftData panel index (from SubInventoryLoad's bench switch)
        private static readonly Dictionary<string, int> BenchPanels = new Dictionary<string, int>
        {
            ["campfire_01"] = 1,
            ["bench_wood"] = 2,
            ["bench_iron"] = 3,
            ["furnace_01"] = 4,
            ["bench_meat"] = 5,
            ["bench_spider"] = 6,
            ["bench_mens"] = 7,
            ["bench_plant"] = 8,
            ["bench_wing"] = 9,
            ["bench_sand"] = 10,
            ["bench_pretty"] = 11,
            ["bench_xmas"] = 17,
            ["stonemill_01"] = 18,
            ["bench_cook_01"] = 19,
            ["bench_cook_02"] = 20,
            ["bench_summer"] = 22,
            ["bench_halloween"] = 23,
            ["bench_hand"] = 24,
            ["bench_workshop"] = 25,
            ["bench_chaos"] = 26,
            ["bench_surround"] = 28,
            ["bench_spring"] = 29,
            ["bench_drink"] = 30,
        };

        private static CraftInfo[] _pristine;
        private static bool _gridDumped;

        // the innate recipe array as authored (before aggregation swaps craftData[0]);
        // the RecipeCatalog builds from this so bench recipes aren't misattributed
        internal static CraftInfo[] PristineInnate => _pristine;

        // reachability: is this panel's bench/crafter currently usable?
        // Innate always; the scoped bench is by definition the one just clicked;
        // otherwise whatever the last aggregation scan found in reach.
        internal static bool PanelReachable(int panelID)
        {
            if (panelID == 0)
            {
                return true;
            }
            if (BenchScope != 0 && panelID == BenchScope)
            {
                return true;
            }
            return LastPanelIDs.Contains(panelID);
        }

        // ---------- one crafting menu: bench opens become scoped opens ----------
        //
        // The vanilla bench/NPC panels are gone: CraftOpen(id != 0) is redirected to
        // the aggregated panel 0 with BenchScope as an intrinsic filter, so the
        // custom menu (with search, detail card, availability) serves every crafting
        // surface. Scope lifetime mirrors vanilla's craftPanelID semantics: a genuine
        // vanilla CraftOpen(0) (chest/bag close via SubInventoryVisible, rack open,
        // build menu) resets it, while our own refills and LeftPanelState(1)'s
        // panel-show re-run (which only fires because craftPanelID is 0) keep it.
        internal static int BenchScope;

        private static bool _refill;
        private static bool _inLeftPanel;

        // re-run the pipeline for the CURRENT view without resetting the scope
        internal static void Refill(InventoryManager inv)
        {
            _refill = true;
            try
            {
                inv.CraftOpen(inv.craftPanelID);
            }
            finally
            {
                _refill = false;
            }
        }

        [HarmonyPatch(typeof(UIManager), nameof(UIManager.LeftPanelState))]
        [HarmonyPrefix]
        private static void LeftPanelState_ScopePrefix()
        {
            _inLeftPanel = true;
        }

        [HarmonyPatch(typeof(UIManager), nameof(UIManager.LeftPanelState))]
        [HarmonyPostfix]
        private static void LeftPanelState_ScopePostfix()
        {
            _inLeftPanel = false;
        }

        [HarmonyPatch(nameof(InventoryManager.CraftOpen))]
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)] // before every other CraftOpen prefix
        private static void CraftOpen_RedirectPrefix(ref int id)
        {
            if (!Settings.CustomGrid.Value)
            {
                BenchScope = 0; // feature off: vanilla bench panels as before
                return;
            }
            if (id != 0)
            {
                BenchScope = id;
                id = 0;
            }
            else if (!_refill && !_inLeftPanel)
            {
                BenchScope = 0;
            }
        }

        // recipe -> source panel, for section headers (0 = innate); rebuilt per open
        internal static readonly Dictionary<CraftInfo, int> SourceOf = new Dictionary<CraftInfo, int>();

        // NPC crafters (from UIManager.NPCMenuOpen's switch): npcID -> panel.
        // Gating mirrors vanilla: employed, alive, and npc 71 only while in water.
        private static readonly Dictionary<int, int> NpcPanels = new Dictionary<int, int>
        {
            [18] = 13,
            [19] = 12,
            [71] = 21,
            [90] = 14,
            [91] = 15,
            [113] = 16,
            [116] = 27,
        };

        // reverse: panelID -> npcID, for section labels and the detail card
        internal static readonly Dictionary<int, int> NpcOfPanel = BuildNpcReverse();

        private static Dictionary<int, int> BuildNpcReverse()
        {
            var reverse = new Dictionary<int, int>();
            foreach (KeyValuePair<int, int> pair in NpcPanels)
            {
                reverse[pair.Value] = pair.Key;
            }
            return reverse;
        }

        private static Dictionary<int, string> _panelToBenchKey;

        // The panel a bench opens, by its item key — this table IS
        // InventoryManager.SubInventoryLoad's bench switch, entry for entry. The
        // container replacement intercepts a bench's load now and so has to dispatch that switch
        // itself; 0 means the switch has no case for the key, which in vanilla
        // opens no panel at all.
        internal static int PanelOf(string benchKey)
        {
            return benchKey != null && BenchPanels.TryGetValue(benchKey, out int panel) ? panel : 0;
        }

        // bench item key for a panel id (reverse of BenchPanels), for header labels
        internal static string BenchKeyOf(int panelID)
        {
            if (_panelToBenchKey == null)
            {
                _panelToBenchKey = new Dictionary<int, string>();
                foreach (KeyValuePair<string, int> pair in BenchPanels)
                {
                    if (!_panelToBenchKey.ContainsKey(pair.Value))
                    {
                        _panelToBenchKey[pair.Value] = pair.Key;
                    }
                }
            }
            return _panelToBenchKey.TryGetValue(panelID, out string key) ? key : null;
        }

        // one-shot diagnostic: the header/section work needs the grid's runtime
        // layout facts, which only exist in scene data
        private static void DumpGridInfo(ManagersScript mn)
        {
            if (_gridDumped || mn.uiMN == null || mn.uiMN.craftPanel == null)
            {
                return;
            }
            _gridDumped = true;
            Transform grid = mn.uiMN.craftPanel.transform.Find("mask/grid");
            if (grid == null)
            {
                Plugin.Log.LogInfo("GridDump: mask/grid not found under craftPanel");
                return;
            }
            Plugin.Log.LogInfo($"GridDump: grid children={grid.childCount}, components: " +
                string.Join(", ", System.Array.ConvertAll(grid.GetComponents<Component>(), c => c.GetType().Name)));
            if (grid.GetComponent<UnityEngine.UI.GridLayoutGroup>() is UnityEngine.UI.GridLayoutGroup glg)
            {
                Plugin.Log.LogInfo($"GridDump: cellSize={glg.cellSize}, spacing={glg.spacing}, " +
                    $"constraint={glg.constraint}, count={glg.constraintCount}, startCorner={glg.startCorner}, padding={glg.padding}");
            }
            var rt = grid.GetComponent<RectTransform>();
            Plugin.Log.LogInfo($"GridDump: grid rect={rt.rect.size}, anchoredPos={rt.anchoredPosition}; " +
                $"panel rect={mn.uiMN.craftPanel.GetComponent<RectTransform>().rect.size}");
            if (grid.childCount > 0)
            {
                Transform first = grid.GetChild(0);
                Plugin.Log.LogInfo($"GridDump: firstChild='{first.name}' active={first.gameObject.activeSelf} " +
                    $"slot={(first.GetComponent<ItemSlot>() != null ? first.GetComponent<ItemSlot>().slotID.ToString() : "none")}");
            }
        }

        [HarmonyPatch(nameof(InventoryManager.CraftOpen))]
        [HarmonyPrefix]
        [HarmonyPriority(Priority.High)] // before BrowserSearch's filter prefix
        private static void CraftOpen_Prefix(InventoryManager __instance, int id)
        {
            if (id != 0)
            {
                return;
            }
            ManagersScript mn = MnRef(__instance);
            if (mn == null || CraftLazyFill.ShouldSkip(mn, id))
            {
                return;
            }
            CraftManager.CraftData innate = mn.craftMN.craftData[0];
            if (_pristine == null)
            {
                // CraftManager.Awake keeps the authored innate recipes in the
                // hand-bench panel. Panel 0 may already be aggregated or filtered
                // when a hot generation starts, so it cannot be the baseline.
                var hand = mn.craftMN.craftData.Length > 24 ? mn.craftMN.craftData[24].craftInfo : null;
                _pristine = hand != null && hand.Length > 0 ? hand : innate.craftInfo;
            }
            DumpGridInfo(mn);

            // scoped open (clicked bench/crafter): ONLY that panel's recipes —
            // vanilla bench-menu content, custom-menu presentation
            if (BenchScope > 0 && BenchScope < mn.craftMN.craftData.Length)
            {
                CraftInfo[] scoped = mn.craftMN.craftData[BenchScope].craftInfo;
                SourceOf.Clear();
                for (int i = 0; i < scoped.Length; i++)
                {
                    if (scoped[i] != null)
                    {
                        SourceOf[scoped[i]] = BenchScope;
                    }
                }
                innate.craftInfo = scoped;
                return;
            }

            SourceOf.Clear();
            for (int i = 0; i < _pristine.Length; i++)
            {
                if (_pristine[i] != null)
                {
                    SourceOf[_pristine[i]] = 0;
                }
            }

            PlayerMove pMove = mn.gameMN != null ? mn.gameMN.pMove : null;
            if (pMove == null)
            {
                LastPanelIDs.Clear();
                innate.craftInfo = ApplyGrouping(mn, _pristine);
                return;
            }

            // which owned bench types are in reach?
            var panelIDs = new HashSet<int>();
            ScanPanelIDs(mn, pMove.transform.position, panelIDs);
            LastPanelIDs.Clear();
            LastPanelIDs.UnionWith(panelIDs);

            // collect their recipes, deduped against innate and each other by item name
            var seen = new HashSet<string>();
            for (int i = 0; i < _pristine.Length; i++)
            {
                seen.Add(_pristine[i].name);
            }
            var extras = new List<CraftInfo>();
            foreach (int panelID in panelIDs)
            {
                CraftInfo[] recipes = mn.craftMN.craftData[panelID].craftInfo;
                for (int i = 0; i < recipes.Length; i++)
                {
                    if (recipes[i] != null && seen.Add(recipes[i].name))
                    {
                        extras.Add(recipes[i]);
                        SourceOf[recipes[i]] = panelID;
                    }
                }
            }

            if (extras.Count == 0)
            {
                innate.craftInfo = ApplyGrouping(mn, _pristine);
                return;
            }
            // NO capacity truncation here: the search filter (lower-priority prefix)
            // must see the full recipe set, else over-capacity recipes are silently
            // unfindable. The clamp to the button-grid capacity runs after it, below.

            var combined = new CraftInfo[_pristine.Length + extras.Count];
            _pristine.CopyTo(combined, 0);
            extras.CopyTo(combined, _pristine.Length);
            innate.craftInfo = ApplyGrouping(mn, combined);
            Plugin.Log.LogDebug($"Aggregated crafting: +{extras.Count} recipes from {panelIDs.Count} bench type(s) in reach");
        }

        // owned bench types AND employed crafter NPCs within the crafting range of
        // `center` (the game's 5 m, ReachRule.Workshop - D145), as craftData
        // panel IDs
        private static void ScanPanelIDs(ManagersScript mn, Vector3 center, HashSet<int> result)
        {
            Collider[] hits = Physics.OverlapSphere(center, ReachRule.Workshop, LayerMask.GetMask("BG"));
            for (int i = 0; i < hits.Length; i++)
            {
                InventorySlot bench = hits[i].GetComponent<InventorySlot>();
                if (bench == null || bench.type != InventorySlot.Type.Bench)
                {
                    continue;
                }
                ItemInfo info = bench.GetComponent<ItemInfo>();
                if (info != null && mn.buildMN.OwnCheck(info)
                    && ReachRule.Live(bench, center, ReachRule.Workshop)
                    && BenchPanels.TryGetValue(info.itemKey, out int panelID))
                {
                    result.Add(panelID);
                }
            }

            // crafter NPCs, vanilla-gated (employed, alive, 71 water-only)
            List<CommonStates> friends = mn.npcMN != null ? mn.npcMN.friendList : null;
            if (friends == null)
            {
                return;
            }
            for (int i = 0; i < friends.Count; i++)
            {
                CommonStates npc = friends[i];
                if (npc == null || npc.life <= 0.0 || !npc.Employed()
                    || !NpcPanels.TryGetValue(npc.npcID, out int panelID))
                {
                    continue;
                }
                if (npc.npcID == 71 && (npc.nMove == null || npc.nMove.inWater == 0))
                {
                    continue;
                }
                if (ReachRule.Live(npc, center, ReachRule.Workshop))
                {
                    result.Add(panelID);
                }
            }
        }

        // bench set applied by the last full CraftOpen(0); lets the movement handler
        // skip the full 500-button rebuild while walking within the same bench reach
        internal static readonly HashSet<int> LastPanelIDs = new HashSet<int>();

        private static readonly HashSet<int> ScanScratch = new HashSet<int>();

        internal static bool NearbyBenchSetChanged(ManagersScript mn)
        {
            PlayerMove pMove = mn.gameMN != null ? mn.gameMN.pMove : null;
            if (pMove == null)
            {
                return false;
            }
            ScanScratch.Clear();
            ScanPanelIDs(mn, pMove.transform.position, ScanScratch);
            return !ScanScratch.SetEquals(LastPanelIDs);
        }

        // Late stage of the CraftOpen prefix chain: aggregation (High) grows the
        // array, the search filter (Low) projects it — and this clamp (VeryLow) cuts
        // whatever survives down to the button-grid capacity, so the fill loop and
        // every itemSlot[100+i] consumer stay in bounds. Clamping after the filter
        // is what makes over-capacity recipes reachable by narrowing the search.
        // (CraftLazyFill's fill replacement runs after this, at Last.)
        private static int _lastDropWarned;

        [HarmonyPatch(nameof(InventoryManager.CraftOpen))]
        [HarmonyPrefix]
        [HarmonyPriority(Priority.VeryLow)]
        private static void CraftOpen_ClampPrefix(InventoryManager __instance, int id)
        {
            ManagersScript mn = MnRef(__instance);
            if (mn == null || CraftLazyFill.ShouldSkip(mn, id))
            {
                return;
            }
            CraftManager.CraftData data = mn.craftMN.craftData[id];
            if (data.craftInfo.Length <= __instance.craftCount)
            {
                _lastDropWarned = 0;
                return;
            }
            int dropped = data.craftInfo.Length - __instance.craftCount;
            var clamped = new CraftInfo[__instance.craftCount];
            System.Array.Copy(data.craftInfo, clamped, __instance.craftCount);
            data.craftInfo = clamped;
            if (dropped != _lastDropWarned)
            {
                _lastDropWarned = dropped;
                Plugin.Log.LogWarning($"Crafting menu: {dropped} recipe(s) beyond the " +
                    $"{__instance.craftCount}-button grid capacity are hidden — use search to narrow the list");
            }
        }

        // Grouping-as-ordering: recipes cluster by the selected dimension. SourceBench
        // is the natural build order (innate first, then per bench) — a no-op. ItemType
        // stable-sorts by the game's own item category, preserving inner order.
        // Always returns a new/unchanged array — the pristine array is never mutated.
        private static CraftInfo[] ApplyGrouping(ManagersScript mn, CraftInfo[] recipes)
        {
            if (Settings.GroupRecipesBy.Value != Settings.RecipeGrouping.ItemType)
            {
                return recipes;
            }
            return recipes
                .OrderBy(recipe =>
                {
                    ItemData item = recipe != null ? mn.itemMN.FindItem(recipe.name) : null;
                    return item != null ? (int)item.itemType : int.MaxValue;
                })
                .ToArray();
        }
    }
}
