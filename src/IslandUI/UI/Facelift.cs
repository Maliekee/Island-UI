using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI.UI
{
    // THE WINDOW FACELIFT (art/ref/window-facelift-v1, approved): the
    // vicinity's amber leather (look 02, full and compact) and crafting's blue
    // painted blueprint (overview look 11, recipe look 12). The pieces are the
    // runtime package's content-free 1x components, copied flat into art/ under
    // their manifest ids (runtime-ready/assets.json): one sprite pixel a reference
    // screen pixel at 1440p, so Theme.KitPpu draws them at their designed size.
    //
    // Painted pieces are drawn white; only the glyph masks are tinted. A disabled
    // background carries its own alpha - its label is faded, never the piece twice.
    internal static class Facelift
    {
        public const float Px = 1f / Theme.KitPpu;

        // 9-slice borders, per side, from the manifest (all symmetric). A piece not
        // listed is simple (a close key, a checkbox, a glyph) or stretched (paper).
        private static readonly Dictionary<string, int> Borders = new Dictionary<string, int>
        {
            { "vicinity-frame", 25 },
            { "vicinity-cell-empty", 5 },
            { "vicinity-cell-occupied", 5 },
            { "vicinity-button", 4 },
            { "vicinity-portrait-mount", 5 },
            { "vicinity-quantity-badge", 2 },
            { "vicinity-filter", 2 },
            { "vicinity-scroll-track", 2 },
            { "vicinity-scroll-thumb", 3 },
            { "craft-station-header", 5 },
            { "craft-station-body", 7 },
            { "craft-station-collapsed", 6 },
            { "craft-button-secondary", 3 },
            { "craft-button-primary", 3 },
            { "craft-filter", 2 },
            { "craft-scroll-track", 2 },
            { "craft-scroll-thumb", 3 },
            { "craft-outer-frame", 20 },
            { "craft-recipe-section-frame", 5 },
            { "craft-dropdown", 2 },
            { "craft-related-cell", 2 },
        };

        // the family a state sprite belongs to: "vicinity-button-hover" -> "vicinity-button"
        private static int BorderOf(string id)
        {
            if (Borders.TryGetValue(id, out int b))
            {
                return b;
            }
            int cut = id.LastIndexOf('-');
            return cut > 0 && Borders.TryGetValue(id.Substring(0, cut), out b) ? b : 0;
        }

        public enum State { Idle, Hover, Pressed, Focus, Selected, Disabled }

        // The state priority the package asks for: disabled, pressed, focus or
        // selected, hover, idle. Selection outranks hover, so a selected piece under
        // the pointer keeps its selected rim.
        public static State Pick(bool disabled, bool pressed, bool focus, bool selected, bool hover)
        {
            return disabled ? State.Disabled : pressed ? State.Pressed : selected ? State.Selected
                : focus ? State.Focus : hover ? State.Hover : State.Idle;
        }

        public static string Id(string family, State state)
        {
            switch (state)
            {
                case State.Hover: return family + "-hover";
                case State.Pressed: return family + "-pressed";
                case State.Focus: return family + "-focus";
                case State.Selected: return family + "-selected";
                case State.Disabled: return family + "-disabled";
                default: return family + "-idle";
            }
        }

        // A piece on an Image at its designed size: sliced by its manifest border,
        // or simple. Idempotent, so a paint loop may call it every frame.
        public static void Skin(Image image, string id)
        {
            if (image == null)
            {
                return;
            }
            int border = BorderOf(id);
            Sprite sprite = Theme.Kit(id, border);
            if (image.sprite != sprite)
            {
                image.sprite = sprite;
            }
            Image.Type type = border > 0 ? Image.Type.Sliced : Image.Type.Simple;
            if (image.type != type)
            {
                image.type = type;
            }
            image.preserveAspect = false;
            image.pixelsPerUnitMultiplier = Theme.KitPpu;
            image.fillCenter = true;
        }

        // A stretched piece (the papers): drawn whole across its rect.
        public static void Stretch(Image image, string id)
        {
            Skin(image, id);
            image.type = Image.Type.Simple;
        }

        // A grid that repeats at its own logical size and is never stretched.
        public static void Tile(Image image, string id)
        {
            Sprite sprite = Theme.KitTile(id);
            if (image.sprite != sprite)
            {
                image.sprite = sprite;
            }
            image.type = Image.Type.Tiled;
            image.preserveAspect = false;
            image.pixelsPerUnitMultiplier = Theme.KitPpu;
        }

        public static Image Make(Transform parent, string name, string id)
        {
            var go = Owned.New(name);
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.raycastTarget = false;
            image.color = Color.white;
            Skin(image, id);
            return image;
        }

        // ---------- type ----------

        // The HUD's Noto Serif, as the leather's head sets it: the strong weight for
        // titles, labels and buttons, the body weight for the rest. Size in
        // reference pixels.
        public static TextMeshProUGUI Text(Transform parent, string name, bool strong, float px, Color ink,
            TextAlignmentOptions align)
        {
            TextMeshProUGUI text = Theme.MakeText(parent, name, px * Px, ink, align);
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            Face(text, strong);
            return text;
        }

        public static void Face(TMP_Text text, bool strong, bool shade = false, Color shadeInk = default)
        {
            TMP_FontAsset face = strong ? InventoryWindow.HudStrongFont : InventoryWindow.HudBodyFont;
            if (face == null)
            {
                return;
            }
            if (text.font != face)
            {
                text.font = face;
                text.fontStyle = FontStyles.Normal;
                text.characterSpacing = 0f;
                Theme.FlattenType(text);
                if (shade && text.fontSharedMaterial != null)
                {
                    Material m = text.fontMaterial;
                    m.EnableKeyword(ShaderUtilities.Keyword_Underlay);
                    m.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(shadeInk.r, shadeInk.g, shadeInk.b, 0.85f));
                    m.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.45f);
                    m.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.35f);
                    m.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0f);
                }
            }
        }

        // ---------- the vicinity's inks (vicinity-system.css) ----------

        public static readonly Color VicInk = Theme.ParseColor("#F4E4C7", Color.white);       // title, labels
        public static readonly Color VicCaption = Theme.ParseColor("#F1DBB9", Color.white);   // a holder's name
        public static readonly Color VicFoot = Theme.ParseColor("#F7E5C4", Color.white);      // the footer's words
        public static readonly Color VicFree = Theme.ParseColor("#E3C9A3", Color.white);      // "11 free"
        public static readonly Color VicQuery = Theme.ParseColor("#FFEDCE", Color.white);     // typed in the filter
        public static readonly Color VicHint = Theme.ParseColor("#D3B895", Color.white);      // its placeholder
        public static readonly Color VicCount = Theme.ParseColor("#E9C58A", Color.white);     // its count
        public static readonly Color VicRule = Theme.ParseColor("#D4A16455", Color.white);    // between holders
        public static readonly Color VicFootRule = Theme.ParseColor("#D4A16477", Color.white); // over the footer
        public static readonly Color VicShade = Theme.ParseColor("#30170E", Color.black);     // type's soft shadow
        public const float SearchDim = 0.30f;   // a nonmatching item, not the disabled state

        // ---------- crafting's inks (look 11 / 12) ----------

        public static readonly Color CraftInk = Theme.ParseColor("#F6ECD8", Color.white);     // labels
        public static readonly Color CraftDim = Theme.ParseColor("#BDD4E1", Color.white);     // secondary
        public static readonly Color CraftShort = Theme.ParseColor("#FFBA9C", Color.white);   // a shortage
        public static readonly Color CraftLit = Theme.ParseColor("#F0CD89", Color.white);     // actionable hint, focus
        public static readonly Color CraftOnPrimary = Theme.ParseColor("#18365B", Color.black); // Craft's word, enabled
        public static readonly Color CraftOff = Theme.ParseColor("#D0E2EB", Color.white);     // Craft's word, disabled
        public static readonly Color CraftTint = Theme.ParseColor("#06285222", Color.clear);  // a section's interior
    }
}
