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
        // stands in the world, so it lives on its own layer low in the canvas the
        // unit shares (just over the game's top HUD: KeepMarkerOverHud), not
        // inside the unit - which is created after the windows and drew over
        // them. The dock stays in the unit, above them; the layer comes and goes
        // with it (ShowUnit).
        private static GameObject _worldLayer;

        // Selected A / Enamel Coin: a compact walnut label behind
        // a brass-rimmed enamel medallion. Friendly NPCs also carry the selected
        // A status footer, below the original kind label and health/stun bars.
        private static Image _emblemRim, _emblemFill, _emblemGlyph, _emblemCoin;
        private static Image _emblemStarEdge, _emblemStar;
        private static Image _markerHealth, _markerHealthFill, _markerStamina, _markerStaminaFill;
        private static SheetRow _markerHealthRuler, _markerStunRuler;
        private static TextMeshProUGUI _emblemLevel;
        private static TMP_FontAsset _markerFont, _markerFontBold, _captionFont;
        private static bool _markerFontsTried;
        private static string _markerMeasuredName, _markerMeasuredSubtitle;
        private static float _markerMeasuredWidth;
        private const float MarkerScale = .88f;
        private const float MarkerRightPad = 8f;
        // Keep the established bar length; only the words can widen the plate.
        private const float MarkerBarWidth = 64f;

        // THE MARKER IS ONE PLAQUE, in the combat plaque's language: the board's
        // portrait card sits INSIDE the frame at its left, the words and tracks beside
        // it, and one of yours carries the status row as a footer of the same frame.
        // The card used to hang half outside the plate and the status row sat on a
        // second, narrower plate under it, so the marker read as loose pieces.
        private const float MarkerInset = 3f;              // the frame's border
        private const float CardAspect = 180f / 198f;      // the target_card piece
        private static float _cardW = 29f, _cardH = 32f;
        private static float EmblemPad => MarkerInset + _cardW + 3.5f;
        private static float MarkerMinWidth => EmblemPad + MarkerBarWidth + MarkerRightPad;

        // the card stands the plate's height inside its border (one of yours,
        // whose plaque is taller for the status row, lets it grow: MarkerStatusCard)
        private static void MarkerCard(float height, float most = 32f)
        {
            _cardH = Mathf.Min(height - 2f * MarkerInset, most);
            _cardW = _cardH * CardAspect;
        }

        private static float CardTop(float height) => (height - _cardH) * .5f;

        // the card's opening, in its painting's 180 x 198: (25, 20), 143 x 161
        private static Rect CardOpening(float height)
        {
            float x = MarkerInset, y = CardTop(height);
            return new Rect(x + _cardW * 25f / 180f, y + _cardH * 20f / 198f, _cardW * 143f / 180f, _cardH * 161f / 198f);
        }
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
            AnimePieces.Sliced(_worldMarker, "frame_plate", MarkerFrameBorder, MarkerFrameScale);
            _unitRow.LabelFont = _markerFont;
            _worldName = MarkerText("Name", 9.5f, MarkerCream, true);
            _worldSubtitle = MarkerText("Kind", 7f, MarkerMuted, false);
            _markerMeasuredName = null;
            _unitStrip.Go.SetActive(false);
            _emblemRim = Theme.KitImage(_worldMarker.transform, "EmblemRim", "kit_emblem_rim", 0, Color.white);
            _emblemFill = Theme.KitImage(_worldMarker.transform, "Emblem", "kit_emblem_enamel", 0, Color.white);
            _emblemGlyph = Theme.KitImage(_worldMarker.transform, "EmblemGlyph", "kit_emblem_heart", 0, Color.white);
            _emblemCoin = Theme.KitImage(_worldMarker.transform, "EmblemCoin", "kit_emblem_coin", 0, Color.white);
            // the board's portrait card and level coin (AnimePieces), as on the
            // target dock and the combat plaque
            AnimePieces.Set(_emblemRim, "target_card_bare");   // without the dock card's leaves
            AnimeHud.Skin(_emblemFill,"mask");
            AnimePieces.Set(_emblemCoin, "coin");
            _emblemStarEdge = Theme.KitImage(_worldMarker.transform, "FavouriteEdge", "kit_dossier_star", 0, EmblemInk);
            _emblemStar = Theme.KitImage(_worldMarker.transform, "Favourite", "kit_dossier_star", 0, StarShine);
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
            // the board's faces (make_hud_fonts.py): Noto Serif carried by
            // the mod, semi-condensed with lining figures for words and readings,
            // condensed bold for the tile captions. Georgia if they cannot be built.
            _markerFont = SystemFont.BuildEmbedded("IslandUI.Fonts.NotoSerif-HudBody.ttf", "IslandUI Hud Body", Theme.Font)
                ?? SystemFont.Build("georgia.ttf", "IslandUI Emblem Georgia", Theme.Font);
            _markerFontBold = SystemFont.BuildEmbedded("IslandUI.Fonts.NotoSerif-HudStrong.ttf", "IslandUI Hud Strong", Theme.Font)
                ?? SystemFont.Build("georgiab.ttf", "IslandUI Emblem Georgia Bold", Theme.Font);
            _captionFont = SystemFont.BuildEmbedded("IslandUI.Fonts.NotoSerif-HudCaption.ttf", "IslandUI Hud Caption", Theme.Font)
                ?? _markerFontBold;
        }

        // The board's semi-bold serif, for type that is part of a painted piece (the
        // enamel count pills).
        internal static TMP_FontAsset HudStrongFont
        {
            get { EnsureMarkerFonts(); return _markerFontBold; }
        }

        // ...and its regular weight, for a painted piece's words (the sheet's tabs)
        internal static TMP_FontAsset HudBodyFont
        {
            get { EnsureMarkerFonts(); return _markerFont; }
        }

        // A tile's caption in the board's condensed capitals.
        internal static void SetCaptionFont(TMP_Text text)
        {
            EnsureMarkerFonts();
            if (_captionFont == null || text.font == _captionFont) { return; }
            text.font = _captionFont;
            text.fontStyle = FontStyles.Normal;
            Theme.FlattenType(text);
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

        // the plate's frame and the combat plaque's tracks and fills, drawn small
        private static readonly Vector4 MarkerFrameBorder = new Vector4(12, 24, 22, 30);
        private const float MarkerFrameScale = .7f;

        private static Image MarkerTrack(string name, out Image fill)
        {
            Image track = Theme.KitImage(_worldMarker.transform, name, "kit_emblem_track", 8, Color.white);
            AnimePieces.Sliced(track, "track", new Vector4(16, 0, 16, 0));
            fill = Theme.KitImage(track.transform, "Fill", "kit_emblem_bar", 8, Color.white);
            AnimePieces.Sliced(fill, name == "Health" ? "combat_hp_fill" : "combat_faint_fill", new Vector4(20, 0, 20, 0));
            return track;
        }

        private static void PaintMarkerBar(Image track, Image fill, float x, float y, float width,
            float height, float fraction, Color color)
        {
            Theme.Place(track, x, y, width, height);
            float f = Mathf.Clamp01(fraction);
            fill.gameObject.SetActive(f > 0f);
            // the painted pieces scale with the bar's height: their ends stay round
            track.pixelsPerUnitMultiplier = 48f / height;
            float inner = height - 1.2f;
            fill.pixelsPerUnitMultiplier = 32f / inner;
            Theme.Place(fill, .6f, .6f, Mathf.Max(inner, (width - 1.2f) * f), inner);
            fill.color = Color.white;
        }

        private static void ShowMarkerBars(bool on)
        {
            _markerHealth.gameObject.SetActive(on);
            _markerStamina.gameObject.SetActive(on);
        }

        // Use the roster's ruler verbatim: thin hundreds, heavy thousands, and
        // omit a rank when its intervals cannot be drawn at this width. Each
        // pool lives under its track so corpse and prop transitions hide it.
        // True when it placed them again.
        private static bool PaintMarkerRuler(SheetRow row, float max, float width, float height)
        {
            float innerWidth = width - 1.2f;
            float innerHeight = height - 1.2f;
            if (Mathf.Approximately(row.DrewMax, max) && Mathf.Approximately(row.TrackW, innerWidth)
                && Mathf.Approximately(row.BarH, innerHeight)) { return false; }
            PlaceTicks(row, new SheetLine { Max = max }, .6f, innerWidth, .6f, innerHeight);
            row.TrackW = innerWidth;
            row.BarH = innerHeight;
            ShowMarkerMarks(row.Ticks, row.DrewTicks);
            ShowMarkerMarks(row.Majors, row.DrewMajors);
            return true;
        }

        // A MARK PAST THE FILL IS DRAWN LIGHT (Forgejo #88). The ruler's dark
        // ink reads on a fill and is lost in the dark groove past it, so the
        // marks over the empty track take the cream of the stun fill instead,
        // each rank keeping its weight. Run when the fill moves or the ruler is
        // placed again.
        private static readonly Color TrackTick = new Color(.97f, .92f, .80f, .3f);
        private static readonly Color TrackMajor = new Color(.97f, .92f, .80f, .45f);

        private static void ShadeMarkerRuler(SheetRow row, float frac)
        {
            float filled = Mathf.Clamp01(frac) * Mathf.Max(1f, row.DrewMax);
            ShadeMarks(row.Ticks, row.DrewTicks, BoxWorth, filled, StripTick, TrackTick);
            ShadeMarks(row.Majors, row.DrewMajors, MajorWorth, filled, StripMajor, TrackMajor);
        }

        private static void ShadeMarks(Image[] pool, int count, float worth, float filled, Color onFill, Color onTrack)
        {
            for (int k = 1; pool != null && k <= count && k <= pool.Length; k++)
            {
                Color ink = worth * k <= filled ? onFill : onTrack;
                if (pool[k - 1].color != ink) { pool[k - 1].color = ink; }
            }
        }

        private static void ShowMarkerMarks(Image[] pool, int count)
        {
            for (int i = 0; pool != null && i < pool.Length; i++)
            {
                pool[i].gameObject.SetActive(i < count);
                if (i < count) { pool[i].transform.SetAsLastSibling(); }
            }
        }

        private static float _markerMeasuredPad;

        private static float MarkerWidth()
        {
            if (_markerMeasuredName != _worldName.text || _markerMeasuredSubtitle != _worldSubtitle.text
                || !Mathf.Approximately(_markerMeasuredPad, EmblemPad))
            {
                _markerMeasuredName = _worldName.text;
                _markerMeasuredSubtitle = _worldSubtitle.text;
                _markerMeasuredPad = EmblemPad;
                float words = Mathf.Max(_worldName.GetPreferredValues(_worldName.text).x,
                    _worldSubtitle.GetPreferredValues(_worldSubtitle.text).x);
                _markerMeasuredWidth = Mathf.Max(MarkerMinWidth, Mathf.Ceil(words) + EmblemPad + MarkerRightPad);
            }
            // Ellipsis remains a fallback only for text wider than the screen.
            var parent = (RectTransform)_unit.transform.parent;
            float available = (parent.rect.width - 12f) / MarkerScale;
            return Mathf.Min(_markerMeasuredWidth, Mathf.Max(MarkerMinWidth, available));
        }

        // every layout starts as a thing's or a stranger's - the emblem, no
        // portrait - and the portrait is put on after it (PaintMarkerPortrait)
        private static void LayoutMarkerWords(float width)
        {
            _worldName.gameObject.SetActive(true);
            _worldSubtitle.gameObject.SetActive(true);
            float textW = Mathf.Max(0f, width - EmblemPad - MarkerRightPad);
            Theme.Place(_worldName, EmblemPad, 1f, textW, 17f);
            Theme.Place(_worldSubtitle, EmblemPad, 14.5f, textW, 12f);
            MarkerPortraitOff();
        }

        // THE PLATE: the plaque's frame over the whole marker (the marker's own
        // image stays off). One of yours had the status row as a footer behind a
        // gold hairline, centred on the whole plate under two columns, so it lined
        // up with neither (Forgejo #51); the row is the words' column's last line
        // now (MarkerStatusH), and the plaque one grid.
        private static Image _markerPlate;

        private static void PaintMarkerPlate(float width, float height)
        {
            if (_markerPlate == null)
            {
                _markerPlate = AnimePieces.Image(_worldMarker.transform, "Plate", "frame_plate");
            }
            _worldMarker.enabled = false;
            AnimePieces.Sliced(_markerPlate, "frame_plate", MarkerFrameBorder, MarkerFrameScale);
            Theme.Place(_markerPlate, 0f, 0f, width, height);
            _markerPlate.transform.SetAsFirstSibling();
        }

        // ---------- the portrait on the marker: yours only ----------

        // A PERSON HAS THEIR PORTRAIT IN THE MEDALLION - the sheets' own portrait,
        // the link to them - on plain paper (their type's colour behind it was
        // tried, and read as noise), and the ring round it in what they are to
        // you: red hostile, yellow a stranger. The enamel said yours, and a
        // villager in revolt is yours and hostile at once; the ring says which.
        // A body keeps the skull; a head the portrait cannot frame keeps the
        // emblem. The ring's colour runs on round the whole frame - the plate's
        // brass band, or the status header's and footer's: a band of the brass
        // alone (make_emblem.py) laid over each and tinted, so the wood stays wood. The label stays as it always was, the name in the marker's cream:
        // the sheets' person mark was tried on it - the token and the name in the
        // kind's colour, behind a paper slip, over a soft light, on a sand band,
        // and all in cream - and every way of it looked wrong on the wood. A
        // hostile still withholds its name and condition (D89); its face it
        // cannot hide.
        private const float LivePortraitEvery = 0.1f;   // the live portrait's render, at most this often
        private static Image _markerPortrait, _bandPlate, _bandHeader, _bandFooter;
        private static RawImage _markerPortraitRaw;
        private static Image _markerSkyFade;
        private static float _livePortraitAt = -1f;
        private static CommonStates _livePortraitWho;
        private static bool _livePortraitOk;

        // THE TARGET'S FACE, LIVE: the marker and the dock's card show the same
        // render, a few times a second, in the pose of the moment - the sheet's own
        // camera and framing. Both ask for it each frame; one render serves both.
        // Null when they cannot be framed (not in the scene, no head).
        private static Texture LivePortrait(CommonStates who)
        {
            if (who == null)
            {
                return null;
            }
            float now = Time.unscaledTime;
            if (!ReferenceEquals(_livePortraitWho, who) || now - _livePortraitAt >= LivePortraitEvery)
            {
                _livePortraitWho = who;
                _livePortraitAt = now;
                _livePortraitOk = RenderPortrait(PortraitLive, who);
            }
            return _livePortraitOk ? _portraitTex[PortraitLive] : null;
        }

        private static readonly Color PortraitGround = new Color32(222, 208, 180, 255);

        private static void EnsureMarkerPortrait()
        {
            if (_markerPortrait != null) { return; }
            _markerPortrait = Theme.KitImage(_worldMarker.transform, "Portrait", "kit_dossier_dot", 0, Color.white);
            AnimeHud.Skin(_markerPortrait,"mask");
            _markerPortrait.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var face = Owned.New("Face");
            face.transform.SetParent(_markerPortrait.transform, false);
            _markerPortraitRaw = face.AddComponent<RawImage>();
            _markerPortraitRaw.raycastTarget = false;
            _markerSkyFade = SkyFade(_markerPortrait.transform);
            // the render is taller than wide; the medallion shows its middle square
            float square = PortraitPxW / (float)PortraitPxH;
            _markerPortraitRaw.uvRect = new Rect(0f, (1f - square) * 0.5f, 1f, square);
            var fr = _markerPortraitRaw.rectTransform;
            fr.anchorMin = Vector2.zero;
            fr.anchorMax = Vector2.one;
            fr.offsetMin = fr.offsetMax = Vector2.zero;
            _bandPlate = FrameBand(_worldMarker, "kit_emblem_plate_band");
            if (_markerStatusHeader != null) { _bandHeader = FrameBand(_markerStatusHeader, "kit_emblem_status_header_band"); }
            if (_markerStatusFooter != null) { _bandFooter = FrameBand(_markerStatusFooter, "kit_emblem_status_footer_band"); }
        }

        // a band laid over a panel, as its first child: it fills the panel, sliced
        // as the panel is
        private static Image FrameBand(Image panel, string sprite)
        {
            Image band = Theme.KitImage(panel.transform, "Band", sprite, 20, Color.white);
            var r = band.rectTransform;
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = r.offsetMax = Vector2.zero;
            // Relationship is a narrow header accent, leaving the portrait and
            // outer frame in the same warm metal as the docks.
            band.sprite=null;band.type=Image.Type.Simple;
            r.anchorMin=new Vector2(0,1);r.anchorMax=new Vector2(1,1);
            r.pivot=new Vector2(.5f,1);r.sizeDelta=new Vector2(-12, .65f);
            r.anchoredPosition=new Vector2(0,-2.5f);
            band.transform.SetAsFirstSibling();
            band.gameObject.SetActive(false);
            return band;
        }

        private static void ShowFrameBand(Image band, bool on, Color tint)
        {
            if (band == null) { return; }
            if (band.gameObject.activeSelf != on) { band.gameObject.SetActive(on); }
            if (on && band.color != tint) { band.color = tint; }
        }

        private static void MarkerPortraitOff()
        {
            if (_markerPortrait != null) { _markerPortrait.gameObject.SetActive(false); }
            ShowFrameBand(_bandPlate, false, Color.white);
            ShowFrameBand(_bandHeader, false, Color.white);
            ShowFrameBand(_bandFooter, false, Color.white);
            if (_emblemGlyph != null) { _emblemGlyph.gameObject.SetActive(true); }
        }

        private static void PaintMarkerPortrait(CommonStates who, bool alive, float height)
        {
            if (!alive)
            {
                return;
            }
            EnsureMarkerPortrait();
            Texture face = LivePortrait(who);
            if (face == null)
            {
                return;
            }
            Rect o = CardOpening(height);
            Theme.Place(_markerPortrait, o.x, o.y, o.width, o.height);
            _markerPortraitRaw.uvRect = PortraitFraming;
            _markerPortraitRaw.texture = face;
            // over the ground, under the card and the coin
            _markerPortrait.transform.SetSiblingIndex(_emblemFill.transform.GetSiblingIndex() + 1);
            _emblemRim.transform.SetSiblingIndex(_markerPortrait.transform.GetSiblingIndex() + 1);
            _markerPortrait.gameObject.SetActive(true);
            _emblemGlyph.gameObject.SetActive(false);
            // the board's sky behind the face, as on the target's card and the plaque:
            // the clock's, or where they are (PaintSky)
            AnimePieces.Set(_emblemFill, SkyPieces[0]);
            PaintSky(_emblemFill, _markerSkyFade, who, MarkerPlace);
            _emblemRim.color = Color.white;
            // WHAT THEY ARE TO YOU is the health bar's colour, as on the combat
            // markers: jade for yours, red for the rest (PaintWorldMarker). A band
            // along the plate's top said it, then an enamel gem on the card's
            // corner, which read as the roster's kind pin (Forgejo #51).
        }

        // OFF THE SCREEN the marker waits on the screen's rim, and on the rim it
        // looked like any target standing at the edge. An arrow beside it now
        // points to where they are, with how far on a chip beside the arrow.
        // A target behind the camera used to hide the marker; it waits on the rim
        // on the side they are, like any other target out of sight. The combat
        // markers carry the same pair (Combat.cs): a RimArrow each.
        private sealed class RimArrow
        {
            internal Image Glow, Arrow, Fill, Far;
            internal bool Threat;                   // the enemy is on you: the arrow glows (Combat.cs)
            internal TextMeshProUGUI FarText;
            internal bool Outlined;
            internal string Measured;
            internal float MeasuredW;
            internal bool Built => Arrow != null;   // gone with the scene
        }
        private static RimArrow _markerRim;
        private const float ArrowW = 16f, ArrowH = 10.5f, ArrowGap = 3f, FarH = 13f;
        private static readonly Vector3[] HudCorners = new Vector3[4];

        // THE RIM. At the bottom the marker stops above the HUD there (the act
        // row, the action bar, the docks, your vitals hid the arrow and part of
        // the marker); at the top it draws OVER the game's HUD - the clock, the
        // log - by standing just after the log among MainCanvas's children, which
        // still keeps it under every window (they all come later, but for the
        // pause menus, which hide it: MenuOverMarker).
        //
        // The layers that stand in the world stand there together, in one order:
        // the link arcs (Links.cs), the combat markers (Combat.cs), and this
        // marker over them. Each keeper sets the whole stack, so once it is in
        // order nothing moves (the arcs and the marker, each keeping its own
        // place beside the other, swapped every frame they were both up).
        private static void KeepMarkerOverHud()
        {
            Transform log = _mn != null && _mn.uiMN != null && _mn.uiMN.logPanel != null
                ? _mn.uiMN.logPanel.transform.parent : null;
            Transform anchor = log;
            StackAfter(ref anchor, _logFeed);
            StackAfter(ref anchor, _arcsRoot);
            StackAfter(ref anchor, _combatLayer);
            StackAfter(ref anchor, _worldLayer);
        }

        // ...each just after the one before it, or first among its siblings
        private static void StackAfter(ref Transform anchor, GameObject layer)
        {
            if (layer == null) { return; }
            Transform t = layer.transform;
            int want = 0;
            if (anchor != null && anchor.parent == t.parent)
            {
                int at = anchor.GetSiblingIndex();
                want = t.GetSiblingIndex() < at ? at : at + 1;
            }
            if (t.GetSiblingIndex() != want) { t.SetSiblingIndex(want); }
            anchor = t;
        }

        // the pause menus stand before the log, so under the marker (the other
        // windows there - the NPC menu, status, inventory - are vanilla's that
        // this mod veils or replaces)
        private static bool MenuOverMarker()
        {
            UIManager ui = _mn != null ? _mn.uiMN : null;
            return ui != null && (Up(ui.settingMenu) || Up(ui.settingPanel) || Up(ui.savePanel));
        }

        private static bool Up(GameObject go) => go != null && go.activeInHierarchy;

        // the top of the HUD along the bottom of the screen, in the marker's space
        private static float HudTop(RectTransform parent)
        {
            float top = float.NegativeInfinity;
            foreach (RectTransform rect in new[] { _unitRect, ActionBar.Rect, _generalDock, _playerDock, _playerVitals })
            {
                if (rect == null || !rect.gameObject.activeInHierarchy) { continue; }
                rect.GetWorldCorners(HudCorners);   // 1 = top-left
                top = Mathf.Max(top, parent.InverseTransformPoint(HudCorners[1]).y);
            }
            return top;
        }

        // Whether the target's feet are out of sight; a marker point behind the
        // camera is mirrored to the side it is on and pushed off the screen.
        private static bool OffScreen(Camera camera, Vector3 feet, ref Vector3 screen)
        {
            Vector3 at = camera.WorldToScreenPoint(feet);
            bool off = at.z <= 0f || at.x < 0f || at.y < 0f || at.x > Screen.width || at.y > Screen.height;
            if (screen.z <= 0f)
            {
                var centre = new Vector2(Screen.width * .5f, Screen.height * .5f);
                Vector2 away = centre - (Vector2)screen;
                if (away.sqrMagnitude < 1f) { away = Vector2.down; }
                Vector2 far = centre + away.normalized * (Screen.width + Screen.height);
                screen = new Vector3(far.x, far.y, 1f);
                off = true;
            }
            return off;
        }

        // how far you are from them, or null while they are in sight: across the
        // ground, so a house's interior, 25 m under it, is as far as the house
        private static string FarWords(bool off, Vector3 feet)
        {
            GameObject player = _mn != null && _mn.gameMN != null ? _mn.gameMN.player : null;
            if (!off || player == null) { return null; }
            Vector3 d = feet - player.transform.position;
            d.y = 0f;
            return Shared.Loc.Get("marker.metres", "{0} m", Mathf.RoundToInt(d.magnitude));
        }

        private static void PlaceWorldMarker(Vector3 screen, float width, float height, float footer = 0f, string far = null,
            bool medallion = true)
        {
            var parent = (RectTransform)_unit.transform.parent;
            KeepMarkerOverHud();
            var canvas = parent.GetComponentInParent<Canvas>();
            var uiCam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, uiCam, out Vector2 local);
            // The footer extends below the plaque's rect: lift the whole marker
            // by that amount so its bottom still clears the target and the dock.
            local.y += footer * MarkerScale;
            Vector2 wanted = local;
            // Out of sight, the rim keeps room for the arrow and its distance chip
            // beyond the silhouette: wider at the sides, where they stack.
            bool off = far != null;
            float farW = off ? MarkerFarWidth(far) * MarkerScale : 0f;
            float roomX = off ? ArrowGap + Mathf.Max(ArrowH * MarkerScale, farW) + 3f : 0f;
            float roomY = off ? ArrowGap + Mathf.Max(ArrowH, FarH) * MarkerScale + 3f : 0f;
            // Centre and clamp the complete silhouette. The card sits inside the
            // plaque now: nothing overhangs its frame.
            const float overhang = 0f;
            float visualWidth = (width + overhang) * MarkerScale;
            local.x = Mathf.Clamp(local.x, parent.rect.xMin + visualWidth * .5f + 6f + roomX,
                parent.rect.xMax - visualWidth * .5f - 6f - roomX);
            float centreX = local.x;
            local.x += overhang * .5f * MarkerScale;
            const float extraTop = 0f;
            float floor = Mathf.Max(parent.rect.yMin + 51f, HudTop(parent) + 6f);
            local.y = Mathf.Clamp(local.y, floor + roomY + footer * MarkerScale,
                parent.rect.yMax - height * MarkerScale - extraTop - 6f - roomY);
            if (_animeEnemy != null && _animeEnemy.gameObject.activeInHierarchy)
            {
                RectTransform combat = _animeEnemy.rectTransform;
                Vector3 left = parent.InverseTransformPoint(combat.TransformPoint(new Vector3(combat.rect.xMin,combat.rect.yMin,0)));
                Vector3 right = parent.InverseTransformPoint(combat.TransformPoint(new Vector3(combat.rect.xMax,combat.rect.yMax,0)));
                if (local.x+visualWidth*.5f>left.x && local.x-visualWidth*.5f<right.x && local.y+height*MarkerScale>left.y-4)
                    local.y = Mathf.Max(floor+footer*MarkerScale,left.y-height*MarkerScale-5);
            }
            var r = _worldMarker.rectTransform;
            r.localScale = Vector3.one * MarkerScale;
            r.pivot = new Vector2(.5f, 0f);
            r.sizeDelta = new Vector2(width, height);
            float bottom = local.y - footer * MarkerScale;
            float top = local.y + height * MarkerScale + extraTop;
            PlaceMarkerArrow(parent, far, farW, new Vector2(centreX, (bottom + top) * .5f),
                new Vector2(visualWidth * .5f, (top - bottom) * .5f), wanted, floor, out Vector2 push);
            r.position = parent.TransformPoint(local + push);
        }

        private static void EnsureMarkerArrow()
        {
            if (_markerRim != null && _markerRim.Built) { return; }
            _markerRim = BuildRimArrow(_worldMarker.transform);
        }

        private static RimArrow BuildRimArrow(Transform marker)
        {
            var rim = new RimArrow();
            rim.Glow = ThreatGlow(marker, "OffScreenThreat");
            rim.Arrow = Theme.KitImage(marker, "OffScreenArrow", "kit_pointer", 0, EmblemInk);
            rim.Fill = Theme.KitImage(rim.Arrow.transform, "Fill", "kit_pointer", 0, Color.white);
            // no box, only cream lettering with a dark edge: a walnut chip read
            // as a second marker, and masking tape was tried and passed over.
            // The image stays as the words' undrawn frame.
            rim.Far = Theme.KitImage(marker, "OffScreenDistance", "kit_paper", 0, Color.white);
            rim.Far.enabled = false;
            rim.FarText = Theme.MakeKitText(rim.Far.transform, "Metres", 8f, MarkerCream, TextAlignmentOptions.Center, true);
            SetMarkerFont(rim.FarText, true);
            rim.Outlined = Theme.OutlineNumber(rim.FarText);
            rim.FarText.enableWordWrapping = false;
            rim.FarText.raycastTarget = false;
            RectTransform tr = rim.FarText.rectTransform;
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = tr.offsetMax = Vector2.zero;
            foreach (Image piece in new[] { rim.Arrow, rim.Fill, rim.Far })
            {
                RectTransform pr = piece.rectTransform;
                pr.anchorMin = pr.anchorMax = pr.pivot = new Vector2(.5f, .5f);
            }
            rim.Arrow.rectTransform.sizeDelta = new Vector2(ArrowW, ArrowH);
            // the enamel inset about the triangle's incentre, which sits a
            // little toward its base (the sprite points down, its base is up)
            const float k = .55f;
            rim.Fill.rectTransform.sizeDelta = new Vector2(ArrowW * k, ArrowH * k);
            rim.Fill.rectTransform.anchoredPosition = new Vector2(0f, ArrowH * .125f * (1f - k));
            return rim;
        }

        // the chip's width in the marker's own units
        private static float MarkerFarWidth(string far)
        {
            EnsureMarkerArrow();
            return RimFarWidth(_markerRim, far);
        }

        // ...measured again only when the words change
        private static float RimFarWidth(RimArrow rim, string far)
        {
            if (!rim.Outlined) { rim.Outlined = Theme.OutlineNumber(rim.FarText); }
            if (rim.FarText.text != far) { rim.FarText.text = far; }
            if (rim.Measured != far)
            {
                rim.Measured = far;
                rim.MeasuredW = Mathf.Ceil(rim.FarText.GetPreferredValues(far).x) + 4f;
            }
            return rim.MeasuredW;
        }

        // The arrow and its chip stand together just outside the silhouette's
        // box, on the line from its centre to where the marker would be if the
        // screen were larger: the chip under the arrow when it points to a side,
        // beside it when it points up or down. Toward a corner one turns into the
        // other - the chip swings round the arrow and the pair's footprint follows
        // the arrow's turn - so nothing jumps there (a switch at the diagonal did).
        private static void PlaceMarkerArrow(RectTransform parent, string far, float farW, Vector2 centre,
            Vector2 half, Vector2 wanted, float floor, out Vector2 push)
        {
            push = Vector2.zero;
            if ((_markerRim == null || !_markerRim.Built) && far == null) { return; }
            EnsureMarkerArrow();
            PlaceRimArrow(_markerRim, parent, far, farW, centre, half, wanted, floor, out push);
        }

        private static void PlaceRimArrow(RimArrow rim, RectTransform parent, string far, float farW, Vector2 centre,
            Vector2 half, Vector2 wanted, float floor, out Vector2 push)
        {
            push = Vector2.zero;
            bool off = far != null;
            if (rim.Arrow.gameObject.activeSelf != off) { rim.Arrow.gameObject.SetActive(off); }
            if (rim.Far.gameObject.activeSelf != off) { rim.Far.gameObject.SetActive(off); }
            if (!off)
            {
                if (rim.Glow != null && rim.Glow.gameObject.activeSelf) { rim.Glow.gameObject.SetActive(false); }
                return;
            }
            Vector2 d = wanted - centre;
            if (d.sqrMagnitude < 1f) { d = Vector2.down; }
            d.Normalize();
            float s = MarkerScale;
            float turn = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg + 90f;
            // the turned arrow's footprint
            float cos = Mathf.Abs(Mathf.Cos(turn * Mathf.Deg2Rad)), sin = Mathf.Abs(Mathf.Sin(turn * Mathf.Deg2Rad));
            Vector2 arrow = new Vector2(cos * ArrowW + sin * ArrowH, sin * ArrowW + cos * ArrowH) * s;
            Vector2 chip = new Vector2(farW, FarH * s);
            // the chip's place round the arrow: under it (-90°) when the pair
            // leaves the marker by a side, beside it (0°) by the top or bottom -
            // the marker is wide, so a target off to the side but a little up
            // leaves by the top, where only a low pair has room - swinging between
            // near the marker's corner
            float bySide = Mathf.Abs(d.x) * half.y, byTop = Mathf.Abs(d.y) * half.x;
            float upright = Mathf.Clamp01((byTop / (bySide + byTop) - .3f) / .4f);
            float swing = (-90f + 90f * Mathf.SmoothStep(0f, 1f, upright)) * Mathf.Deg2Rad;
            Vector2 offset = new Vector2(Mathf.Cos(swing) * ((arrow.x + chip.x) * .5f + 2f),
                Mathf.Sin(swing) * ((arrow.y + chip.y) * .5f + 1f));
            Vector2 min = Vector2.Min(-arrow * .5f, offset - chip * .5f);
            Vector2 max = Vector2.Max(arrow * .5f, offset + chip * .5f);
            Vector2 middle = (min + max) * .5f;
            // where the line leaves the box grown by the pair's half and the gap
            Vector2 grown = half + (max - min) * .5f + Vector2.one * ArrowGap;
            float tx = Mathf.Abs(d.x) > 1e-4f ? grown.x / Mathf.Abs(d.x) : float.MaxValue;
            float ty = Mathf.Abs(d.y) > 1e-4f ? grown.y / Mathf.Abs(d.y) : float.MaxValue;
            Vector2 at = centre + d * Mathf.Min(tx, ty);
            // never nearer the screen's rim (or the bottom HUD) than the marker
            // itself is kept: where
            // the pair would be, it and the marker step in together
            Rect screen = parent.rect;
            Vector2 low = at - (max - min) * .5f, high = at + (max - min) * .5f;
            push = new Vector2(
                Mathf.Max(0f, screen.xMin + 6f - low.x) - Mathf.Max(0f, high.x - (screen.xMax - 6f)),
                Mathf.Max(0f, floor - low.y) - Mathf.Max(0f, high.y - (screen.yMax - 6f)));
            at += push;
            Vector2 arrowAt = at - middle;
            Vector2 chipAt = arrowAt + offset;
            RectTransform ar = rim.Arrow.rectTransform;
            ar.position = parent.TransformPoint(arrowAt);
            ar.localRotation = Quaternion.Euler(0f, 0f, turn);
            RectTransform cr = rim.Far.rectTransform;
            cr.sizeDelta = new Vector2(farW / s, FarH);
            cr.position = parent.TransformPoint(chipAt);
            // the pair over the marker's own pieces, the threat's glow under the
            // arrow (Combat.cs), moved only when something else has come after
            PaintRimThreat(rim, ar);
            Transform glow = rim.Glow != null ? rim.Glow.transform : null;
            int n = ar.parent.childCount;
            if (cr.GetSiblingIndex() != n - 1 || ar.GetSiblingIndex() != n - 2 || (glow != null && glow.GetSiblingIndex() != n - 3))
            {
                if (glow != null) { glow.SetAsLastSibling(); }
                ar.SetAsLastSibling();
                cr.SetAsLastSibling();
            }
        }

        // the emblem by their stance (Stance, target-matrix D4): green yours, red a
        // hostile that came for you, amber the rest, grey a body (below). The ring,
        // the rim arrow and the light markers' arrows take this colour too.
        private static void EmblemOf(CommonStates who, out string glyph, out Color colour)
        {
            StanceKind stance = Stance.Living(who);
            bool yours = stance == StanceKind.Yours, hostile = stance == StanceKind.Hostile;
            glyph = yours ? "heart" : hostile ? "swords" : "person";
            colour = yours ? EmblemYours : hostile ? EmblemHostile : EmblemStranger;
            if (_unitDead) { glyph = "skull"; }
            if (who.dead != 0) { colour = EmblemBody; }
        }

        private static void PaintEmblem(string glyph, Color colour, int level, float width, float height, bool status = false,
            bool favourite = false, bool levelKnown = true)
        {
            // the card inside the frame's left border, its opening an enamel in the
            // emblem's colour (a portrait puts the sky there: PaintMarkerPortrait)
            float x0 = MarkerInset, y0 = CardTop(height);
            Theme.Place(_emblemRim, x0, y0, _cardW, _cardH);
            Rect o = CardOpening(height);
            Theme.Place(_emblemFill, o.x, o.y, o.width, o.height);
            AnimePieces.Set(_emblemFill, "card_enamel");
            _emblemFill.color = colour;
            _emblemRim.color = Color.white;
            Sprite mark = Theme.Kit("kit_emblem_" + glyph, 0);
            if (_emblemGlyph.sprite != mark) { _emblemGlyph.sprite = mark; }
            float icon = Mathf.Min(o.width, o.height) * .72f;
            Theme.Place(_emblemGlyph, o.x + (o.width - icon) * .5f, o.y + (o.height - icon) * .5f, icon, icon);
            bool coin = level > 0;
            _emblemCoin.gameObject.SetActive(coin);
            if (coin)
            {
                // the level on the board's coin, set over the card's lower right
                // corner as the panel's sits over its frame; beside the status
                // row it keeps to the gap before the row, and off the frame's edge
                const float c = 13f;
                float over = status ? .9f : .66f, lift = status ? 2.4f : 0f;
                Theme.Place(_emblemCoin, x0 + _cardW - c * over, y0 + _cardH - c * .74f - lift, c, c);
                // a stranger's level is the Observation skill's first (Observe.cs):
                // without it the coin asks, as the combat plaque's does
                string lv = levelKnown ? level.ToString() : Observe.UnknownShort;
                if (_emblemLevel.text != lv) { _emblemLevel.text = lv; }
                Theme.Place(_emblemLevel, 1f, 0f, c - 2f, c);
            }
            // the card's opening frames what is behind it: fill and glyph first
            _emblemFill.transform.SetAsLastSibling();
            _emblemGlyph.transform.SetAsLastSibling();
            _emblemRim.transform.SetAsLastSibling();
            _emblemCoin.transform.SetAsLastSibling();
            // A FAVOURITE wears the head's gold star, set into the brass at 10:30,
            // opposite the level coin: on every favourite's marker - knocked out,
            // carried, behind a door, a long name - and it moves nothing. A dark
            // edge, not the head's printed rim, which went muddy on the wood. It is
            // the dock card's star (BuildCardStar) scaled to this card, lit only and
            // with no glow past its edge:
            // the unlit switch is the dock's, a marker shows no star for a
            // non-favourite
            _emblemStarEdge.gameObject.SetActive(favourite);
            _emblemStar.gameObject.SetActive(favourite);
            if (favourite)
            {
                float k = _cardW / 180f;
                float star = CardStarS * k, e = star * SmallStarEdge;
                float sx = x0 + CardStarX * k, sy = y0 + CardStarY * k;
                Theme.Place(_emblemStarEdge, sx - (star + e) * .5f, sy - (star + e) * .5f, star + e, star + e);
                Theme.Place(_emblemStar, sx - star * .5f, sy - star * .5f, star, star);
                _emblemStarEdge.transform.SetAsLastSibling();
                _emblemStar.transform.SetAsLastSibling();
            }
            if (_markerRim != null && _markerRim.Built) { _markerRim.Fill.color = colour; }
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
            // an item held over them: the ring says who it goes to (Drop.cs)
            if (ReferenceEquals(_dropOver, who) && !HandEmpty())
            {
                ringColour = OfferGold;
            }
            // a door between you: the marker stands on that door, with a tag
            // saying which side they are (WorldMarker.Door.cs)
            bool behind = DoorBetween(who, out Vector3 door, out bool inside);
            Vector3 position;
            if (behind)
            {
                HideGroundRing();
                position = door + Vector3.up * DoorTagLift;
            }
            else
            {
                DoorTagOff();
                PaintGroundRing(who.transform, ringColour, PersonRadius);
                position = who.transform.position + Vector3.up * 3f;
                var body = who.GetComponentInChildren<Renderer>();
                if (body != null) { position.y = Mathf.Max(position.y, body.bounds.max.y + .25f); }
            }
            var screen = camera.WorldToScreenPoint(position);
            bool off = OffScreen(camera, behind ? door : who.transform.position, ref screen);
            // the Talk bubble stands where the marker would, its tail on their head
            bool shown = !MenuOverMarker() && !TalkBubble.IsOpen;
            _worldMarker.gameObject.SetActive(shown);
            if (!shown)
            {
                if (_markerUnder != null) { _markerUnder.gameObject.SetActive(false); }
                return;
            }
            string kind = KindName(_mn != null ? _mn.npcMN : null, who);
            string called = !string.IsNullOrEmpty(_unitName.text) ? _unitName.text : kind;
            // a withheld name (D2) says the stance instead: Hostile once it came
            // for you, Neutral before (target-matrix D4)
            _worldName.text = _unitDead ? CorpseWords(who)
                : Hostile(who) && !who.Employed()
                    ? Stance.Living(who) == StanceKind.Hostile ? Shared.Loc.Get("marker.hostile", "Hostile") : Shared.Loc.Get("marker.neutral", "Neutral")
                : called;
            _worldSubtitle.text = _unitDead ? Shared.Loc.Get("marker.dead", "Dead")
                : who.dead == 1 ? Shared.Loc.Get("marker.knocked_out", "Knocked out")
                : who.dead == 5 ? Shared.Loc.Get("marker.carried", "Carried") : kind;
            // the kind even where it repeats the name ("Reika" / "Reika", a mummy
            // called Mummy): the label's second line is always what they are, as
            // the sheets' KIND is. Empty was tried, and read as missing.
            // The same marker whether the sheet is open or not: it
            // used to fold to the bare medallion while the sheet was up.
            // the status footer for one of yours, in revolt too: their condition is
            // why they revolted (the frame's red says they have)
            // The row is the words' column's last line, and the card stands the
            // taller plaque's height beside it (Forgejo #51)
            bool status = who.dead == 0 && who.Employed();
            float height = _unitDead ? 33f : status ? MarkerStatusH : 39f;
            if (status) { MarkerCard(height, MarkerStatusCard); } else { MarkerCard(height); }
            float width = MarkerWidth();
            if (status) { width = Mathf.Max(width, MarkerStatusWidth()); }
            PlaceWorldMarker(screen, width, height, behind ? DoorTagExtra : 0f, FarWords(off, who.transform.position));
            if (behind)
            {
                PaintDoorTag(inside, width, height, 0f);
            }
            LayoutMarkerWords(width);
            PaintMarkerPlate(width, height);
            PaintMarkerStatus(width, status);
            bool bars = !_unitDead;
            ShowMarkerBars(bars);
            if (bars)
            {
                // the tracks run the words' column, as the plaque's run its plate
                float barW = width - EmblemPad - MarkerRightPad;
                // one of yours reads green, as on the fight's light marker
                // (PaintLightMarker): the health fill in jade, the rest the same
                // - read off the fill itself: a flag beside it outlived the marker,
                // which is built again red, and kept yours red after
                Sprite fill = AnimePieces.Get(Stance.HealthFill(who), new Vector4(20, 0, 20, 0));
                if (_markerHealthFill.sprite != fill) { _markerHealthFill.sprite = fill; }
                PaintMarkerBar(_markerHealth, _markerHealthFill, EmblemPad, 27f, barW, 4.5f,
                    _unitLine.Fracs[0], LifeTint);
                PaintMarkerBar(_markerStamina, _markerStaminaFill, EmblemPad, 32.5f, barW, 2.8f,
                    _unitLine.Fracs[1], MarkerCream);
                // the rulers' marks count the pools, so they are the numbers the
                // Observation skill keeps from a stranger (Observe.cs): the bars alone
                PaintMarkerRuler(_markerHealthRuler, Observe.Sees(who, Observe.Life) ? (float)who.maxLife : 0f, barW, 4.5f);
                PaintMarkerRuler(_markerStunRuler, Observe.Sees(who, Observe.Faint) ? (float)who.maxFaint : 0f, barW, 2.8f);
            }
            EmblemOf(who, out string glyph, out Color colour);
            if (status && !Hostile(who)) { colour = new Color(.16f, .70f, .45f, 1f); }
            PaintEmblem(glyph, colour, who.level, width, height, status, who.employ == CommonStates.Employ.Favorite,
                Observe.Sees(who, Observe.Level));
            PaintMarkerPortrait(who, who.dead == 0 && !_unitDead, height);
            PaintMarkerOffer(width, height, 0f, GiftOffered(who));
        }

        // THE MARKER AS A DROP PLACE (Drop.cs). Its whole silhouette - medallion,
        // plate, footer - is where an item is dropped on the target: an invisible
        // hit rect, laid out every paint whether or not anything is held. With a
        // gift in hand the indicator is outlined in gold. (A chevron tab hung under
        // it at first, and was one thing too many.)
        //
        // THE OUTLINE FOLLOWS THE CONTOUR (a rectangle around the whole silhouette
        // came first, and floated loose of the shape). Each piece - the plate
        // or the status header, the footer, the medallion - gets a gold copy a
        // little larger, BEHIND the indicator, so only the outer edge of their
        // union shows and where pieces overlap the gold is hidden under them. The
        // copies live in their own layer, a sibling before the marker that mirrors
        // its rect: a child of the marker would draw over the plate, which is the
        // marker's own image.
        private static Image _markerHit;
        private static RectTransform _markerUnder;
        private static Image _underPlate, _underFooter, _underEmblem;
        private const float UnderPad = 1.5f;

        private static void PaintMarkerOffer(float width, float height, float footer, bool offer)
        {
            if (_markerHit == null)
            {
                _markerHit = Theme.KitImage(_worldMarker.transform, "GiveHit", "kit_paper", 8, Color.white);
                _markerHit.enabled = false;   // a rect to hit, never drawn
                var under = Owned.New("IslandUI_GiveOutline");
                under.transform.SetParent(_worldMarker.transform.parent, false);
                _markerUnder = (RectTransform)under.transform;
                _underPlate = UnderPiece("Plate");
                _underFooter = UnderPiece("Footer");
                _underEmblem = UnderPiece("Emblem");
            }
            // one plaque: its frame is the whole silhouette
            const float top = -3f, left = -3f;
            float bottom = height + footer + 3f;
            Theme.Place(_markerHit, left, top, width + 3f - left, bottom - top);
            bool outline = offer && _worldMarker.gameObject.activeSelf;
            if (_markerUnder.gameObject.activeSelf != outline)
            {
                _markerUnder.gameObject.SetActive(outline);
            }
            if (!offer)
            {
                return;
            }
            // the layer stands exactly where the marker stands, just under it
            RectTransform m = _worldMarker.rectTransform;
            _markerUnder.anchorMin = m.anchorMin;
            _markerUnder.anchorMax = m.anchorMax;
            _markerUnder.pivot = m.pivot;
            _markerUnder.sizeDelta = m.sizeDelta;
            _markerUnder.localScale = m.localScale;
            _markerUnder.position = m.position;
            _markerUnder.SetAsFirstSibling();
            // the plaque's frame, footer and card included, is the one silhouette
            Under(_underPlate, _markerPlate);
            Under(_underFooter, null);
            Under(_underEmblem, null);
        }

        // ...of a piece placed with Theme.Place, read back from its rect
        private static void Under(Image under, Image piece)
        {
            bool on = piece != null && piece.gameObject.activeSelf;
            RectTransform r = on ? piece.rectTransform : null;
            Under(under, piece, on, on ? r.anchoredPosition.x : 0f, on ? -r.anchoredPosition.y : 0f,
                on ? r.sizeDelta.x : 0f, on ? r.sizeDelta.y : 0f);
        }

        private static Image UnderPiece(string name)
        {
            var go = Owned.New(name);
            go.transform.SetParent(_markerUnder, false);
            var image = go.AddComponent<Image>();
            image.color = OfferGold;
            image.raycastTarget = false;
            return image;
        }

        // A piece's own shape in white - the kit's plates have cut corners, which a
        // rounded rect only guessed at - keeping its 9-slice border and density, so
        // the copy slices exactly as the piece does and only grows at the edges.
        private static readonly System.Collections.Generic.Dictionary<Sprite, Sprite> Silhouettes =
            new System.Collections.Generic.Dictionary<Sprite, Sprite>();

        private static Sprite SilhouetteOf(Sprite source)
        {
            if (source == null)
            {
                return null;
            }
            if (Silhouettes.TryGetValue(source, out Sprite hit) && hit != null)
            {
                return hit;
            }
            Sprite mask = Theme.MaskOf(source);
            Sprite shape = mask;
            if (mask != null && mask != source)
            {
                shape = Sprite.Create(mask.texture, mask.rect, new Vector2(.5f, .5f), source.pixelsPerUnit, 0,
                    SpriteMeshType.FullRect, source.border);
                shape.hideFlags = HideFlags.HideAndDontSave;
            }
            Silhouettes[source] = shape;
            return shape;
        }

        // a gold copy of one piece, UnderPad larger all round
        private static void Under(Image under, Image piece, bool on, float x, float y, float w, float h)
        {
            if (under.gameObject.activeSelf != on)
            {
                under.gameObject.SetActive(on);
            }
            if (on)
            {
                Sprite shape = SilhouetteOf(piece.sprite);
                if (under.sprite != shape)
                {
                    under.sprite = shape;
                }
                under.type = piece.type;
                under.pixelsPerUnitMultiplier = piece.pixelsPerUnitMultiplier;
                under.preserveAspect = piece.preserveAspect;
                Theme.Place(under, x - UnderPad, y - UnderPad, w + UnderPad * 2f, h + UnderPad * 2f);
            }
        }

        private static string CorpseWords(CommonStates who)
        {
            string kind = KindName(_mn != null ? _mn.npcMN : null, who);
            return string.IsNullOrEmpty(kind) ? Shared.Loc.Get("marker.corpse", "Corpse") : kind;
        }
    }
}
