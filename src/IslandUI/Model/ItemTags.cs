using System.Collections.Generic;

namespace IslandUI
{
    // Feature: derived tag index for craftable items, so search finds "weapon",
    // "tool", "food", "storage"… — categories no display name contains.
    //
    // Tags are auto-derived from the game's own item classification (ItemType,
    // SubType, ToolType enums, the attack stat, the deco flag and key prefixes) —
    // no curated table to maintain, new game items tag themselves. The raw enum
    // names are normalized to natural search words (ClothU -> underwear, Pickax ->
    // pickaxe, Acce -> accessory) with umbrella tags layered on top (any attack
    // item -> weapon, all wearables -> equipment/clothing).
    //
    // Tags are English regardless of the game language (the enums have no
    // localization); localized DISPLAY-name search is unaffected and lives in
    // BrowserSearch. Cached by item key — values are static game data.
    internal static class ItemTags
    {
        private static readonly Dictionary<string, string[]> Cache = new Dictionary<string, string[]>();

        internal static bool Matches(ItemData item, string lowerQuery)
        {
            string[] tags = TagsOf(item);
            for (int i = 0; i < tags.Length; i++)
            {
                if (tags[i].Contains(lowerQuery))
                {
                    return true;
                }
            }
            return false;
        }

        internal static string[] TagsOf(ItemData item)
        {
            if (Cache.TryGetValue(item.name, out string[] cached))
            {
                return cached;
            }
            var tags = new HashSet<string>();

            switch (item.itemType)
            {
                case ItemData.ItemType.Resource:
                case ItemData.ItemType.ResourceW:
                    tags.Add("resource");
                    tags.Add("material");
                    break;
                case ItemData.ItemType.Tool:
                case ItemData.ItemType.Tool2:
                    tags.Add("tool");
                    tags.Add("equipment");
                    break;
                case ItemData.ItemType.Prop:
                    tags.Add("building");
                    tags.Add("prop");
                    break;
                case ItemData.ItemType.Hat:
                    tags.Add("hat");
                    Wearable(tags);
                    break;
                case ItemData.ItemType.Cloth:
                case ItemData.ItemType.ClothU:
                    if (item.itemType == ItemData.ItemType.ClothU)
                    {
                        tags.Add("underwear");
                    }
                    tags.Add("cloth");
                    Wearable(tags);
                    break;
                case ItemData.ItemType.Hip:
                    tags.Add("underwear");
                    Wearable(tags);
                    break;
                case ItemData.ItemType.Acce:
                    tags.Add("accessory");
                    Wearable(tags);
                    break;
                case ItemData.ItemType.Shoes:
                    tags.Add("shoes");
                    Wearable(tags);
                    break;
                case ItemData.ItemType.Food:
                    tags.Add("food");
                    break;
                case ItemData.ItemType.Bottle:
                    tags.Add("bottle");
                    tags.Add("drink");
                    break;
                case ItemData.ItemType.Animal:
                    tags.Add("animal");
                    break;
                case ItemData.ItemType.NPC:
                    tags.Add("npc");
                    break;
                case ItemData.ItemType.Shield:
                    tags.Add("shield");
                    tags.Add("equipment");
                    break;
                case ItemData.ItemType.Consumables:
                    tags.Add("consumable");
                    break;
                case ItemData.ItemType.Support:
                    tags.Add("support");
                    break;
            }

            switch (item.subType)
            {
                case ItemData.SubType.Chest:
                    tags.Add("storage");
                    tags.Add("chest");
                    break;
                case ItemData.SubType.Bait:
                    tags.Add("bait");
                    tags.Add("fishing");
                    break;
                case ItemData.SubType.Arrow:
                    tags.Add("arrow");
                    tags.Add("ammo");
                    break;
                case ItemData.SubType.FeedSmall:
                    tags.Add("feed");
                    break;
                case ItemData.SubType.Bed:
                    tags.Add("bed");
                    break;
                case ItemData.SubType.Stock:
                    tags.Add("storage");
                    break;
                case ItemData.SubType.Cook:
                    tags.Add("cooking");
                    break;
            }

            switch (item.toolType)
            {
                case ItemData.ToolType.Axe:
                case ItemData.ToolType.Axe2:
                    tags.Add("axe");
                    tags.Add("tool");
                    break;
                case ItemData.ToolType.Pickax:
                case ItemData.ToolType.Pickax2:
                case ItemData.ToolType.Picknaxe:
                    tags.Add("pickaxe");
                    tags.Add("tool");
                    break;
                case ItemData.ToolType.FishingRod:
                    tags.Add("fishing");
                    tags.Add("rod");
                    tags.Add("tool");
                    break;
                case ItemData.ToolType.Shovel:
                    tags.Add("shovel");
                    tags.Add("tool");
                    break;
                case ItemData.ToolType.Net:
                    tags.Add("net");
                    tags.Add("tool");
                    break;
                case ItemData.ToolType.Torch:
                    tags.Add("torch");
                    tags.Add("light");
                    break;
                case ItemData.ToolType.Bow:
                    tags.Add("bow");
                    tags.Add("ranged");
                    tags.Add("weapon");
                    break;
                case ItemData.ToolType.Throwing:
                    tags.Add("throwing");
                    tags.Add("ranged");
                    tags.Add("weapon");
                    break;
                case ItemData.ToolType.Knife:
                    tags.Add("knife");
                    tags.Add("weapon");
                    break;
                case ItemData.ToolType.Stun:
                    tags.Add("stun");
                    tags.Add("weapon");
                    break;
                case ItemData.ToolType.Weapon:
                    tags.Add("melee");
                    tags.Add("weapon");
                    break;
                case ItemData.ToolType.Shield:
                    tags.Add("shield");
                    break;
                case ItemData.ToolType.Plant:
                    tags.Add("plant");
                    break;
                case ItemData.ToolType.Fish:
                    tags.Add("fish");
                    break;
                case ItemData.ToolType.Cook:
                    tags.Add("cooking");
                    break;
            }

            // anything that hits counts as a weapon (swords, spears, bows, thrown…);
            // wp_ is the game's weapon prefab key prefix
            if (item.attack > 0f || item.name.StartsWith("wp_"))
            {
                tags.Add("weapon");
                tags.Add("equipment");
            }
            if (item.deco)
            {
                tags.Add("decoration");
            }

            string[] result = new string[tags.Count];
            tags.CopyTo(result);
            Cache[item.name] = result;
            return result;
        }

