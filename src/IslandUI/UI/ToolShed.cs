using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI.UI
{
    // THE TOOL SHED (window-system.md 6.30), one suite of the left slot's drawn
    // directions. The slot's three windows and the containers stop being
    // parchment in a walnut rail and become a shed's furniture, each its own
    // material: every container a pine SHIPPING CRATE, the roster a CORKBOARD
    // (crafting, which was a workshop PEGBOARD, is the blueprint now). What they
    // share is how a shed labels things - masking tape, marker, stencil paint - so
    // they read as one room and not several themes.
    //
    // This class holds the room's vocabulary; each window applies it where it
    // already decides its own look.
    internal static class ToolShed
    {
        // the room's inks
        public static readonly Color Paint = Theme.ParseColor("#F0E7D2", Color.white);     // stencil paint on the pegboard
        public static readonly Color PaintRed = Theme.ParseColor("#B8412F", Color.red);    // a painted outline, the close tag
        // inside an empty outline: a step darker and a little thin, so a board of
        // missing tools stays quieter than the tools that hang on it
        public static readonly Color OutlineFill = Theme.ParseColor("#E6D8B8D9", Color.white);
        public static readonly Color Stencil = Theme.ParseColor("#1E1812", Color.black);    // stencil on a crate
        public static readonly Color Marker = Theme.ParseColor("#1F1A15", Color.black);     // marker on tape and paper
        public static readonly Color MarkerRed = Theme.ParseColor("#C0392B", Color.red);
        public static readonly Color Pencil = Theme.ParseColor("#6B6458", Color.gray);
        public static readonly Color Brand = Theme.ParseColor("#5A3A1E", Color.black);      // a burnt-in brand, a multiply
        public static readonly Color Highlight = Theme.ParseColor("#F7D35A", Color.yellow); // what the pointer is on

        // THE ROOM'S HANDS (D136). All three are installed Windows faces, built into
        // TMP fonts from their files at runtime (UI/SystemFont), so nothing ships.
        // Impact stands in for stencil paint: Bahnschrift is a variable font whose
        // condensed bold the mockups used, and a file read gives only its regular.
        private static TMP_FontAsset _stencil, _marker, _typed;
        private static bool _stencilTried, _markerTried, _typedTried;
        private static Font _legacyMarker, _legacyStencil;

        public static TMP_FontAsset StencilFont => Face(ref _stencil, ref _stencilTried, "impact.ttf", "Impact");
        public static TMP_FontAsset MarkerFont => Face(ref _marker, ref _markerTried, "Inkfree.ttf", "Ink Free");
        public static TMP_FontAsset TypedFont => Face(ref _typed, ref _typedTried, "courbd.ttf", "Courier New Bold");

        // For vanilla's legacy Text (the crafting cell's stack count, cloned from
        // the game's slot): a UnityEngine.Font, which OS fonts give directly.
        public static Font LegacyMarker => _legacyMarker != null ? _legacyMarker
            : (_legacyMarker = Font.CreateDynamicFontFromOSFont("Ink Free", 32));
        public static Font LegacyStencil => _legacyStencil != null ? _legacyStencil
            : (_legacyStencil = Font.CreateDynamicFontFromOSFont("Impact", 32));

        private static TMP_FontAsset Face(ref TMP_FontAsset face, ref bool tried, string file, string name)
        {
            if (!tried && Theme.Font != null)
            {
                tried = true;
                face = SystemFont.Build(file, "IslandUI " + name, Theme.Font);
            }
            return face;
        }

        // Set a text in one of the room's hands, flat (no harvested outline).
        public static void Hand(TMP_Text text, TMP_FontAsset face, Color color)
        {
            if (text == null)
            {
                return;
            }
            if (face != null && text.font != face)
            {
                text.font = face;
            }
            text.color = color;
            Theme.FlattenType(text);
        }

        // A PUSHPIN: its shadow on the cork, the head in the colour it is tinted, a
        // glint that stays white. Three images, made once and placed together.
        public static Image[] MakePin(Transform parent, string name)
        {
            var pin = new Image[3];
            pin[0] = Theme.KitImage(parent, name + "Shadow", "kit_shed_pin_shadow", 0, Color.white);
            pin[1] = Theme.KitImage(parent, name, "kit_shed_pin", 0, Color.white);
            pin[2] = Theme.KitImage(parent, name + "Glint", "kit_shed_pin_glint", 0, Color.white);
            return pin;
        }

        public static void PlacePin(Image[] pin, float x, float y, float size, Color color, bool shown)
        {
            if (pin == null)
            {
                return;
            }
            for (int i = 0; i < pin.Length; i++)
            {
                if (pin[i] == null)
                {
                    return;   // destroyed with its parent: nothing to place, nothing to throw
                }
                if (pin[i].gameObject.activeSelf != shown)
                {
                    pin[i].gameObject.SetActive(shown);
                }
                if (shown)
                {
                    Theme.Place(pin[i], x, y, size, size);
                    pin[i].transform.SetAsLastSibling();
                }
            }
            if (shown)
            {
                pin[1].color = color;
            }
        }

        // A tiled field: the pegboard, the cork, the crate's boards.
        public static void Tile(Image image, string name)
        {
            if (image == null)
            {
                return;
            }
            image.sprite = Theme.KitTile(name);
            image.type = Image.Type.Tiled;
            image.preserveAspect = false;
            image.pixelsPerUnitMultiplier = Theme.KitPpu;
            image.color = Color.white;
        }

        // A sliced piece: tape, a label, a frame (fillCenter off for a frame).
        public static void Slice(Image image, string name, int border, bool fillCenter = true)
        {
            if (image == null)
            {
                return;
            }
            Theme.KitSkin(image, name, border);
            image.fillCenter = fillCenter;
            image.color = Color.white;
        }
    }

    // THE CARD AS A MANILA TAG (window-system.md 6.30). An item's card - the one
    // beside a recipe on the pegboard, the one over a crate's slot - is a shipping
    // tag: manila, its free end notched, an eyelet there, every word on it in
    // marker. The card keeps drawing itself as it did; this runs after it and
    // turns its type from cream-on-dark to marker-on-manila, remembering what it
    // changed so the dark card can be handed back.
    internal sealed class ShedTag
    {
        private struct Ink
        {
            public Color Plain;
            public Color Applied;
            public TMP_FontAsset Font;
        }

        private readonly GameObject _card;
        private readonly Image _back;
        private readonly Outline _edge;
        private Image _eyelet;
        private bool _worn;
        private readonly System.Collections.Generic.Dictionary<TMP_Text, Ink> _inks =
            new System.Collections.Generic.Dictionary<TMP_Text, Ink>();
        private static readonly System.Collections.Generic.List<TMP_Text> Texts =
            new System.Collections.Generic.List<TMP_Text>();

        public ShedTag(GameObject card)
        {
            _card = card;
            _back = card.GetComponent<Image>();
            _edge = card.GetComponent<Outline>();
        }

        public void Apply(bool? enabled = null)
        {
            if (_card == null || _back == null)
            {
                return;
            }
            bool on = enabled ?? true;
            if (!on && !_worn)
            {
                return;
            }
            if (on)
            {
                Theme.KitSkin(_back, "kit_shed_tag", 30);
                _back.preserveAspect = false;
                _back.color = Color.white;
                if (_edge != null)
                {
                    _edge.enabled = false;
                }
                if (_eyelet == null)
                {
                    _eyelet = Theme.KitImage(_card.transform, "Eyelet", "kit_shed_eyelet", 0, Color.white);
                    RectTransform r = _eyelet.rectTransform;
                    r.anchorMin = r.anchorMax = new Vector2(1f, 0.5f);
                    r.pivot = new Vector2(0.5f, 0.5f);
                    // on the notched end, clear of the words: the card pads its
                    // content six units in, the eyelet stands in the last five
                    r.sizeDelta = new Vector2(8f, 8f);
                    r.anchoredPosition = new Vector2(-4.5f, 0f);
                }
                _eyelet.enabled = true;
            }
            else
            {
                _back.sprite = null;
                _back.type = Image.Type.Simple;
                _back.color = Theme.PlateDark;
                if (_edge != null)
                {
                    _edge.enabled = true;
                }
                if (_eyelet != null)
                {
                    _eyelet.enabled = false;
                }
            }
            _card.GetComponentsInChildren(true, Texts);
            TMP_FontAsset marker = ToolShed.MarkerFont;
            for (int i = 0; i < Texts.Count; i++)
            {
                TMP_Text t = Texts[i];
                bool known = _inks.TryGetValue(t, out Ink ink);
                // the card recoloured it since (a material row's state): that is its
                // plain colour now
                if (!known || t.color != ink.Applied)
                {
                    ink.Plain = t.color;
                    ink.Font = known && t.font == marker ? ink.Font : t.font;
                }
                if (on)
                {
                    ink.Applied = Remap(ink.Plain);
                    t.color = ink.Applied;
                    if (marker != null && t.font != marker)
                    {
                        t.font = marker;
                        Theme.FlattenType(t);
                    }
                }
                else
                {
                    t.color = ink.Plain;
                    ink.Applied = ink.Plain;
                    if (ink.Font != null && t.font != ink.Font)
                    {
                        t.font = ink.Font;
                        // a font assignment brings the face's own material back,
                        // outline and all: the recipe card's type came home bold
                        // after a visit to a raw material's tag
                        Theme.FlattenType(t);
                    }
                }
                _inks[t] = ink;
            }
            Texts.Clear();
            _worn = on;
        }

        // light type becomes marker; a strong hue (a gold heading, a green count,
        // a red shortfall) keeps some of itself; what is already dark stays
        private static Color Remap(Color c)
        {
            float l = 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
            if (l < 0.42f)
            {
                return c;
            }
            float sat = Mathf.Max(c.r, Mathf.Max(c.g, c.b)) - Mathf.Min(c.r, Mathf.Min(c.g, c.b));
            Color ink = sat > 0.35f ? Color.Lerp(ToolShed.Marker, c, 0.4f)
                : l > 0.8f ? ToolShed.Marker : ToolShed.Pencil;
            ink.a = c.a;
            return ink;
        }
    }
}
