using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace IslandUI
{
    // Feature: search box on the crafting menu. Filtering uses the same mechanism as
    // bench aggregation: the active panel's craftData[id].craftInfo is swapped for a
    // filtered projection before CraftOpen fills the buttons, so the grid re-packs and
    // every consumer (checks, counts, tooltips, crafting) stays consistent.
    // Runs AFTER AggregatedCrafting's prefix (priority ordering), so on the innate
    // panel the search filters the aggregated recipe set.
    //
    // Matching is against ItemData.GetItemName() — the display name in the player's
    // active language — plus the internal key as a bonus for power users, plus the
    // derived category tags (ItemTags: "weapon", "tool", "food", …; English-only).
    [HarmonyPatch]
    internal static class BrowserSearch
    {
        private static readonly AccessTools.FieldRef<InventoryManager, ManagersScript> MnRef =
            AccessTools.FieldRefAccess<InventoryManager, ManagersScript>("mn");

        // per-bench pristine recipe arrays (panel 0's base is whatever aggregation produced)
        private static readonly Dictionary<int, CraftInfo[]> BenchPristine = new Dictionary<int, CraftInfo[]>();

        // The innate menu's recipes BEFORE the search projected them: everything in reach.
        // A recipe the card navigated to is in reach whether or not it matches the words in
        // the search box (the Drone's card led to Iron Ingot, the search
        // still said "drone", and the ingot's card refused to craft beside the furnace).
        internal static CraftInfo[] UnfilteredInnate { get; private set; }


        // ---------- filter engine ----------

        [HarmonyPatch(typeof(InventoryManager), nameof(InventoryManager.CraftOpen))]
        [HarmonyPrefix]
        [HarmonyPriority(Priority.Low)]
        private static void CraftOpen_Prefix(InventoryManager __instance, int id)
        {
            ManagersScript mn = MnRef(__instance);
            if (mn == null || CraftLazyFill.ShouldSkip(mn, id))
            {
                return;
            }

            CraftManager.CraftData data = mn.craftMN.craftData[id];
            CraftInfo[] baseRecipes;
            if (id == 0)
            {
                // AggregatedCrafting's prefix (higher priority) has already set this
                baseRecipes = data.craftInfo;
            }
            else if (!BenchPristine.TryGetValue(id, out baseRecipes))
            {
                baseRecipes = data.craftInfo;
                BenchPristine[id] = baseRecipes;
            }

            if (id == 0)
            {
                UnfilteredInnate = baseRecipes;
            }
            if (!BrowserShell.Filter.Active)
            {
                data.craftInfo = baseRecipes;
                return;
            }
            // The matching itself is UI.SearchSurface's: multi-term AND over the
            // localized name, the key, the tips and ItemTags. It was written here
            // and moved there so the vicinity window answers the same word the
            // same way -- two windows disagreeing about what "iron" means would be
            // a worse bug than either of them lacking search.
            // A bench's or a crafter's name finds everything it makes
            // ("iron workbench" lists every item crafted at that workbench).
            HashSet<int> benches = BenchesNamed(mn, id, baseRecipes);
            bool bySource = Settings.GroupRecipesBy.Value != Settings.RecipeGrouping.ItemType;
            var filtered = new List<CraftInfo>();
            for (int i = 0; i < baseRecipes.Length; i++)
            {
                CraftInfo recipe = baseRecipes[i];
                if (recipe == null)
                {
                    continue;
                }
                ItemData item = mn.itemMN.FindItem(recipe.name);
                if (item == null)
                {
                    continue;
                }
                if (BrowserShell.Filter.MatchesItem(mn, item))
                {
                    filtered.Add(recipe);
                    continue;
                }
                if (benches.Count == 0)
                {
                    continue;
                }
                if (benches.Contains(SourcePanel(recipe)))
                {
                    filtered.Add(recipe);
                    continue;
                }
                // Made at the named bench too, but listed under another source:
                // the aggregation keeps one recipe per item, by hand first. List
                // the named bench's own recipe under its header instead, so the
                // header says why it matched - and crafting it crafts that one.
                CraftInfo there = OfferedAt(mn, benches, recipe.name, out int panel);
                if (there != null)
                {
                    if (bySource)
                    {
                        AggregatedCrafting.SourceOf[there] = panel;
                        filtered.Add(there);
                    }
                    else
                    {
                        filtered.Add(recipe);
                    }
                }
            }
            if (bySource && benches.Count > 0)
            {
                filtered = InSectionOrder(filtered, baseRecipes);
            }
            data.craftInfo = filtered.ToArray();
        }

        // The words the hand section answers to: its label, and the old one.
        internal const string HandLabel = "By hand";
        private const string HandWords = "by hand basic";

        private static int SourcePanel(CraftInfo recipe)
        {
            return AggregatedCrafting.SourceOf.TryGetValue(recipe, out int panel) ? panel : 0;
        }

        // The panels this list was built from - by hand, the benches and crafters
        // nearby, the one bench opened on its own - whose owner's name holds
        // every term of the query.
        private static HashSet<int> BenchesNamed(ManagersScript mn, int id, CraftInfo[] recipes)
        {
            var named = new HashSet<int>();
            var panels = new HashSet<int>(AggregatedCrafting.LastPanelIDs);
            for (int i = 0; i < recipes.Length; i++)
            {
                if (recipes[i] != null)
                {
                    panels.Add(SourcePanel(recipes[i]));
                }
            }
            if (id != 0)
            {
                panels.Add(id);
            }
            foreach (int panel in panels)
            {
                string owner = panel == 0 ? HandWords : RecipeCatalog.PanelOwnerName(mn, panel);
                if (owner != null && BrowserShell.Filter.MatchesText(owner.ToLowerInvariant()))
                {
                    named.Add(panel);
                }
            }
            return named;
        }

        private static CraftInfo OfferedAt(ManagersScript mn, HashSet<int> panels, string itemName, out int at)
        {
            foreach (int panel in panels)
            {
                if (panel <= 0 || panel >= mn.craftMN.craftData.Length)
                {
                    continue;
                }
                CraftInfo[] offered = mn.craftMN.craftData[panel].craftInfo;
                for (int i = 0; offered != null && i < offered.Length; i++)
                {
                    if (offered[i] != null && offered[i].name == itemName)
                    {
                        at = panel;
                        return offered[i];
                    }
                }
            }
            at = 0;
            return null;
        }

        // The grid draws a header wherever the section changes, so a recipe moved
        // to its bench's section has to sit in it: stable-sort by where each
        // section first appears in the unfiltered list, a section new to it last.
        private static List<CraftInfo> InSectionOrder(List<CraftInfo> recipes, CraftInfo[] order)
        {
            var rank = new Dictionary<int, int>();
            for (int i = 0; i < order.Length; i++)
            {
                if (order[i] != null && !rank.ContainsKey(SourcePanel(order[i])))
                {
                    rank[SourcePanel(order[i])] = i;
                }
            }
            var sorted = new List<CraftInfo>(recipes);
            var at = new Dictionary<CraftInfo, int>();
            for (int i = 0; i < sorted.Count; i++)
            {
                at[sorted[i]] = i;
            }
            sorted.Sort((a, b) =>
            {
                int ra = rank.TryGetValue(SourcePanel(a), out int x) ? x : int.MaxValue;
                int rb = rank.TryGetValue(SourcePanel(b), out int y) ? y : int.MaxValue;
                if (ra != rb) { return ra.CompareTo(rb); }
                int pa = SourcePanel(a), pb = SourcePanel(b);
                if (pa != pb) { return pa.CompareTo(pb); }
                return at[a].CompareTo(at[b]);
            });
            return sorted;
        }

        private static readonly char[] SplitChars = { ' ', '\t' };

        // Lowercased searchable side-text per item: localized tips + the equip-target
        // character names the tooltip generates via RandomCharacter (EquipableNPCCache
        // -> DefaultNaming: "Female Native", "Yona", …). Only NAMED lists are indexed
        // (the tooltip shows "everyone" past 100 targets — indexing that would make a
        // character query match all generic gear). Cached per item, invalidated on
        // language change. EquipableNPCCache is warmed in the background after load
        // (see Plugin.WarmFindItemCache), so this stays cheap even on first search.
        private static readonly Dictionary<string, string> SearchTextCache = new Dictionary<string, string>();
        private static int _searchTextLang = -1;

        private static string SearchTextOf(ManagersScript mn, ItemData item)
        {
            int lang = SaveManager.SaveSettingStatic.lang;
            if (lang != _searchTextLang)
            {
                SearchTextCache.Clear();
                _searchTextLang = lang;
            }
            if (SearchTextCache.TryGetValue(item.name, out string cached))
            {
                return cached;
            }
            string text = item.GetItemTips() ?? "";
            if (mn.randChar != null && mn.randChar.ItemTypeToEquipID(item.itemType) != -1)
            {
                List<int> npcIDs = mn.randChar.EquipableNPCCache(item.name);
                if (npcIDs != null && npcIDs.Count > 0 && npcIDs.Count <= 100)
                {
                    var sb = new System.Text.StringBuilder(text);
                    for (int i = 0; i < npcIDs.Count; i++)
                    {
                        string npcName = "";
                        mn.randChar.DefaultNaming(npcIDs[i], ref npcName);
                        if (npcName.Length > 0)
                        {
                            sb.Append(' ').Append(npcName);
                        }
                    }
                    text = sb.ToString();
                }
            }
            text = text.ToLowerInvariant();
            SearchTextCache[item.name] = text;
            return text;
        }

        // ---------- the search field ----------

        // Keystrokes are debounced: each CraftOpen refill re-runs the whole
        // pipeline (aggregation scan -> filter -> fill), which is far too much per
        // keypress when typing fast. Fire once, shortly after typing pauses.
        private const float RefillDebounce = 0.25f;
        private static float _refillAt = -1f;
        private static int _sawVersion = -1;

        // Called every frame from Plugin.Update. All this still does is notice that
        // the shared query moved and re-run the crafting pipeline behind it -- the
        // field, the keyboard grab, Escape and the focus chord all live in
        // UI.SearchSurface now, because none of them were ever about crafting.
        internal static void Tick(ManagersScript mn)
        {
            if (mn == null || mn.uiMN == null)
            {
                return;
            }
            if (BrowserShell.Filter.Version != _sawVersion)
            {
                _sawVersion = BrowserShell.Filter.Version;
                _refillAt = Time.unscaledTime + RefillDebounce;
            }
            if (_refillAt < 0f || Time.unscaledTime < _refillAt)
            {
                return;
            }
            _refillAt = -1f;
            bool panelOpen = mn.uiMN.craftPanel != null && mn.uiMN.craftPanel.activeSelf;
            if (panelOpen && mn.inventory != null)
            {
                // refill through the normal pipeline (aggregation -> filter ->
                // fill) without resetting an active bench scope
                AggregatedCrafting.Refill(mn.inventory);
            }
        }
    }
}
