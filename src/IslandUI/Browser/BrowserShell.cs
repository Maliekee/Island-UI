using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI
{
    // Feature: custom-built view for the innate/aggregated crafting menu.
    //
    // The vanilla panel's floor was a ~40-53ms native canvas-batching frame per open
    // (500 live cells churning ~100 graphics at once — probe data). This
    // view replaces the vanilla "mask" (the 500-button grid) with a FIXED pool of
    // ~cols x (rows+2) minimal cells inside the same craftPanel shell; scrolling
    // REBINDS pooled cells instead of creating/activating anything, positions are
    // computed arithmetic (no GridLayoutGroup, no ContentSizeFitter, no per-cell
    // Buttons), and the shell already sits on its own isolation sub-canvas.
    //
    // Reuse over rebuild:
    // - Data: the CraftOpen prefix pipeline (aggregation -> search -> clamp ->
    //   grouping) runs unchanged; this class only renders craftData[0].craftInfo.
    // - Shell: the vanilla craftPanel GameObject stays (background, header, search
    //   field, activeSelf conventions) — Escape/C/movement integrations untouched.
    // - Cells: cloned from a real recipe button, stripped to Image + icon + count —
    //   native look for free.
    // - Game entry points: clicking calls CraftItem, hovering fills the hidden
    //   vanilla slot and calls ItemDescOpen — consumption rules and the tooltip
    //   (incl. TooltipAvailability) work unmodified.
    //
    // Bench panels (craftPanelID != 0) keep the vanilla grid: OnVanillaOpen()
    // restores the mask and hides this view.
    internal static partial class BrowserShell
    {
        // vanilla's own pitch, by choice: the crafting window uses the original
        // spacing; the cell is the vanilla 20-unit slot, pivot-centred
        private const float CellPitch = UI.Slot.CraftPitch;
        private const float CellHalf = 10f;
        private const float SearchRowH = 14f;       // compact rounded bar
        // The title's ROW is now the filter's. The word "Crafting" is gone and
        // comes back later, with the title plates and the UI kit — a title drawn
        // twice in two places is a title argued about twice.
        // the crafting window's own field in the slot's header (UI.SlotFilter)
        private static UI.FilterField _filter => UI.SlotFilter.Crafting;
        internal static UI.FilterField Filter => _filter;
        private static Image _panelFill;
        // 2, not 0: the content is a RectMask2D, and a field flush with its top edge
        // had the paper's rim clipped off (it cropped some of the search bar at the
        // top). The row wants a hair of air above it.
        private const float SearchTopMargin = 2f;
        private const float CollapseAllW = 18f;     // expand/collapse-all toggle
        private const float HeaderGap = 3f;         // between header-row controls
        // Text sits this far inside its row. The tabs and the filter field get it
        // from their plates' padding; a section header has no plate, so it has to
        // be applied by hand or its label lands short of every label above it.
        private const float RowTextInset = 5f;
        // WI-6: the tab bar owns a row of its own. Sharing the search row was the
        // first attempt and it does not fit — the plate is only ~145 units wide
        // (GridColumns x CellPitch), and two tabs plus a search well wide enough
        // to read a query in overrun it, so "Items" landed on top of the field.
        // A row costs one row of grid height and reads the way tabbed windows are
        // expected to.
        private const float TitleRowH = 13f;  // was the tab bar's height
        // how far the panel is lifted from vanilla's place, clear of the
        // bottom-left buttons, and the grid's nudge from the centred position
        private const float PanelLift = 35f;
        private const float GridOffsetX = -2f;
        private const float SearchRowY = SearchTopMargin + TitleRowH + 2f;

        // The search row is GONE from this window, not hidden in it. The
        // field it held now floats as UI.SearchSurface and belongs to no window,
        // which is the whole point — a field docked into a window's chrome is what
        // made search a per-window feature, and what forced every window that
        // wanted it to reserve a row of its own.
        // 0: the header row lives on the SHELL now, above the grid's own rect, so
        // nothing inside the grid has to leave room for it
        private const float GridTop = 0f;
        // The collapse-all pill shares the title's row now. It was never a search
        // control and does not leave with the search.
        private const float PillRowY = SearchTopMargin;
        private const float PillRowH = TitleRowH;

        private sealed class CellView
        {
            public GameObject Go;
            public RectTransform Rect;
            public Image Background;
            public Image Icon;
            public Text Count;
            public int Recipe = -1;
            public int VanillaCountSize;
            // the blueprint's parts (BrowserShell.Sheet.cs), made on first use
            public Image Square;    // its square of the sheet's grid
            public Image Drawn;     // the recipe as a drawing, when it cannot be made
            public Image Pin;       // the chosen recipe's amber box
            public Outline Halo;    // a thin drawn edge under a colour icon
            public bool OwedDrawing; // its drawing was put off to a later frame
        }

        // Click = select; the detail window renders/crafts the selection.
        // Selection is held BY IDENTITY: the CraftInfo reference is the
        // truth and survives refills AND walking out of bench reach — the grid
        // index (for the cell outline) is re-derived after every list change
        // and simply goes -1 while the recipe isn't displayed.
        internal static int SelectedRecipe = -1;
        // The Items tab pins BY KEY. Recipes pin by CraftInfo identity because a
        // recipe can leave and re-enter the list; an item key never changes, so
        // the same guarantee costs nothing here.
        private static CraftInfo _selectedRef;
        internal static CraftInfo SelectedRef => _selectedRef;

        private sealed class HeaderView
        {
            public GameObject Go;
            public RectTransform Rect;
            public TextMeshProUGUI Label;
            public Image Icon;      // bench item sprite; hidden when none
            public Image Arrow;     // disclosure triangle (▼ open / ▶ collapsed)
            public Image Rule;      // the blueprint's rule from the name to the arrow
            public Image Under;     // ...and the one that closes the band
        }

        private struct RowDef
        {
            public string Header;     // non-null => header row
            public Sprite HeaderIcon; // bench icon beside the label
            public bool Collapsed;    // header rows: section folded shut
            public int[] Recipes;     // length = columns, -1 = empty cell
        }

        // collapsed state per section label; survives rebuilds and reopens.
        // _defaultCollapsed covers sections NOT in the dictionary — so
        // collapse-all also applies to benches that come into reach later
        private static readonly Dictionary<string, bool> CollapsedGroups
            = new Dictionary<string, bool>();
        private static bool _defaultCollapsed;

        private static bool IsCollapsed(string group)
        {
            return CollapsedGroups.TryGetValue(group, out bool collapsed)
                ? collapsed
                : _defaultCollapsed;
        }

        private static GameObject _root;        // replaces the mask, child of craftPanel
        private static RectTransform _content;  // top-left coordinate space for cells
        private static RectTransform _plateRect; // the dark menu surface
        private static Vector2 _panelBasePos;   // vanilla craftPanel position
        // The panel stays where TryHandleOpen lifted it after it closes; only a
        // vanilla open puts it back. The vicinity window reads this to know
        // whether the slot's rect already includes the lift.
        internal static bool PanelLifted { get; private set; }
        private static bool _panelBaseCaptured;
        private static RectTransform _titlePill; // vanilla "Craft" bubble above the panel
        // THE WORD IS BACK IN THE ROW: the earlier layout read well there.
        // Giving the filter the whole row left the naming to the vanilla
        // bubble; the old bar read better with "Crafting" at the row's left and a
        // compact field beside it, count and x inside. So: word, field, pill.
        private static TMPro.TextMeshProUGUI _titleWord;
        // 44, and the field starts a full gap after: at 38 the field began two units
        // after the word's last letter ("too close to the text to the left of it").
        private const float TitleWordW = 44f;
        // THE CRAFTING WINDOW WEARS THE KIT'S FRAME TOO (its borders use our own
        // style, like every other window): vanilla's
        // ui_frame on the panel is switched off, PanelFill becomes the kit's
        // textured field tinted down to the bench colour, and a hairline walnut
        // rail closes the shape. The close chip is the settings window's, on the
        // panel's top-right corner, and does what vanilla's close does
        // (LeftPanelState(0)).
        private static Image _panelRail;
        private static Image _vanillaFrame;
        // THE PLATE ROW IS THE TAB ROW (T14): "Crafting" wears
        // the oak plate while this window is up, "In reach" the walnut one beside
        // it, and picking the walnut plate swaps the slot to the vicinity window -
        // same frame, same dimensions, the field changing from bench to parchment.
        private const float TitlePlateX = 9f;

        private static Image _closeChip;
        private static bool _closePressed;
        private static readonly Color KitParch = new Color(238f / 255f, 206f / 255f, 168f / 255f, 1f);

        // THE SHELL: our own window on the root canvas, at the slot (UI.Slot),
        // holding the field, the rail, the tabs, the chip, the header row and
        // the grid. The crafting window used to be vanilla's panel with our fill,
        // cover strip, mask and header layered on it, "several different colours
        // in boxes" - the layers were the boxes.
        // Vanilla's panel stays ACTIVE and invisible underneath, because the game
        // reads craftPanel.activeSelf for its own state; only the bench grid,
        // which is vanilla's, shows it again.
        private static GameObject _shell;
        private static RectTransform _shellRect;
        internal static RectTransform NavigationFrame => _shellRect;
        private static RectTransform _header;

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
        private static Vector2 _titleBasePos;
        private static Vector2 _titleBaseAnchorMin, _titleBaseAnchorMax, _titleBasePivot;
        private static GameObject _maskGo;      // the vanilla grid we displace
        private static Scrollbar _scrollbar;
        private static UnityEngine.UI.Image _cover;           // the strip that hides scrolled rows
        private static GameObject _collapseAllGo;             // expand/collapse-all pill
        private static UnityEngine.UI.Image _collapseAllPlate;
        private static UnityEngine.UI.Image _collapseAllArrow;
        private static TMPro.TextMeshProUGUI _noMatches;      // empty-state message
        private static int _matchCount;
        private static CellView[] _cells;
        private static HeaderView[] _headers;
        private static int _poolColumns, _poolRows;
        private static readonly List<RowDef> Rows = new List<RowDef>();
        private static float _scroll;           // in rows; 0 = top
        private static int _hoverRecipe = -1;
        private static InventoryManager _inv;
        private static ManagersScript _mn;

        // set by CraftLazyFill's fill prefix; read by the Sections postfix
        internal static bool HandledThisOpen;

        internal static bool Active =>
            Settings.CustomGrid.Value && _root != null && _root.activeInHierarchy
            && _inv != null && _inv.craftPanelID == 0;

        // ---------- open/close ----------

        // Called from the CraftOpen fill prefix for id == 0 when the feature is on.
        // Returns false to fall back to the vanilla path (build failure).
        internal static bool TryHandleOpen(InventoryManager inv, ManagersScript mn)
        {
            if (!EnsureBuilt(inv, mn))
            {
                return false;
            }
            _inv = inv;
            _mn = mn;
            // one occupant of the left slot (D48): the vicinity window yields
            InventoryWindow.CloseReach();
            if (_maskGo.activeSelf)
            {
                _maskGo.SetActive(false);
            }
            // THE ORIGINAL FRAME STAYS ON. It used to be switched off so our
            // dark plate could be the menu surface; now the window is meant to look
            // like the game's, and the game's own panel Image is that look, at the
            // right geometry, with no harvest and no timing to get wrong.
            //
            // The first attempt copied Theme.Frame onto our own plate instead, and
            // it rendered a WHITE BOX: the plate is built before Theme.Ensure has
            // run, so the sprite assigned was null, and a null-sprite Image is a
            // white quad. Using vanilla's own enabled Image cannot fail that way.
            // vanilla's frame is off while the shell is up; OnVanillaOpen puts it
            // back for the bench grid, which is vanilla's own
            if (_vanillaFrame != null && _vanillaFrame.enabled)
            {
                _vanillaFrame.enabled = false;
            }
            if (_shell != null && !_shell.activeSelf)
            {
                _shell.SetActive(true);
                _shell.transform.SetAsLastSibling();
            }
            // lift the panel clear of the bottom-left buttons / storage strip
            var panelMove = (RectTransform)mn.uiMN.craftPanel.transform;
            if (!_panelBaseCaptured)
            {
                _panelBasePos = panelMove.anchoredPosition;
                _panelBaseCaptured = true;
            }
            panelMove.anchoredPosition = _panelBasePos
                + new Vector2(0f, PanelLift);
            PanelLifted = true;
            // the vanilla scrollbar (our clone's source) would show a stale thumb
            if (mn.uiMN.craftPanelScroll != null && mn.uiMN.craftPanelScroll.gameObject.activeSelf)
            {
                mn.uiMN.craftPanelScroll.gameObject.SetActive(false);
            }
            SyncGeometry();
            if (!_root.activeSelf)
            {
                _root.SetActive(true);
            }
            EnsureFilter(mn);
            RebuildRows(inv, mn);
            UpdateSearchFeedback();
            inv.FindCraftReference(); // player-centered chest scan (vanilla open rhythm)
            CraftingPatches.BeginSweepWindow();
            BindVisible();
            CraftingPatches.EndSweepWindow();

            // The docked detail window rides along with the menu. Selection is
            // sticky: remap the remembered recipe to its index in the new list
            // (it drops only when the recipe truly left the menu).
            if (ItemCard.EnsureBuilt(inv, mn))
            {
                ItemCard.SetVisible(true);
                // the pin persists by identity even when its recipe left the
                // grid (bench out of reach, filtered out): no outline then, and
                // the card shows the out-of-reach state instead
                SelectedRecipe = _selectedRef != null
                    ? System.Array.IndexOf(mn.craftMN.craftData[0].craftInfo, _selectedRef)
                    : -1;
                RefreshSelectionOutlines();
                _hoverRecipe = -2; // indices shifted: force a preview re-fire
                ItemCard.NotifyChanged();
            }
            return true;
        }

        // THE FILTER TAKES THE TITLE'S ROW, and it is built HERE rather than in
        // EnsureBuilt -- which is the bug that made it never appear at all.
        // EnsureBuilt also runs from Prewarm, moments after a save loads, and
        // uiMN.mainInputField (the field we clone) is not populated that early. So
        // Build returned false, EnsureBuilt returned true anyway and was never
        // called again, and there was no filter for the rest of the session.
        //
        // The lesson is not "call it later", it is that AN EARLY-RETURN CACHE MUST
        // NOT CACHE A FAILURE. This one is idempotent and retried on every open, so
        // one bad moment costs one frame instead of the session.
        //
        // FontBody, not FontHeader: a FontHeader line in a 13-unit row is exactly
        // Theme.CaptionH's case, where TMP's Ellipsis overflow DROPS a line too
        // tall for its rect rather than clipping it, silently, space still
        // reserved. That is how "Crafting" once rendered as nothing at all.
        // The slot's shared field (UI.SlotFilter), in this shell's header row
        // while the crafting window is up, at the row's left edge.
        private static void EnsureFilter(ManagersScript mn)
        {
            if (_shellRect == null)
            {
                return;
            }
            UI.SlotFilter.Attach(_shellRect, mn, UI.Slot.Inset, UI.Slot.Height - UI.Slot.PadTop, dark: true);
        }

        // The shared search field lives inside our bar while the custom grid
        // is active, and returns to its floating spot for the vanilla/bench grid
        // The only search feedback this window still owns: an empty grid has to
        // say WHY it is empty. The match count and the × went with the bar — they
        // described the FIELD, and the field does not live here any more.
        private static void UpdateSearchFeedback()
        {
            _filter.SetCount(_filter.Active ? _matchCount.ToString() : "");
            if (_noMatches != null)
            {
                _noMatches.gameObject.SetActive(_filter.Active && _matchCount == 0);
            }
        }

        // A vanilla-grid open (bench panel, or the feature toggled off): give the
        // mask back and stand down.
        // THE LIFT IS A RELATIVE EDIT OF A VANILLA RECT, and the one thing a hot
        // reload cannot see. A new generation captures the panel's position as
        // its base on the first open and lifts from there - so a panel left
        // lifted by the generation before it climbed PanelLift higher on every
        // reload (the craft window kept moving upwards).
        // Called from Plugin.Teardown as well as from a vanilla open, so the
        // panel a generation hands over is the panel it was handed.
        internal static void RestoreVanillaPanel()
        {
            if (_mn == null || _mn.uiMN == null || _mn.uiMN.craftPanel == null)
            {
                return;
            }
            if (_panelBaseCaptured)
            {
                ((RectTransform)_mn.uiMN.craftPanel.transform).anchoredPosition = _panelBasePos;
                PanelLifted = false;
            }
            if (_titlePill != null)
            {
                _titlePill.anchorMin = _titleBaseAnchorMin;
                _titlePill.anchorMax = _titleBaseAnchorMax;
                _titlePill.pivot = _titleBasePivot;
                _titlePill.anchoredPosition = _titleBasePos;
            }
        }

        internal static void OnVanillaOpen()
        {
            // a bench menu takes the slot too
            InventoryWindow.CloseReach();
            if (_shell != null && _shell.activeSelf)
            {
                _shell.SetActive(false);
            }
            if (_vanillaFrame != null && !_vanillaFrame.enabled)
            {
                _vanillaFrame.enabled = true;   // the bench grid is vanilla's, frame and all
            }
            if (_root != null && _root.activeSelf)
            {
                _root.SetActive(false);
            }
            if (_scrollbar != null && _scrollbar.gameObject.activeSelf)
            {
                _scrollbar.gameObject.SetActive(false);
            }
            if (_maskGo != null && !_maskGo.activeSelf)
            {
                _maskGo.SetActive(true);
            }
            if (_mn != null && _mn.uiMN != null && _mn.uiMN.craftPanelScroll != null)
            {
                _mn.uiMN.craftPanelScroll.gameObject.SetActive(true);
            }
            // the frame is never switched off any more; only the position
            // needs putting back
            RestoreVanillaPanel();
            if (_mn != null && _mn.uiMN != null && _mn.uiMN.craftPanel != null)
            {
                if (_titlePill != null && !_titlePill.gameObject.activeSelf)
                {
                    _titlePill.gameObject.SetActive(true);
                }
            }
            _hoverRecipe = -1;
            ItemCard.SetVisible(false);
        }

        // ---------- construction ----------

        private static bool EnsureBuilt(InventoryManager inv, ManagersScript mn)
        {
            if (_root != null)
            {
                UI.Slot.Read(mn, _shellRect.parent as RectTransform);
                if (_poolColumns == UI.Slot.CraftCols && _poolRows >= RequiredPoolRows())
                {
                    return true;
                }
                Object.Destroy(_shell); // column config changed: rebuild the pool
                _shell = null;
                _root = null;
            }
            if (mn.uiMN == null || mn.uiMN.craftPanel == null)
            {
                return false;
            }
            Transform panel = mn.uiMN.craftPanel.transform;
            _maskGo = panel.Find("mask") != null ? panel.Find("mask").gameObject : null;
            if (_maskGo == null || inv.itemSlot.Length <= 100 || inv.itemSlot[100] == null)
            {
                return false;
            }

            // leftovers of the days this window lived on vanilla's panel
            for (int i = panel.childCount - 1; i >= 0; i--)
            {
                Transform child = panel.GetChild(i);
                if (child.name == "PanelFill" || child.name == "PanelRail"
                    || child.name == "Close" || child.name == "PlateTabs")
                {
                    Object.Destroy(child.gameObject);
                }
            }
            _vanillaFrame = panel.GetComponent<Image>();

            Canvas panelCanvas = mn.uiMN.craftPanel.GetComponentInParent<Canvas>();
            if (panelCanvas == null)
            {
                return false;
            }
            if (_shell != null)
            {
                Object.Destroy(_shell);
            }
            _shell = Owned.New("IslandUI_Crafting");
            _shell.transform.SetParent(panelCanvas.rootCanvas.transform, false);
            _shellRect = (RectTransform)_shell.transform;
            _shellRect.anchorMin = Vector2.zero;   // placed from the canvas's bottom-left,
            _shellRect.anchorMax = Vector2.zero;   // like the inventory's columns
            _shellRect.pivot = Vector2.zero;
            UI.CanvasIsolation.Isolate(_shell);
            // the field: the kit's textured parchment, multiplied down to the
            // bench colour by ApplyCraftSurface (D46: dark because this window MAKES)
            _panelFill = UI.Theme.KitImage(_shellRect, "Field", "kit_field", 12, Color.white);
            _panelFill.raycastTarget = true;   // the surface feeds the game's OnUI flag
            Stretch(_panelFill.rectTransform);
            _panelRail = UI.Theme.KitImage(_shellRect, "Rail", "kit_rail", 12, Color.white);
            Stretch(_panelRail.rectTransform);
            _closeChip = UI.CloseChip.Create(_shellRect);
            // Navigation lives in the bottom dock.
            // the header row: the filter and the collapse-all pill, placed by
            // SyncGeometry under the top padding
            var headerGo = Owned.New("Header");
            headerGo.transform.SetParent(_shellRect, false);
            _header = (RectTransform)headerGo.transform;

            _root = Owned.New("CustomCraftGrid");
            _root.transform.SetParent(_shellRect, false);
            var rootRect = (RectTransform)_root.transform;
            rootRect.anchorMin = new Vector2(0f, 1f);
            rootRect.anchorMax = new Vector2(0f, 1f);
            rootRect.pivot = new Vector2(0f, 1f);

            // the invisible raycast catcher the grid keeps under its cells
            var plateGo = Owned.New("plate");
            plateGo.transform.SetParent(_root.transform, false);
            var plateRect = (RectTransform)plateGo.transform;
            Stretch(plateRect);
            _plateRect = plateRect;
            var plate = plateGo.AddComponent<Image>();
            plate.color = new Color(0f, 0f, 0f, 0f);
            plate.raycastTarget = true;

            // top-left coordinate space + pointer catcher + cell clipping
            var contentGo = Owned.New("content");
            contentGo.transform.SetParent(_root.transform, false);
            _content = (RectTransform)contentGo.transform;
            _content.anchorMin = new Vector2(0f, 0f);
            _content.anchorMax = new Vector2(1f, 1f);
            _content.pivot = new Vector2(0f, 1f);
            _content.offsetMin = Vector2.zero;
            _content.offsetMax = Vector2.zero;
            var mask = contentGo.AddComponent<RectMask2D>(); // clip partial rows, same as vanilla mask
            // Vector4 is (left, bottom, right, top), and NEGATIVE EXPANDS. The
            // sideways expansion is real work: the selection outline reaches 1.5u
            // past its cell, and on the first and last column an exact mask cuts
            // it off. The top stays exact — the cover strip handles that edge.
            //
            // THE BOTTOM WAS EXPANDED FOR THE SAME REASON AND SHOULD NOT HAVE
            // BEEN. Sideways, the thing being saved from the crop is a 1.5u line
            // on a cell that is fully on screen. Downward, the bottom row is the
            // SCROLLING edge — the row there is usually a partial one, so the 3u
            // of slack was not protecting an outline, it was letting a sixth of a
            // cell hang over the panel edge before the crop caught it.
            // Losing the last row's outline underline is the cheaper of the two.
            mask.padding = new Vector4(-3f, 0f, -3f, 0f);
            Image catcher = contentGo.AddComponent<Image>();
            catcher.color = new Color(0f, 0f, 0f, 0f); // absorbs UI raycasts over the grid

            // the collapse-all pill at the header row's right end
            _collapseAllGo = Owned.New("CollapseAll");
            _collapseAllGo.transform.SetParent(_header, false);
            var caRect = (RectTransform)_collapseAllGo.transform;
            caRect.anchorMin = new Vector2(1f, 1f);
            caRect.anchorMax = new Vector2(1f, 1f);
            caRect.pivot = new Vector2(1f, 1f);
            caRect.anchoredPosition = Vector2.zero;
            caRect.sizeDelta = new Vector2(CollapseAllW, UI.Slot.HeaderH);
            _collapseAllPlate = _collapseAllGo.AddComponent<Image>();
            _collapseAllPlate.sprite = UI.Theme.RoundedFill;
            _collapseAllPlate.type = Image.Type.Sliced;
            _collapseAllPlate.raycastTarget = false;
            var caArrowGo = Owned.New("Arrow");
            caArrowGo.transform.SetParent(_collapseAllGo.transform, false);
            _collapseAllArrow = caArrowGo.AddComponent<Image>();
            _collapseAllArrow.sprite = UI.Theme.Triangle;
            _collapseAllArrow.raycastTarget = false;
            var caArrowRect = (RectTransform)caArrowGo.transform;
            caArrowRect.anchorMin = new Vector2(0.5f, 0.5f);
            caArrowRect.anchorMax = new Vector2(0.5f, 0.5f);
            caArrowRect.pivot = new Vector2(0.5f, 0.5f);
            caArrowRect.anchoredPosition = Vector2.zero;
            caArrowRect.sizeDelta = new Vector2(8f, 8f);

            _noMatches = UI.Theme.MakeText(_content, "NoMatches",
                UI.Theme.FontBody, UI.Theme.InkDim, TMPro.TextAlignmentOptions.Center);
            _noMatches.text = "No matches";
            var nmRect = (RectTransform)_noMatches.transform;
            nmRect.anchorMin = new Vector2(0f, 1f);
            nmRect.anchorMax = new Vector2(1f, 1f);
            nmRect.pivot = new Vector2(0f, 1f);
            nmRect.offsetMin = new Vector2(0f, -(GridTop + 60f));
            nmRect.offsetMax = new Vector2(0f, -(GridTop + 40f));
            _noMatches.gameObject.SetActive(false);

            BuildPools(inv, mn);
            BuildScrollbar(mn);
            ApplyCraftSurface();
            _poolColumns = UI.Slot.CraftCols;
            _root.SetActive(false); // shown by TryHandleOpen
            Plugin.Log.LogDebug($"Custom craft menu built: {_cells.Length} pooled cells, {_headers.Length} headers");
            return true;
        }

        // build the pool during idle time after a save loads, so the first menu
        // open doesn't pay ~90 Instantiate calls. Also parks the vanilla mask right
        // away: if it is still active when the panel first shows, the open pays the
        // 500-button canvas enable AND the mask->custom swap (measured 53-67ms).
        // Bench opens reactivate it via OnVanillaOpen.
        internal static void Prewarm(ManagersScript mn)
        {
            if (Settings.CustomGrid.Value && mn != null && mn.inventory != null
                && EnsureBuilt(mn.inventory, mn))
            {
                if (_maskGo != null && _maskGo.activeSelf)
                {
                    _maskGo.SetActive(false);
                }
            }
        }

        private static int RequiredPoolRows() => Mathf.CeilToInt(UI.Slot.GridH / CellPitch) + 2;

        private static void BuildPools(InventoryManager inv, ManagersScript mn)
        {
            UI.Slot.Read(mn, _shellRect.parent as RectTransform);
            int columns = UI.Slot.CraftCols;
            int poolRows = RequiredPoolRows();
            _poolRows = poolRows;

            // native look for free: clone a real recipe button, strip logic
            GameObject template = Owned.Instantiate(inv.itemSlot[100].gameObject);
            template.SetActive(false);
            foreach (Component comp in template.GetComponents<Component>())
            {
                if (!(comp is RectTransform) && !(comp is CanvasRenderer) && !(comp is Image))
                {
                    Object.DestroyImmediate(comp);
                }
            }
            var doomed = new List<GameObject>();
            foreach (Transform child in template.transform)
            {
                if (child.name != "itemImage" && child.name != "stack")
                {
                    doomed.Add(child.gameObject);
                }
                else
                {
                    foreach (Component comp in child.GetComponents<Component>())
                    {
                        if (!(comp is RectTransform) && !(comp is CanvasRenderer)
                            && !(comp is Image) && !(comp is Text))
                        {
                            Object.DestroyImmediate(comp);
                        }
                    }
                }
            }
            for (int i = 0; i < doomed.Count; i++)
            {
                Object.DestroyImmediate(doomed[i]);
            }

            _cells = new CellView[columns * poolRows];
            for (int i = 0; i < _cells.Length; i++)
            {
                GameObject go = Owned.Instantiate(template, _content, false);
                go.name = "cell";
                var rect = (RectTransform)go.transform;
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                Transform iconT = go.transform.Find("itemImage");
                Transform countT = go.transform.Find("stack");
                _cells[i] = new CellView
                {
                    Go = go,
                    Rect = rect,
                    Background = go.GetComponent<Image>(),
                    Icon = iconT != null ? iconT.GetComponent<Image>() : null,
                    Count = countT != null ? countT.GetComponent<Text>() : null,
                };
            }
            Object.Destroy(template);

            // header rows: one TMP label spanning the grid width (font borrowed from
            // the game's canvas, same approach as Sections)
            TMP_FontAsset font = null;
            TextMeshProUGUI reference = mn.uiMN.craftPanel.GetComponentInParent<Canvas>() != null
                ? mn.uiMN.craftPanel.GetComponentInParent<Canvas>().GetComponentInChildren<TextMeshProUGUI>()
                : null;
            if (reference != null)
            {
                font = reference.font;
            }
            _headers = new HeaderView[poolRows];
            float width = columns * CellPitch - 5f;
            for (int i = 0; i < _headers.Length; i++)
            {
                var go = Owned.New("header");
                go.transform.SetParent(_content, false);
                var rect = (RectTransform)go.transform;
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 0.5f);
                rect.sizeDelta = new Vector2(width, 20f);
                rect.sizeDelta = new Vector2(width, UI.Theme.SectionRowHeight);
                // Ink on the parchment surface. It was AccentGold, which was
                // right when the grid sat on a dark plate — and now the
                // surface decides, so the swing back to cream on wood is the rule
                // working rather than the argument reopening.
                var label = UI.Theme.MakeText(go.transform, "Label",
                    UI.Theme.FontBody, UI.Theme.Ink, TextAlignmentOptions.MidlineLeft);
                if (font != null)
                {
                    label.font = font;
                }
                // NOTE the font swap above: ApplyCraftSurface runs after this loop,
                // so the material it instances is the one the label actually keeps.
                // Stripping the outline before the swap would have decorated a
                // material that is about to be thrown away.
                label.enableWordWrapping = false;
                var labelRect = (RectTransform)label.transform;
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = Vector2.zero;
                labelRect.offsetMax = Vector2.zero;
                // disclosure triangle on the COLLAPSE-ALL PILL'S COLUMN. "Fold this
                // section" and "fold every section" are the same control at two
                // scopes, one directly above the other, so they must share a
                // vertical line — and it is the pill that cannot move, since its
                // arrow is centred in a plate flush to the grid's right edge. Half
                // the pill's width IS that centre; a hand-tuned inset here was 5
                // units off it and read as a wobble.
                var arrowGo = Owned.New("Arrow");
                arrowGo.transform.SetParent(go.transform, false);
                var arrow = arrowGo.AddComponent<Image>();
                arrow.sprite = UI.Theme.Triangle;
                arrow.raycastTarget = false;
                var arrowRect = (RectTransform)arrowGo.transform;
                arrowRect.anchorMin = new Vector2(1f, 0.5f);
                arrowRect.anchorMax = new Vector2(1f, 0.5f);
                arrowRect.pivot = new Vector2(0.5f, 0.5f);
                arrowRect.anchoredPosition = new Vector2(-(CollapseAllW / 2f), 0f);
                arrowRect.sizeDelta = new Vector2(8f, 8f);
                // bench icon beside the label, like the detail card's Bench row
                var iconGo = Owned.New("Icon");
                iconGo.transform.SetParent(go.transform, false);
                var icon = iconGo.AddComponent<Image>();
                icon.raycastTarget = false;
                icon.preserveAspect = true;
                var iconRect = (RectTransform)iconGo.transform;
                iconRect.anchorMin = new Vector2(0f, 0.5f);
                iconRect.anchorMax = new Vector2(0f, 0.5f);
                iconRect.pivot = new Vector2(0f, 0.5f);
                iconRect.anchoredPosition = new Vector2(RowTextInset, 0f);
                iconRect.sizeDelta = new Vector2(UI.Theme.SectionIconSize, UI.Theme.SectionIconSize);
                go.SetActive(false);
                _headers[i] = new HeaderView { Go = go, Rect = rect, Label = label, Icon = icon, Arrow = arrow };
            }
        }

        private static void BuildScrollbar(ManagersScript mn)
        {
            var go = Owned.New("CustomCraftScrollbar");
            go.transform.SetParent(_shellRect, false);
            var track = go.AddComponent<Image>();
            track.sprite = UI.Theme.RoundedFill;
            track.type = Image.Type.Sliced;
            track.color = new Color(0f, 0f, 0f, 0.16f);
            track.raycastTarget = true;
            var slide = Owned.New("SlidingArea");
            slide.transform.SetParent(go.transform, false);
            Stretch((RectTransform)slide.transform);
            var handle = Owned.New("Thumb");
            handle.transform.SetParent(slide.transform, false);
            var thumb = handle.AddComponent<Image>();
            thumb.sprite = UI.Theme.RoundedFill;
            thumb.type = Image.Type.Sliced;
            thumb.color = UI.SlotFilter.Accent(0, true);
            Stretch(thumb.rectTransform);
            _scrollbar = go.AddComponent<Scrollbar>();
            _scrollbar.handleRect = thumb.rectTransform;
            _scrollbar.targetGraphic = thumb;
            _scrollbar.direction = Scrollbar.Direction.BottomToTop;
            _scrollbar.navigation = new Navigation { mode = Navigation.Mode.None };
            _scrollbar.onValueChanged.AddListener(OnScrollbarValue);
            go.SetActive(false);
        }

        // Position the grid + scrollbar as one block, CENTERED horizontally in the
        // panel window and vertically where the vanilla mask sits — computed from
        // the actual rects (no magic offsets; the vanilla mask position leans left
        // because it reserved space for a left-side scrollbar). GridOffsetX
        // fine-tunes around the centered position.
        // D46 in one place. EVERY colour the crafting window's material decides is
        // assigned here and nowhere else — construction sets none of them — so
        // "what colour is the header label" has exactly one answer to read, and a
        // live config edit and a fresh build converge on the same code rather than
        // on two copies that agree until one of them is edited.
        //
        // This is the shape the parchment surface wanted and did not have: the parchment pass had to
        // find and change eleven scattered assignments, and the one it missed (the
        // search field's own text, in another file) shipped Ink on Ink for a day.
        internal static void ApplyCraftSurface()
        {
            if (_panelFill == null)
            {
                return; // not built yet; EnsureBuilt calls this at the end
            }
            UI.Theme.Surface s = UI.Theme.CraftSurface;

            // the kit field is parchment-coloured art; multiplying it down to the
            // bench colour keeps its grain as the dark field's own
            _panelFill.color = _panelFill.sprite != null
                ? new Color(s.Field.r / KitParch.r, s.Field.g / KitParch.g, s.Field.b / KitParch.b, 1f)
                : s.Field;
            if (_cover != null)
            {
                _cover.color = s.Field; // must equal the fill — see its build note
            }
            if (_collapseAllPlate != null)
            {
                _collapseAllPlate.color = s.Well;
            }
            if (_collapseAllArrow != null)
            {
                _collapseAllArrow.color = s.TextSoft;
            }
            if (_scrollbar != null)
            {
                _scrollbar.targetGraphic.color = UI.SlotFilter.Accent(0, true);
                _scrollbar.GetComponent<Image>().color = new Color(0.30f, 0.22f, 0.16f, 1f);
            }
            if (Active) EnsureFilter(_mn);
            PaintText(_titleWord, s.Text);
            if (_filter.Alive)
            {
                _filter.Paint(s);
            }
            PaintText(_noMatches, s.TextSoft);
            if (_headers != null)
            {
                for (int i = 0; i < _headers.Length; i++)
                {
                    HeaderView h = _headers[i];
                    if (h == null)
                    {
                        continue;
                    }
                    PaintText(h.Label, s.Text);
                    if (h.Arrow != null)
                    {
                        h.Arrow.color = s.TextSoft;
                    }
                }
            }
            ApplySheet(); // the blueprint paints over the surface's colours
        }

        // The chip's press-and-release; true while the pointer is over it, so the
        // grid beneath does not also read the click.
        private static bool TickClose(ManagersScript mn, Camera cam)
        {
            if (_closeChip == null || !_closeChip.gameObject.activeInHierarchy)
            {
                return false;
            }
            bool over = RectTransformUtility.RectangleContainsScreenPoint(
                _closeChip.rectTransform, Input.mousePosition, cam);
            if (Input.GetMouseButtonDown(0) && over)
            {
                _closePressed = true;
            }
            if (Input.GetMouseButtonUp(0))
            {
                bool fire = _closePressed && over;
                _closePressed = false;
                if (fire && mn != null && mn.uiMN != null)
                {
                    mn.uiMN.LeftPanelState(0); // vanilla's own close for the left panels
                    return true;
                }
            }
            else if (_closePressed && !Input.GetMouseButton(0))
            {
                _closePressed = false;
            }
            UI.CloseChip.Paint(_closeChip, over, over && _closePressed);
            return over;
        }

        private static void PaintText(TMPro.TextMeshProUGUI text, Color color)
        {
            if (text == null)
            {
                return;
            }
            text.color = color;
            UI.Theme.FlattenType(text);
        }

        internal static void SyncGeometry()
        {
            if (_root == null || _shellRect == null || _mn == null || _mn.uiMN == null
                || _mn.uiMN.craftPanel == null)
            {
                return;
            }
            var panelRect = (RectTransform)_mn.uiMN.craftPanel.transform;
            var canvasRoot = _shellRect.parent as RectTransform;
            if (canvasRoot == null || !UI.Slot.Read(_mn, canvasRoot))
            {
                return;
            }
            float w = UI.Slot.Width;
            float h = UI.Slot.Height;
            _shellRect.anchoredPosition = new Vector2(UI.Slot.Left, UI.Slot.Bottom);
            _shellRect.sizeDelta = new Vector2(w, h);
            UI.Theme.Place(_header, UI.Slot.Inset, UI.Slot.PadTop, w - 2f * UI.Slot.Inset, UI.Slot.HeaderH);
            float contentW = UI.Slot.CraftW;
            float gridTop = UI.Slot.GridTop;
            float gridH = UI.Slot.GridH;
            var rootRect = (RectTransform)_root.transform;
            UI.Theme.Place(rootRect, UI.Slot.CraftLeft + GridOffsetX, gridTop, contentW, gridH);
            if (_scrollbar != null)
            {
                UI.Theme.Place((RectTransform)_scrollbar.transform,
                    w - UI.Slot.Inset - UI.Slot.BarW, gridTop, UI.Slot.BarW, gridH);
            }
            // the preview card docks right of the shell. It is a child of vanilla's
            // panel and measures from that panel's top-left; the shell's top-left
            // is the same point, so the shell's width is the card's x.
            ItemCard.AlignTo(new Vector2(w + 6f, 8f));

            // the vanilla "Craft" bubble retires — the title lives INSIDE the
            // plate now (the shared header language); OnVanillaOpen restores the bubble
            if (_titlePill == null)
            {
                _titlePill = FindTitlePill(panelRect);
                if (_titlePill != null)
                {
                    _titleBasePos = _titlePill.anchoredPosition;
                    _titleBaseAnchorMin = _titlePill.anchorMin;
                    _titleBaseAnchorMax = _titlePill.anchorMax;
                    _titleBasePivot = _titlePill.pivot;
                }
            }
            if (_titlePill != null && _titlePill.gameObject.activeSelf)
            {
                _titlePill.gameObject.SetActive(false);
            }
        }

        private static bool _geometryDumped;

        // ---------- row model ----------

        private static void RebuildRows(InventoryManager inv, ManagersScript mn)
        {
            Rows.Clear();
            _matchCount = 0;
            CraftInfo[] recipes = mn.craftMN.craftData[0].craftInfo;
            int columns = Mathf.Max(1, _poolColumns);
            bool grouped = Settings.GroupRecipesBy.Value != Settings.RecipeGrouping.None;
            string currentGroup = null;
            var buffer = new List<int>(columns);

            for (int i = 0; i < recipes.Length && i < inv.craftCount; i++)
            {
                if (recipes[i] == null)
                {
                    continue;
                }
                ItemData item = mn.itemMN.FindItem(recipes[i].name);
                if (item == null || !inv.CraftListCheck(item))
                {
                    continue; // locked/hidden, exactly like the vanilla fill
                }
                if (grouped)
                {
                    string group = Sections.GroupLabel(mn, recipes[i]);
                    if (group != currentGroup)
                    {
                        FlushRow(buffer, columns);
                        Rows.Add(new RowDef
                        {
                            Header = group,
                            HeaderIcon = HeaderIconOf(mn, recipes[i]),
                            Collapsed = IsCollapsed(group),
                        });
                        currentGroup = group;
                    }
                    if (IsCollapsed(currentGroup))
                    {
                        _matchCount++; // still a filter match, just folded away
                        continue;
                    }
                }
                buffer.Add(i);
                _matchCount++;
                if (buffer.Count == columns)
                {
                    FlushRow(buffer, columns);
                }
            }
            FlushRow(buffer, columns);
            UpdateCollapseAllArrow();
        }

        private static void FlushRow(List<int> buffer, int columns)
        {
            if (buffer.Count == 0)
            {
                return;
            }
            var row = new int[columns];
            for (int i = 0; i < columns; i++)
            {
                row[i] = i < buffer.Count ? buffer[i] : -1;
            }
            Rows.Add(new RowDef { Recipes = row });
            buffer.Clear();
        }

        // Keep the grid's selection in agreement with in-card navigation.
        internal sealed class RecipePlace
        {
            public string Query;
            public int Scope;
            public float Scroll;
            public bool DefaultCollapsed;
            public Dictionary<string, bool> Groups;
        }

        internal static RecipePlace CaptureRecipePlace() => new RecipePlace
        {
            Query = _filter.Query,
            Scope = AggregatedCrafting.BenchScope,
            Scroll = _scroll,
            DefaultCollapsed = _defaultCollapsed,
            Groups = new Dictionary<string, bool>(CollapsedGroups),
        };

        internal static void RestoreRecipePlace(RecipePlace place)
        {
            if (place == null || _inv == null) return;
            AggregatedCrafting.BenchScope = place.Scope;
            if (_filter.Field != null) _filter.Field.text = place.Query;
            _defaultCollapsed = place.DefaultCollapsed;
            CollapsedGroups.Clear();
            foreach (var group in place.Groups) CollapsedGroups[group.Key] = group.Value;
            AggregatedCrafting.Refill(_inv);
            _scroll = Mathf.Clamp(place.Scroll, 0, MaxScroll());
            BindVisible();
            RefreshSelectionOutlines();
        }

        internal static void RevealRecipe(CraftInfo recipe)
        {
            if (recipe == null || _mn == null || _inv == null) return;
            // Filtering swaps the actual vanilla recipe array. A navigated-to
            // ingredient must enter that array before its Craft button can work.
            if (System.Array.IndexOf(_mn.craftMN.craftData[0].craftInfo, recipe) < 0
                && RecipeCatalog.IsReachable(RecipeCatalog.Of(recipe)))
            {
                _filter.Clear();
                AggregatedCrafting.BenchScope = 0;
                AggregatedCrafting.Refill(_inv);
            }
            SelectFromCard(recipe);
            if (SelectedRecipe < 0) return;
            string group = Sections.GroupLabel(_mn, recipe);
            if (IsCollapsed(group))
            {
                CollapsedGroups[group] = false;
                RebuildRows(_inv, _mn);
            }
            for (int row = 0; row < Rows.Count; row++)
            {
                if (Rows[row].Recipes == null || System.Array.IndexOf(Rows[row].Recipes, SelectedRecipe) < 0) continue;
                if (row < _scroll || row >= _scroll + VisibleRows())
                    _scroll = Mathf.Clamp(row - 1, 0, MaxScroll());
                break;
            }
            BindVisible();
            RefreshSelectionOutlines();
        }

        internal static void SelectFromCard(CraftInfo recipe)
        {
            _selectedRef = recipe;
            SelectedRecipe = recipe != null && _mn != null
                ? System.Array.IndexOf(_mn.craftMN.craftData[0].craftInfo, recipe) : -1;
            RefreshSelectionOutlines();
        }

        // Escape cascade: peel the deepest chain card first, then the
        // pin — only a bare Escape falls through to the close-all (CloseAll.cs)
        internal static bool TryEscapeDeselect()
        {
            if (!Active)
            {
                return false;
            }
            if (ItemCard.TryBack()) return true;
            // Escape RESETS and gets out of the way: it clears the pins, the chain
            // and every disclosure, then returns false so the close-all runs in the
            // same press. It used to peel one chain card at a time, which made
            // closing a menu you had browsed into cost as many presses as you had
            // taken steps — Escape means "put this away", not "one step back".
            DeselectFromCard();
            return false;
        }

        // the card's enamel tag: put the card away outright — the pin, the chain
        // and the previews — without Escape's step back through a detour
        internal static void DeselectFromCard()
        {
            SelectedRecipe = -1;
            _selectedRef = null;
            RefreshSelectionOutlines();
            _hoverRecipe = -2; // re-fire the hover preview under the cursor
            ItemCard.ResetChain();
            ItemCard.NotifyChanged();
        }

        private static void RefreshAfterFold()
        {
            RebuildRows(_inv, _mn);
            CraftingPatches.BeginSweepWindow();
            BindVisible();
            CraftingPatches.EndSweepWindow();
        }

        // the pill's arrow mirrors the dominant state: ▼ = something to collapse,
        // ▶ = everything folded; hidden entirely when there are no sections
        private static void UpdateCollapseAllArrow()
        {
            if (_collapseAllGo == null)
            {
                return;
            }
            bool anyHeader = false;
            bool anyExpanded = false;
            for (int i = 0; i < Rows.Count; i++)
            {
                if (Rows[i].Header != null)
                {
                    anyHeader = true;
                    if (!Rows[i].Collapsed)
                    {
                        anyExpanded = true;
                        break;
                    }
                }
            }
            if (_collapseAllGo.activeSelf != anyHeader)
            {
                _collapseAllGo.SetActive(anyHeader);
            }
            _collapseAllArrow.rectTransform.localRotation =
                Quaternion.Euler(0f, 0f, anyExpanded ? 0f : 90f);
        }

        // The vanilla title bubble is the panel child whose rect pokes ABOVE the
        // panel's top edge (everything else lives inside); ours are excluded.
        private static bool _titleDumped;

        private static RectTransform FindTitlePill(RectTransform panelRect)
        {
            var panelCorners = new Vector3[4];
            panelRect.GetWorldCorners(panelCorners); // [1] = top-left
            var corners = new Vector3[4];
            RectTransform found = null;
            var dump = new System.Text.StringBuilder("Custom menu: craftPanel children:");
            for (int i = 0; i < panelRect.childCount; i++)
            {
                var child = panelRect.GetChild(i) as RectTransform;
                if (child == null)
                {
                    continue;
                }
                child.GetWorldCorners(corners); // [1] = top-left, [0] = bottom-left
                bool ours = (_root != null && child.gameObject == _root)
                    || child.name == "ItemCard" || child.name == "FilterWell";
                dump.Append($"\n  '{child.name}' active={child.gameObject.activeSelf} " +
                    $"top={corners[1].y:0.0} bottom={corners[0].y:0.0} panelTop={panelCorners[1].y:0.0} ours={ours}");
                if (found == null && !ours && child.gameObject.activeSelf
                    && child.name != "mask"
                    && corners[1].y > panelCorners[1].y + 0.5f)
                {
                    found = child;
                }
            }
            if (!_titleDumped)
            {
                _titleDumped = true;
                Plugin.Log.LogInfo(dump.ToString());
                Plugin.Log.LogInfo(found != null
                    ? $"Custom menu: title bubble = '{found.name}'"
                    : "Custom menu: title bubble NOT found");
            }
            return found;
        }

        // The section's bench item sprite, like the detail card's Bench row.
        // Innate ("Basic"), NPC crafters and ItemType groupings have no bench item.
        private static Sprite HeaderIconOf(ManagersScript mn, CraftInfo recipe)
        {
            if (Settings.GroupRecipesBy.Value == Settings.RecipeGrouping.ItemType
                || !AggregatedCrafting.SourceOf.TryGetValue(recipe, out int panelID)
                || panelID == 0)
            {
                return null;
            }
            string benchKey = AggregatedCrafting.BenchKeyOf(panelID);
            ItemData bench = benchKey != null ? mn.itemMN.FindItem(benchKey) : null;
            return bench != null ? bench.itemSprite : null;
        }

        // ---------- binding ----------

        private static float VisibleRows()
        {
            return Mathf.Max(1f, (_content.rect.height - GridTop) / CellPitch);
        }

        private static float MaxScroll()
        {
            return Mathf.Max(0f, Rows.Count - VisibleRows());
        }

        private static void BindVisible()
        {
            _scroll = Mathf.Clamp(_scroll, 0f, MaxScroll());
            int firstRow = Mathf.FloorToInt(_scroll);
            int lastRow = Mathf.Min(Rows.Count - 1, firstRow + Mathf.CeilToInt(VisibleRows()));
            int cellCursor = 0;
            int headerCursor = 0;
            CraftInfo[] recipes = _mn.craftMN.craftData[0].craftInfo;

            for (int row = firstRow; row <= lastRow; row++)
            {
                float y = -((row - _scroll) * CellPitch + CellHalf + GridTop);
                RowDef def = Rows[row];
                if (def.Header != null)
                {
                    if (headerCursor < _headers.Length)
                    {
                        HeaderView header = _headers[headerCursor++];
                        header.Label.text = def.Header;
                        bool hasIcon = def.HeaderIcon != null;
                        header.Icon.enabled = hasIcon;
                        header.Icon.sprite = def.HeaderIcon;
                        // ▼ open, ▶ collapsed (down-triangle rotated +90°)
                        header.Arrow.rectTransform.localRotation =
                            Quaternion.Euler(0f, 0f, def.Collapsed ? 90f : 0f);
                        // indent the label past the icon (TMP margin, not a move)
                        header.Label.margin = new Vector4(
                            RowTextInset + (hasIcon ? 15f : 0f), 0f, 0f, 0f);
                        // x=0: the row IS the grid's span. It used to sit at 2,
                        // which pushed its right edge (and its arrow) 2 units past
                        // the last column and left its label 2 units short of the
                        // tab and filter labels above — off in both directions at
                        // once, which is why it read as a wobble rather than a
                        // margin.
                        header.Rect.anchoredPosition = new Vector2(0f, y);
                        SheetHeader(header, def.Header, hasIcon);
                        if (!header.Go.activeSelf)
                        {
                            header.Go.SetActive(true);
                        }
                    }
                    continue;
                }
                for (int col = 0; col < def.Recipes.Length && cellCursor < _cells.Length; col++)
                {
                    int recipeIdx = def.Recipes[col];
                    if (recipeIdx < 0)
                    {
                        continue;
                    }
                    CellView cell = _cells[cellCursor++];
                    cell.Rect.anchoredPosition = new Vector2(col * CellPitch + CellHalf, y);
                    {
                        BindCell(cell, recipeIdx, recipes[recipeIdx]);
                    }
                    if (!cell.Go.activeSelf)
                    {
                        cell.Go.SetActive(true);
                    }
                }
            }
            for (int i = cellCursor; i < _cells.Length; i++)
            {
                if (_cells[i].Go.activeSelf)
                {
                    _cells[i].Go.SetActive(false);
                }
                _cells[i].Recipe = -1;
            }
            for (int i = headerCursor; i < _headers.Length; i++)
            {
                if (_headers[i].Go.activeSelf)
                {
                    _headers[i].Go.SetActive(false);
                }
            }
            UpdateScrollbar();
        }

        private static void BindCell(CellView cell, int recipeIdx, CraftInfo recipe)
        {
            cell.Recipe = recipeIdx;
            ItemData item = _mn.itemMN.FindItem(recipe.name);
            if (cell.Icon != null)
            {
                cell.Icon.overrideSprite = item != null ? item.itemSprite : null;
            }
            RefreshCellAvailability(cell, recipe);
        }

        private static void RefreshCellAvailability(CellView cell, CraftInfo recipe)
        {
            bool craftable = _mn.gameMN.creative
                || CraftingPatches.RequirementsMet(_inv, recipe, bench: false);
            cell.Background.color = craftable
                ? new Color(1f, 1f, 1f, 1f)
                : new Color(1f, 1f, 1f, 0.5f);
            if (cell.Icon != null)
            {
                // vanilla dims the icon through the Button's disabled tint (we
                // stripped the Button) — apply the same standard disabled color
                cell.Icon.color = craftable
                    ? Color.white
                    : new Color(0.784f, 0.784f, 0.784f, 0.502f);
            }
            SheetCell(cell, craftable);
            if (cell.Count != null)
            {
                if (!_mn.gameMN.creative)
                {
                    int crafts = CraftCountDisplay.CraftableTimes(_inv, recipe, bench: false);
                    cell.Count.text = crafts <= 0 ? "" : (crafts > 999 ? "x999+" : $"x{crafts}");
                }
                else
                {
                    cell.Count.text = "";
                }
            }
        }

        // availability changed (inventory delta, chest re-scan) — no re-layout needed
        internal static void RefreshAvailability(InventoryManager inv)
        {
            if (!Active || _mn == null)
            {
                return;
            }
            CraftInfo[] recipes = _mn.craftMN.craftData[0].craftInfo;
            CraftingPatches.BeginSweepWindow();
            for (int i = 0; i < _cells.Length; i++)
            {
                int idx = _cells[i].Recipe;
                if (idx >= 0 && idx < recipes.Length && recipes[idx] != null && _cells[i].Go.activeSelf)
                {
                    RefreshCellAvailability(_cells[i], recipes[idx]);
                }
            }
            CraftingPatches.EndSweepWindow();
            ItemCard.RefreshCounts();
        }

        // ---------- scrolling ----------

        // same sign convention as CraftPanelLayout.ScrollRows: positive = toward top
        internal static void ScrollRows(float signedRows)
        {
            if (!Active)
            {
                return;
            }
            float before = _scroll;
            _scroll = Mathf.Clamp(_scroll - signedRows, 0f, MaxScroll());
            if (!Mathf.Approximately(before, _scroll))
            {
                CraftingPatches.BeginSweepWindow();
                BindVisible();
                CraftingPatches.EndSweepWindow();
            }
        }

        internal static float PageRows()
        {
            return Mathf.Floor(VisibleRows());
        }

        private static void OnScrollbarValue(float value)
        {
            if (!Active)
            {
                return;
            }
            // vanilla scrollbar convention: 1 = top
            _scroll = (1f - value) * MaxScroll();
            CraftingPatches.BeginSweepWindow();
            BindVisible();
            CraftingPatches.EndSweepWindow();
        }

        private static void UpdateScrollbar()
        {
            if (_scrollbar == null)
            {
                return;
            }
            float max = MaxScroll();
            _scrollbar.gameObject.SetActive(max > 0f);
            if (max > 0f)
            {
                _scrollbar.SetValueWithoutNotify(1f - _scroll / max);
                _scrollbar.size = Mathf.Clamp(VisibleRows() / Mathf.Max(1f, Rows.Count),
                    Mathf.Min(1f, 12f / Mathf.Max(1f, _content.rect.height)), 1f);
            }
        }

        // ---------- pointer input (polled, same proven path as hover) ----------

        private static int HitTest(Vector2 screenPos, Camera cam, out bool insideGrid, out Vector2 local)
        {
            insideGrid = false;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_content, screenPos, cam, out local))
            {
                return -1;
            }
            Rect rect = _content.rect;
            if (local.x < 0f || local.x > rect.width || local.y > 0f || local.y < -rect.height)
            {
                return -1;
            }
            insideGrid = true;
            float gridY = -local.y - GridTop;
            if (gridY < 0f)
            {
                return -1; // in the search bar strip, not the grid
            }
            int row = Mathf.FloorToInt((_scroll * CellPitch + gridY) / CellPitch);
            if (row < 0 || row >= Rows.Count || Rows[row].Header != null)
            {
                return -1;
            }
            int col = Mathf.FloorToInt(local.x / CellPitch);
            if (col < 0 || col >= Rows[row].Recipes.Length)
            {
                return -1;
            }
            return Rows[row].Recipes[col];
        }

        // ---------- hover / wheel / click (called every frame from Plugin.Update) ----------

        internal static void Tick(ManagersScript mn)
        {

            // the shell is not a child of the panel any more, so the panel
            // closing (LeftPanelState(0), Escape, the chip) has to take it down
            if (_shell != null && _shell.activeSelf
                && (mn == null || mn.uiMN == null || mn.uiMN.craftPanel == null
                    || !mn.uiMN.craftPanel.activeSelf))
            {
                _shell.SetActive(false);
            }
            if (!Active)
            {
                if (_hoverRecipe != -1)
                {
                    _hoverRecipe = -1;
                    ItemCard.Preview(-1);
                }
                // closing the menu drops the pin — each open starts unselected
                // (the ref can be set with index -1: an out-of-reach pin).
                if (SelectedRecipe >= 0 || _selectedRef != null)
                {
                    SelectedRecipe = -1;
                    _selectedRef = null;
                    ItemCard.NotifyChanged();
                }
                return;
            }
            Canvas canvas = _content.GetComponentInParent<Canvas>();
            Camera cam = canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.rootCanvas.worldCamera
                : null;
            // Nothing in the header row answers clicks any more. The tab bar
            // used to get first refusal here so a click that landed on a tab was
            // not also a click on the cell beneath it; the title is not clickable,
            // so the grid reads the pointer directly.
            // The close chip, then the filter, get FIRST REFUSAL on a click, before
            // the grid reads it: a press that lands on either must not also pin
            // the recipe behind it. This is the same order the tab bar had before
            // it was retired.
            if (TickClose(mn, cam))
            {
                return;
            }
            if (Input.GetMouseButtonDown(0)
                && _filter.HandleClick(Input.mousePosition, cam))
            {
                return;
            }
            int recipe = HitTest(Input.mousePosition, cam, out bool insideGrid, out Vector2 local);

            // Wheel over either the list or its scroll gutter.
            bool overScrollbar = _scrollbar != null && _scrollbar.gameObject.activeInHierarchy
                && RectTransformUtility.RectangleContainsScreenPoint(
                    (RectTransform)_scrollbar.transform, Input.mousePosition, cam);
            if (insideGrid || overScrollbar)
            {
                float wheel = Input.mouseScrollDelta.y;
                if (!Mathf.Approximately(wheel, 0f))
                {
                    ScrollRows((wheel > 0f ? 1f : -1f) * Mathf.Max(0.1f, Settings.ScrollSpeed.Value));
                }
            }
            // clicking a header row folds/unfolds its section
            if (insideGrid && Input.GetMouseButtonDown(0))
            {
                float gridY = -local.y - GridTop;
                int row = gridY >= 0f ? Mathf.FloorToInt(gridY / CellPitch + _scroll) : -1;
                if (row >= 0 && row < Rows.Count && Rows[row].Header != null)
                {
                    string group = Rows[row].Header;
                    CollapsedGroups[group] = !IsCollapsed(group);
                    RefreshAfterFold();
                }
            }
            if (Input.GetMouseButtonDown(0) && _collapseAllGo != null && _collapseAllGo.activeInHierarchy
                && RectTransformUtility.RectangleContainsScreenPoint(
                    (RectTransform)_collapseAllGo.transform, Input.mousePosition, cam))
            {
                bool anyExpanded = false;
                for (int i = 0; i < Rows.Count; i++)
                {
                    if (Rows[i].Header != null && !Rows[i].Collapsed)
                    {
                        anyExpanded = true;
                        break;
                    }
                }
                // GLOBAL state: the default covers sections not yet seen, so
                // a bench walked into later arrives in the same fold state
                _defaultCollapsed = anyExpanded;
                CollapsedGroups.Clear();
                RefreshAfterFold();
            }

            // RIGHT-CLICK CRAFTS. The gesture was free on this grid, and it already
            // means "do what this item does" in the Inventory — for a recipe that
            // is: make one. It saves the trip to the card for the common case
            // without taking anything from the left button, which still only pins.
            // More than one goes through the count dialog and nowhere else (crafting
            // several at once happens via the shift-click dialog) — Ctrl's x100 is gone.
            if (recipe >= 0 && Input.GetMouseButtonDown(1))
            {
                CraftInfo[] all = _mn.craftMN.craftData[0].craftInfo;
                CraftInfo info = recipe < all.Length ? all[recipe] : null;
                if (info != null)
                {
                    if (ClickMap.Shift)
                    {
                        ItemCard.ChooseQuantity(info);
                        return;
                    }
                    // craftableCheck lets vanilla refuse: out of reach, short of
                    // a material, or clamped out of the live grid
                    _inv.CraftItem(100 + recipe, craftableCheck: true);
                }
            }

            // Interaction model: click purely toggles the pin — crafting lives
            // in the detail card's buttons. Clicking the pinned item again unpins
            // it (back to hover-preview mode); clicking another item moves the pin.
            if (recipe >= 0 && Input.GetMouseButtonDown(0))
            {
                ItemCard.ResetChain();
                SelectedRecipe = SelectedRecipe == recipe ? -1 : recipe;
                CraftInfo[] recipes = _mn.craftMN.craftData[0].craftInfo;
                _selectedRef = SelectedRecipe >= 0 && SelectedRecipe < recipes.Length
                    ? recipes[SelectedRecipe]
                    : null;
                RefreshSelectionOutlines();
                ItemCard.NotifyChanged();
                // keep showing the hovered item either way (NotifyChanged clears
                // the preview, and the pointer hasn't moved)
                ItemCard.Preview(recipe);
            }

            // hover previews in the docked window (the vanilla teaser is gone) —
            // but a PINNED item locks the card: hover changes nothing until unpinned
            if (recipe != _hoverRecipe)
            {
                _hoverRecipe = recipe;
                if (_selectedRef == null) // an out-of-reach pin still locks the card
                {
                    ItemCard.Preview(recipe);
                }
            }
            SettleDrawings();
            ItemCard.Tick(cam);
        }

        private static void RefreshSelectionOutlines()
        {
            for (int i = 0; i < _cells.Length; i++)
            {
                // the choice is the sheet's amber box (BrowserShell.Sheet.cs)
                if (_cells[i].Pin != null)
                {
                    _cells[i].Pin.enabled = _cells[i].Go.activeSelf && _cells[i].Recipe == SelectedRecipe;
                }
            }
        }
    }
}
