using System.Collections.Generic;
using IslandUI.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI
{
    // WI-7: ONE card adapter, not three renderers that happen to agree.
    //
    // The catalogue card, the chain cards and the slot-hover card were built
    // separately and each grew its own copy of "icon, name, a stack of stat rows,
    // a measured description". They render the same FACETS of the same item, so
    // the facets live here and each window composes them.
    //
    // The rule the split follows (§9.2):
    //   Facets are informational and belong to the ITEM.
    //   Actions belong to the WINDOW you are standing in.
    // ...with the amendment that a facet whose question the CONTEXT has
    // already answered is noise: the STOCK facet ("which chests hold this") is the
    // whole question in the browser and pure clutter in the Inventory, where the
    // item is in the slot under your cursor. So unification is of the CARD, not of
    // what every context has reason to show — the stock facet stays with the
    // reading window, and lives in StockFacet rather than here.
    internal static class CardFacets
    {
        internal const float StatRowH = 10f;
        // the label gutter every stat row shares. Internal because the card draws
        // one row of its own (used-in) that has to line up with them.
        internal const float LabelW = 46f;
        private const float IconW = 9f;

        // A pooled column of "label   [icon] value" lines. Rows are positioned per
        // render rather than laid out once, because which facets an item HAS
        // decides how many there are.
        internal sealed class StatPool
        {
            private sealed class Row
            {
                public GameObject Go;
                public TextMeshProUGUI Label;
                public Image Icon;
                public TextMeshProUGUI Value;
            }

            private readonly List<Row> _rows = new List<Row>();
            private int _cursor;

            internal StatPool(Transform content, float width, int rows)
            {
                for (int i = 0; i < rows; i++)
                {
                    var row = new Row();
                    row.Go = Owned.New("Stat");
                    row.Go.transform.SetParent(content, false);
                    Theme.Place(row.Go.GetComponent<RectTransform>(), 0f, 0f, width, StatRowH);
                    row.Label = Theme.MakeText(row.Go.transform, "Label",
                        Theme.FontCaption, Theme.TextDim, TextAlignmentOptions.MidlineLeft);
                    row.Label.enableWordWrapping = false;
                    Theme.Place(row.Label, 0f, 0f, LabelW, StatRowH);
                    var iconGo = Owned.New("Icon");
                    iconGo.transform.SetParent(row.Go.transform, false);
                    row.Icon = iconGo.AddComponent<Image>();
                    row.Icon.raycastTarget = false;
                    row.Icon.preserveAspect = true;
                    Theme.Place(row.Icon, LabelW, 0.5f, IconW, IconW);
                    row.Value = Theme.MakeText(row.Go.transform, "Value",
                        Theme.FontCaption, Theme.TextCream, TextAlignmentOptions.MidlineLeft);
                    row.Value.enableWordWrapping = false;
                    Theme.Place(row.Value, LabelW, 0f, width - LabelW, StatRowH);
                    row.Go.SetActive(false);
                    _rows.Add(row);
                }
            }

            internal void Begin()
            {
                _cursor = 0;
            }

            // Silently returning past the end of the pool does not clip a card, it
            // makes whole facets fail to appear with nothing in the log to say so.
            internal float Set(float y, string label, string value, Color valueColor,
                Sprite icon = null)
            {
                if (_cursor >= _rows.Count)
                {
                    Plugin.Log.LogWarning($"Card: stat pool exhausted, '{label}' dropped");
                    return y;
                }
                Row row = _rows[_cursor++];
                row.Go.SetActive(true);
                ((RectTransform)row.Go.transform).anchoredPosition = new Vector2(0f, -y);
                row.Label.text = label;
                Theme.FitLanguage(row.Label, Theme.FontCaption);
                row.Value.text = value;
                row.Value.color = valueColor;
                row.Icon.enabled = icon != null;
                row.Icon.sprite = icon;
                // pooled rows are reused: normalize back to a single line (a prior
                // render may have left this row in wrapped mode)
                row.Value.enableWordWrapping = false;
                Theme.FitLanguage(row.Value, Theme.FontCaption);
                var valueRect = (RectTransform)row.Value.transform;
                valueRect.sizeDelta = new Vector2(valueRect.sizeDelta.x, StatRowH);
                // icon (when present) sits between label and value
                valueRect.anchoredPosition = new Vector2(icon != null ? LabelW + 12f : LabelW, 0f);
                return y + StatRowH;
            }

            // like Set, but the value wraps over multiple lines and the row grows
            internal float SetWrapped(float y, string label, string value, Color valueColor,
                float maxHeight = 60f)
            {
                if (_cursor >= _rows.Count)
                {
                    Plugin.Log.LogWarning($"Card: stat pool exhausted, '{label}' dropped");
                    return y;
                }
                Row row = _rows[_cursor];
                Set(y, label, value, valueColor);
                row.Value.enableWordWrapping = true;
                // a wrapped value grows its row instead of shrinking
                row.Value.enableAutoSizing = false;
                row.Value.fontSize = Theme.FontCaption;
                var valueRect = (RectTransform)row.Value.transform;
                float width = valueRect.sizeDelta.x;
                float h = Mathf.Clamp(row.Value.GetPreferredValues(value, width, 0f).y + 1f,
                    StatRowH, maxHeight); // roomy: the summarized string must never clip
                valueRect.sizeDelta = new Vector2(width, h);
                var rowRect = (RectTransform)row.Go.transform;
                rowRect.sizeDelta = new Vector2(rowRect.sizeDelta.x, h);
                return y + h;
            }

            internal void HideRest()
            {
                for (int i = _cursor; i < _rows.Count; i++)
                {
                    if (_rows[i].Go.activeSelf)
                    {
                        _rows[i].Go.SetActive(false);
                    }
                }
            }
        }

        // The PROPERTIES facet — what the item IS. Every card shows it in every
        // window, because it is the item's own data and no context answers it for
        // you. `recipe` is optional: when one is given, the bench/crafter
        // attribution rides along, since that is what the shown recipe belongs to.
        internal static float RenderProperties(ManagersScript mn, ItemData item,
            CraftInfo recipe, StatPool stats, float y, out int panelID)
        {
            panelID = 0;
            y = stats.Set(y, Shared.Loc.Get("card.class", "Class"), FriendlyType(item), Theme.TextCream);
            if (recipe != null)
            {
                // how it entered the current view when it did (SourceOf), the
                // catalog's preferred panel otherwise
                if (!AggregatedCrafting.SourceOf.TryGetValue(recipe, out panelID))
                {
                    panelID = RecipeCatalog.DisplayPanel(RecipeCatalog.Of(recipe));
                }
                y = panelID != 0
                    ? stats.Set(y, RecipeCatalog.IsNpcPanel(panelID)
                        ? Shared.Loc.Get("card.crafter", "Crafter") : Shared.Loc.Get("card.bench", "Bench"),
                        RecipeCatalog.PanelOwnerName(mn, panelID), Theme.TextCream,
                        RecipeCatalog.PanelOwnerIcon(mn, panelID))
                    : stats.Set(y, Shared.Loc.Get("card.bench", "Bench"),
                        Shared.Loc.Get("card.bench.none", "None (by hand)"), Theme.TextDim);
            }
            if (item.attack > 0f)
            {
                y = stats.Set(y, Shared.Loc.Get("card.attack", "Attack"), item.attack.ToString("0.#"), Theme.TextCream);
            }
            if (item.itemLife > 0f)
            {
                y = stats.Set(y, Shared.Loc.Get("card.durability", "Durability"), item.itemLife.ToString("0.#"), Theme.TextCream);
            }
            FoodInfo food = item.GetComponent<FoodInfo>();
            if (food != null && (food.food != 0f || food.water != 0f || food.life != 0f))
            {
                var parts = new List<string>();
                if (food.food != 0f) parts.Add(Shared.Loc.Get("card.use.food", "Food {0}", food.food.ToString("+0.#;-0.#")));
                if (food.water != 0f) parts.Add(Shared.Loc.Get("card.use.water", "Water {0}", food.water.ToString("+0.#;-0.#")));
                if (food.life != 0f) parts.Add(Shared.Loc.Get("card.use.hp", "HP {0}", food.life.ToString("+0.#;-0.#")));
                y = stats.Set(y, Shared.Loc.Get("card.use", "Use"), string.Join("  ", parts), Theme.Positive);
            }
            string equip = ItemTags.EquipTargetNames(mn, item);
            if (equip.Length > 0)
            {
                y = stats.SetWrapped(y, Shared.Loc.Get("card.equip", "Equip"), ItemTags.SummarizeNames(equip, 6), Theme.TextCream);
            }
            return y;
        }

        // HOW TO GET IT (ItemGuide), behind [Card] ItemGuide: one line per way
        internal static float RenderGuide(ItemData item, StatPool stats, float y)
        {
            if (!Settings.ItemGuide.Value || item == null)
            {
                return y;
            }
            string how = IslandUI.ItemGuide.Of(item.name);
            // a guide entry can run to several ways, each its own line: room for them
            return how == null ? y : stats.SetWrapped(y, Shared.Loc.Get("card.get", "Get"), how, Theme.TextCream, 140f);
        }

        // the item's own tip text, measured — the card grows around it
        internal static float RenderDescription(ItemData item, TextMeshProUGUI description,
            float width, float y)
        {
            string tips = (item.GetItemTips() ?? "").Replace("/n", "\n").Trim();
            description.gameObject.SetActive(tips.Length > 0);
            if (tips.Length == 0)
            {
                return y;
            }
            y += 2f;
            description.text = tips;
            // generous cap: the card shrink-wraps, so tall descriptions just grow
            // it; the cap only guards against wall-of-text
            float height = Mathf.Min(description.GetPreferredValues(tips, width, 0f).y + 1f, 90f);
            Theme.Place(description, 0f, y, width, height);
            return y + height + 3f;
        }

        internal static string FriendlyType(ItemData item)
        {
            string[] tags = ItemTags.TagsOf(item);
            string type = tags.Length > 0 ? TagWord(tags[0]) : item.itemType.ToString();
            return type.Length > 0 ? char.ToUpperInvariant(type[0]) + type.Substring(1) : type;
        }

        // ItemTags' words are search terms, in English; this is the one the card shows
        private static string TagWord(string tag)
        {
            switch (tag)
            {
                case "accessory": return Shared.Loc.Get("card.type.accessory", "Accessory");
                case "ammo": return Shared.Loc.Get("card.type.ammo", "Ammo");
                case "animal": return Shared.Loc.Get("card.type.animal", "Animal");
                case "armor": return Shared.Loc.Get("card.type.armor", "Armor");
                case "arrow": return Shared.Loc.Get("card.type.arrow", "Arrow");
                case "axe": return Shared.Loc.Get("card.type.axe", "Axe");
                case "bait": return Shared.Loc.Get("card.type.bait", "Bait");
                case "bed": return Shared.Loc.Get("card.type.bed", "Bed");
                case "bottle": return Shared.Loc.Get("card.type.bottle", "Bottle");
                case "bow": return Shared.Loc.Get("card.type.bow", "Bow");
                case "building": return Shared.Loc.Get("card.type.building", "Building");
                case "chest": return Shared.Loc.Get("card.type.chest", "Chest");
                case "cloth": return Shared.Loc.Get("card.type.cloth", "Cloth");
                case "clothing": return Shared.Loc.Get("card.type.clothing", "Clothing");
                case "consumable": return Shared.Loc.Get("card.type.consumable", "Consumable");
                case "cooking": return Shared.Loc.Get("card.type.cooking", "Cooking");
                case "decoration": return Shared.Loc.Get("card.type.decoration", "Decoration");
                case "drink": return Shared.Loc.Get("card.type.drink", "Drink");
                case "equipment": return Shared.Loc.Get("card.type.equipment", "Equipment");
                case "feed": return Shared.Loc.Get("card.type.feed", "Feed");
                case "fish": return Shared.Loc.Get("card.type.fish", "Fish");
                case "fishing": return Shared.Loc.Get("card.type.fishing", "Fishing");
                case "food": return Shared.Loc.Get("card.type.food", "Food");
                case "hat": return Shared.Loc.Get("card.type.hat", "Hat");
                case "knife": return Shared.Loc.Get("card.type.knife", "Knife");
                case "light": return Shared.Loc.Get("card.type.light", "Light");
                case "material": return Shared.Loc.Get("card.type.material", "Material");
                case "melee": return Shared.Loc.Get("card.type.melee", "Melee");
                case "net": return Shared.Loc.Get("card.type.net", "Net");
                case "npc": return Shared.Loc.Get("card.type.npc", "Npc");
                case "pickaxe": return Shared.Loc.Get("card.type.pickaxe", "Pickaxe");
                case "plant": return Shared.Loc.Get("card.type.plant", "Plant");
                case "prop": return Shared.Loc.Get("card.type.prop", "Prop");
                case "ranged": return Shared.Loc.Get("card.type.ranged", "Ranged");
                case "resource": return Shared.Loc.Get("card.type.resource", "Resource");
                case "rod": return Shared.Loc.Get("card.type.rod", "Rod");
                case "shield": return Shared.Loc.Get("card.type.shield", "Shield");
                case "shoes": return Shared.Loc.Get("card.type.shoes", "Shoes");
                case "shovel": return Shared.Loc.Get("card.type.shovel", "Shovel");
                case "storage": return Shared.Loc.Get("card.type.storage", "Storage");
                case "stun": return Shared.Loc.Get("card.type.stun", "Stun");
                case "support": return Shared.Loc.Get("card.type.support", "Support");
                case "throwing": return Shared.Loc.Get("card.type.throwing", "Throwing");
                case "tool": return Shared.Loc.Get("card.type.tool", "Tool");
                case "torch": return Shared.Loc.Get("card.type.torch", "Torch");
                case "underwear": return Shared.Loc.Get("card.type.underwear", "Underwear");
                case "weapon": return Shared.Loc.Get("card.type.weapon", "Weapon");
                default: return tag;
            }
        }
    }
}
