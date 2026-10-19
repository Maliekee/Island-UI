using System.Collections.Generic;
using UnityEngine;

namespace IslandUI
{
    // Foundation: the game loads EVERY recipe up front — craftMN.craftData
    // holds all panels' CraftInfo arrays whether or not their bench is anywhere
    // near the player. Proximity was never about data, only about which recipes
    // aggregation shows and which the vanilla flow can execute. This module
    // separates the concerns that used to be conflated:
    //   catalog      — what exists: recipe -> producing panels, item -> producers
    //   reachability — which panels are usable right now (aggregation's set)
    //   craftability — reachable AND materials satisfied (the hammer's business)
    // so any surface (detail card, material previews, browse-all, reverse
    // lookup) can display any recipe without caring what is in reach.
    internal static class RecipeCatalog
    {
        internal sealed class Entry
        {
            public CraftInfo Recipe;
            public readonly List<int> Panels = new List<int>(); // every panel offering it
        }

        private static readonly Dictionary<CraftInfo, Entry> ByRecipe
            = new Dictionary<CraftInfo, Entry>();
        // item key -> recipes producing it (multiple benches can make the same item)
        private static readonly Dictionary<string, List<Entry>> Producers
            = new Dictionary<string, List<Entry>>();
        // the same index the other way round: item key -> recipes that
        // CONSUME it. ProducersOf answers "what makes this"; ConsumersOf answers
        // "what is this for", which is the card's used-in facet. Built in the same
        // pass — every recipe's material list is already in hand, so the reverse
        // direction costs a dictionary rather than a second scan.
        private static readonly Dictionary<string, List<Entry>> Consumers
            = new Dictionary<string, List<Entry>>();
        private static readonly List<Entry> NoEntries = new List<Entry>();

        // scene/content identity: the CraftManager dies with its scene (save load),
        // and panels other than 0 can be rebuilt late (WorkshopItemManager fills
        // panel 25 from workshop content) — either invalidates the catalog.
        // Panel 0 is EXCLUDED from the array watch: our own aggregation swaps it
        // per refill by design; the catalog reads the pristine array instead.
        private static CraftManager _builtFrom;
        private static CraftInfo[][] _seenArrays;

        internal static bool EnsureBuilt(ManagersScript mn)
        {
            if (mn == null || mn.craftMN == null || mn.craftMN.craftData == null)
            {
                return false;
            }
            CraftManager.CraftData[] data = mn.craftMN.craftData;
            if (_builtFrom == mn.craftMN && !PanelsChanged(data))
            {
                return true;
            }

            ByRecipe.Clear();
            Producers.Clear();
            Consumers.Clear();
            _seenArrays = new CraftInfo[data.Length][];
            for (int panel = 0; panel < data.Length; panel++)
            {
                CraftInfo[] recipes = PanelRecipes(data, panel);
                _seenArrays[panel] = recipes;
                if (recipes == null)
                {
                    continue;
                }
                for (int i = 0; i < recipes.Length; i++)
                {
                    CraftInfo recipe = recipes[i];
                    if (recipe == null)
                    {
                        continue;
                    }
                    if (!ByRecipe.TryGetValue(recipe, out Entry entry))
                    {
                        entry = new Entry { Recipe = recipe };
                        ByRecipe[recipe] = entry;
                        if (!Producers.TryGetValue(recipe.name, out List<Entry> list))
                        {
                            Producers[recipe.name] = list = new List<Entry>();
                        }
                        list.Add(entry);
                        IndexMaterials(entry);
                    }
                    if (!entry.Panels.Contains(panel))
                    {
                        entry.Panels.Add(panel);
                    }
                }
            }
            _builtFrom = mn.craftMN;
            Plugin.Log.LogInfo($"RecipeCatalog: {ByRecipe.Count} recipes producing " +
                $"{Producers.Count} distinct items across {data.Length} panels; " +
                $"{Consumers.Count} items are used as materials");
            return true;
        }

        // Every material a recipe consumes points back at it. A recipe that lists
        // the same material twice must still appear once — the used-in facet is a
        // list of recipes, not of requirement lines.
        private static void IndexMaterials(Entry entry)
        {
            CraftInfo.Required[] required = entry.Recipe.required;
            if (required == null)
            {
                return;
            }
            for (int i = 0; i < required.Length; i++)
            {
                ItemData material = required[i] != null ? required[i].itemData : null;
                if (material == null)
                {
                    continue;
                }
                if (!Consumers.TryGetValue(material.name, out List<Entry> list))
                {
                    Consumers[material.name] = list = new List<Entry>();
                }
                if (!list.Contains(entry))
                {
                    list.Add(entry);
                }
            }
        }

