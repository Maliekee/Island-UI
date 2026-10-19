using IslandUI.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI
{
    // THE LOVE WIDGET (window-system.md §6.20, decided from the R6
    // concept mockup): one relation between two
    // people, both directions, on one scale.
    //
    // One quiet track on the game's scale; the fill runs from the zero line to
    // HIS heart - the carrier's own edge, the one the game reads - in the colour
    // of its class; his heart solid and rimmed, standing on the track with a
    // pointer; the edge back a smaller hollow heart hanging under the track.
    // Gate ticks at 50 and 75 through the track, the zero line through all of
    // it. Nothing at 100: the largest gate in the code is 75.
    //
    // THE CLASSES FOLLOW THE GATES, read from the decompiled source (every
    // numeric comparison on a love): acquainted to 50 (pale), lover
    // above 50 (pink - the menu's Sex button, NPCManager.SexButton needs > 50),
    // devoted at 75 (deep - Kana's and Reika's stories), hostile below 0 (ash,
    // and the heart cracked - red already means love). Vanilla's 70 is only a
    // colour in its own lists and is not a class here.
    internal sealed class LoveWidget
    {
        public const float RowH = 16f;          // a relation row, on the sheet and in the matrix
        public const float ValueW = 52f;        // "1315 · 1315"

        // THE SCALE: 0 (a lover at all), 50, 75 - and love has no cap (items
        // double it; 1315 seen), so the band is linear to 100 where the gates
        // are and a log tail to 2000 beyond; below zero only -10 does anything,
        // so the negative side is short.
        private const float Min = -25f;
        private const float LinearEnd = 100f;
        private const float Cap = 2000f;
        private const float NegShare = 0.12f;
        private const float LinShare = 0.58f;

        public static float Pos(float love)
        {
            if (love < 0f)
            {
                return NegShare * (1f - Mathf.InverseLerp(0f, -Min, Mathf.Min(-love, -Min)));
            }
            if (love <= LinearEnd)
            {
                return NegShare + LinShare * Mathf.InverseLerp(0f, LinearEnd, love);
            }
            float tail = Mathf.Log10(Mathf.Min(love, Cap) / LinearEnd) / Mathf.Log10(Cap / LinearEnd);
            return NegShare + LinShare + (1f - NegShare - LinShare) * Mathf.Clamp01(tail);
        }

        public static readonly Color Pale = new Color(238f / 255f, 172f / 255f, 205f / 255f, 1f);
        public static readonly Color Pink = new Color(255f / 255f, 140f / 255f, 235f / 255f, 1f);
        public static readonly Color Deep = new Color(200f / 255f, 60f / 255f, 175f / 255f, 1f);
        public static readonly Color Ash = new Color(128f / 255f, 116f / 255f, 104f / 255f, 1f);
        private static readonly Color HollowQuiet = new Color(150f / 255f, 110f / 255f, 130f / 255f, 1f);
        private static readonly Color InkLover = new Color(196f / 255f, 84f / 255f, 178f / 255f, 1f);
        private static readonly Color InkDevoted = new Color(150f / 255f, 40f / 255f, 130f / 255f, 1f);

        public static Color ClassColor(float love)
        {
            if (love < 0f) { return Ash; }
            if (love <= 50f) { return Pale; }
            return love < 75f ? Pink : Deep;
        }

        // the same class as type on parchment: acquainted in the sheet's ink
        public static Color ClassInk(float love)
        {
            if (love < 0f) { return Ash; }
            if (love <= 50f) { return Theme.Ink; }
            return love < 75f ? InkLover : InkDevoted;
        }

        // "55 · 0", each number in the ink of its class, the back "?" when there
        // is no edge back yet
        public static string Numbers(float love, float back, bool backKnown)
        {
            string a = "<color=#" + ColorUtility.ToHtmlStringRGBA(ClassInk(love)) + ">" + love.ToString("0") + "</color>";
            string sep = "<color=#" + ColorUtility.ToHtmlStringRGBA(Theme.InkDim) + "> · </color>";
            string b = backKnown
                ? "<color=#" + ColorUtility.ToHtmlStringRGBA(ClassInk(back)) + ">" + back.ToString("0") + "</color>"
                : "<color=#" + ColorUtility.ToHtmlStringRGBA(Theme.InkDim) + ">?</color>";
            return a + sep + b;
        }

        // THE NUMBERS LIVE IN A TOOLTIP: the bar is illustrative enough that the
        // numbers belong on hover. Two lines, each edge named by its direction, so
        // the tip also answers which way an edge runs.
        public static string TipText(string from, string to, float love, float back, bool backKnown)
        {
            string a = "<color=#" + ColorUtility.ToHtmlStringRGBA(TipInk(love)) + ">" + love.ToString("0") + "</color>";
            string b = backKnown
                ? "<color=#" + ColorUtility.ToHtmlStringRGBA(TipInk(back)) + ">" + back.ToString("0") + "</color>"
                : "?";
            return from + " → " + to + "  " + a + "\n" + to + " → " + from + "  " + b;
        }

        // the class on dark paper: the fill colours read there; ash goes a shade lighter
        private static Color TipInk(float love)
        {
            return love < 0f ? new Color(178f / 255f, 166f / 255f, 152f / 255f, 1f) : ClassColor(love);
        }

        // ---- the parts, in canvas units
        private const float TrackH = 2.5f;      // kit_hairline at its designed size
        private const float HeartS = 5.0f;
        private const float RimS = 6.2f;
        private const float HollowS = 4.2f;
        private const float PointerW = 3.0f;
        private const float PointerH = 2.0f;
        private const float Lift = 5.4f;         // heart centres, above and below the track's centre

        public Image Track;
        public Image Fill;      // the fill is zoned, its colour following the
        public Image Fill2;     // different zones: pale to 50,
        public Image Fill3;     // pink to 75, deep beyond - each piece its zone's colour
        public Image Zero;
        public Image Tick50;
        public Image Tick75;
        public Image Rim;
        public Image Heart;
        public Image Pointer;
        public Image Heart2;
        public Image Pointer2;
        public float TrackX;
        public float TrackW;
        private float _cy;
        private string _heartSkin;
        private string _heart2Skin;

        public static LoveWidget Create(Transform parent)
        {
            var w = new LoveWidget();
            w.Track = Theme.KitImage(parent, "Track", "kit_hairline", 4, TrackTint());
            w.Fill = Theme.KitImage(parent, "Fill", "kit_hairline", 4, Pale);
            w.Fill2 = Theme.KitImage(parent, "Fill2", "kit_hairline", 4, Pink);
            w.Fill3 = Theme.KitImage(parent, "Fill3", "kit_hairline", 4, Deep);
            w.Tick50 = Theme.KitImage(parent, "Tick50", "kit_hairline", 4, TickTint());
            w.Tick75 = Theme.KitImage(parent, "Tick75", "kit_hairline", 4, TickTint());
            w.Zero = Theme.KitImage(parent, "Zero", "kit_hairline", 4, Theme.Ink);
            w.Pointer2 = Theme.KitImage(parent, "Pointer2", "kit_pointer", 0, Theme.Ink);
            w.Heart2 = Theme.KitImage(parent, "Heart2", "kit_heart_hollow", 0, HollowQuiet);
            w.Pointer = Theme.KitImage(parent, "Pointer", "kit_pointer", 0, Theme.Ink);
            w.Rim = Theme.KitImage(parent, "Rim", "kit_heart", 0, Theme.Ink);
            w.Heart = Theme.KitImage(parent, "Heart", "kit_heart", 0, Pale);
            w._heartSkin = "kit_heart";
            w._heart2Skin = "kit_heart_hollow";
            w.Pointer2.rectTransform.localEulerAngles = new Vector3(0f, 0f, 180f);
            return w;
        }

        private static Color TrackTint() => new Color(Theme.Ink.r, Theme.Ink.g, Theme.Ink.b, 0.30f);
        private static Color TickTint() => new Color(Theme.Ink.r, Theme.Ink.g, Theme.Ink.b, 0.5f);

        public void Show(bool on)
        {
            Set(Track, on); Set(Fill, on); Set(Fill2, on); Set(Fill3, on);
            Set(Zero, on); Set(Tick50, on); Set(Tick75, on);
            Set(Rim, on); Set(Heart, on); Set(Pointer, on);
            if (!on) { Set(Heart2, false); Set(Pointer2, false); }
        }

        private static void Set(Image image, bool on)
        {
            if (image != null && image.gameObject.activeSelf != on) { image.gameObject.SetActive(on); }
        }

        // `x, y, w, h` is the row's rect the widget lives in; the track spans w
        public void Place(float x, float y, float w, float h)
        {
            TrackX = x;
            TrackW = Mathf.Max(20f, w);
            _cy = y + h * 0.5f + 0.5f;
            Theme.Place(Track, TrackX, _cy - TrackH * 0.5f, TrackW, TrackH);
            Theme.Place(Fill, TrackX, _cy - TrackH * 0.5f, 0f, TrackH);
            Theme.Place(Fill2, TrackX, _cy - TrackH * 0.5f, 0f, TrackH);
            Theme.Place(Fill3, TrackX, _cy - TrackH * 0.5f, 0f, TrackH);
            float z = TrackX + Pos(0f) * TrackW;
            Theme.Place(Zero, z - 0.6f, _cy - 3.4f, 1.2f, 6.8f);
            Theme.Place(Tick50, TrackX + Pos(50f) * TrackW - 0.4f, _cy - 2.2f, 0.8f, 4.4f);
            Theme.Place(Tick75, TrackX + Pos(75f) * TrackW - 0.4f, _cy - 2.2f, 0.8f, 4.4f);
        }

        public void Paint(float love, float back, bool backKnown)
        {
            float zero = TrackX + Pos(0f) * TrackW;
            float hx = TrackX + Pos(love) * TrackW;
            Color col = ClassColor(love);
            // the fill in pieces, each the colour of the zone it crosses: below
            // zero one ash piece leftwards; above, pale to 50, pink to 75, deep on
            if (love < 0f)
            {
                Span(Fill, hx, zero, Ash);
                Span(Fill2, zero, zero, Pink);
                Span(Fill3, zero, zero, Deep);
            }
            else
            {
                float at50 = TrackX + Pos(50f) * TrackW;
                float at75 = TrackX + Pos(75f) * TrackW;
                Span(Fill, zero, Mathf.Min(hx, at50), Pale);
                Span(Fill2, at50, Mathf.Clamp(hx, at50, at75), Pink);
                Span(Fill3, at75, Mathf.Max(hx, at75), Deep);
            }
            string skin = love < 0f ? "kit_heart_broken" : "kit_heart";
            if (skin != _heartSkin) { Theme.KitSkin(Heart, skin, 0); _heartSkin = skin; }
            Heart.color = col;
            Centre(Rim, hx, _cy - Lift + 0.15f, RimS, RimS);
            Centre(Heart, hx, _cy - Lift, HeartS, HeartS);
            Centre(Pointer, hx, _cy - 2.3f, PointerW, PointerH);
            Set(Heart2, backKnown);
            Set(Pointer2, backKnown);
            if (backKnown)
            {
                float bx = TrackX + Pos(back) * TrackW;
                string skin2 = back < 0f ? "kit_heart_broken_hollow" : "kit_heart_hollow";
                if (skin2 != _heart2Skin) { Theme.KitSkin(Heart2, skin2, 0); _heart2Skin = skin2; }
                Heart2.color = back < 0f || back > 50f ? ClassColor(back) : HollowQuiet;
                Centre(Heart2, bx, _cy + Lift, HollowS, HollowS);
                Centre(Pointer2, bx, _cy + 2.3f, PointerW, PointerH);
            }
        }

        // a piece of the fill from x0 to x1 along the track (nothing when x1 <= x0)
        private static void Span(Image image, float x0, float x1, Color color)
        {
            var rect = image.rectTransform;
            rect.anchoredPosition = new Vector2(x0, rect.anchoredPosition.y);
            rect.sizeDelta = new Vector2(Mathf.Max(0f, x1 - x0), rect.sizeDelta.y);
            image.color = color;
        }

        // a mark by its centre, so the flipped pointer turns about itself
        private static void Centre(Image image, float cx, float cy, float w, float h)
        {
            var rect = image.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(cx, -cy);
            rect.sizeDelta = new Vector2(w, h);
        }
    }

    // THE GATES, written once over a column of relations rather than on every
    // row: the spark at -10 (the relation drains moral), the zero, the heart at
    // 50 (they say yes), the star at 75 (the story) - each with its number.
    internal sealed class GateHeader
    {
        private const float MarkS = 3.4f;
        private const float LabelSize = 4.6f;
        private static readonly float[] Gates = { -10f, 50f, 75f };
        private static readonly string[] Skins = { "kit_spark", "kit_heart", "kit_star" };

        private readonly Image[] _marks = new Image[3];
        private readonly TextMeshProUGUI[] _labels = new TextMeshProUGUI[3];
        private Image _zero;
        private static readonly Color MarkTint = new Color(90f / 255f, 62f / 255f, 38f / 255f, 1f);

        public static GateHeader Create(Transform parent)
        {
            var g = new GateHeader();
            for (int i = 0; i < 3; i++)
            {
                g._marks[i] = Theme.KitImage(parent, "Gate" + i, Skins[i], 0, MarkTint);
                g._labels[i] = Theme.MakeKitText(parent, "GateLabel" + i, LabelSize, Theme.InkDim,
                    TextAlignmentOptions.Center, bold: true);
                g._labels[i].enableWordWrapping = false;
                g._labels[i].overflowMode = TextOverflowModes.Overflow;
                g._labels[i].text = Gates[i].ToString("0");
            }
            g._zero = Theme.KitImage(parent, "GateZero", "kit_hairline", 4, MarkTint);
            return g;
        }

        public void Show(bool on)
        {
            for (int i = 0; i < 3; i++)
            {
                if (_marks[i].gameObject.activeSelf != on) { _marks[i].gameObject.SetActive(on); }
                if (!on && _labels[i].gameObject.activeSelf) { _labels[i].gameObject.SetActive(false); }
            }
            if (_zero.gameObject.activeSelf != on) { _zero.gameObject.SetActive(on); }
        }

        // over a track that spans trackX..trackX+trackW; `y` is the top of the
        // strip, `h` its height (marks in the upper half, numbers below)
        public void Place(float trackX, float trackW, float y, float h)
        {
            float my = y + h * 0.28f;
            // the numbers only where 50 and 75 do not run into each other - a
            // narrow track (the matrix's columns) keeps the marks alone
            bool numbered = (LoveWidget.Pos(75f) - LoveWidget.Pos(50f)) * trackW >= 11f;
            for (int i = 0; i < 3; i++)
            {
                float x = trackX + LoveWidget.Pos(Gates[i]) * trackW;
                Theme.Place(_marks[i], x - MarkS * 0.5f, my - MarkS * 0.5f, MarkS, MarkS);
                Theme.Place(_labels[i], x - 8f, my + MarkS * 0.5f - 0.6f, 16f, LabelSize + 1.5f);
                if (_labels[i].gameObject.activeSelf != numbered) { _labels[i].gameObject.SetActive(numbered); }
            }
            float z = trackX + LoveWidget.Pos(0f) * trackW;
            Theme.Place(_zero, z - 0.5f, my - MarkS * 0.5f, 1f, MarkS);
        }
    }
}
