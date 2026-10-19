using System.Collections.Generic;
using IslandUI.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI
{
    // A single recipe surface. Material and used-in links replace its subject;
    // visits retain identity and quantity so a detour can return to the same job.
    internal static partial class ItemCard
    {
        private const float Width = 166f;
        private const float ChainGap = 4f;
        private const int MaxCards = 1;
        // the used-in strip: small item cells in a stat row's value column, so one
        // collapsed row costs exactly what "Class" costs
        private const float UsedInBox = 10f;
        private const float UsedInPitch = 11f;
        private const int UsedInMaxRows = 4;   // the ceiling when expanded
        private const float ArrowSize = 8f;
        private const float MoreW = 16f;       // the "+12" tail
        // the properties facet at its widest: class, bench, attack, durability,
        // use, equip — plus headroom, since a pool that runs out drops a facet
        private const int MaxStatRows = 10;

        // what a card can display: a recipe, or a plain item (raw resources —
        // nothing produces them, but they still deserve a preview)
        private struct Link
        {
            public CraftInfo Recipe; // null for plain items
            public ItemData Item;    // never null for a valid link

            public bool Valid => Item != null;
            public string Key => Item != null ? Item.name : null;
        }

        private static readonly Card[] Cards = new Card[MaxCards];
        private struct Visit
        {
            public Link Link;
            public int Quantity;
            public BrowserShell.RecipePlace Place;
        }
        private static readonly List<Visit> History = new List<Visit>();
        private static Link _current;
        private static CraftInfo _lastRoot;
        private static int _backFrame = -1;
        internal static bool HasDetour => History.Count > 0;
        // the recipe's counter is open: digits and Escape are its own
        internal static bool Counting => Cards[0] != null && Cards[0].CountingNow;

        private static InventoryManager _inv;
        private static ManagersScript _mn;
        private static int _preview = -1; // grid hover index
        private static string _previewItem;  // Items-tab hover: by key, not index
        private static Camera _cam;          // for hit-testing controls inside a card
        private static bool _allowed;     // the menu's open/close gate
        private static Vector2? _dockPos;

        // ---------- lifecycle ----------

        internal static bool EnsureBuilt(InventoryManager inv, ManagersScript mn)
        {
            _inv = inv;
            _mn = mn;
            if (Cards[0] != null && Cards[0].Window != null && Cards[0].Window.Go != null)
            {
                return true;
            }
            if (!Theme.Ensure(mn, inv))
            {
                return false;
            }
            // (re)build — after a save load the old cards died with their scene;
            // fresh Card instances mean fresh pools, no stale row references
            History.Clear();
            _lastRoot = null;
            _preview = -1;
            _current = default;
            Transform panel = mn.uiMN.craftPanel.transform;
            for (int i = 0; i < MaxCards; i++)
            {
                Cards[i] = new Card();
                Cards[i].Build(panel, i);
            }
            Plugin.Log.LogDebug("Craft detail cards built");
            RenderAll();
            return true;
        }

        // the root card docks against the custom menu's dark plate: SyncGeometry
        // reports the plate's right edge and top in panel top-left coords
        internal static void AlignTo(Vector2 panelTopLeftPos)
        {
            _dockPos = panelTopLeftPos;
            ApplyLayout();
        }

        // the card docks right of the menu
        private static void ApplyLayout()
        {
            if (_dockPos == null)
            {
                return;
            }
            float x = _dockPos.Value.x;
            for (int i = 0; i < MaxCards; i++)
            {
                Card card = Cards[i];
                if (card == null || card.Window == null || card.Window.Go == null)
                {
                    continue;
                }
                RectTransform rect = card.Window.Rect;
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.anchoredPosition = new Vector2(x, _dockPos.Value.y);
                if (card.Window.Go.activeSelf)
                {
                    x += Width + ChainGap;
                }
            }
        }

        internal static void SetVisible(bool on)
        {
            _allowed = on;
            if (!on)
            {
                ResetChain();  // chain, hover and every disclosure: session state
                Cards[0]?.DropHover();
            }
            for (int i = 0; i < MaxCards; i++)
            {
                Cards[i]?.ApplyVisibility();
            }
        }

        // ---------- inputs from the menu ----------

        internal static void Preview(int recipeIdx)
        {
            if (_preview != recipeIdx)
            {
                _preview = recipeIdx;
                _previewItem = null;
                RenderAll();
            }
        }

        // WI-6/WI-7: the same card, asked for by ITEM instead of by recipe. The
        // Items tab hovers stock, which has no recipe index — but the card was
        // never really about recipes: ResolveLink already turns a key into "a
        // producer if there is one, else the plain item", which is exactly the
        // adapter §9.2 describes. So a stock hover is not a second card path, it
        // is the same one entered from the other end.
        internal static void PreviewItem(string itemKey)
        {
            if (_previewItem == itemKey)
            {
                return;
            }
            _previewItem = itemKey;
            _preview = -1;
            RenderAll();
        }

        internal static void NotifyChanged()
        {
            _preview = -1;
            _previewItem = null;
            RenderAll();
        }

        internal static void RefreshCounts()
        {
            RenderAll();
        }

        // Closing the browser or deliberately choosing a grid item starts a
        // fresh job. Escape itself returns through TryBack before close-all.
        internal static void ResetChain()
        {
            History.Clear();
            _lastRoot = null;
            _current = default;
            for (int i = 0; i < MaxCards; i++)
            {
                Cards[i]?.CancelQuantity();
                Cards[i]?.SetQuantity(1);
                Cards[i]?.ResetDisclosure();
            }
        }

        // ---------- recipe navigation ----------

        // what a material cell navigates to: a reachable producer first (what
        // the player can act on), else any producer, else the plain item
        private static Link ResolveLink(string itemKey)
        {
            var link = new Link { Item = _mn != null ? _mn.itemMN.FindItem(itemKey) : null };
            IReadOnlyList<RecipeCatalog.Entry> producers = RecipeCatalog.ProducersOf(itemKey);
            for (int i = 0; i < producers.Count; i++)
            {
                if (RecipeCatalog.IsReachable(producers[i]))
                {
                    link.Recipe = producers[i].Recipe;
                    return link;
                }
            }
            if (producers.Count > 0)
            {
                link.Recipe = producers[0].Recipe;
            }
            return link;
        }

        private static void OnMaterialClicked(string itemKey)
        {
            Link target = ResolveLink(itemKey);
            Card card = Cards[0];
            if (!target.Valid || target.Key == card.ShownLink.Key) return;
            Link source = card.ShownLink;
            int quantity = card.Quantity;
            History.Add(new Visit { Link = source, Quantity = quantity, Place = BrowserShell.CaptureRecipePlace() });
            int initial = MissingCrafts(source.Recipe, quantity, target);
            ShowVisit(target, initial);
        }

        private static void ShowVisit(Link link, int quantity)
        {
            _current = link;
            _lastRoot = link.Recipe;
            BrowserShell.SelectFromCard(link.Recipe);
            Cards[0].SetQuantity(quantity);
            Cards[0].ResetDisclosure();
            BrowserShell.RevealRecipe(link.Recipe);
            RenderAll();
        }

        internal static bool TryBack()
        {
            if (_backFrame == Time.frameCount) return true;
            // Escape closes what is on top first: the counter, then the detour
            if (Counting)
            {
                _backFrame = Time.frameCount;
                Cards[0].CancelQuantity();
                RenderAll();
                return true;
            }
            if (!_allowed || History.Count == 0) return false;
            _backFrame = Time.frameCount;
            Visit visit = History[History.Count - 1];
            History.RemoveAt(History.Count - 1);
            ShowVisit(visit.Link, visit.Quantity);
            BrowserShell.RestoreRecipePlace(visit.Place);
            RenderAll();
            return true;
        }

        internal static bool EscapeNavigates()
        {
            if (!Input.GetKeyDown(KeyCode.Escape) || FilterInput.AnyFocused
                || DevConsole.IsOpen || SettingsWindow.IsOpen) return false;
            if (TryBack()) return true;
            if (_allowed && (_current.Valid || BrowserShell.SelectedRef != null))
            {
                BrowserShell.TryEscapeDeselect();
                return true;
            }
            return false;
        }

        // Fractional vanilla costs yield a batch (CraftConsume); quantity is
        // crafting operations, while the caption also exposes the output count.
        private static int OutputPerCraft(CraftInfo recipe)
        {
            int output = 1;
            if (recipe?.required == null || (_mn != null && _mn.gameMN.creative)) return output;
            foreach (CraftInfo.Required required in recipe.required)
                if (required != null && required.count > 0f && required.count < 1f)
                    output = Mathf.CeilToInt(1f / required.count);
            return output;
        }

        private static int MissingCrafts(CraftInfo parent, int quantity, Link target)
        {
            if (parent?.required == null || target.Recipe == null) return 1;
            float need = 0;
            foreach (CraftInfo.Required required in parent.required)
                if (required?.itemData != null && required.itemData.name == target.Key)
                    need += Mathf.Max(1f, required.count) * quantity;
            float have = CraftingPatches.CountAvailable(_inv, target.Key, bench: false);
            return Mathf.Max(1, Mathf.CeilToInt((need - have) / OutputPerCraft(target.Recipe)));
        }

        // ---------- rendering ----------

        private static void RenderAll()
        {
            if (Cards[0] == null || Cards[0].Window == null || Cards[0].Window.Go == null
                || _inv == null || _mn == null)
            {
                return;
            }
            RecipeCatalog.EnsureBuilt(_mn);

            // the pin owns the root card BY IDENTITY (it survives refills and
            // walking out of reach); previews apply only while nothing is pinned
            // Each tab owns its own pin, and the ACTIVE tab decides which one the
            // card is showing — reading the other tab's pin would leave a card
            // pinned to something the grid underneath is no longer displaying.
            // The second tab is gone, so there is one root and one way in.
            // `subject` survives because PreviewItem still enters the card BY KEY
            // — the crafting grid's hover does it, and the chest list will too.
            CraftInfo root = BrowserShell.SelectedRef;
            var rootLink = default(Link);
            string subject = root == null && !_current.Valid ? _previewItem : null;
            if (root == null && subject != null)
            {
                // an item with no producer still deserves a card: ResolveLink
                // degrades to the plain item, which is the properties facet alone
                rootLink = ResolveLink(subject);
                root = rootLink.Recipe;
            }
            if (root == null && rootLink.Item == null && !_current.Valid)
            {
                CraftInfo[] recipes = _mn.craftMN.craftData[0].craftInfo;
                root = _preview >= 0 && _preview < recipes.Length ? recipes[_preview] : null;
            }
            if (root != _lastRoot)
            {
                History.Clear();
                _current = default;
                Cards[0].SetQuantity(1);
                _lastRoot = root;
            }

            if (rootLink.Item == null)
            {
                rootLink = new Link
                {
                    Recipe = root,
                    Item = root != null ? _mn.itemMN.FindItem(root.name) : null,
                };
            }
            Cards[0].Render(_current.Valid ? _current : rootLink);
            ApplyLayout();
        }

        // ---------- input (polled, same pattern as the grid) ----------

        internal static void Tick(Camera cam)
        {
            _cam = cam;
            Card card = Cards[0];
            if (card?.Window?.Go != null && card.Window.Go.activeInHierarchy)
            {
                card.TickClose(cam);
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    card.Window.Content, Input.mousePosition, cam, out Vector2 local))
                {
                    card.Tick(local);
                }
            }
            // after every card has had its say: whoever asked for a tip this
            // frame gets one, and nobody asking is what puts it away
            ReasonTip.Commit(Cards[0]?.Window?.Go != null
                ? Cards[0].Window.Go.transform.parent : null, cam);
        }

        // ---------- one card ----------

        // one destination in the used-in strip
        private struct UsedInEntry
        {
            public string Key;
            public Sprite Sprite;
            public bool Reachable;
        }

        private static readonly List<UsedInEntry> UsedInBuffer = new List<UsedInEntry>();
        private static readonly HashSet<string> UsedInSeen = new HashSet<string>();

        private sealed partial class Card
        {
            private sealed class UsedInCell
            {
                public GameObject Go;
                public Image Box;
                public Image Icon;
                public string ItemKey;
                public bool Sheet;   // wearing the blueprint's box, not the slot frame
            }

            public UIWindow Window;
            private ShedTag _tag;   // the tool shed's manila tag
            private Image _close;   // the enamel tag — puts the card away
            private bool _closePressed;
            private Image _icon;
            private TextMeshProUGUI _name;
            private TextMeshProUGUI _description;
            private CardFacets.StatPool _stats;
            private TextMeshProUGUI _usedInLabel;
            private TextMeshProUGUI _usedInMore;
            private TMP_FontAsset _usedInPlainFace;
            private Image _usedInArrow;
            private readonly List<UsedInCell> _usedInCells = new List<UsedInCell>();
            private float _usedInTop;
            private int _usedInCols = 1;
            private int _usedInShown;
            private bool _usedInExpanded;
            private bool _usedInExpandable;

            private int _lastCrafts;
            private bool _hasContent;
            private string _shownKey;    // what the stock facet's plates are about

            public void Build(Transform panel, int index)
            {
                Window = UIWindow.Create(panel, $"ItemCard{index}", Width, 96f,
                    darkCard: true);
                Window.Rect.anchorMin = new Vector2(1f, 1f);
                Window.Rect.anchorMax = new Vector2(1f, 1f);
                Window.Rect.pivot = new Vector2(0f, 1f);
                Window.Rect.anchoredPosition = new Vector2(4f, 0f);
                _tag = new ShedTag(Window.Go);
                _close = CloseChip.Create(Window.Go.transform);

                Transform content = Window.Content;
                float w = Window.Content.sizeDelta.x;

                // header: slot-boxed icon + name (the box is the grid cells' frame)
                var boxGo = Owned.New("IconBox");
                boxGo.transform.SetParent(content, false);
                Image iconBox = boxGo.AddComponent<Image>();
                iconBox.sprite = Theme.SlotFrame;
                iconBox.raycastTarget = false;
                Theme.Place(iconBox, 0f, 0f, 22f, 22f);
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

                // stat rows (positioned dynamically per render) — shared with the
                // slot-hover card, which renders the same properties facet
                _stats = new CardFacets.StatPool(content, w, MaxStatRows);

                _description = Theme.MakeText(content, "Description", Theme.FontCaption, Theme.TextDim);
                _description.enableWordWrapping = true;
                _description.overflowMode = TextOverflowModes.Ellipsis; // only bites at the line cap

                // the used-in facet — a stat row, not a section; see RenderUsedIn
                _usedInLabel = Theme.MakeText(content, "UsedInLabel", Theme.FontCaption,
                    Theme.TextDim, TextAlignmentOptions.MidlineLeft);
                _usedInLabel.enableWordWrapping = false;
                _usedInLabel.text = "Used in";
                _usedInMore = Theme.MakeText(content, "UsedInMore", Theme.FontCaption,
                    Theme.TextDim, TextAlignmentOptions.MidlineRight);
                _usedInMore.enableWordWrapping = false;
                // "+200" is wider than its reserve; let it spill left into the gap
                // rather than become an ellipsis (Theme.CaptionH's lesson, sideways)
                _usedInMore.overflowMode = TextOverflowModes.Overflow;
                _usedInMore.gameObject.SetActive(false);
                var arrowGo = Owned.New("UsedInArrow");
                arrowGo.transform.SetParent(content, false);
                _usedInArrow = arrowGo.AddComponent<Image>();
                _usedInArrow.sprite = Theme.Triangle;
                _usedInArrow.color = Theme.TextDim;
                _usedInArrow.raycastTarget = false;
                var arrowRect = (RectTransform)arrowGo.transform;
                arrowRect.anchorMin = new Vector2(0f, 1f);
                arrowRect.anchorMax = new Vector2(0f, 1f);
                arrowRect.pivot = new Vector2(0.5f, 0.5f);   // it rotates about itself
                arrowRect.sizeDelta = new Vector2(ArrowSize, ArrowSize);
                arrowGo.SetActive(false);

                BuildRecipeView();
            }

            public void ResetDisclosure()
            {
                _usedInExpanded = false;
            }

            public void ApplyVisibility()
            {
                bool show = _allowed && _hasContent;
                if (Window != null && Window.Go != null && Window.Go.activeSelf != show)
                {
                    Window.Go.SetActive(show);
                }
            }

            // The card is the item's facets, composed top to bottom:
            //   properties   what it IS          (shared with the hover card)
            //   stock        how much, where     (reading window only; expandable)
            //   used in      what it is FOR      (expandable)
            //   description  its own tip text    (shared)
            //   made from    how to GET one, and the hammer that does it
            // Stock and used-in are stat ROWS, not sections: each answers its
            // common question in one line and keeps the occasional answer behind
            // an arrow. The hammer sits with the cost it acts on, at the bottom.
            public void Render(Link link)
            {
                if (Window == null || Window.Go == null || _inv == null || _mn == null)
                {
                    return;
                }
                ItemData item = link.Item;

                // A disclosure belongs to the CARD SHOWING THIS ITEM, and to nothing
                // else. Carried across subjects it made the card change height as
                // you moved the pointer, and re-opening a card remembered a choice
                // you made about something else entirely.
                string key = item != null ? item.name : null;
                if (key != _shownKey)
                {
                    ResetDisclosure();
                    EndCounting();   // the counter was about the last subject
                }
                _hasContent = item != null;
                _shownKey = key;
                ApplyVisibility();
                if (item == null)
                {
                    return; // hidden — no empty-state card
                }

                ShownLink = link;
                if (link.Recipe != null)
                {
                    RenderRecipe(link);
                    return;
                }
                SetRecipeVisible(false);
                float w = Window.Content.sizeDelta.x;
                _name.text = item.GetItemName();
                _icon.sprite = item.itemSprite;

                _stats.Begin();
                float y = CardFacets.RenderProperties(_mn, item, link.Recipe, _stats, 26f,
                    out int panelID);
                y = CardFacets.RenderGuide(item, _stats, y);
                _stats.HideRest();
                // The STOCK facet used to render here — "which containers
                // hold this" — and went with the by-item view it was built on.
                // Used-in keeps the pattern: the common answer inline, the
                // occasional one (all 200 recipes) behind an arrow.
                y = RenderUsedIn(item, w, y);
                y = CardFacets.RenderDescription(item, _description, w, y);

                _lastCrafts = 0;
                y = RenderReturn(y);
                Window.SetSize(Width, y + 2f * (Theme.Pad + 2f));
                _tag.Apply();
            }

            // What this item is a material FOR — a STAT ROW whose
            // value is a strip of icons, and which expands when there are more
            // than fit on one line.
            //
            // It began as rows like MADE FROM's, on the reasoning that it means
            // the same thing in the other direction. It does not, and the rows
            // proved it: two lookalike lists sharing the slot-frame cell, the
            // dimming and a right-aligned number that meant have/need above and a
            // cost in somebody ELSE's recipe below. MADE FROM is a checklist
            // against your stock — state, numbers about you, and it gates the
            // hammer under it. USED IN is a catalogue of other subjects, with no
            // state and nothing to gate. So the shape differs, not the tint: you
            // READ a checklist and you SCAN a strip.
            //
            // And a strip belongs UP with Class and Bench rather than down in a
            // section of its own. One line answers "what is this for" for most
            // items; the arrow is there for wood, which answers it 200 times.
            //
            // Reachable products come first, so the icons that fit while collapsed
            // are the ones you can act on. A click navigates there, with a way back.
            private float RenderUsedIn(ItemData item, float w, float y, bool sheet = false)
            {
                IReadOnlyList<RecipeCatalog.Entry> consumers = RecipeCatalog.ConsumersOf(item.name);
                float iconsX = CardFacets.LabelW;
                // the tail is reserved in BOTH states, so the icons do not reflow
                // when the row opens
                float iconsW = w - iconsX - ArrowSize - 2f - MoreW;
                _usedInCols = Mathf.Max(1,
                    Mathf.FloorToInt((iconsW + (UsedInPitch - UsedInBox)) / UsedInPitch));
                int distinct = CollectUsedIn(consumers, item.name, _usedInCols * UsedInMaxRows);
                _usedInExpandable = distinct > _usedInCols;
                _usedInShown = _usedInExpanded
                    ? UsedInBuffer.Count
                    : Mathf.Min(_usedInCols, UsedInBuffer.Count);
                bool any = _usedInShown > 0;
                int hidden = distinct - _usedInShown;
                _usedInLabel.gameObject.SetActive(any);
                SetActive(_usedInMore, any && hidden > 0);
                if (_usedInArrow.gameObject.activeSelf != (any && _usedInExpandable))
                {
                    _usedInArrow.gameObject.SetActive(any && _usedInExpandable);
                }
                if (!any)
                {
                    for (int i = 0; i < _usedInCells.Count; i++)
                    {
                        if (_usedInCells[i].Go.activeSelf)
                        {
                            _usedInCells[i].Go.SetActive(false);
                        }
                    }
                    _usedInTop = -1f;
                    return y;
                }

                Theme.Place(_usedInLabel, 0f, y, iconsX, CardFacets.StatRowH);
                // one strip, two materials: on the recipe's sheet it is lettered
                // and drawn as the sheet is, on a plain item's card it is the kit's
                if (_usedInPlainFace == null)
                {
                    _usedInPlainFace = _usedInLabel.font;
                }
                TMP_FontAsset face = sheet && Blueprint.Face != null ? Blueprint.Face : _usedInPlainFace;
                Color quiet = sheet ? Blueprint.Dim : Theme.TextDim;
                foreach (TextMeshProUGUI text in new[] { _usedInLabel, _usedInMore })
                {
                    if (text.font != face)
                    {
                        text.font = face;
                        Theme.FlattenType(text);
                    }
                    text.color = quiet;
                }
                _usedInLabel.text = sheet ? "USED IN" : "Used in";
                _usedInArrow.color = quiet;
                if (hidden > 0)
                {
                    // how many products are NOT on screen — which is why it counts
                    // distinct products rather than recipes: several recipes can
                    // make the same thing, and that is one destination, not two
                    _usedInMore.text = "+" + hidden;
                    Theme.Place(_usedInMore, w - ArrowSize - 2f - MoreW, y, MoreW,
                        CardFacets.StatRowH);
                }
                if (_usedInExpandable)
                {
                    ((RectTransform)_usedInArrow.transform).anchoredPosition = new Vector2(
                        w - ArrowSize * 0.5f, -(y + CardFacets.StatRowH * 0.5f));
                    // ▼ open, ▶ collapsed (the browser's own section convention)
                    _usedInArrow.rectTransform.localRotation =
                        Quaternion.Euler(0f, 0f, _usedInExpanded ? 0f : 90f);
                }
                _usedInTop = y;

                EnsureUsedInCells(_usedInShown);
                for (int i = 0; i < _usedInCells.Count; i++)
                {
                    UsedInCell cell = _usedInCells[i];
                    bool used = i < _usedInShown;
                    if (cell.Go.activeSelf != used)
                    {
                        cell.Go.SetActive(used);
                    }
                    if (!used)
                    {
                        cell.ItemKey = null;
                        continue;
                    }
                    UsedInEntry product = UsedInBuffer[i];
                    Theme.Place(cell.Go.GetComponent<RectTransform>(),
                        CardFacets.LabelW + (i % _usedInCols) * UsedInPitch,
                        _usedInTop + (i / _usedInCols) * UsedInPitch, UsedInBox, UsedInBox);
                    cell.ItemKey = product.Key;
                    cell.Icon.sprite = product.Sprite;
                    cell.Icon.enabled = product.Sprite != null;
                    if (sheet != cell.Sheet)
                    {
                        cell.Sheet = sheet;
                        if (sheet)
                        {
                            Theme.KitSkin(cell.Box, "kit_bp_box", 6);
                        }
                        else
                        {
                            cell.Box.sprite = Theme.SlotFrame;
                            cell.Box.type = Image.Type.Simple;
                        }
                    }
                    // one meaning for the dim channel across both sections: you
                    // cannot act on this right now
                    Color full = sheet ? Blueprint.Ink : Color.white;
                    cell.Icon.color = product.Reachable ? Color.white : Theme.DisabledTint;
                    cell.Box.color = Blueprint.WithAlpha(full, product.Reachable ? (sheet ? 0.7f : 1f) : (sheet ? 0.3f : 0.5f));
                }
                // one row costs exactly a stat row, so a collapsed used-in sits in
                // the stat rhythm rather than beside it
                int rows = Mathf.CeilToInt(_usedInShown / (float)_usedInCols);
                return y + Mathf.Max(CardFacets.StatRowH,
                    rows * UsedInPitch - (UsedInPitch - UsedInBox));
            }

            // Fills the display buffer reachable-first and returns the TRUE number
            // of distinct products, which the tail count needs: several recipes
            // can make the same thing, and that is one destination rather than
            // two, so the recipe count cannot answer "how many am I not seeing".
            // Both passes therefore walk the whole list even once the buffer is
            // full — a HashSet keeps that O(n).
            private static int CollectUsedIn(IReadOnlyList<RecipeCatalog.Entry> consumers,
                string material, int cap)
            {
                UsedInBuffer.Clear();
                UsedInSeen.Clear();
                int distinct = 0;
                for (int pass = 0; pass < 2; pass++)
                {
                    bool wantReachable = pass == 0;
                    for (int i = 0; i < consumers.Count; i++)
                    {
                        if (RecipeCatalog.IsReachable(consumers[i]) != wantReachable)
                        {
                            continue;
                        }
                        string key = consumers[i].Recipe.name;
                        // never point at the card's own subject
                        if (key == material || !UsedInSeen.Add(key))
                        {
                            continue;
                        }
                        ItemData product = _mn.itemMN.FindItem(key);
                        if (product == null)
                        {
                            continue;
                        }
                        distinct++;
                        if (UsedInBuffer.Count < cap)
                        {
                            UsedInBuffer.Add(new UsedInEntry
                            {
                                Key = key,
                                Sprite = product.itemSprite,
                                Reachable = wantReachable,
                            });
                        }
                    }
                }
                return distinct;
            }

            private static void SetActive(TextMeshProUGUI text, bool on)
            {
                if (text.gameObject.activeSelf != on)
                {
                    text.gameObject.SetActive(on);
                }
            }

            private void EnsureUsedInCells(int count)
            {
                while (_usedInCells.Count < count)
                {
                    var cell = new UsedInCell();
                    cell.Go = Owned.New("UsedIn");
                    cell.Go.transform.SetParent(Window.Content, false);
                    cell.Box = cell.Go.AddComponent<Image>();
                    cell.Box.sprite = Theme.SlotFrame;
                    cell.Box.raycastTarget = false;
                    var iconGo = Owned.New("Icon");
                    iconGo.transform.SetParent(cell.Go.transform, false);
                    cell.Icon = iconGo.AddComponent<Image>();
                    cell.Icon.raycastTarget = false;
                    cell.Icon.preserveAspect = true;
                    Theme.PlaceSlotIcon(cell.Icon, UsedInBox);
                    cell.Go.SetActive(false);
                    _usedInCells.Add(cell);
                }
            }

            private RectTransform UsedInRect(string itemKey)
            {
                if (itemKey == null)
                {
                    return null;
                }
                for (int i = 0; i < _usedInShown && i < _usedInCells.Count; i++)
                {
                    if (_usedInCells[i].ItemKey == itemKey)
                    {
                        return (RectTransform)_usedInCells[i].Go.transform;
                    }
                }
                return null;
            }

            private string HitUsedIn(Vector2 local)
            {
                for (int i = 0; i < _usedInShown && i < _usedInCells.Count; i++)
                {
                    float x = CardFacets.LabelW + (i % _usedInCols) * UsedInPitch;
                    float cy = _usedInTop + (i / _usedInCols) * UsedInPitch;
                    if (local.x >= x && local.x <= x + UsedInBox
                        && local.y <= -cy && local.y >= -cy - UsedInBox)
                    {
                        return _usedInCells[i].ItemKey;
                    }
                }
                return null;
            }

            // the label gutter and the arrow — everything on the row that is not
            // an icon, since the icons are their own targets
            private bool TryToggleUsedIn(Vector2 local, float w)
            {
                if (!_usedInExpandable || _usedInTop < 0f
                    || local.y > -_usedInTop || local.y < -_usedInTop - CardFacets.StatRowH)
                {
                    return false;
                }
                // everything on the row that is not an icon: the label gutter, and
                // the "+12" tail together with the arrow it sits beside
                if (local.x < 0f
                    || (local.x >= CardFacets.LabelW && local.x < w - ArrowSize - 2f - MoreW))
                {
                    return false;
                }
                _usedInExpanded = !_usedInExpanded;
                return true;
            }

            // the chip lives on the frame's top edge, outside Content's rect, so
            // it hit-tests in screen space like the browser's own
            public void TickClose(Camera cam)
            {
                if (_close == null || !_close.gameObject.activeInHierarchy)
                {
                    _closePressed = false;
                    return;
                }
                bool over = RectTransformUtility.RectangleContainsScreenPoint(
                    _close.rectTransform, Input.mousePosition, cam);
                if (Input.GetMouseButtonDown(0) && over)
                {
                    _closePressed = true;
                }
                if (Input.GetMouseButtonUp(0))
                {
                    bool fire = _closePressed && over;
                    _closePressed = false;
                    if (fire)
                    {
                        BrowserShell.DeselectFromCard();
                    }
                }
                else if (_closePressed && !Input.GetMouseButton(0))
                {
                    _closePressed = false;
                }
                CloseChip.Paint(_close, over, over && _closePressed);
            }

            public string Tick(Vector2 local)
            {
                if (ShownLink.Recipe != null) { TickRecipe(local); return null; }
                TickReturn(local);
                string linked = HitUsedIn(local);
                if (linked != null && Input.GetMouseButtonDown(0))
                {
                    OnMaterialClicked(linked);
                    return linked;
                }
                if (Input.GetMouseButtonDown(0) && TryToggleUsedIn(local, Window.Content.sizeDelta.x))
                    RenderAll();
                return linked;
            }
        }
    }
}