        private static CraftInfo[] PanelRecipes(CraftManager.CraftData[] data, int panel)
        {
            if (data[panel] == null)
            {
                return null;
            }
            // panel 0 is mutated by aggregation/search/scope per refill — the
            // catalog wants the innate recipes as authored
            if (panel == 0)
            {
                return AggregatedCrafting.PristineInnate ?? data[0].craftInfo;
            }
            return data[panel].craftInfo;
        }

        private static bool PanelsChanged(CraftManager.CraftData[] data)
        {
            if (_seenArrays == null || _seenArrays.Length != data.Length)
            {
                return true;
            }
            for (int panel = 1; panel < data.Length; panel++)
            {
                if (_seenArrays[panel] != (data[panel] != null ? data[panel].craftInfo : null))
                {
                    return true;
                }
            }
            return false;
        }

        // ---------- lookups ----------

        internal static Entry Of(CraftInfo recipe)
        {
            return recipe != null && ByRecipe.TryGetValue(recipe, out Entry entry) ? entry : null;
        }

        internal static IReadOnlyList<Entry> ProducersOf(string itemKey)
        {
            return itemKey != null && Producers.TryGetValue(itemKey, out List<Entry> list)
                ? list : (IReadOnlyList<Entry>)NoEntries;
        }

        // every recipe this item is a material for.
        internal static IReadOnlyList<Entry> ConsumersOf(string itemKey)
        {
            return itemKey != null && Consumers.TryGetValue(itemKey, out List<Entry> list)
                ? list : (IReadOnlyList<Entry>)NoEntries;
        }

        // ---------- reachability ----------

        internal static bool IsReachable(Entry entry)
        {
            if (entry == null)
            {
                return true; // unknown recipe: never block on missing catalog data
            }
            for (int i = 0; i < entry.Panels.Count; i++)
            {
                if (AggregatedCrafting.PanelReachable(entry.Panels[i]))
                {
                    return true;
                }
            }
            return false;
        }

        // does anything IN REACH produce this item? (the material-row hint)
        internal static bool HasReachableProducer(string itemKey)
        {
            IReadOnlyList<Entry> producers = ProducersOf(itemKey);
            for (int i = 0; i < producers.Count; i++)
            {
                if (IsReachable(producers[i]))
                {
                    return true;
                }
            }
            return false;
        }

        // the panel a surface should attribute the recipe to: a reachable one
        // first (matches what the player can act on), else the first bench/NPC
        // panel (what they WOULD need), else innate
        internal static int DisplayPanel(Entry entry)
        {
            if (entry == null)
            {
                return 0;
            }
            for (int i = 0; i < entry.Panels.Count; i++)
            {
                if (AggregatedCrafting.PanelReachable(entry.Panels[i]))
                {
                    return entry.Panels[i];
                }
            }
            for (int i = 0; i < entry.Panels.Count; i++)
            {
                if (entry.Panels[i] != 0)
                {
                    return entry.Panels[i];
                }
            }
            return 0;
        }

        // ---------- panel identity (shared by bench rows / requirement lines) ----------

        // human name of what offers a panel: the bench item's localized name, the
        // crafter NPC's name, or null for innate/unknown
        internal static string PanelOwnerName(ManagersScript mn, int panelID)
        {
            if (panelID == 0 || mn == null)
            {
                return null;
            }
            if (AggregatedCrafting.NpcOfPanel.TryGetValue(panelID, out int npcID))
            {
                StatsData stats = mn.npcMN != null ? mn.npcMN.GetStatsData(npcID) : null;
                return stats != null ? stats.GetLocalizedCharaName() : $"NPC {npcID}";
            }
            string benchKey = AggregatedCrafting.BenchKeyOf(panelID);
            ItemData bench = benchKey != null ? mn.itemMN.FindItem(benchKey) : null;
            return bench != null ? bench.GetItemName() : $"#{panelID}";
        }

        internal static Sprite PanelOwnerIcon(ManagersScript mn, int panelID)
        {
            if (panelID == 0 || mn == null
                || AggregatedCrafting.NpcOfPanel.ContainsKey(panelID))
            {
                return null;
            }
            string benchKey = AggregatedCrafting.BenchKeyOf(panelID);
            ItemData bench = benchKey != null ? mn.itemMN.FindItem(benchKey) : null;
            return bench != null ? bench.itemSprite : null;
        }

        internal static bool IsNpcPanel(int panelID)
        {
            return AggregatedCrafting.NpcOfPanel.ContainsKey(panelID);
        }
    }
}