        private static void Wearable(HashSet<string> tags)
        {
            tags.Add("clothing");
            tags.Add("equipment");
            tags.Add("armor");
        }

        // ---------- equip-target names (for the detail window) ----------

        // "Female Native, Yona" — the same generated association the vanilla tooltip
        // shows (EquipableNPCCache -> DefaultNaming); empty for non-equipables and
        // for "everyone" gear (>100 targets). Cached per item, language-invalidated.
        private static readonly Dictionary<string, string> EquipNamesCache = new Dictionary<string, string>();
        private static int _equipNamesLang = -1;

        // display form of a ", "-joined name list: first maxNames, then "+K more"
        // (deterministic height for the cards — no mid-word ellipsis, no dead gap)
        internal static string SummarizeNames(string names, int maxNames)
        {
            string[] parts = names.Split(new[] { ", " }, System.StringSplitOptions.None);
            if (parts.Length <= maxNames)
            {
                return names;
            }
            return string.Join(", ", parts, 0, maxNames)
                + $" +{parts.Length - maxNames} more";
        }

        internal static string EquipTargetNames(ManagersScript mn, ItemData item)
        {
            int lang = SaveManager.SaveSettingStatic.lang;
            if (lang != _equipNamesLang)
            {
                EquipNamesCache.Clear();
                _equipNamesLang = lang;
            }
            if (EquipNamesCache.TryGetValue(item.name, out string cached))
            {
                return cached;
            }
            string result = "";
            if (mn.randChar != null && mn.randChar.ItemTypeToEquipID(item.itemType) != -1)
            {
                List<int> npcIDs = mn.randChar.EquipableNPCCache(item.name);
                if (npcIDs != null && npcIDs.Count > 0 && npcIDs.Count <= 100)
                {
                    var sb = new System.Text.StringBuilder();
                    for (int i = 0; i < npcIDs.Count; i++)
                    {
                        string npcName = "";
                        mn.randChar.DefaultNaming(npcIDs[i], ref npcName);
                        if (npcName.Length > 0)
                        {
                            if (sb.Length > 0)
                            {
                                sb.Append(", ");
                            }
                            sb.Append(npcName);
                        }
                    }
                    result = sb.ToString();
                }
            }
            EquipNamesCache[item.name] = result;
            return result;
        }
    }
}
