using IslandUI.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI
{
    internal static partial class InventoryWindow
    {
        private static Image _worldMarker;
        private static TextMeshProUGUI _worldName, _worldSubtitle;
        // UNDER EVERY WINDOW (the indicator icon showed through a character's
        // open sheets): the marker
        // stands in the world, so it lives on its own layer at the bottom of the
        // canvas the unit shares, not inside the unit - which is created after the
        // windows and drew over them. The dock stays in the unit, above them;
        // the layer comes and goes with it (ShowUnit).
        private static GameObject _worldLayer;

        // Selected A / Enamel Coin: a compact walnut label behind
        // a brass-rimmed enamel medallion. Friendly NPCs also carry the selected
        // A status footer, below the original kind label and health/stun bars.
        private static Image _emblemRim, _emblemFill, _emblemGlyph, _emblemCoin;
        private static Image _markerHealth, _markerHealthFill, _markerStamina, _markerStaminaFill;
        private static SheetRow _markerHealthRuler, _markerStunRuler;
        private static TextMeshProUGUI _emblemLevel;
        private static TMP_FontAsset _markerFont, _markerFontBold;
        private static bool _markerFontsTried;
        private static string _markerMeasuredName, _markerMeasuredSubtitle;
        private static float _markerMeasuredWidth;
        private const float EmblemD = 36f;
        private const float MarkerScale = .88f;
        private const float EmblemPad = 23f;
        private const float MarkerRightPad = 8f;
        // Keep the established bar length; only the words can widen the plate.
        private const float MarkerBarWidth = 64f;
        private const float MarkerMinWidth = EmblemPad + MarkerBarWidth + MarkerRightPad;
        private static readonly Color EmblemYours = Theme.ParseColor("#65C99B", Color.green);
        private static readonly Color EmblemHostile = Theme.ParseColor("#E95447", Color.red);
        private static readonly Color EmblemStranger = Theme.ParseColor("#E4B454", Color.yellow);
        private static readonly Color EmblemBody = Theme.ParseColor("#BAB9B3", Color.grey);
        private static readonly Color EmblemThing = Theme.ParseColor("#E9B858", Color.yellow);
        private static readonly Color EmblemTheirThing = Theme.ParseColor("#B59A78", Color.grey);
        private static readonly Color EmblemInk = Theme.ParseColor("#20150E", Color.black);
        private static readonly Color MarkerCream = Theme.ParseColor("#F8EBCD", Color.white);
        private static readonly Color MarkerMuted = Theme.ParseColor("#D4C09B", Color.white);

        private static void EnsureWorldMarker()
        {
            if (_worldMarker != null) { return; }
            EnsureMarkerFonts();
            _worldLayer = Owned.New("IslandUI_WorldTarget");
            _worldLayer.transform.SetParent(_unit.transform.parent, false);
            var layer = (RectTransform)_worldLayer.transform;
            layer.anchorMin = Vector2.zero;
            layer.anchorMax = Vector2.one;
            layer.offsetMin = layer.offsetMax = Vector2.zero;
            layer.SetAsFirstSibling();
            CanvasIsolation.Isolate(_worldLayer);
            _worldLayer.SetActive(_unit.activeSelf);
            _worldMarker = Theme.KitImage(layer, "WorldTarget", "kit_emblem_plate", 20, Color.white);
            _unitRow.LabelFont = _markerFont;
            _worldName = MarkerText("Name", 9.5f, MarkerCream, true);
            _worldSubtitle = MarkerText("Kind", 7f, MarkerMuted, false);
            _markerMeasuredName = null;
            _unitStrip.Go.SetActive(false);
            _emblemRim = Theme.KitImage(_worldMarker.transform, "EmblemRim", "kit_emblem_rim", 0, Color.white);
            _emblemFill = Theme.KitImage(_worldMarker.transform, "Emblem", "kit_emblem_enamel", 0, Color.white);
            _emblemGlyph = Theme.KitImage(_worldMarker.transform, "EmblemGlyph", "kit_emblem_heart", 0, Color.white);
            _emblemCoin = Theme.KitImage(_worldMarker.transform, "EmblemCoin", "kit_emblem_coin", 0, Color.white);
            // Center the visible digits, not Georgia's ascender/descender space
            // and side bearings (which make the numeral drift inside the coin).
            _emblemLevel = Theme.MakeKitText(_emblemCoin.transform, "Level", 8f, EmblemInk, TextAlignmentOptions.MidlineGeoAligned, bold: true);
            SetMarkerFont(_emblemLevel, true);
            _emblemLevel.enableWordWrapping = false;
            _emblemLevel.enableAutoSizing = true;
            _emblemLevel.fontSizeMin = 5f;
            _emblemLevel.fontSizeMax = 8f;
            _emblemLevel.raycastTarget = false;
            _markerHealth = MarkerTrack("Health", out _markerHealthFill);
            _markerStamina = MarkerTrack("Stamina", out _markerStaminaFill);
            _markerHealthRuler = new SheetRow { Go = _markerHealth.gameObject };
            _markerStunRuler = new SheetRow { Go = _markerStamina.gameObject };
            EnsureMarkerStatus();
        }

        private static void EnsureMarkerFonts()
        {
            if (_markerFontsTried) { return; }
            _markerFontsTried = true;
            _markerFont = SystemFont.Build("georgia.ttf", "IslandUI Emblem Georgia", Theme.Font);
            _markerFontBold = SystemFont.Build("georgiab.ttf", "IslandUI Emblem Georgia Bold", Theme.Font);
        }

        private static TextMeshProUGUI MarkerText(string name, float size, Color color, bool bold)
        {
            var text = Theme.MakeKitText(_worldMarker.transform, name, size, color, TextAlignmentOptions.MidlineLeft, bold);
            SetMarkerFont(text, bold);
            text.enableWordWrapping = false;
            text.enableAutoSizing = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            return text;
        }

        private static void SetMarkerFont(TMP_Text text, bool bold)
        {
            TMP_FontAsset font = bold ? _markerFontBold : _markerFont;
            if (font != null)
            {
                text.font = font;
                text.fontStyle = FontStyles.Normal; // bold is its own font, not an extra dilation
                Theme.FlattenType(text);
            }
        }

        private static Image MarkerTrack(string name, out Image fill)
        {
            Image track = Theme.KitImage(_worldMarker.transform, name, "kit_emblem_track", 8, Color.white);
            fill = Theme.KitImage(track.transform, "Fill", "kit_emblem_bar", 8, MarkerCream);
            return track;
        }

        private static void PaintMarkerBar(Image track, Image fill, float x, float y, float width,
            float height, float fraction, Color color)
        {
            Theme.Place(track, x, y, width, height);
            float f = Mathf.Clamp01(fraction);
            fill.gameObject.SetActive(f > 0f);
            Theme.Place(fill, .6f, .6f, (width - 1.2f) * f, height - 1.2f);
            fill.color = color;
        }

        private static void ShowMarkerBars(bool on)
        {
            _markerHealth.gameObject.SetActive(on);
            _markerStamina.gameObject.SetActive(on);
        }

        // Use the roster's ruler verbatim: thin hundreds, heavy thousands, and
        // omit a rank when its intervals cannot be drawn at this width. Each
        // pool lives under its track so corpse/folded/prop transitions hide it.
        private static void PaintMarkerRuler(SheetRow row, float max, float width, float height)
        {
            float innerWidth = width - 1.2f;
            float innerHeight = height - 1.2f;
            if (Mathf.Approximately(row.DrewMax, max) && Mathf.Approximately(row.TrackW, innerWidth)
                && Mathf.Approximately(row.BarH, innerHeight)) { return; }
            PlaceTicks(row, new SheetLine { Max = max }, .6f, innerWidth, .6f, innerHeight);
            row.TrackW = innerWidth;
            row.BarH = innerHeight;
            ShowMarkerMarks(row.Ticks, row.DrewTicks);
            ShowMarkerMarks(row.Majors, row.DrewMajors);
        }

        private static void ShowMarkerMarks(Image[] pool, int count)
        {
            for (int i = 0; pool != null && i < pool.Length; i++)
            {
                pool[i].gameObject.SetActive(i < count);
                if (i < count) { pool[i].transform.SetAsLastSibling(); }
            }
        }

        private static float MarkerWidth()
        {
            if (_markerMeasuredName != _worldName.text || _markerMeasuredSubtitle != _worldSubtitle.text)
            {
                _markerMeasuredName = _worldName.text;
                _markerMeasuredSubtitle = _worldSubtitle.text;
                float words = Mathf.Max(_worldName.GetPreferredValues(_worldName.text).x,
                    _worldSubtitle.GetPreferredValues(_worldSubtitle.text).x);
                _markerMeasuredWidth = Mathf.Max(MarkerMinWidth, Mathf.Ceil(words) + EmblemPad + MarkerRightPad);
            }
            // Ellipsis remains a fallback only for text wider than the screen.
            var parent = (RectTransform)_unit.transform.parent;
            float available = (parent.rect.width - 12f) / MarkerScale - (EmblemD * .5f - 2f);
            return Mathf.Min(_markerMeasuredWidth, Mathf.Max(MarkerMinWidth, available));
        }

        private static void LayoutMarkerWords(float width, bool on)
        {
            _worldName.gameObject.SetActive(on);
            _worldSubtitle.gameObject.SetActive(on);
            float textW = Mathf.Max(0f, width - EmblemPad - MarkerRightPad);
            Theme.Place(_worldName, EmblemPad, 0f, textW, 17f);
            Theme.Place(_worldSubtitle, EmblemPad, 13.5f, textW, 12f);
        }

        private static void PlaceWorldMarker(Vector3 screen, float width, float height, bool folded, float footer = 0f)
        {
            var parent = (RectTransform)_unit.transform.parent;
            var canvas = parent.GetComponentInParent<Canvas>();
            var uiCam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, uiCam, out Vector2 local);
            // The footer extends below the plaque's rect: lift the whole marker
            // by that amount so its bottom still clears the target and the dock.
            local.y += footer * MarkerScale;
            // Centre and clamp the complete silhouette, including the overhanging
            // medallion. Clamping only the plaque could clip the badge at the edge.
            float overhang = folded ? 0f : EmblemD * .5f - 2f;
            float visualWidth = (width + overhang) * MarkerScale;
            local.x = Mathf.Clamp(local.x, parent.rect.xMin + visualWidth * .5f + 6f,
                parent.rect.xMax - visualWidth * .5f - 6f);
            local.x += overhang * .5f * MarkerScale;
            float extraTop = Mathf.Max(0f, (EmblemD - height) * .5f) * MarkerScale;
            local.y = Mathf.Clamp(local.y, parent.rect.yMin + 51f + footer * MarkerScale,
                parent.rect.yMax - height * MarkerScale - extraTop - 6f);
            var r = _worldMarker.rectTransform;
            r.localScale = Vector3.one * MarkerScale;
            r.pivot = new Vector2(.5f, 0f);
            r.sizeDelta = new Vector2(width, height);
            r.position = parent.TransformPoint(local);
        }

        private static void EmblemOf(CommonStates who, out string glyph, out Color colour)
        {
            bool hostile = Hostile(who);
            bool yours = who.Employed() && !hostile;
            glyph = yours ? "heart" : hostile ? "swords" : "person";
            colour = yours ? EmblemYours : hostile ? EmblemHostile : EmblemStranger;
            if (_unitDead) { glyph = "skull"; }
            if (who.dead != 0) { colour = EmblemBody; }
        }

        private static void PaintEmblem(string glyph, Color colour, int level, float width, float height, bool folded, bool status = false)
        {
            float d = EmblemD;
            float cx = folded ? width * .5f : 2f;
            float cy = height * .5f;
            Theme.Place(_emblemRim, cx - d * .5f, cy - d * .5f, d, d);
            float inset = d * .74f;
            Theme.Place(_emblemFill, cx - inset * .5f, cy - inset * .5f - .3f, inset, inset);
            _emblemFill.color = colour;
            Sprite mark = Theme.Kit("kit_emblem_" + glyph, 0);
            if (_emblemGlyph.sprite != mark) { _emblemGlyph.sprite = mark; }
            float icon = d * (status ? .66f : .57f);
            Theme.Place(_emblemGlyph, cx - icon * .5f, cy - icon * .5f - .5f, icon, icon);
            bool coin = level > 0;
            _emblemCoin.gameObject.SetActive(coin);
            if (coin)
            {
                // Anchor the level coin to the medallion, independent of the
                // relationship, footer or folded state.
                const float c = 15f;
                Theme.Place(_emblemCoin, cx + 4.5f, cy + 4f, c, c);
                string lv = level.ToString();
                if (_emblemLevel.text != lv) { _emblemLevel.text = lv; }
                Theme.Place(_emblemLevel, 1f, 0f, c - 2f, c);
            }
            _emblemRim.transform.SetAsLastSibling();
            _emblemFill.transform.SetAsLastSibling();
            _emblemGlyph.transform.SetAsLastSibling();
            _emblemCoin.transform.SetAsLastSibling();
        }

        private static void PaintWorldMarker(CommonStates who)
        {
            EnsureWorldMarker();
            ShowThingBars(0);
            _unitHead.gameObject.SetActive(false);
            _unitName.gameObject.SetActive(false);
            _unitKind.gameObject.SetActive(false);
            _unitAlertPlate.gameObject.SetActive(false);
            _unitStrip.Go.SetActive(false);
            _unitChevron.gameObject.SetActive(false);
            var camera = Camera.main;
            if (camera == null || who == null)
            {
                _worldMarker.gameObject.SetActive(false);
                HideGroundRing();
                return;
            }
            EmblemOf(who, out _, out Color ringColour);
            PaintGroundRing(who.transform, ringColour, PersonRadius);
            var position = who.transform.position + Vector3.up * 3f;
            var body = who.GetComponentInChildren<Renderer>();
            if (body != null) { position.y = Mathf.Max(position.y, body.bounds.max.y + .25f); }
            var screen = camera.WorldToScreenPoint(position);
            bool visible = screen.z > 0;
            _worldMarker.gameObject.SetActive(visible);
            if (!visible) { return; }
            string kind = KindName(_mn != null ? _mn.npcMN : null, who);
            string called = !string.IsNullOrEmpty(_unitName.text) ? _unitName.text : kind;
            _worldName.text = _unitDead ? CorpseWords(who) : Hostile(who) ? "Hostile" : called;
            _worldSubtitle.text = _unitDead ? "Dead" : who.dead == 1 ? "Knocked out"
                : who.dead == 5 ? "Carried" : kind;
            if (string.Equals(_worldName.text, _worldSubtitle.text, System.StringComparison.OrdinalIgnoreCase))
            {
                _worldSubtitle.text = who.Employed() ? "Yours" : "Neutral";
            }
            float width = _sheetOpen ? EmblemD : MarkerWidth();
            float height = _sheetOpen ? EmblemD : _unitDead ? 29f : 38f;
            bool status = !_sheetOpen && who.dead == 0 && who.Employed() && !Hostile(who);
            PlaceWorldMarker(screen, width, height, _sheetOpen, status ? MarkerFooterExtra : 0f);
            LayoutMarkerWords(width, !_sheetOpen);
            _worldMarker.enabled = !_sheetOpen && !status;
            PaintMarkerStatus(width, status);
            bool bars = !_sheetOpen && !_unitDead;
            ShowMarkerBars(bars);
            if (bars)
            {
                const float barW = MarkerBarWidth;
                PaintMarkerBar(_markerHealth, _markerHealthFill, EmblemPad, 26f, barW, 4.5f,
                    _unitLine.Fracs[0], LifeTint);
                PaintMarkerBar(_markerStamina, _markerStaminaFill, EmblemPad, 31.5f, barW, 2.8f,
                    _unitLine.Fracs[1], MarkerCream);
                PaintMarkerRuler(_markerHealthRuler, (float)who.maxLife, barW, 4.5f);
                PaintMarkerRuler(_markerStunRuler, (float)who.maxFaint, barW, 2.8f);
            }
            EmblemOf(who, out string glyph, out Color colour);
            if (status) { colour = new Color(.16f, .70f, .45f, 1f); }
            PaintEmblem(glyph, colour, who.level, width, height, _sheetOpen, status);
        }

        private static string CorpseWords(CommonStates who)
        {
            string kind = KindName(_mn != null ? _mn.npcMN : null, who);
            return string.IsNullOrEmpty(kind) ? "Corpse" : kind;
        }
    }
}
