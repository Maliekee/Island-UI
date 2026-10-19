using System;
using System.Collections.Generic;
using IslandUI.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI
{
    internal static partial class ItemCard
    {
        internal static void ChooseQuantity(CraftInfo recipe)
        {
            if (recipe == null || Cards[0] == null) return;
            ResetChain();
            ShowVisit(new Link { Recipe = recipe, Item = _mn.itemMN.FindItem(recipe.name) }, 1);
            Cards[0].OpenQuantity();
        }

        private static int AvailableCrafts(CraftInfo recipe)
        {
            if (recipe == null || _inv == null || _mn == null
                || !RecipeCatalog.IsReachable(RecipeCatalog.Of(recipe))
                || Array.IndexOf(MenuRecipes(), recipe) < 0) return 0;
            return _mn.gameMN.creative ? 999 : CraftCountDisplay.CraftableTimes(_inv, recipe, false);
        }

        // "In this menu" is everything in reach, not what the search box leaves showing
        // (BrowserSearch.UnfilteredInnate); the grid's own array when there is nothing else.
        private static CraftInfo[] MenuRecipes()
        {
            return BrowserSearch.UnfilteredInnate ?? _mn.craftMN.craftData[0].craftInfo;
        }

        private sealed partial class Card
        {
            // THE RECIPE IS A BLUEPRINT (the cyanotype). The whole card is the sheet:
            // a title block, the item as a line drawing with a dimension line, the
            // materials as a ruled bill of materials, and controls drawn as the
            // sheet draws things - outlines, and one solid block for Craft. The
            // material is `UI/Blueprint.cs`; a plain item keeps the kit's card.
            private const float Bleed = 3f;      // a rule runs out to the frame's inner line
            private const float DrawingH = 66f;
            private const float HeroSize = 50f;
            private GameObject _recipeRoot;
            private Image _sheetFrame, _grid, _hero, _centreH, _centreV;
            private readonly Image[] _rules = new Image[6];
            private readonly Image[] _dimension = new Image[6]; // two ticks, two runs, two arrowheads
            private TextMeshProUGUI _recipeName, _sheetNo, _class, _benchLabel, _recipeBench, _makes,
                _noteLabel, _recipeTip, _recipeHeading, _recipeOutput, _recipeStatus;
            private readonly List<RecipeRow> _recipeRows = new List<RecipeRow>();
            private SheetButton _craft, _less, _quantityButton, _more, _return;
            private Image _returnIcon, _returnArrow, _returnRule;
            private Outline _heroEdge, _returnEdge;
            private TextMeshProUGUI _returnStatus, _returnKey;
            private bool _recipeVisible;
            private bool _modeInitialized;
            private float _actionsY;
            private int _quantity = 1;
            // THE COUNTER (Enter must not craft: it closes the counter and leaves the
            // chosen number in it; and the counter follows the blueprint design rather
            // than the split-item dialog's). A slip of the sheet over the count row: - n + with the number typed,
            // wheeled or stepped, and SET. Enter or SET keeps the number and closes;
            // Escape, a right-click or a click outside puts the old one back. It
            // never crafts: Craft does.
            private GameObject _counter;
            private Image _counterField;
            private TextMeshProUGUI _counterNumber, _counterMax;
            private SheetButton _counterLess, _counterMore, _counterSet;
            private bool _counting;
            private string _typed;          // the digits typed since it opened or stepped, or null
            private int _countMax;
            private int _quantityBefore;
            private const float CounterH = 38f;
            public bool CountingNow => _counting;
            private int _craftedFrame = -1;
            public Link ShownLink;
            public int Quantity => _quantity;

            // A control on the sheet: an outline that fills faintly under the
            // pointer, or - Craft, when it can - the one solid block of ink.
            private sealed class SheetButton
            {
                public Image Box, Fill;
                public TextMeshProUGUI Label;
                public bool Enabled = true;
                public bool Solid;
                public bool Outlined = true;
                public Color Tone = Blueprint.Ink;
                public Color Quiet = Blueprint.Dim;
                public Color Ground = Blueprint.Ground;
                private bool _pressed;
                private readonly Action _click;
                public SheetButton(Transform parent, string name, float w, float h, float size, Action click)
                {
                    Fill = Theme.KitImage(parent, name, null, 0, Color.clear);
                    Fill.preserveAspect = false;
                    Theme.Place(Fill, 0, 0, w, h);
                    Box = Theme.KitImage(Fill.transform, "Rule", "kit_bp_box", 6, Tone);
                    Theme.Place(Box, 0, 0, w, h);
                    Label = Blueprint.Text(Fill.transform, "Label", size, Tone, TextAlignmentOptions.Center);
                    Label.overflowMode = TextOverflowModes.Ellipsis;
                    Theme.Place(Label, 2, 0, w - 4, h);
                    _click = click;
                }
                public GameObject Go => Fill.gameObject;
                public void Place(float x, float y) => Fill.rectTransform.anchoredPosition = new Vector2(x, -y);
                public void Tick(Vector2 local)
                {
                    if (!Fill.gameObject.activeInHierarchy) { _pressed = false; return; }
                    Vector2 p = Fill.rectTransform.anchoredPosition;
                    Vector2 size = Fill.rectTransform.sizeDelta;
                    bool hot = local.x >= p.x && local.x <= p.x + size.x
                        && local.y <= p.y && local.y >= p.y - size.y;
                    if (Input.GetMouseButtonDown(0)) _pressed = hot && Enabled;
                    if (Input.GetMouseButtonUp(0))
                    {
                        bool invoke = _pressed && hot && Enabled;
                        _pressed = false;
                        if (invoke) _click();
                    }
                    Paint(hot);
                }
                public void Paint(bool hot)
                {
                    bool solid = Solid && Enabled;
                    Color tone = Enabled ? Tone : Blueprint.WithAlpha(Quiet, 0.55f);
                    Fill.color = solid
                        ? (_pressed ? Blueprint.WithAlpha(Tone, 0.75f) : hot ? Blueprint.Lit : Tone)
                        : Blueprint.WithAlpha(Tone, !Enabled ? 0f : _pressed ? 0.30f : hot ? 0.16f : 0f);
                    Box.enabled = Outlined && !solid;
                    Box.color = tone;
                    Label.color = solid ? Ground : tone;
                }
            }

            private sealed class RecipeRow
            {
                public GameObject Go;
                public Image Icon, Highlight, Arrow, Rule;
                public Outline Edge;
                public TextMeshProUGUI Name, Hint, Count;
                public string Key, Title;   // Title: the name as the game writes it, for the tip
                public float Y, H;
                public RecipeRow(Transform parent)
                {
                    Go = Owned.New("Ingredient");
                    Go.transform.SetParent(parent, false);
                    Highlight = Theme.KitImage(Go.transform, "Lead", null, 0, Blueprint.WithAlpha(Blueprint.Lit, 0.13f));
                    Highlight.preserveAspect = false;
                    Icon = Theme.KitImage(Go.transform, "Item", null, 0, Blueprint.Ink);
                    Icon.preserveAspect = true;
                    Edge = Card.Edge(Icon, 0.4f);
                    Name = Blueprint.Text(Go.transform, "Name", 7, Blueprint.Ink, TextAlignmentOptions.MidlineLeft);
                    Name.overflowMode = TextOverflowModes.Ellipsis;
                    Hint = Blueprint.Text(Go.transform, "Hint", 5.4f, Blueprint.Dim, TextAlignmentOptions.MidlineLeft);
                    Hint.overflowMode = TextOverflowModes.Ellipsis;
                    Count = Blueprint.Text(Go.transform, "Count", 7, Blueprint.Ink, TextAlignmentOptions.MidlineRight);
                    Arrow = Theme.KitImage(Go.transform, "Navigate", "kit_chevron", 0, Blueprint.Dim);
                    Rule = Blueprint.Rule(Go.transform, "Rule", 0.28f);
                }
                public void Layout(float width, bool hint, bool chevron)
                {
                    H = hint ? 19 : 15;
                    float top = hint ? 1 : 2;
                    Theme.Place((RectTransform)Go.transform, -Bleed, Y, width + 2 * Bleed, H);
                    Theme.Place(Highlight, 0, 0.5f, width + 2 * Bleed, H - 1);
                    Theme.Place(Icon, Bleed + 1, (H - 12) / 2, 12, 12);
                    Theme.Place(Name, Bleed + 17, top, width - 62, 11);
                    Theme.Place(Hint, Bleed + 17, 11, width - 24, 7);
                    float right = Bleed + width - 1 - (chevron ? 7 : 0);
                    Theme.Place(Count, right - 50, top, 50, 11);
                    Centre(Arrow, Bleed + width - 3.5f, top + 5.5f, 5, 5, 90);
                    Theme.Place(Rule, 0, H - 0.4f, width + 2 * Bleed, 0.4f);
                }
                public bool Hit(Vector2 point, float width) => Go.activeSelf && point.x >= -Bleed
                    && point.x <= width + Bleed && point.y <= -Y && point.y >= -Y - H;
            }

            // a thin drawn edge under a colour sprite, so dark things do not sink into the blue
            private static Outline Edge(Image image, float weight)
            {
                Outline edge = image.gameObject.AddComponent<Outline>();
                edge.effectColor = Blueprint.WithAlpha(Blueprint.Ink, 0.55f);
                edge.effectDistance = new Vector2(weight, -weight);
                edge.enabled = false;
                return edge;
            }

            private static void Centre(Image image, float cx, float cy, float w, float h, float turn)
            {
                RectTransform r = image.rectTransform;
                r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
                r.pivot = new Vector2(0.5f, 0.5f);
                r.sizeDelta = new Vector2(w, h);
                r.anchoredPosition = new Vector2(cx, -cy);
                r.localEulerAngles = new Vector3(0f, 0f, turn);
            }

            private void BuildRecipeView()
            {
                Transform content = Window.Content;
                float w = Window.Content.sizeDelta.x;
                _sheetFrame = Theme.KitImage(Window.Go.transform, "SheetFrame", "kit_bp_frame", 16, Blueprint.Ink);
                RectTransform frame = _sheetFrame.rectTransform;
                frame.anchorMin = Vector2.zero; frame.anchorMax = Vector2.one;
                frame.offsetMin = new Vector2(2f, 2f); frame.offsetMax = new Vector2(-2f, -2f);
                frame.SetAsFirstSibling();
                _recipeRoot = Owned.New("Recipe");
                _recipeRoot.transform.SetParent(content, false);
                Theme.Place((RectTransform)_recipeRoot.transform, 0, 0, w, 1);
                Transform root = _recipeRoot.transform;

                // the title block
                _recipeName = Blueprint.Text(root, "Name", 12.5f, Blueprint.Ink, TextAlignmentOptions.MidlineLeft);
                _recipeName.enableAutoSizing = true;
                _recipeName.fontSizeMin = 7f;
                _recipeName.fontSizeMax = 12.5f;
                Theme.Place(_recipeName, 1, 0, w - 44, 19);
                _sheetNo = Blueprint.Text(root, "Sheet", 5.2f, Blueprint.Dim, TextAlignmentOptions.MidlineRight);
                Theme.Place(_sheetNo, w - 44, 2, 43, 7);
                _class = Blueprint.Text(root, "Class", 6.2f, Blueprint.Ink, TextAlignmentOptions.MidlineRight);
                _class.overflowMode = TextOverflowModes.Ellipsis;
                Theme.Place(_class, w - 44, 9, 43, 9);
                _benchLabel = Blueprint.Text(root, "BenchLabel", 5.2f, Blueprint.Dim, TextAlignmentOptions.MidlineLeft);
                _benchLabel.text = "BENCH";
                Theme.Place(_benchLabel, 1, 21, 22, 12);
                _recipeBench = Blueprint.Text(root, "Bench", 6.2f, Blueprint.Ink, TextAlignmentOptions.MidlineLeft);
                _recipeBench.overflowMode = TextOverflowModes.Ellipsis;
                Theme.Place(_recipeBench, 23, 21, w - 24, 12);

                // the drawing
                _grid = Blueprint.Tile(root, "Grid", "kit_bp_grid", Blueprint.WithAlpha(Blueprint.Ink, 0.55f));
                Theme.Place(_grid, -Bleed, 34, w + 2 * Bleed, DrawingH);
                _centreH = Blueprint.Tile(root, "CentreH", "kit_bp_dash", Blueprint.WithAlpha(Blueprint.Ink, 0.42f));
                Theme.Place(_centreH, 5, 34 + DrawingH / 2 - 4, w - 10, 0.625f);
                _centreV = Blueprint.Tile(root, "CentreV", "kit_bp_dash", Blueprint.WithAlpha(Blueprint.Ink, 0.42f));
                Centre(_centreV, w / 2, 34 + (DrawingH - 10) / 2, DrawingH - 18, 0.625f, 90f);
                _hero = Theme.KitImage(root, "Item", null, 0, Blueprint.Ink);
                _hero.preserveAspect = true;
                _heroEdge = Edge(_hero, 0.6f);
                Centre(_hero, w / 2, 34 + DrawingH / 2 - 4, HeroSize, HeroSize, 0f);
                for (int i = 0; i < _dimension.Length; i++)
                {
                    _dimension[i] = Blueprint.Rule(root, "Dimension");
                    if (i >= 4) _dimension[i].sprite = Theme.Triangle;
                }
                _makes = Blueprint.Text(root, "Makes", 5.4f, Blueprint.Ink, TextAlignmentOptions.Center);

                _noteLabel = Blueprint.Text(root, "NoteLabel", 5.2f, Blueprint.Dim, TextAlignmentOptions.MidlineLeft);
                _noteLabel.text = "NOTE";
                _recipeTip = Blueprint.Text(root, "Description", 6f, Blueprint.Ink, TextAlignmentOptions.TopLeft);
                _recipeTip.enableWordWrapping = true;
                _recipeTip.overflowMode = TextOverflowModes.Ellipsis;

                // the bill of materials
                _recipeHeading = Blueprint.Text(root, "MaterialsHeading", 6.2f, Blueprint.Ink, TextAlignmentOptions.MidlineLeft);
                _recipeHeading.text = "BILL OF MATERIALS";
                _recipeOutput = Blueprint.Text(root, "Output", 5.2f, Blueprint.Dim, TextAlignmentOptions.MidlineRight);
                _recipeStatus = Blueprint.Text(root, "Availability", 6.4f, Blueprint.Ink, TextAlignmentOptions.TopLeft);
                _recipeStatus.enableWordWrapping = true;
                for (int i = 0; i < _rules.Length; i++) _rules[i] = Blueprint.Rule(root, "Rule");

                _craft = new SheetButton(root, "Craft", 64, 19, 8.4f, () =>
                {
                    if (ClickMap.Shift) OpenQuantity(); else CraftSelected();
                }) { Solid = true };
                _less = new SheetButton(root, "Less", 14, 17, 9, () => StepQuantity(-1));
                _less.Label.text = "-";
                _quantityButton = new SheetButton(root, "Quantity", 28, 17, 8, OpenQuantity);
                _more = new SheetButton(root, "More", 14, 17, 9, () => StepQuantity(1));
                _more.Label.text = "+";

                _counter = Owned.New("Counter");
                _counter.transform.SetParent(root, false);
                Theme.Place((RectTransform)_counter.transform, -1, 0, w + 2, CounterH);
                Image slip = Theme.KitImage(_counter.transform, "Slip", null, 0, new Color32(34, 84, 152, 255));
                slip.preserveAspect = false;
                Theme.Place(slip, 0, 0, w + 2, CounterH);
                Shadow lift = slip.gameObject.AddComponent<Shadow>();
                lift.effectColor = new Color(0f, 0f, 0f, 0.55f);
                lift.effectDistance = new Vector2(0f, -2.5f);
                Image slipRule = Theme.KitImage(_counter.transform, "Rule", "kit_bp_box", 6, Blueprint.Ink);
                Theme.Place(slipRule, 0, 0, w + 2, CounterH);
                TextMeshProUGUI counterLabel = Blueprint.Text(_counter.transform, "Label", 5.2f, Blueprint.Dim,
                    TextAlignmentOptions.MidlineLeft);
                counterLabel.text = "QUANTITY";
                Theme.Place(counterLabel, 4, 2.5f, 60, 8);
                _counterMax = Blueprint.Text(_counter.transform, "Max", 5.2f, Blueprint.Dim, TextAlignmentOptions.MidlineRight);
                Theme.Place(_counterMax, w + 2 - 84, 2.5f, 80, 8);
                _counterLess = new SheetButton(_counter.transform, "Less", 14, 17, 9, () => StepCount(-1));
                _counterLess.Label.text = "-";
                _counterLess.Place(4, 14);
                _counterField = Theme.KitImage(_counter.transform, "Field", "kit_bp_box", 6, Blueprint.Lit);
                Theme.Place(_counterField, 20, 14, 36, 17);
                _counterNumber = Blueprint.Text(_counterField.transform, "Number", 8.4f, Blueprint.Ink, TextAlignmentOptions.Center);
                Theme.Place(_counterNumber, 1, 0, 34, 17);
                _counterMore = new SheetButton(_counter.transform, "More", 14, 17, 9, () => StepCount(1));
                _counterMore.Label.text = "+";
                _counterMore.Place(58, 14);
                _counterSet = new SheetButton(_counter.transform, "Set", 64, 19, 8.4f, ConfirmCount) { Solid = true };
                _counterSet.Place(w + 2 - 4 - 64, 13);
                _counter.SetActive(false);

                // the way back: shown under a plain item's card too, in its ink
                _return = new SheetButton(content, "ReturnToRecipe", w + 2 * Bleed, 24, 7, () => TryBack())
                    { Outlined = false };
                _return.Label.alignment = TextAlignmentOptions.MidlineLeft;
                Theme.Place(_return.Label, Bleed + 31, 3, w - 52, 10);
                _returnRule = Blueprint.Rule(_return.Fill.transform, "Rule");
                Theme.Place(_returnRule, 0, 0, w + 2 * Bleed, 0.5f);
                _returnArrow = Blueprint.Rule(_return.Fill.transform, "Back");
                _returnArrow.sprite = Theme.Triangle;
                Centre(_returnArrow, Bleed + 4, 12, 6, 6, -90f);
                _returnIcon = Theme.KitImage(_return.Fill.transform, "Item", null, 0, Blueprint.Ink);
                _returnIcon.preserveAspect = true;
                _returnEdge = Edge(_returnIcon, 0.4f);
                Theme.Place(_returnIcon, Bleed + 11, 3.5f, 17, 17);
                _returnStatus = Blueprint.Text(_return.Fill.transform, "Status", 5.4f, Blueprint.Dim, TextAlignmentOptions.MidlineLeft);
                _returnStatus.overflowMode = TextOverflowModes.Ellipsis;
                Theme.Place(_returnStatus, Bleed + 31, 13, w - 52, 8);
                _returnKey = Blueprint.Text(_return.Fill.transform, "Key", 5.4f, Blueprint.Dim, TextAlignmentOptions.MidlineRight);
                _returnKey.text = "ESC";
                Theme.Place(_returnKey, Bleed + w - 21, 8, 20, 8);
                _recipeRoot.SetActive(false);
                _sheetFrame.gameObject.SetActive(false);
                _return.Go.SetActive(false);
            }

            private void SetRecipeVisible(bool on)
            {
                if (_modeInitialized && on == _recipeVisible) return;
                _modeInitialized = true;
                _recipeVisible = on;
                // The tag recolours even inactive descendants. Remove its skin
                // before exposing the recipe after a visit to a raw material.
                if (on) _tag.Apply(false);
                foreach (Transform child in Window.Content)
                    if (child.gameObject != _recipeRoot && child != _return.Go.transform)
                        child.gameObject.SetActive(!on);
                _recipeRoot.SetActive(on);
                _sheetFrame.gameObject.SetActive(on);
                Image background = Window.Go.GetComponent<Image>();
                Outline edge = Window.Go.GetComponent<Outline>();
                if (edge != null) edge.enabled = !on;
                background.sprite = null;
                background.type = Image.Type.Simple;
                background.color = on ? Blueprint.Ground : Theme.PlateDark;
            }

            public void SetQuantity(int quantity) { _quantity = Mathf.Max(1, quantity); }

            private static void Span(Image rule, float y, float w, float weight = 0.5f)
            {
                Theme.Place(rule, -Bleed, y, w + 2 * Bleed, weight);
            }

            private void RenderRecipe(Link link)
            {
                SetRecipeVisible(true);
                float w = Window.Content.sizeDelta.x;
                _recipeName.text = link.Item.GetItemName().ToUpperInvariant();
                _sheetNo.text = "SHEET " + (History.Count + 1);
                _class.text = CardFacets.FriendlyType(link.Item).ToUpperInvariant();
                RecipeCatalog.Entry entry = RecipeCatalog.Of(link.Recipe);
                int panel = RecipeCatalog.DisplayPanel(entry);
                string bench = RecipeCatalog.PanelOwnerName(_mn, panel) ?? "By hand";
                bool reachable = RecipeCatalog.IsReachable(entry);
                _recipeBench.text = (panel == 0 ? bench : bench + (reachable ? " · in reach" : " · not in reach"))
                    .ToUpperInvariant();
                _recipeBench.color = reachable ? Blueprint.Ink : Blueprint.Short;
                Span(_rules[0], 20, w);
                Span(_rules[1], 33.5f, w);

                // THE CARD ALWAYS DRAWS THE ITEM, never just the silhouette. Whether it can be made is the status line's, the
                // counts' and Craft's to say; the drawing-or-colour convention is the
                // window's alone, where ninety recipes have to be told apart at a glance.
                _hero.sprite = Blueprint.Trimmed(link.Item.itemSprite);
                _hero.color = Color.white;
                _heroEdge.enabled = _hero.sprite != null;
                int output = OutputPerCraft(link.Recipe);
                float drawnW = HeroSize;
                if (_hero.sprite != null && _hero.sprite.rect.height > _hero.sprite.rect.width)
                    drawnW = HeroSize * _hero.sprite.rect.width / _hero.sprite.rect.height;
                drawnW = Mathf.Max(drawnW, 30f);
                float dimY = 34 + DrawingH - 7;
                float x0 = w / 2 - drawnW / 2, x1 = w / 2 + drawnW / 2;
                _makes.text = "MAKES " + output;
                float gap = _makes.GetPreferredValues(_makes.text).x + 4f;
                Theme.Place(_makes, w / 2 - 30, dimY - 4, 60, 8);
                Centre(_dimension[0], x0, dimY, 0.5f, 6, 0);
                Centre(_dimension[1], x1, dimY, 0.5f, 6, 0);
                float run = Mathf.Max(0f, (drawnW - gap) / 2);
                Centre(_dimension[2], x0 + run / 2, dimY, run, 0.5f, 0);
                Centre(_dimension[3], x1 - run / 2, dimY, run, 0.5f, 0);
                Centre(_dimension[4], x0 + 2, dimY, 3.2f, 4, -90);
                Centre(_dimension[5], x1 - 2, dimY, 3.2f, 4, 90);
                float y = 34 + DrawingH;
                Span(_rules[2], y, w);

                string tips = (link.Item.GetItemTips() ?? "").Replace("/n", "\n").Trim();
                bool note = tips.Length > 0;
                _noteLabel.gameObject.SetActive(note);
                _recipeTip.gameObject.SetActive(note);
                _rules[3].gameObject.SetActive(note);
                if (note)
                {
                    _recipeTip.text = tips;
                    float noteW = w - 23;
                    float noteH = Mathf.Min(_recipeTip.GetPreferredValues(tips, noteW, 0f).y + 1f, 40f);
                    Theme.Place(_noteLabel, 1, y + 3, 20, 8);
                    Theme.Place(_recipeTip, 22, y + 3.5f, noteW, noteH);
                    y += noteH + 7;
                    Span(_rules[3], y, w);
                }

                Theme.Place(_recipeHeading, 1, y + 1, w / 2 + 10, 10);
                Theme.Place(_recipeOutput, w / 2, y + 1, w / 2 - 1, 10);
                y += 12;
                Span(_rules[4], y, w, 0.4f);
                CraftingPatches.BeginSweepWindow();
                try
                {
                    _lastCrafts = AvailableCrafts(link.Recipe);
                    if (!_counting && _lastCrafts > 0) _quantity = Mathf.Clamp(_quantity, 1, _lastCrafts);
                    _recipeOutput.text = "HAVE / NEED · MAKES " + (_quantity * (long)output);
                    int cursor = 0;
                    string shortage = null;
                    foreach (CraftInfo.Required required in link.Recipe.required)
                    {
                        ItemData item = required?.itemData;
                        if (item == null) continue;
                        while (_recipeRows.Count <= cursor) _recipeRows.Add(new RecipeRow(_recipeRoot.transform));
                        RecipeRow row = _recipeRows[cursor++];
                        float need = Mathf.Max(1f, required.count) * _quantity;
                        float have = CraftingPatches.CountAvailable(_inv, item.name, false);
                        bool shortfall = have < need;
                        Link producer = ResolveLink(item.name);
                        bool hasRecipe = producer.Recipe != null;
                        bool inReach = hasRecipe && RecipeCatalog.IsReachable(RecipeCatalog.Of(producer.Recipe));
                        bool makeable = inReach && AvailableCrafts(producer.Recipe) > 0;
                        string owner = hasRecipe ? RecipeCatalog.PanelOwnerName(_mn,
                            RecipeCatalog.DisplayPanel(RecipeCatalog.Of(producer.Recipe))) ?? "By hand" : null;
                        string hint = !shortfall ? ""
                            : !hasRecipe ? "Gather in the world"
                            : !inReach ? owner + " not in reach"
                            : makeable ? (owner == "By hand" ? "Make by hand" : "Make at " + owner)
                            : "Recipe needs materials";
                        bool lit = shortfall && makeable;
                        row.Key = item.name;
                        row.Y = y;
                        row.Layout(w, hint.Length > 0, hasRecipe);
                        row.Go.SetActive(true);
                        row.Icon.sprite = item.itemSprite;
                        row.Icon.color = Color.white;
                        row.Edge.enabled = item.itemSprite != null;
                        row.Title = item.GetItemName();
                        row.Name.text = row.Title.ToUpperInvariant();
                        row.Count.text = $"<color=#{ColorUtility.ToHtmlStringRGB(shortfall ? Blueprint.Short : Blueprint.Ink)}>{have:0}</color> / {need:0}";
                        row.Arrow.enabled = hasRecipe;
                        row.Arrow.color = lit ? Blueprint.Lit : inReach ? Blueprint.Ink : Blueprint.Dim;
                        row.Hint.text = hint.ToUpperInvariant();
                        row.Hint.color = lit ? Blueprint.Lit : Blueprint.Dim;
                        row.Highlight.enabled = lit;
                        if (shortfall && shortage == null) shortage = $"Need {Mathf.CeilToInt(need - have)} more {item.GetItemName()}";
                        y += row.H;
                    }
                    for (int i = cursor; i < _recipeRows.Count; i++) _recipeRows[i].Go.SetActive(false);
                    Span(_rules[5], y, w);
                    bool bad = !reachable || shortage != null;
                    _recipeStatus.text = (!reachable ? $"Requires {bench} — not in reach"
                        : shortage ?? (_lastCrafts == 0 ? "Not available in this menu" : $"Can craft {_lastCrafts} times"))
                        .ToUpperInvariant();
                    _recipeStatus.color = bad ? Blueprint.Short : Blueprint.Ink;
                    y += 5;
                    Theme.Place(_recipeStatus, 1, y, w - 2, 10);
                    y += 14;
                    _actionsY = y;
                    _less.Place(1, y + 1); _quantityButton.Place(17, y + 1); _more.Place(47, y + 1);
                    _craft.Place(w - 65, y);
                    _quantityButton.Label.text = _quantity.ToString();
                    _craft.Label.text = $"CRAFT {_quantity}";
                    _craft.Enabled = _lastCrafts >= _quantity;
                    _quantityButton.Enabled = _lastCrafts > 0;
                    _less.Enabled = _quantity > 1;
                    _more.Enabled = _quantity < _lastCrafts;
                    _less.Paint(false); _quantityButton.Paint(false); _more.Paint(false); _craft.Paint(false);
                    _counter.SetActive(_counting);
                    if (_counting)
                    {
                        ((RectTransform)_counter.transform).anchoredPosition = new Vector2(-1, -(y - 17));
                        _counter.transform.SetAsLastSibling();
                        PaintCounter();
                    }
                    y += 23;
                    y = RenderUsedIn(link.Item, w, y, sheet: true);
                    y = RenderReturn(y + 3);
                    Window.SetSize(Width, y + 2f * (Theme.Pad + 2));
                }
                finally { CraftingPatches.EndSweepWindow(); }
            }

            private float RenderReturn(float y)
            {
                bool show = History.Count > 0;
                _return.Go.SetActive(show);
                if (!show) return y;
                Visit parent = History[History.Count - 1];
                bool ready = parent.Link.Recipe != null && AvailableCrafts(parent.Link.Recipe) >= parent.Quantity;
                // on the sheet it is the sheet's ink; under a plain item's card
                // (the shed's manila tag) it is that card's
                bool sheet = ShownLink.Recipe != null;
                bool manila = !sheet;
                Color ink = sheet ? Blueprint.Ink : manila ? Theme.KitInk : Theme.KitCream;
                Color quiet = sheet ? Blueprint.Dim : manila ? Theme.KitInk : Theme.TextDim;
                Color tone = ready && !manila ? Blueprint.Lit : ink;
                _return.Tone = tone;
                _return.Quiet = quiet;
                _return.Place(-Bleed, y);
                _returnIcon.sprite = parent.Link.Item.itemSprite;
                _returnIcon.color = Color.white;
                _returnEdge.enabled = sheet && _returnIcon.sprite != null;
                _returnArrow.color = tone;
                _returnRule.color = ink;
                _return.Label.text = ("Back to " + parent.Link.Item.GetItemName()).ToUpperInvariant();
                string status = "Return to the job";
                if (parent.Link.Recipe != null)
                {
                    if (ready) status = "Ready to craft";
                    else if (!RecipeCatalog.IsReachable(RecipeCatalog.Of(parent.Link.Recipe)))
                        status = "Required bench not in reach";
                    else
                    {
                        float need = 0;
                        foreach (CraftInfo.Required req in parent.Link.Recipe.required)
                            if (req?.itemData != null && req.itemData.name == ShownLink.Key)
                                need += Mathf.Max(1, req.count) * parent.Quantity;
                        int missing = Mathf.CeilToInt(need - CraftingPatches.CountAvailable(_inv, ShownLink.Key, false));
                        status = missing > 0 ? $"Needs {missing} more {ShownLink.Item.GetItemName()}"
                            : "Other materials still needed";
                    }
                }
                _returnStatus.text = status.ToUpperInvariant();
                _returnStatus.color = ready ? tone : quiet;
                _returnKey.color = quiet;
                _return.Paint(false);
                return y + 25;
            }

            private void TickReturn(Vector2 local) { _return.Tick(local); }

            // ONE CLICK, ONE MEANING (a click that meant one thing on a craftable
            // material and another on a plain one was confusing, so only the
            // craftable ones are clickable). A row with a recipe - the chevron - opens
            // that recipe; a row without one does nothing. What an item IS belongs to
            // the hover, and it is the game-wide item hover, beside the row.
            private bool _hoverShown;

            public void DropHover()
            {
                if (_hoverShown)
                {
                    _hoverShown = false;
                    HoverCard.HideRecipeHover();
                }
            }

            private void TickRecipe(Vector2 local)
            {
                if (_counting) { DropHover(); TickCounter(local); return; }
                float w = Window.Content.sizeDelta.x;
                RecipeRow over = null;
                foreach (RecipeRow row in _recipeRows)
                    if (row.Hit(local, w)) { over = row; break; }
                string used = over == null ? HitUsedIn(local) : null;
                // the item itself, too: its picture answers the same hover, the card
                // standing clear of the sheet, beside the drawing
                float heroY = 34 + DrawingH / 2 - 4;
                bool onHero = over == null && used == null
                    && Mathf.Abs(local.x - w / 2) <= HeroSize / 2 && Mathf.Abs(-local.y - heroY) <= HeroSize / 2;
                RectTransform beside = over != null ? (RectTransform)over.Go.transform
                    : onHero ? _grid.rectTransform : UsedInRect(used);
                if (beside != null)
                {
                    HoverCard.ShowForItem(_mn, _mn.uiMN, over != null ? over.Key : onHero ? ShownLink.Key : used, beside);
                    _hoverShown = true;
                }
                else
                {
                    DropHover();
                }
                if (over != null && over.Arrow.enabled && Input.GetMouseButtonDown(0))
                {
                    OnMaterialClicked(over.Key);
                    return;
                }
                if (used != null && Input.GetMouseButtonDown(0)) { OnMaterialClicked(used); return; }
                if (Input.GetMouseButtonDown(0) && TryToggleUsedIn(local, w)) { RenderAll(); return; }
                _less.Tick(local); _quantityButton.Tick(local); _more.Tick(local); _craft.Tick(local);
                TickReturn(local);
                if (local.y <= -_actionsY && local.y >= -_actionsY - 19 && local.x >= 0 && local.x <= 62)
                {
                    ReasonTip.Show("How many · click the number to type one · wheel steps");
                    if (Mathf.Abs(Input.mouseScrollDelta.y) > 0.01f)
                        StepQuantity(Input.mouseScrollDelta.y > 0 ? 1 : -1);
                }
            }

            private void StepQuantity(int step)
            {
                _quantity = Mathf.Clamp(_quantity + step, 1, Mathf.Max(1, AvailableCrafts(ShownLink.Recipe)));
                RenderAll();
            }

            // ---------- the counter ----------

            public void OpenQuantity()
            {
                if (_counting) return;
                int max = AvailableCrafts(ShownLink.Recipe);
                if (max <= 0) return;
                _countMax = max;
                _quantityBefore = _quantity;
                _quantity = Mathf.Clamp(_quantity, 1, max);
                _typed = null;
                _counting = true;
                RenderAll();
            }

            // keeps the number and closes; crafting is Craft's
            public void ConfirmCount()
            {
                if (!_counting) return;
                _counting = false;
                _typed = null;
                RenderAll();
            }

            // closes without touching the number: the subject changed under it
            private void EndCounting()
            {
                _counting = false;
                _typed = null;
                if (_counter != null) _counter.SetActive(false);
            }

            public void CancelQuantity()
            {
                if (!_counting) return;
                _counting = false;
                _typed = null;
                _quantity = _quantityBefore;
                if (_counter != null) _counter.SetActive(false);
            }

            private void StepCount(int by)
            {
                _typed = null;
                _quantity = Mathf.Clamp(_quantity + by, 1, _countMax);
                RenderAll();
            }

            // Digits build a number from nothing - the first one replaces the count -
            // and Backspace takes one off, as in the split dialog.
            public void TypeCount(string chars)
            {
                if (!_counting || string.IsNullOrEmpty(chars)) return;
                bool changed = false;
                foreach (char ch in chars)
                {
                    if (ch >= '0' && ch <= '9')
                    {
                        _typed = (_typed ?? string.Empty) + ch;
                        if (_typed.Length > 6) _typed = _typed.Substring(_typed.Length - 6);
                    }
                    else if (ch == '\b' && !string.IsNullOrEmpty(_typed))
                    {
                        _typed = _typed.Substring(0, _typed.Length - 1);
                    }
                    else
                    {
                        continue;
                    }
                    changed = true;
                    int typed = string.IsNullOrEmpty(_typed) ? 1 : int.Parse(_typed);
                    _quantity = Mathf.Clamp(typed, 1, _countMax);
                }
                if (changed) RenderAll();
            }

            private void PaintCounter()
            {
                _counterMax.text = "MAX " + _countMax;
                _counterSet.Label.text = "SET " + _quantity;
                _counterLess.Enabled = _quantity > 1;
                _counterMore.Enabled = _quantity < _countMax;
                _counterLess.Paint(false); _counterMore.Paint(false); _counterSet.Paint(false);
                PaintCaret();
            }

            private void PaintCaret()
            {
                bool caret = Mathf.Repeat(Time.unscaledTime, 1f) < 0.55f;
                string shown = _typed != null ? _typed : _quantity.ToString();
                // the caret keeps its width when it is dark, so the number does not jitter
                _counterNumber.text = shown + (caret ? "|" : "<color=#00000000>|</color>");
            }

            private void TickCounter(Vector2 local)
            {
                Vector2 origin = ((RectTransform)_counter.transform).anchoredPosition;
                Vector2 inner = local - origin;
                float w = Window.Content.sizeDelta.x;
                bool inside = inner.x >= 0 && inner.x <= w + 2 && inner.y <= 0 && inner.y >= -CounterH;
                if ((Input.GetMouseButtonDown(0) && !inside) || Input.GetMouseButtonDown(1))
                {
                    CancelQuantity();
                    RenderAll();
                    return;
                }
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                {
                    ConfirmCount();
                    return;
                }
                TypeCount(Input.inputString);
                if (!_counting) return;
                float wheel = Input.mouseScrollDelta.y;
                if (Mathf.Abs(wheel) > 0.01f) StepCount((int)Mathf.Sign(wheel) * (ClickMap.Shift ? 10 : 1));
                _counterLess.Tick(inner); _counterMore.Tick(inner); _counterSet.Tick(inner);
                PaintCaret();
            }

            private void CraftSelected()
            {
                if (_craftedFrame == Time.frameCount || _inv.craftPanelID != 0) return;
                CraftInfo recipe = ShownLink.Recipe;
                int available = AvailableCrafts(recipe);
                if (_quantity < 1 || _quantity > available) { RenderAll(); return; }
                _craftedFrame = Time.frameCount;
                int requested = _quantity;
                // vanilla crafts by position in the menu's array (CraftItem reads
                // craftData[0].craftInfo[slot] and nothing else by position), and the
                // search may have projected this recipe out of it - or the 500-button
                // clamp cut it off, and its index in the unfiltered array is past the
                // grid that CraftableCheckSlot indexes (Iron Ingot at ~600
                // threw IndexOutOfRange and crafted nothing). Craft it from a
                // one-recipe array at slot 0, then hand the grid its own back
                CraftManager.CraftData menu = _mn.craftMN.craftData[0];
                CraftInfo[] shown = menu.craftInfo;
                CraftInfo[] single = { recipe };
                try
                {
                    for (int i = 0; i < requested; i++)
                    {
                        // Crafting can refill the grid; ask afresh whether the recipe
                        // is still in reach before every one
                        if (Array.IndexOf(MenuRecipes(), recipe) < 0
                            || !RecipeCatalog.IsReachable(RecipeCatalog.Of(recipe))) break;
                        menu.craftInfo = single;
                        _inv.CraftItem(100, craftableCheck: true);
                        // a craft that refilled the menu left a fresh array: that one
                        // is the grid's now, and must not be overwritten with the old
                        if (menu.craftInfo == single) { menu.craftInfo = shown; }
                        else { shown = menu.craftInfo; }
                    }
                }
                finally
                {
                    if (menu.craftInfo == single && shown != null)
                    {
                        menu.craftInfo = shown;
                    }
                }
                // anything CraftItem swept while the arrays were swapped is re-swept
                _inv.CraftableCheck();
                RenderAll();
            }
        }
    }
}
