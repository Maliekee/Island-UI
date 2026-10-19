using HarmonyLib;
using IslandUI.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI
{
    // Feature: item hovers in the inventory/chests/equip render through the
    // Island UI detail card (the crafting menu's preview) instead of the vanilla
    // itemDescPanel. Positioned like the vanilla tooltip — at the pointer, with
    // the vanilla panel's own pivot — then clamped to the screen.
    //
    // Vanilla keeps two slot families we don't replicate: craft-grid slots
    // (100-999, need-panel machinery) and shop slots (1000+, price display). A
    // slot holding a character keeps vanilla's tooltip in VANILLA's slots and gets
    // the character card (RenderCharacter) in ours, where vanilla's is never reached.
    //
    // WI-7: the same card as the browser's, composed from the shared facets in
    // CardFacets — but NOT the same facets. It shows properties and description
    // and stops there: the STOCK facet is gated to the reading window, because in
    // the Inventory the item is already in the slot under your cursor and listing
    // where else it lives turns a quick "what is this" into a research panel
    // (§9.2). It carries no actions either — here a click already means "move
    // this item", and a card with buttons would put two meanings on one click.
    [HarmonyPatch(typeof(UIManager))]
    internal static class HoverCard
    {
        private const float Width = 150f;
        // a carried person's seven readings plus one row per favourite food
        private const int MaxStatRows = 20;
        // How far the card stands off the hovered cell (too close, it hides what is
        // around the item you are hovering) - about half a cell of clear air, in canvas units.
        private const float CardGap = 14f;

        private static readonly AccessTools.FieldRef<UIManager, ManagersScript> MnRef =
            AccessTools.FieldRefAccess<UIManager, ManagersScript>("mn");

        private static UIWindow _window;

        private static ShedTag _tag;   // the tool shed's manila tag
        private static GameObject _sourceSlot; // the slot that opened the card
        // Driven by OUR window rather than by a vanilla hover event. The card was
        // built as a patch on `UIManager.ItemDescOpen`, and vanilla calls that from
        // one place only — a real `ItemSlot`'s own pointer handler (ItemSlot.cs:270).
        // WI-4 then replaced the panel this decorated with the Inventory window,
        // whose cells are ours and whose input is polled, so nothing has called
        // ItemDescOpen over an inventory slot since. The feature did not break; its
        // call site was removed out from under it, which is why it read as "gone"
        // rather than as "wrong".
        //
        // In external mode the WINDOW owns the lifetime: it says show and it says
        // hide, so the two vanilla-shaped auto-hides below must stand down.
        private static bool _external;
        private static string _externalKey;
        private static Image _icon;
        private static TextMeshProUGUI _name;
        private static TextMeshProUGUI _description;
        private static CardFacets.StatPool _stats;

        [HarmonyPatch(nameof(UIManager.ItemDescOpen))]
        [HarmonyPrefix]
        private static bool ItemDescOpen_Prefix(UIManager __instance, int slotID)
        {
            if (!Settings.CustomItemTooltip.Value)
            {
                return true;
            }
            ManagersScript mn = MnRef(__instance);
            if (mn == null || mn.inventory == null || mn.itemMN == null
                || slotID >= 100 || slotID < 0 || slotID >= mn.inventory.itemSlot.Length)
            {
                return true; // craft grid / shop / out of range: vanilla
            }
            ItemSlot slot = mn.inventory.itemSlot[slotID];
            if (slot == null || string.IsNullOrEmpty(slot.itemKey) || slot.common != null)
            {
                return true; // characters keep vanilla's status suffixes
            }
            ItemData item = mn.itemMN.FindItem(slot.itemKey);
            if (item == null || !EnsureBuilt(mn, __instance))
            {
                return true;
            }
            Render(mn, item);
            Position(__instance);
            _external = false;
            _sourceSlot = slot.gameObject;
            return false; // vanilla panel stays hidden
        }

        // ---------- driven by the Inventory window ----------

        // Same gating as the prefix, minus the slot-id families: our window shows
        // real slots but never the craft grid or a shop. A slot holding a CHARACTER
        // gets the character's card (an NPC carried in the inventory showed nothing
        // on hover) - vanilla's tooltip, which said who they are and their state,
        // was never reached from our cells, and the prefix left them to it.
        //
        // Re-rendered only when the item under the pointer changes, so the card
        // parks where you entered rather than trailing the cursor — which is what
        // the vanilla tooltip does, and this has to sit beside vanilla slots that
        // still behave that way.
        internal static void ShowForSlot(ManagersScript mn, UIManager ui, ItemSlot slot, RectTransform cell = null)
        {
            if (!Settings.CustomItemTooltip.Value || mn == null || ui == null
                || mn.itemMN == null || slot == null
                || string.IsNullOrEmpty(slot.itemKey))
            {
                HideExternal();
                return;
            }
            if (!EnsureBuilt(mn, ui))
            {
                return;
            }
            _external = true;
            _recipeOwned = false;
            // never the vanilla auto-hide's business while we own it
            _sourceSlot = null;
            // two carried people share an item key and are not the same card
            string key = slot.common != null ? slot.itemKey + "#" + slot.common.GetInstanceID() : slot.itemKey;
            if (key == _externalKey)
            {
                // the same item in the next cell: it stays, but beside THAT cell
                if (cell != null && !ReferenceEquals(cell, _externalCell))
                {
                    _externalCell = cell;
                    PlaceBeside(cell);
                }
                return;
            }
            ItemData item = mn.itemMN.FindItem(slot.itemKey);
            if (item == null)
            {
                HideExternal();
                return;
            }
            _externalKey = key;
            _externalCell = cell;
            if (slot.common != null)
            {
                RenderCharacter(mn, item, slot.common);
            }
            else
            {
                Render(mn, item);
            }
            if (cell != null)
            {
                PlaceBeside(cell);
            }
            else
            {
                Position(ui);
            }
        }

        // An item that sits in no slot: a material or a used-in product on the recipe
        // card shows the normal item hover. The same card, beside that row.
        internal static void ShowForItem(ManagersScript mn, UIManager ui, string itemKey, RectTransform cell)
        {
            if (!Settings.CustomItemTooltip.Value || mn == null || ui == null || mn.itemMN == null
                || string.IsNullOrEmpty(itemKey) || cell == null)
            {
                HideExternal();
                return;
            }
            if (!EnsureBuilt(mn, ui))
            {
                return;
            }
            _external = true;
            _recipeOwned = true;
            _sourceSlot = null;
            string key = "recipe:" + itemKey;
            if (key == _externalKey)
            {
                if (!ReferenceEquals(cell, _externalCell))
                {
                    _externalCell = cell;
                    PlaceBeside(cell);
                }
                return;
            }
            ItemData item = mn.itemMN.FindItem(itemKey);
            if (item == null)
            {
                HideExternal();
                return;
            }
            _externalKey = key;
            _externalCell = cell;
            Render(mn, item);
            PlaceBeside(cell);
        }

        private static RectTransform _externalCell;
        private static readonly Vector3[] CellCorners = new Vector3[4];

        // BESIDE THE CELL, NEVER OVER IT (the card slightly overlapped the hovered
        // item). Hung from the pointer the card's corner sat inside the cell the pointer
        // was in; our window knows the cell, so the card stands clear of it - to
        // its right, or to its left where the screen runs out, top-aligned with it.
        private static void PlaceBeside(RectTransform cell)
        {
            RectTransform rect = _window.Rect;
            cell.GetWorldCorners(CellCorners);   // screen pixels: the canvas is an overlay
            Vector3 scale = rect.lossyScale;
            float w = rect.sizeDelta.x * scale.x;
            float h = rect.sizeDelta.y * scale.y;
            float gap = CardGap * scale.x;
            rect.pivot = new Vector2(0f, 1f);
            float x = CellCorners[2].x + gap;
            if (x + w > Screen.width)
            {
                x = CellCorners[1].x - gap - w;
            }
            x = Mathf.Clamp(x, 0f, Mathf.Max(0f, Screen.width - w));
            float y = Mathf.Clamp(CellCorners[1].y, Mathf.Min(h, Screen.height), Screen.height);
            rect.position = new Vector3(x, y, rect.position.z);
        }

        // Only ever closes a card WE opened: a vanilla hover happening to be up is
        // not ours to take down. And only the side that raised it: the Inventory
        // hides its card every frame it hovers no cell, which took the recipe card's
        // hover down the frame it went up.
        private static bool _recipeOwned;

        internal static void HideExternal()
        {
            if (_external && !_recipeOwned)
            {
                Hide();
            }
        }

        internal static void HideRecipeHover()
        {
            if (_external && _recipeOwned)
            {
                Hide();
            }
        }

        [HarmonyPatch(nameof(UIManager.ItemDescClose))]
        [HarmonyPostfix]
        private static void ItemDescClose_Postfix()
        {
            Hide();
        }

        // Vanilla's tooltip cleanup lives in OnUICheck: pointer off ALL UI ->
        // ItemDescClose — but it's gated on itemDescPanel.activeSelf, which our
        // prefix keeps false, so the vanilla path never sees our card. Mirror it
        // here, and also drop the card when the slot that opened it went away
        // (closing a chest under a motionless pointer fires no exit event).
        [HarmonyPatch(typeof(GameManager), nameof(GameManager.OnUICheck))]
        [HarmonyPostfix]
        private static void OnUICheck_Postfix(GameManager __instance)
        {
            if (_window == null || _window.Go == null || !_window.Go.activeSelf || _external)
            {
                return;   // external: the Inventory window hides its own card
            }
            if (!__instance.OnUI || _sourceSlot == null || !_sourceSlot.activeInHierarchy)
            {
                Hide();
            }
        }

        private static void Hide()
        {
            _sourceSlot = null;
            _external = false;
            _recipeOwned = false;
            _externalKey = null;
            _externalCell = null;
            if (_window != null && _window.Go != null && _window.Go.activeSelf)
            {
                _window.Go.SetActive(false);
            }
        }

        // ---------- construction ----------

        private static bool EnsureBuilt(ManagersScript mn, UIManager ui)
        {
            if (_window != null && _window.Go != null)
            {
                return true;
            }
            if (ui.itemDescPanel == null || !Theme.Ensure(mn, mn.inventory))
            {
                return false;
            }
            Transform parent = ui.itemDescPanel.transform.parent;
            _window = UIWindow.Create(parent, "HoverCard", Width, 96f, darkCard: true);
            _tag = new ShedTag(_window.Go);
            // a tooltip must never catch the pointer — hover exit would close it
            var group = _window.Go.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            // own canvas island: hover on/off must not rebatch the main canvas
            CanvasIsolation.Isolate(_window.Go);

            Transform content = _window.Content;
            float w = _window.Content.sizeDelta.x;

            var boxGo = Owned.New("IconBox");
            boxGo.transform.SetParent(content, false);
            var box = boxGo.AddComponent<Image>();
            box.sprite = Theme.SlotFrame;
            box.raycastTarget = false;
            Theme.Place(box, 0f, 0f, 22f, 22f);
            var iconGo = Owned.New("Icon");
            iconGo.transform.SetParent(boxGo.transform, false);
            _icon = iconGo.AddComponent<Image>();
            _icon.raycastTarget = false;
            _icon.preserveAspect = true;
            Theme.PlaceSlotIcon(_icon, 22f);

            _name = Theme.MakeText(content, "Name", Theme.FontHeader, Theme.AccentGold,
                TextAlignmentOptions.MidlineLeft);
            _name.enableWordWrapping = false;
            Theme.Place(_name, 27f, 0f, w - 27f, 22f);

            // a fresh pool after a save load: the old rows died with their scene
            _stats = new CardFacets.StatPool(content, w, MaxStatRows);

            _description = Theme.MakeText(content, "Description", Theme.FontCaption, Theme.TextDim);
            _description.enableWordWrapping = true;
            _description.overflowMode = TextOverflowModes.Ellipsis;

            Plugin.Log.LogDebug("Item hover card built");
            return true;
        }

        // ---------- rendering ----------

        private static void Render(ManagersScript mn, ItemData item)
        {
            float w = _window.Content.sizeDelta.x;
            _name.text = item.GetItemName();
            _icon.sprite = item.itemSprite;

            // properties and description, and deliberately nothing else — see the
            // class comment for why the stock facet stops at the browser's door
            _stats.Begin();
            float y = CardFacets.RenderProperties(mn, item, null, _stats, 26f, out int _);
            y = CardFacets.RenderGuide(item, _stats, y);
            y = RenderFavorites(mn, item, y);
            _stats.HideRest();
            y = CardFacets.RenderDescription(item, _description, w, y);
            Finish(y);
        }

        // A CARRIED PERSON'S CARD: who they are, what state they are in - the words
        // vanilla's tooltip appended in red (dead, outlaw, pregnant, daruma), read
        // from its own text table so they stay in the game's language - and the
        // readings you would want before setting them down. The item's tip closes
        // it, as on every card.
        private static void RenderCharacter(ManagersScript mn, ItemData item, CommonStates who)
        {
            float w = _window.Content.sizeDelta.x;
            string kind = item.GetItemName();
            _name.text = string.IsNullOrEmpty(who.charaName) ? kind : who.charaName;
            _icon.sprite = item.itemSprite;
            _stats.Begin();
            float y = _stats.Set(26f, "Kind", kind, Theme.TextCream);
            string state = CharacterState(mn, who, out Color tint);
            if (state.Length > 0)
            {
                y = _stats.SetWrapped(y, "State", state, tint);
            }
            y = _stats.Set(y, "Level", who.level.ToString(), Theme.TextCream);
            y = _stats.Set(y, "HP", Reading(who.life, who.maxLife), Theme.TextCream);
            y = _stats.Set(y, "Stun", Reading(who.faint, who.maxFaint), Theme.TextCream);
            y = _stats.Set(y, "Food", Reading(who.food, who.maxFood), Theme.TextCream);
            y = _stats.Set(y, "Water", Reading(who.water, who.maxWater), Theme.TextCream);
            y = RenderFavorites(mn, item, y);
            _stats.HideRest();
            y = CardFacets.RenderDescription(item, _description, w, y);
            Finish(y);
        }

        // WHAT THEY LIKE TO EAT (vanilla showed useful info about their food
        // preferences). Vanilla's tooltip lists it under every NPC and
        // animal item (UIManager.ItemDescOpen, ToolType NPC/Animal): each favourite
        // food and its favRate, from ItemManager.favoriteFood. One row per food with
        // the food's own icon, labelled on the first.
        //
        // Looked up here rather than through GetFavoriteID, which answers 0 for a
        // kind with no list and so shows the first kind's foods for it.
        private static float RenderFavorites(ManagersScript mn, ItemData item, float y)
        {
            if (item.toolType != ItemData.ToolType.NPC && item.toolType != ItemData.ToolType.Animal
                || mn.itemMN.favoriteFood == null)
            {
                return y;
            }
            ItemManager.FavoriteFoodList list = null;
            foreach (ItemManager.FavoriteFoodList f in mn.itemMN.favoriteFood)
            {
                if (f != null && f.npcData != null && f.npcData.name == item.name)
                {
                    list = f;
                    break;
                }
            }
            if (list == null || list.favoData == null)
            {
                return y;
            }
            bool first = true;
            foreach (ItemManager.FavoriteData fav in list.favoData)
            {
                if (fav == null || fav.item == null)
                {
                    continue;
                }
                y = _stats.Set(y, first ? "Likes" : "", fav.item.GetItemName() + "  " + fav.favRate,
                    Theme.TextCream, fav.item.itemSprite);
                first = false;
            }
            return y;
        }

        private static string Reading(double value, double max)
        {
            return value.ToString("0") + " / " + max.ToString("0");
        }

        // Vanilla's suffixes (UIManager.ItemDescOpen), in its order: daruma, dead
        // (a stranger's word or your own person's), outlaw, pregnant.
        private static string CharacterState(ManagersScript mn, CommonStates who, out Color tint)
        {
            string[] words = mn.textMN != null ? mn.textMN.itemStateTexts : null;
            var parts = new System.Collections.Generic.List<string>();
            tint = Theme.Negative;
            if (who.sex == CommonStates.SexState.Daruma) { parts.Add(StateWord(words, 3, "Daruma")); }
            if (who.life <= 0.0 && who.dead != 100)
            {
                parts.Add(who.Employed() ? StateWord(words, 4, "Dead") : StateWord(words, 0, "Dead"));
            }
            if (who.employ == CommonStates.Employ.Outlaw) { parts.Add(StateWord(words, 1, "Outlaw")); }
            if (who.pregnant != null && who.pregnant.Length > 0 && who.pregnant[0] != -1)
            {
                parts.Add(StateWord(words, 5, "Pregnant"));
                if (parts.Count == 1) { tint = Theme.Happiness; }
            }
            return string.Join("  ", parts);
        }

        private static string StateWord(string[] words, int index, string fallback)
        {
            string word = words != null && index < words.Length ? words[index] : null;
            return string.IsNullOrEmpty(word) ? fallback : word.Trim();
        }

        private static void Finish(float y)
        {
            _window.SetSize(Width, y + 2f * (Theme.Pad + 2f));
            _tag.Apply();
            if (!_window.Go.activeSelf)
            {
                _window.Go.SetActive(true);
            }
            _window.Rect.SetAsLastSibling(); // above the panels, like the vanilla tooltip
        }

        // vanilla convention: panel at the pointer; we copy its pivot so the card
        // hangs off the cursor the same way, then clamp fully on-screen
        private static void Position(UIManager ui)
        {
            RectTransform rect = _window.Rect;
            if (ui.itemDescPanel != null)
            {
                rect.pivot = ((RectTransform)ui.itemDescPanel.transform).pivot;
            }
            rect.position = Input.mousePosition;
            Vector3 scale = rect.lossyScale;
            float w = rect.sizeDelta.x * scale.x;
            float h = rect.sizeDelta.y * scale.y;
            Vector3 pos = rect.position;
            pos.x = Mathf.Clamp(pos.x, rect.pivot.x * w, Screen.width - (1f - rect.pivot.x) * w);
            pos.y = Mathf.Clamp(pos.y, rect.pivot.y * h, Screen.height - (1f - rect.pivot.y) * h);
            rect.position = pos;
        }
    }
}
