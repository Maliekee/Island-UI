using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI.UI
{
    // One header travels with the active slot occupant; icon, name and accent
    // identify the window without relying on wood.
    //
    // A FILTER PER WINDOW, in the one header: the search in the left windows -
    // crafting, vicinity, roster - was shared and worked on the target's and your
    // own bag as well; it is specific to the window and disconnected from both
    // bags. Each occupant keeps its own field, and so its own query:
    // switching tabs shows that tab's query, and typing in one filters nothing
    // else - the D47a rule the shared field had quietly undone.
    internal static class SlotFilter
    {
        public const float H = Slot.HeaderH;
        public const float X = 80f;          // right of the window's name
        public const float CountW = 30f;
        public static readonly FilterField Crafting = new FilterField();
        public static readonly FilterField Vicinity = new FilterField();
        public static readonly FilterField Roster = new FilterField();

        public static FilterField For(int occupant)
        {
            return occupant == 0 ? Crafting : occupant == 2 ? Roster : Vicinity;
        }
        private static RectTransform _header;
        private static Image _icon, _rule, _card;
        private static Image[] _cardPin;
        private static TextMeshProUGUI _title;

        public static Color Accent(int occupant, bool dark = false)
        {
            if (occupant == 0)
                return dark ? Theme.ParseColor("#C9AA80", Color.white) : Theme.ParseColor("#995723", Color.white);
            return occupant == 2 ? Theme.RosterControl : Theme.ParseColor("#3F7163", Color.white);
        }

        public static bool Attach(Transform parent, ManagersScript mn, float x, float top, bool dark, int occupant = 0)
        {
            if (parent == null) return false;
            if (_header == null)
            {
                var go = Owned.New("SlotHeader");
                go.transform.SetParent(parent, false);
                _header = (RectTransform)go.transform;
                // the pin belonged to the header that died (a scene load): its
                // Images are gone with it, and PlacePin on them threw for every
                // occupant, cutting off the crafting open and leaving the roster's
                // card unplaced (a white square over the roster)
                _cardPin = null;
                // the roster's title card in the tool shed, under the word
                _card = Theme.KitImage(_header, "Card", "kit_shed_paper", 8, Color.white);
                _card.enabled = false;
                _icon = Theme.KitImage(_header, "Identity", "kit_cross", 0, Color.white);
                _title = Theme.MakeKitText(_header, "Title", Theme.FontBody, Theme.KitInk, TextAlignmentOptions.MidlineLeft);
                _title.fontStyle = FontStyles.Bold;
                _title.enableWordWrapping = false;
                _rule = Theme.KitImage(_header, "Accent", "kit_cross", 0, Color.white);
                _rule.sprite = null;
                _rule.type = Image.Type.Simple;
                _icon.raycastTarget = _title.raycastTarget = _rule.raycastTarget = false;
            }
            if (_header.parent != parent) _header.SetParent(parent, false);
            _header.anchorMin = _header.anchorMax = Vector2.zero;
            _header.pivot = new Vector2(0f, 1f);
            _header.anchoredPosition = new Vector2(x, top);
            _header.sizeDelta = new Vector2(Slot.Width - 2f * Slot.Inset, H);
            if (_header.GetSiblingIndex() != parent.childCount - 1) _header.SetAsLastSibling();
            if (!_header.gameObject.activeSelf) _header.gameObject.SetActive(true);
            FilterField field = For(occupant);
            for (int i = 0; i < 3; i++)
            {
                FilterField other = For(i);
                if (other == field || !other.Alive) continue;
                other.Blur();
                if (other.Go.activeSelf) other.Go.SetActive(false);
            }
            // the wells are named apart, so a harness path names one of them
            string well = occupant == 0 ? "FilterWell" : occupant == 2 ? "RosterFilterWell" : "VicinityFilterWell";
            if (!field.Alive && !field.Build(_header, mn, "", Theme.FontBody, wellName: well)) return false;
            field.SetPlaceholder(Shared.Loc.Get("slot.filter", "Filter..."));
            if (field.Go.transform.parent != _header) field.Go.transform.SetParent(_header, false);
            // to the row's end, short of the collapse-all pill: a fixed 120 was sized
            // when the well still started at the row's left, and once the name stood
            // before it, it ran under the pill
            Theme.Place((RectTransform)field.Go.transform, X, 0f, _header.sizeDelta.x - X - Slot.PillReserve, H);
            if (!field.Go.activeSelf) field.Go.SetActive(true);
            // crafting is a blueprint (BrowserShell.Sheet.cs), the vicinity the
            // facelift's leather; the roster is the shed's
            bool sheet = occupant == 0;
            if (occupant == 1)
            {
                VicinityHead(field);
                return true;
            }
            if (sheet)
            {
                field.Sheet();
            }
            else
            {
                field.Shed(occupant);
            }
            field.Layout(CountW);
            _title.text = occupant == 0 ? Shared.Loc.Get("hud.chip.crafting", "Crafting")
                : occupant == 2 ? Shared.Loc.Get("roster.title", "Roster") : Shared.Loc.Get("hud.chip.vicinity", "Vicinity");
            _icon.sprite = HudGlyphs.Get(occupant == 0 ? "craft" : occupant == 2 ? "roster" : "vicinity");
            _icon.color = _title.color = _rule.color = sheet ? Blueprint.Ink : Accent(occupant, dark);
            Theme.Place(_icon, 0f, 0f, H, H);
            Theme.Place(_title, H + 3f, 0f, 61f, H);
            Theme.Place(_rule, 0f, H + 2f, _header.rect.width, 0.65f);
            ShedTitle(!sheet, occupant);
            if (sheet)
            {
                // the title block: the name lettered, no glyph, the rule kept
                _icon.enabled = false;
                _title.text = _title.text.ToUpperInvariant();
                if (Blueprint.Face != null && _title.font != Blueprint.Face)
                {
                    _title.font = Blueprint.Face;
                    Theme.FlattenType(_title);
                }
                _title.fontSize = 11.5f;
                _title.fontStyle = FontStyles.Normal;
                _title.overflowMode = TextOverflowModes.Overflow;
                Theme.Place(_title, 0f, 0f, 78f, H);
            }
            // every path above set the size it is drawn at
            Theme.FitLanguage(_title, _title.fontSize);
            return true;
        }

        // THE VICINITY'S HEAD (facelift look 02): "Vicinity" in the HUD's serif on
        // the leather, the blank painted well to its right, and the rule under the
        // row. Placed in the design's pixels from the frame's top-left; the header
        // itself stands Slot.Inset in and Slot.PadTop down from there.
        private const float HeadX = Slot.Inset / Facelift.Px;     // 32 px
        private const float HeadY = Slot.PadTop / Facelift.Px;    // 25.6 px

        private static void VicinityHead(FilterField field)
        {
            float px = Facelift.Px;
            Theme.Place((RectTransform)field.Go.transform, (333f - HeadX) * px, (19f - HeadY) * px, 405f * px, 32f * px);
            // the query and count at the roster's size (FontBody), not the design's
            // 16 and 15 px: the two swap places in the same slot
            float roster = Theme.FontBody / px;
            field.Wear("vicinity-filter", Facelift.VicQuery, Facelift.VicHint, Facelift.VicCount, roster, roster);
            field.Layout(CountW);
            _icon.enabled = false;
            _card.enabled = false;
            ToolShed.PlacePin(_cardPin, 0f, 0f, 0f, Color.white, false);
            _title.text = Shared.Loc.Get("hud.chip.vicinity", "Vicinity");
            Facelift.Face(_title, true);
            _title.fontSize = 24f * px;
            _title.fontStyle = FontStyles.Normal;
            _title.characterSpacing = 0f;
            _title.color = Facelift.VicInk;
            _title.overflowMode = TextOverflowModes.Overflow;
            Theme.Place(_title, (27f - HeadX) * px, (23f - HeadY) * px, (333f - 27f - 16f) * px, 24f * px);
            Theme.FitLanguage(_title, _title.fontSize);
            _rule.enabled = true;
            _rule.color = Facelift.VicFootRule;
            Theme.Place(_rule, (22f - HeadX) * px, (63f - HeadY) * px, 760f * px, px);
        }

        // THE TOOL SHED'S NAMES (window-system.md 6.30): the pegboard's painted in
        // stencil, the crate's stencilled on its boards, the roster's written in
        // marker on a card pinned to the cork. No glyph and no rule: the word on
        // the material is the window's name.
        private static void ShedTitle(bool shed, int occupant)
        {
            _icon.enabled = _rule.enabled = !shed;
            _card.enabled = shed && occupant == 2;
            if ((_cardPin == null || _cardPin[1] == null) && shed && occupant == 2)
            {
                _cardPin = ToolShed.MakePin(_header, "CardPin");
            }
            ToolShed.PlacePin(_cardPin, 25f, -4.5f, 7f, Theme.ParseColor("#3A8F5A", Color.green), shed && occupant == 2);
            if (!shed)
            {
                TMP_FontAsset plain = Theme.KitFont(true);
                if (plain != null && _title.font != plain)
                {
                    _title.font = plain;
                }
                _title.fontSize = Theme.FontBody;
                _title.characterSpacing = 0f;
                _title.fontStyle = FontStyles.Bold;
                _title.overflowMode = TextOverflowModes.Ellipsis;
                _card.rectTransform.localRotation = Quaternion.identity;
                return;
            }
            // Overflow, not Ellipsis: a line taller than its rect is DROPPED by
            // Ellipsis (Theme.CaptionH's note), and a painted name is taller than
            // the row it is painted across
            _title.overflowMode = TextOverflowModes.Overflow;
            if (occupant == 2)
            {
                ToolShed.Hand(_title, ToolShed.MarkerFont, ToolShed.Marker);
                _title.fontSize = 10.5f;
                _title.characterSpacing = 0f;
                _title.fontStyle = FontStyles.Bold;
                Theme.Place(_card, -2f, -2f, 62f, H + 4f);
                _card.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 1.6f);
                Theme.Place(_title, 6f, 0f, 54f, H);
                return;
            }
            _title.text = _title.text.ToUpperInvariant();
            ToolShed.Hand(_title, ToolShed.StencilFont, occupant == 0 ? ToolShed.Paint : ToolShed.Stencil);
            _title.fontSize = 11.5f;
            _title.characterSpacing = 9f;
            _title.fontStyle = FontStyles.Normal;
            Theme.Place(_title, 0f, -0.5f, 78f, H + 1f);
        }

        public static void Hide(Transform owner)
        {
            if (_header != null && _header.parent == owner && _header.gameObject.activeSelf)
                _header.gameObject.SetActive(false);
        }
    }
}
