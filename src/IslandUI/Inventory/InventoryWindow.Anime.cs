using System.Collections.Generic;
using IslandUI.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI
{
    internal static partial class InventoryWindow
    {
        private static Image _animeIdentity;
        private static RawImage _animeIdentityFace;
        private static Image _animeIdentityItem;
        private static ItemInfo _animeIdentityThing;
        private static TextMeshProUGUI _animeIdentityName;
        private static Image _animeEnemy, _animeEnemyHpFill, _animeEnemyStunFill;
        private static string _plaqueFill;   // the health fill's piece the plaque wears now
        private static RawImage _animeEnemyFace;
        private static TextMeshProUGUI _animeEnemyName, _animeEnemyLife, _animeEnemyFaint, _animeEnemyMeta, _animeEnemyLevel;
        private const float CombatScale = 1.25f;
        private static Transform _enemyOriginal;

        private static RawImage AnimePortrait(Transform parent, float x, float y, float w, float h)
        {
            var frame = AnimeHud.Image(parent, "PortraitFrame", "rail");
            Theme.Place(frame, x, y, w, h);
            var mask = AnimeHud.Image(frame.transform, "PortraitMask", "mask");
            Theme.Place(mask, 1.7f, 1.7f, w - 3.4f, h - 3.4f);
            mask.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            mask.color = AnimeHud.Hex("D5C6A4");
            var go = Owned.New("LivePortrait"); go.transform.SetParent(mask.transform, false);
            var face = go.AddComponent<RawImage>(); face.raycastTarget = false;
            var r = face.rectTransform; r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
            face.uvRect = new Rect(0, .15f, 1, .75f);
            return face;
        }

        // A LIVE PORTRAIT ON THE BOARD'S GROUND: the render is a figure on nothing,
        // and on the flat brown it stood on it read as a sticker on the card. The
        // board paints its portraits over an open sky with leaves at a corner
        // (make_anime_hud.py: portrait_sky), framed close - head and shoulders
        // filling the opening rather than the whole bust.
        private static readonly Rect PortraitFraming = new Rect(.085f, .05f, .83f, .78f);

        private static Image PortraitSky(Image mask)
        {
            AnimePieces.Set(mask, SkyPieces[0]);
            mask.color = Color.white;
            return SkyFade(mask.transform);
        }

        // THE SKY FOLLOWS THE CLOCK (Forgejo #50): one sky for each of the game's own
        // periods - GameManager.ambient, which AmbientCheck sets at 06:00, 09:00, 18:00
        // and 21:00, the HUD's "Morning", "Daytime", "Evening", "Night" - painted by
        // make_anime_hud.py (SKIES), in the enum's order. At a change the new sky fades
        // in over its period's first ten game minutes, as the game's own light does
        // (AmbientChange: ten seconds, a game minute each at daySpeed 1): the ground
        // keeps the last sky, a layer over it shows the new one at rising alpha.
        private static readonly string[] SkyPieces = { "portrait_sky", "portrait_sky_evening", "portrait_sky_night", "portrait_sky_morning" };
        private static readonly float[] SkyFrom = { 540f, 1080f, 1260f, 360f };
        private const float SkyFadeMinutes = 10f;
        private static readonly Sprite[] Skies = new Sprite[4];
        private static Image _animeIdentitySky, _animeIdentitySkyFade, _animeEnemySky, _animeEnemySkyFade;

        // the layer the next sky fades in on: under everything else the parent holds
        private static Image SkyFade(Transform parent)
        {
            var fade = AnimePieces.Image(parent, "SkyFade", SkyPieces[0]);
            var r = fade.rectTransform; r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
            fade.transform.SetAsFirstSibling();
            fade.gameObject.SetActive(false);
            return fade;
        }

        private static Sprite SkySprite(int period)
        {
            if (Skies[period] == null) { Skies[period] = AnimePieces.Get(SkyPieces[period]); }
            return Skies[period];
        }

        private static void PaintSky(Image ground, Image fade, CommonStates who, Place seen)
        {
            if (ground == null) { return; }
            GameManager gm = _mn != null ? _mn.gameMN : null;
            int now = gm != null ? (int)gm.ambient & 3 : 0;
            // the period before, as AmbientChange counts it: Noon's is Morning
            int last = (now + 3) & 3;
            float t = gm != null ? Mathf.Repeat(gm.tmpDayTime - SkyFrom[now], 1440f) / SkyFadeMinutes : 1f;
            bool fading = t < 1f;
            PortraitPlace(gm, who, seen);
            if (seen.Stage != 0 || seen.House)
            {
                // a new place shows at once; only the clock's light fades, the house's with it
                if (fade != null && fade.gameObject.activeSelf) { fade.gameObject.SetActive(false); }
                bool lab = seen.Stage == 3 || seen.Stage == 10 || seen.Stage == 11;
                Sprite room = Backdrop(seen.Stage == 0 ? "portrait_house" : lab ? "portrait_lab" : "portrait_cave");
                if (ground.sprite != room) { ground.sprite = room; }
                Color tint = seen.Stage != 0 ? (lab ? Color.white : CaveColour(gm, seen.Stage))
                    : fading ? Color.Lerp(HouseTint[last], HouseTint[now], Mathf.SmoothStep(0f, 1f, t)) : HouseTint[now];
                if (ground.color != tint) { ground.color = tint; }
                return;
            }
            if (ground.color != Color.white) { ground.color = Color.white; }
            Sprite under = SkySprite(fading ? last : now);
            if (ground.sprite != under) { ground.sprite = under; }
            if (fade == null) { return; }
            if (fade.gameObject.activeSelf != fading) { fade.gameObject.SetActive(fading); }
            if (!fading) { return; }
            Sprite over = SkySprite(now);
            if (fade.sprite != over) { fade.sprite = over; }
            fade.color = new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, t));
        }

        private static void TickPortraitSkies()
        {
            if (_animeIdentity != null && _animeIdentity.gameObject.activeInHierarchy)
            {
                if (_animeIdentityThing != null) { PaintThingGround(); }
                else { PaintSky(_animeIdentitySky, _animeIdentitySkyFade, _animeIdentityWho, DockPlace); }
            }
            if (_animeEnemy != null && _animeEnemy.gameObject.activeInHierarchy) { PaintSky(_animeEnemySky, _animeEnemySkyFade, _plaqueWho, PlaquePlace); }
        }

        // THE BACKDROP FOLLOWS WHERE THEY ARE (Forgejo #77): a portrait in a cave stood
        // under the noon sky. The game has one stage at a time (GameManager.tmpStage);
        // a stage's own people stand under its objects (stageList[i].active), the player
        // and their followers go in and out with the player, and anyone picked from the
        // roster or the map may still be up on the surface. A stage shows the lab (the
        // Lab, its Basement, the Prison) or the cave in the stage's fixed light
        // (stageColor, CaveTint of the way from white); a house interior, which stands
        // well below the surface's ground, shows the house in the clock's light, as the
        // game lights indoors by the clock; everyone else, and anyone the game cannot
        // place, the clock's sky as before. Read when the portrait's person changes and
        // about once a second: a walk up their parents and one height sample, no search.
        // The paintings: make_anime_hud.py (backdrops), CAVE_TINT and HOUSE_TINT there.
        private sealed class Place
        {
            internal CommonStates Who;
            internal float At = -1f;
            internal int Stage;         // 0 the surface
            internal bool House;
        }

        private const float PlaceEvery = 1f;
        private const float HouseDepth = 10f;       // under the ground: a house's interior
        private const float CaveTint = .4f;
        private static readonly Place DockPlace = new Place(), PlaquePlace = new Place(), MarkerPlace = new Place();
        // SkyPieces' order: noon, evening, night, morning
        private static readonly Color[] HouseTint = { Color.white, new Color32(255, 214, 188, 255), new Color32(132, 142, 190, 255), new Color32(255, 242, 228, 255) };
        private static readonly Dictionary<string, Sprite> Backdrops = new Dictionary<string, Sprite>();
        private static Dictionary<Transform, int> _stageRoots;
        private static GameManager _stageRootsOf;
        private static CommonStates _animeIdentityWho;

        private static Sprite Backdrop(string piece)
        {
            if (!Backdrops.TryGetValue(piece, out Sprite sprite) || sprite == null) { Backdrops[piece] = sprite = AnimePieces.Get(piece); }
            return sprite;
        }

        private static void PortraitPlace(GameManager gm, CommonStates who, Place seen)
        {
            float now = Time.unscaledTime;
            if (ReferenceEquals(seen.Who, who) && seen.At >= 0f && now - seen.At < PlaceEvery) { return; }
            seen.Who = who; seen.At = now; seen.Stage = 0; seen.House = false;
            if (gm == null || who == null) { return; }
            seen.Stage = StageOf(gm, who);
            if (seen.Stage != 0) { return; }
            Terrain[] terrains = gm.mapGen != null && gm.mapGen.terrainMN != null ? gm.mapGen.terrainMN.terrains : null;
            Terrain surface = terrains != null && terrains.Length > 0 ? terrains[0] : null;
            if (surface == null) { return; }
            Vector3 p = who.transform.position;
            seen.House = p.y < surface.SampleHeight(p) + surface.transform.position.y - HouseDepth;
        }

        private static int StageOf(GameManager gm, CommonStates who)
        {
            if (gm.stageList == null) { return 0; }
            if (_stageRoots == null || !ReferenceEquals(_stageRootsOf, gm))
            {
                _stageRoots = new Dictionary<Transform, int>();
                _stageRootsOf = gm;
                for (int i = 1; i < gm.stageList.Length; i++)
                {
                    GameObject[] roots = gm.stageList[i].active;
                    if (roots == null) { continue; }
                    foreach (GameObject root in roots) { if (root != null) { _stageRoots[root.transform] = i; } }
                }
            }
            for (Transform t = who.transform.parent; t != null; t = t.parent)
            {
                if (_stageRoots.TryGetValue(t, out int stage)) { return stage; }
            }
            if (gm.tmpStage == 0) { return 0; }
            var followers = _mn.npcMN != null ? _mn.npcMN.follower : null;
            return ReferenceEquals(PlayerNow(gm), who) || (followers != null && followers.Contains(who)) ? gm.tmpStage : 0;
        }

        private static CommonStates PlayerNow(GameManager gm)
        {
            var players = gm != null ? gm.playerCommons : null;
            int selected = GameManager.selectPlayer;
            return players != null && selected >= 0 && selected < players.Length ? players[selected] : null;
        }

        // the stage's light, its hue at full value, CaveTint of the way from white; a
        // black one (Mine Underground's) says nothing
        private static Color CaveColour(GameManager gm, int stage)
        {
            Color c = gm != null && stage < gm.stageList.Length ? gm.stageList[stage].stageColor : Color.white;
            float top = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            if (top < .02f) { return Color.white; }
            return Color.Lerp(Color.white, new Color(c.r / top, c.g / top, c.b / top, 1f), CaveTint);
        }

        // harness: the sky the portraits show now, and the clock it is read from
        internal static string SkyProbe()
        {
            GameManager gm = _mn != null ? _mn.gameMN : null;
            if (gm == null) { return "no game"; }
            int now = (int)gm.ambient & 3;
            float t = Mathf.Repeat(gm.tmpDayTime - SkyFrom[now], 1440f) / SkyFadeMinutes;
            return gm.ambient + " " + ((int)gm.tmpDayTime / 60).ToString("00") + ":" + ((int)gm.tmpDayTime % 60).ToString("00")
                + " sky=" + SkyPieces[now] + (t < 1f ? " fading from " + SkyPieces[(now + 3) & 3] + " " + t.ToString("0.00") : "")
                + " stage=" + gm.tmpStage + " house=" + gm.tmpHouse
                + " dock=" + SkyShown(_animeIdentitySky, DockPlace)
                + " plaque=" + SkyShown(_animeEnemySky, PlaquePlace)
                + " marker=" + SkyShown(_emblemFill, MarkerPlace);
        }

        private static string SkyShown(Image ground, Place seen)
        {
            if (ground == null || ground.sprite == null) { return "-"; }
            return ground.sprite.name + (ground.color != Color.white ? "#" + ColorUtility.ToHtmlStringRGB(ground.color) : "")
                + "(" + (seen.Who != null ? seen.Who.charaName : "-") + " stage " + seen.Stage + (seen.House ? " house" : "") + ")";
        }

        // harness: the clock set to a minute of the day, as the game's DayLightSet sets
        // it to a period's first - its light and the HUD's word with it. For putting
        // the clock back after a test
        internal static string SetClockProbe(float minutes)
        {
            GameManager gm = _mn != null ? _mn.gameMN : null;
            if (gm == null) { return "no game"; }
            gm.ambAction = false;
            gm.tmpDayTime = Mathf.Repeat(minutes, gm.dayTime);
            gm.AmbientColor(gm.dayColor[gm.AmbientCheck()]);
            gm.DayLightChange();
            return SkyProbe();
        }

        // THE TARGET'S CARD: the board's portrait card left of the dock, the live
        // portrait behind its opening, and the name tab behind it running along the
        // dock's top. Board pixels from the dock frame's top-left (ActRow's Board*).
        private static Image _animeIdentityTab;

        private static void PaintAnimeIdentity(float width)
        {
            float px = AnimePieces.Px;
            if (_animeIdentity == null)
            {
                _animeIdentityTab = AnimePieces.Image(_unit.transform, "TargetTab", "target_tab");
                AnimePieces.Sliced(_animeIdentityTab, "target_tab", new Vector4(30, 0, 30, 0));
                _animeIdentityTab.raycastTarget = true;
                // over the plank's top edge, under the tiles: a lit tile rises into it
                _unitRow.UnderTiles(_animeIdentityTab.transform);
                _animeIdentityName = PlayerVitalText(_animeIdentityTab.transform, "TargetName", 6.2f, true, TextAlignmentOptions.Midline);
                _animeIdentityName.outlineWidth = .15f; _animeIdentityName.outlineColor = new Color32(26, 16, 12, 255);
                _animeIdentity = AnimeHud.Image(_unit.transform, "TargetIdentity", "mask");
                _animeIdentity.color = Color.clear; _animeIdentity.raycastTarget = true;
                var mask = AnimeHud.Image(_animeIdentity.transform, "PortraitMask", "mask");
                AnimePieces.Place(mask, 23, 18, 147, 165);
                mask.gameObject.AddComponent<Mask>().showMaskGraphic = true;
                _animeIdentitySky = mask; _animeIdentitySkyFade = PortraitSky(mask);
                var go = Owned.New("LivePortrait"); go.transform.SetParent(mask.transform, false);
                _animeIdentityFace = go.AddComponent<RawImage>(); _animeIdentityFace.raycastTarget = false;
                var r = _animeIdentityFace.rectTransform; r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
                _animeIdentityFace.uvRect = PortraitFraming;
                _animeIdentityItem = AnimeHud.Art(mask.transform, "ItemIdentity", "vicinity");
                AnimePieces.Place(_animeIdentityItem, (147f - ThingIconS) * .5f, (165f - ThingIconS) * .5f + ThingIconDrop, ThingIconS, ThingIconS);
                var card = AnimePieces.Image(_animeIdentity.transform, "Card", "target_card");
                AnimePieces.Place(card, 0, 0, 180, 198);
                BuildCardStar();
                BuildManageKey();
            }
            bool on = DockUp;
            _animeIdentity.gameObject.SetActive(on);
            _animeIdentityTab.gameObject.SetActive(on);
            if (!on) return;
            AnimePieces.Place(_animeIdentity, -170, -26, 180, 198);
            _animeIdentityName.text = _unitName.text;
            float tabW = AnimeTabWidth(width * px, _unitRow.Any);
            AnimePieces.Place(_animeIdentityTab, -20, -26, tabW, 50);
            // centred in the tab's visible part, between the card and its point, as
            // the board sets "Tumo"
            AnimePieces.Place(_animeIdentityName, 30, 3, tabW - 52, 44);
            var who = Focus() as CommonStates;
            // anything else is where the player is (the backdrop: PortraitPlace)
            _animeIdentityWho = who != null ? who : PlayerNow(_mn.gameMN);
            // live, as the world marker shows them (LivePortrait); the last snapshot
            // when they cannot be framed now - behind a door, out of the scene
            _animeIdentityFace.texture = LivePortrait(who) ?? Snapshot(who);
            _animeIdentityFace.enabled = _animeIdentityFace.texture != null;
            ItemInfo thing = who == null ? FocusProp(Focus()) : null;
            if (thing != _animeIdentityThing)
            {
                _animeIdentityThing = thing;
                var data = thing != null && _mn.itemMN != null ? _mn.itemMN.FindItem(thing.itemKey) : null;
                _animeIdentityItem.sprite = data != null ? data.itemSprite : AnimeHud.Icon("vicinity");
            }
            _animeIdentityItem.enabled = thing != null;
            if (thing != null)
            {
                _animeIdentityThingMine = _mn.buildMN != null && _mn.buildMN.OwnCheck(thing);
                PaintThingGround();
            }
            TickSnapshots();
        }

        // A THING ON THE ENAMEL (Forgejo #53, A): the sky behind a thing's icon made
        // the card read as a face. A thing's opening is the world marker's ground for
        // it instead (PaintThingMarker): card_enamel in the thing's brass, or brown
        // when it is not yours, toward white - so the dock and the marker show a thing
        // one way, and a face on sky and an object on brass are told apart at a glance.
        // The icon is larger than on the sky: the game's 256 px build icon carries
        // its own margin. A person, and a body, keep the sky. Board pixels in the
        // 147 x 165 opening.
        private const float ThingIconS = 132f, ThingIconDrop = 6f;
        private static bool _animeIdentityThingMine;

        private static void PaintThingGround()
        {
            Image ground = _animeIdentitySky;
            if (ground == null) { return; }
            Sprite enamel = Backdrop("card_enamel");
            if (ground.sprite != enamel) { ground.sprite = enamel; }
            Color tint = ThingEnamel(_animeIdentityThingMine);
            if (ground.color != tint) { ground.color = tint; }
            if (_animeIdentitySkyFade != null && _animeIdentitySkyFade.gameObject.activeSelf) { _animeIdentitySkyFade.gameObject.SetActive(false); }
        }

        private static Color ThingEnamel(bool mine) => Color.Lerp(mine ? EmblemThing : EmblemTheirThing, Color.white, .62f);

        // THE FAVOURITE ON THE CARD (Forgejo #49): the world marker's star, set into
        // the brass at 10:30 as the marker wears it, and the favourite's switch as on
        // the roster's photo (DrawPhotoStar). Set, the gold star; unset,
        // the same star unlit - dull brass in the dark edge, since the roster's bare
        // outline went missing on the dark wood and the night sky; under the pointer,
        // the pale gold. No glow round it: a glow past the dark edge made the set
        // star read larger than the unset one, so the contour is the same in every
        // state and only the fill says which. Only on one of yours the game lets you star (CanFavourite):
        // a building, a thing or an enemy has none. Board pixels on the card: the
        // star's centre, on the corner's bevel, and its size.
        private const float CardStarX = 30f, CardStarY = 26f, CardStarS = 54f;
        private static readonly Color StarUnlit = Theme.ParseColor("#7A6446", Color.gray);
        private static Image _cardStarHit, _cardStarEdge, _cardStar;

        private static void BuildCardStar()
        {
            Transform t = _animeIdentity.transform;
            // the hit first, under the star: a raycast target, so a press on the
            // corner it hangs past never reaches the world
            _cardStarHit = AnimeHud.Image(t, "FavouriteHit", "mask");
            _cardStarHit.color = Color.clear; _cardStarHit.raycastTarget = true;
            _cardStarEdge = Theme.KitImage(t, "FavouriteEdge", "kit_dossier_star", 0, EmblemInk);
            _cardStar = Theme.KitImage(t, "Favourite", "kit_dossier_star", 0, StarShine);
            float s = CardStarS, e = s * SmallStarEdge, x = CardStarX - s * .5f, y = CardStarY - s * .5f;
            AnimePieces.Place(_cardStarHit, x - 4f, y - 4f, s + 8f, s + 8f);
            AnimePieces.Place(_cardStarEdge, x - e * .5f, y - e * .5f, s + e, s + e);
            AnimePieces.Place(_cardStar, x, y, s, s);
            PaintCardStar(null, false, false);
        }

        private static bool OnCardStar => _cardStarHit != null && _cardStarHit.gameObject.activeInHierarchy
            && RectTransformUtility.RectangleContainsScreenPoint(_cardStarHit.rectTransform, Input.mousePosition, null);

        // every frame, from the dock's input: the star follows the favourite and the pointer
        private static void PaintCardStar(CommonStates who, bool hot, bool down)
        {
            if (_cardStarHit == null)
            {
                return;
            }
            bool can = CanFavourite(who);
            bool on = can && who.employ == CommonStates.Employ.Favorite;
            if (_cardStarHit.gameObject.activeSelf != can) { _cardStarHit.gameObject.SetActive(can); }
            if (_cardStarEdge.gameObject.activeSelf != can) { _cardStarEdge.gameObject.SetActive(can); }
            if (_cardStar.gameObject.activeSelf != can) { _cardStar.gameObject.SetActive(can); }
            if (!can)
            {
                return;
            }
            _cardStar.color = down ? Alpha(StarShine, 0.7f) : on ? StarShine : hot ? StarHover : StarUnlit;
        }

        // The tab's width for the name, in board pixels: the words, the hexagon's
        // pointed end and the part hidden behind the card. THE ROW SIZES THE PLANK
        // (Forgejo #129), so a name wider than the acts sticks out past the plank's
        // end: the tab is a closed hexagon, its bottom rim and point drawn over the
        // plank, and past it they close it. One whose tip would land in the plank's
        // corner - its chamfer and leaves, from TabEndRoom short of the end to
        // TabClear past it - grows on until it clears them, so a tab ends well short
        // of the corner or plainly past it, never on it. Without a plank (a torch)
        // it is the name's width. The plank ends `end` px right of the dock frame's
        // left, where the tab's box starts 20 px behind the card.
        private const float TabEndRoom = 30f, TabClear = 20f;
        private static float AnimeTabWidth(float end, bool plank)
        {
            if (_animeIdentityName == null || _unitName == null) return 0f;
            float w = Mathf.Max(185f, _animeIdentityName.GetPreferredValues(_unitName.text).x * AnimePieces.Px + 78f);
            float right = w - 20f;
            return plank && right > end - TabEndRoom && right < end + TabClear ? end + TabClear + 20f : w;
        }

        private static void ResetAnimeEnemy()
        {
            _animeEnemy = null; _enemyOriginal = null;
            RestoreCaution(); _caution = null; RestorePrompt(); _prompt = null;
            _animeEnemyMore = null; _animeEnemyMoreText = null; _animeEnemyGlow = null; _plaqueWho = null; _plaqueMore = -1;
            _animeEnemyHit = _animeEnemyRim = _animeEnemyPlate = null; _animeEnemyTip = null; _plaquePressed = false;
            _animeIdentity = null; _animeIdentityFace = null; _animeIdentityName = null; _animeIdentityTab = null;
            _animeIdentityItem = null; _animeIdentityThing = null;
            _animeIdentitySky = _animeIdentitySkyFade = _animeEnemySky = _animeEnemySkyFade = null;
            _animeIdentityWho = null; _stageRoots = null; _stageRootsOf = null;
        }

        private static void TickAnimeEnemy(GameManager gm)
        {
            if (gm.enemyLifeImage == null) return;
            if (_animeEnemy == null)
            {
                EnsureMarkerFonts();
                _enemyOriginal = gm.enemyLifeImage.transform.parent;
                var group = _enemyOriginal.GetComponent<CanvasGroup>(); bool added = group == null;
                if (added) group = _enemyOriginal.gameObject.AddComponent<CanvasGroup>();
                HiddenButtons.Add(new HiddenHud { Group = group, Added = added, Alpha = group.alpha, Interactable = group.interactable, Raycasts = group.blocksRaycasts });
                group.alpha = 0; group.interactable = group.blocksRaycasts = false;
                // the board's combat plaque (section 2) at 1.25x its scale: the plate
                // with its opening, empty tracks and blank coin; live portrait, words
                // and fills on it, at the board's pixel positions (cut at 925,188)
                const float k = CombatScale;
                // at the top centre, where vanilla's bar was (Forgejo #63)
                _animeEnemy = AnimePieces.Image(_hud, "EnemyCombat", "combat_plate");
                _animeEnemy.color = Color.clear;
                var r = _animeEnemy.rectTransform; r.anchorMin = r.anchorMax = new Vector2(.5f, 1); r.pivot = new Vector2(.5f, 1);
                r.anchoredPosition = new Vector2(0, -6); r.sizeDelta = AnimePieces.Size("combat_plate") * k;
                var mask = AnimeHud.Image(r, "PortraitMask", "mask");
                AnimePieces.Place(mask, 24, 51, 142, 157, k);
                mask.gameObject.AddComponent<Mask>().showMaskGraphic = true;
                _animeEnemySky = mask; _animeEnemySkyFade = PortraitSky(mask);
                var go = Owned.New("LivePortrait"); go.transform.SetParent(mask.transform, false);
                _animeEnemyFace = go.AddComponent<RawImage>(); _animeEnemyFace.raycastTarget = false;
                var fr = _animeEnemyFace.rectTransform; fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one; fr.offsetMin = fr.offsetMax = Vector2.zero;
                _animeEnemyFace.uvRect = PortraitFraming;
                // a kill's face, washed to ash over the portrait and its sky
                _animeEnemyAsh = SlainRect(mask.transform, "SlainAsh", SlainAsh);
                var ar = _animeEnemyAsh.rectTransform; ar.anchorMin = Vector2.zero; ar.anchorMax = Vector2.one; ar.offsetMin = ar.offsetMax = Vector2.zero;
                var plate = AnimePieces.Image(r, "Plate", "combat_plate");
                AnimePieces.Place(plate, 0, 0, 605, 232, k);
                _animeEnemyPlate = plate;
                // A BOSS'S PLATE says BOSS on its black tab, by its skull (LayPlaque)
                _animeEnemyBossWord = PlayerVitalText(r, "BossWord", 6.6f, true, TextAlignmentOptions.Center);
                UI.LocLabels.Set(_animeEnemyBossWord, () => Shared.Loc.Get("hud.enemy.boss", "BOSS"));
                AnimePieces.Place(_animeEnemyBossWord, 280, 16, 104, 42, k);
                // THE DOWNED (make_combat_slain.py): Combat's tab in bone with a
                // skull for a kill, in slate with stars for one fainted, the word set
                // live beside it
                _animeEnemySlainTab = AnimePieces.Image(r, "SlainTab", "combat_tab_slain");
                AnimePieces.Place(_animeEnemySlainTab, 205, 0, 210, 66, k);
                _animeEnemySlainWord = PlayerVitalText(r, "SlainWord", 6.6f, true, TextAlignmentOptions.Center);
                UI.LocLabels.Set(_animeEnemySlainWord, DownWord);
                AnimePieces.Place(_animeEnemySlainWord, 280, 16, 104, 42, k);
                // the frame takes the pointer (ReadPlaqueInput); its tab and leaves,
                // and the "+N" plate under it, let it through to the world
                _animeEnemyHit = AnimeHud.Image(r, "TargetHit", "mask");
                _animeEnemyHit.color = Color.clear; _animeEnemyHit.raycastTarget = true;
                AnimePieces.Place(_animeEnemyHit, 2, 28, 601, 202, k);
                _animeEnemyRim = AnimePieces.Image(r, "TargetRim", "rim_on");
                AnimePieces.Sliced(_animeEnemyRim, "rim_on", new Vector4(24, 24, 24, 24));
                AnimePieces.Place(_animeEnemyRim, 2, 28, 601, 202, k);
                _animeEnemyRim.enabled = false;
                _animeEnemyTip = KitTooltip.Create(_hud);
                _animeEnemyHpFill = AnimePieces.Image(r, "HealthFill", "combat_hp_fill");
                AnimePieces.Place(_animeEnemyHpFill, 249, 124, 310, 32, k);
                _animeEnemyStunFill = AnimePieces.Image(r, "FaintFill", "combat_faint_fill");
                AnimePieces.Place(_animeEnemyStunFill, 258, 175, 300, 30, k);
                foreach (var fill in new[] { _animeEnemyHpFill, _animeEnemyStunFill })
                { fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal; fill.fillOrigin = 0; }
                _animeEnemyName = PlayerVitalText(r, "Name", 8.5f, true);
                AnimePieces.Place(_animeEnemyName, 200, 66, 250, 46, k);
                _animeEnemyMeta = PlayerVitalText(r, "Details", 5.8f, false, TextAlignmentOptions.MidlineRight);
                AnimePieces.Place(_animeEnemyMeta, 400, 66, 160, 46, k);
                _animeEnemyLevel = PlayerVitalText(r, "Level", 7f, true, TextAlignmentOptions.Center);
                _animeEnemyLevel.color = AnimeHud.Hex("302015");
                _animeEnemyLevel.enableAutoSizing = true; _animeEnemyLevel.fontSizeMin = 4; _animeEnemyLevel.fontSizeMax = 7;
                AnimePieces.Place(_animeEnemyLevel, 188, 113, 44, 44, k);
                _animeEnemyLife = PlayerVitalText(r, "HealthReading", 6.2f, true, TextAlignmentOptions.MidlineRight);
                _animeEnemyLife.outlineWidth = .15f; _animeEnemyLife.outlineColor = new Color32(30, 15, 16, 255);
                AnimePieces.Place(_animeEnemyLife, 262, 120, 286, 40, k);
                var faintWord = PlayerVitalText(r, "FaintLabel", 5.6f, true, TextAlignmentOptions.MidlineLeft);
                UI.LocLabels.Set(faintWord, () => Shared.Loc.Get("hud.enemy.faint", "Faint"));
                AnimePieces.Place(faintWord, 192, 170, 64, 36, k);
                _animeEnemyFaint = PlayerVitalText(r, "FaintReading", 5.6f, true, TextAlignmentOptions.MidlineRight);
                _animeEnemyFaint.color = AnimeHud.Hex("1E1612");
                AnimePieces.Place(_animeEnemyFaint, 272, 172, 276, 34, k);
                // the rest of the fight, on a small plate hung from the bars' end
                _animeEnemyMore = AnimePieces.Image(r, "MoreInCombat", "frame_plate");
                AnimePieces.Sliced(_animeEnemyMore, "frame_plate", MarkerFrameBorder, MarkerFrameScale * .6f);
                _animeEnemyMoreText = PlayerVitalText(_animeEnemyMore.transform, "Count", 5.6f, true, TextAlignmentOptions.Center);
                _animeEnemyMoreText.outlineWidth = .15f; _animeEnemyMoreText.outlineColor = new Color32(26, 16, 12, 255);
                _animeEnemyMore.transform.SetAsFirstSibling();
                // under everything: the threat's glow round the plate (Combat.cs)
                _animeEnemyGlow = ThreatGlow(r, "Threat");
                _animeEnemyGlow.transform.SetAsFirstSibling();
                _plaqueWho = null; _plaqueSlain = _plaqueBoss = -1;
            }
            // THE PLAQUE SHOWS THE FIGHT'S FIRST (CombatList): the boss before the
            // rest and a summon last (Forgejo #74), and of those the one you hit last,
            // or with no hit, the nearest on you - vanilla's viewCommon when there
            // is one. Its words are read from them as EnemyLifeChange writes
            // vanilla's, and only when they change - or when the Observation
            // skill does (Observe.cs): a level learnt mid-fight fills them in.
            // Only a fight of yours: one only your people fight has its marker
            CombatList.Entry top = CombatList.Count > 0 ? CombatList.At(0) : null;
            bool shown = top != null && top.Who != null && top.Mine;
            if (_animeEnemy.gameObject.activeSelf != shown) { _animeEnemy.gameObject.SetActive(shown); }
            PlaceCaution(shown);
            PlacePrompt(shown);
            if (!shown) { _plaqueWho = null; ReadPlaqueInput(null); return; }
            CommonStates who = top.Who;
            ReadPlaqueInput(who);
            bool boss = CombatList.IsBoss(top);
            LayPlaque(boss);
            // the health fill in their stance's colour (target-matrix D4): amber until
            // they come for you; a boss keeps its own longer fill
            string hpFill = boss ? "combat_hp_fill_boss" : Stance.HealthFill(who);
            if (!ReferenceEquals(hpFill, _plaqueFill)) { _plaqueFill = hpFill; _animeEnemyHpFill.sprite = AnimePieces.Get(hpFill); }
            PaintSlain(top);
            int rank = Observe.Rank();
            // a language switch letters the plaque again, like the rest of the open UI
            if (!ReferenceEquals(who, _plaqueWho) || _plaqueLoc != Shared.Loc.Generation || _plaqueRank != rank)
            {
                _plaqueWho = who; _plaqueLoc = Shared.Loc.Generation; _plaqueRank = rank; _plaqueMore = -1;
                _plaqueLife = _plaqueMaxLife = _plaqueFaint = _plaqueMaxFaint = double.NaN;
                _animeEnemyName.text = PlaqueName(who);
                // the board: the name, and its age at the right; the level on the coin
                string meta = Observe.Sees(who, Observe.Age) && who.age != 0 ? Shared.Loc.Get("hud.enemy.age", "Age {0}", who.age) : "";
                TextManager tm = GameWords.Text(_mn);
                string[] persons = tm != null ? tm.personTexts : null;
                int person = who.person != null && who.person.Length > 0 ? who.person[0] : -1;
                if (Observe.Sees(who, Observe.Personality) && persons != null && person >= 0 && person < persons.Length)
                {
                    meta = (meta + "  " + persons[person]).Trim();
                }
                _animeEnemyMeta.text = meta;
                _animeEnemyLevel.text = Observe.Sees(who, Observe.Level) ? who.level.ToString("0") : Observe.UnknownShort;
            }
            if (who.life != _plaqueLife || who.maxLife != _plaqueMaxLife)
            {
                _plaqueLife = who.life; _plaqueMaxLife = who.maxLife;
                _animeEnemyLife.text = Observe.Sees(who, Observe.Life) ? who.life + " / " + who.maxLife : "";
                _animeEnemyHpFill.fillAmount = who.maxLife > 0 ? Mathf.Clamp01((float)(who.life / who.maxLife)) : 0;
            }
            if (who.faint != _plaqueFaint || who.maxFaint != _plaqueMaxFaint)
            {
                _plaqueFaint = who.faint; _plaqueMaxFaint = who.maxFaint;
                _animeEnemyFaint.text = Observe.Sees(who, Observe.Faint) ? who.faint + " / " + who.maxFaint : "";
                _animeEnemyStunFill.fillAmount = who.maxFaint > 0 ? Mathf.Clamp01((float)(who.faint / who.maxFaint)) : 0;
            }
            PaintMoreInCombat(CombatMore());
            // the plate's frame, under its tab and leaves: board (2, 28) to (603, 230)
            const float g = CombatScale / AnimePieces.Px;
            PaintThreat(_animeEnemyGlow, top.OnYou, 2f * g, 28f * g, (601f + _plaqueExtra) * g, 202f * g, PlaqueGlow);
            _animeEnemyFace.texture = Snapshot(who, whole: true); _animeEnemyFace.enabled = _animeEnemyFace.texture != null; TickSnapshots();
        }

        // THE NAME IN THE LANGUAGE PLAYED. A spawned enemy is named by the game
        // in it (StatsData's localized name); the swamp boss (Planton, npcID 103)
        // is placed in the scene with its prefab's name - the kind's untranslated
        // Japanese one with a part after it, "(本)" - which the plaque's face has
        // no glyphs for, so its line came out empty (Forgejo #184). A name that
        // is the kind's untranslated name, while the language played has its own,
        // gives way to that.
        private static string PlaqueName(CommonStates who)
        {
            string name = who.charaName ?? string.Empty;
            StatsData stats = _mn != null && _mn.npcMN != null ? _mn.npcMN.GetStatsData(who.npcID) : null;
            if (stats == null || string.IsNullOrEmpty(stats.charaName))
            {
                return name;
            }
            string local = stats.GetLocalizedCharaName();
            bool untranslated = name.Length == 0
                || name.StartsWith(stats.charaName, System.StringComparison.Ordinal);
            return untranslated && !string.IsNullOrEmpty(local) && local != stats.charaName ? local : name;
        }

        private static Image _animeEnemyMore, _animeEnemyGlow;
        private static Image _animeEnemySlainTab, _animeEnemyAsh;
        private static TextMeshProUGUI _animeEnemySlainWord, _animeEnemyBossWord;
        private static int _plaqueSlain = -1;   // 1 a kill shows, 2 one fainted, 0 neither, -1 not yet painted
        private static int _plaqueBoss = -1;    // 1 the boss's plate is up, 0 the plain one, -1 not yet laid
        private static float _plaqueExtra;      // how much wider than the plain plate, board px
        private const float PlaqueBossExtra = 180f;   // make_combat_slain.py's BOSS_EXTRA
        private static readonly Color SlainAsh = new Color(.42f, .4f, .38f, .62f), SlainName = new Color32(196, 188, 172, 255);

        // a plain rect, live (Strip.cs makes them hidden), shown by enabled
        private static Image SlainRect(Transform parent, string name, Color color)
        {
            Image image = PlainImage(parent, name, color);
            image.gameObject.SetActive(true);
            return image;
        }

        // THE DOWNED ON THE PLAQUE (design B, make_kill_plaque_ideas.py): for the
        // seconds one stays at the top once down (CombatList.KillLinger) the tab
        // says what happened. The game downs both alike - NPCMove.Dead sets dead 1
        // whether life or stun ran out - so life tells them apart: at 0 a kill,
        // the face gone to ash and the name dimmed; above it, fainted, the face as
        // it was. The bars say the rest.
        private static void PaintSlain(CombatList.Entry top)
        {
            int state = top.Alive ? 0 : top.Who.life > 0 ? 2 : 1;
            if (_plaqueSlain == state) { return; }
            _plaqueSlain = state;
            bool down = state != 0, slain = state == 1;
            _animeEnemySlainTab.enabled = _animeEnemySlainWord.enabled = down;
            if (down)
            {
                _animeEnemySlainTab.sprite = AnimePieces.Get(slain ? "combat_tab_slain" : "combat_tab_fainted");
                _animeEnemySlainWord.text = DownWord();
            }
            _animeEnemyAsh.enabled = slain;
            _animeEnemyName.color = slain ? SlainName : MarkerCream;
            _animeEnemyBossWord.enabled = _plaqueBoss == 1 && !down;
        }

        private static string DownWord() => _plaqueSlain == 2
            ? Shared.Loc.Get("hud.enemy.fainted", "Fainted") : Shared.Loc.Get("hud.enemy.slain", "Slain");

        // THE BOSS'S PLATE (C of make_boss_plaque_ideas.py): a third wider, the
        // run of the bars stretched right of the tab, so everything right of it
        // moves out by PlaqueBossExtra and the bars and their fills grow by it
        private static void LayPlaque(bool boss)
        {
            if (_plaqueBoss == (boss ? 1 : 0)) { return; }
            _plaqueBoss = boss ? 1 : 0;
            const float k = CombatScale;
            float x = _plaqueExtra = boss ? PlaqueBossExtra : 0f;
            string plate = boss ? "combat_plate_boss" : "combat_plate";
            _animeEnemyPlate.sprite = AnimePieces.Get(plate);
            _animeEnemy.rectTransform.sizeDelta = AnimePieces.Size(plate) * k;
            AnimePieces.Place(_animeEnemyPlate, 0, 0, 605 + x, 232, k);
            AnimePieces.Place(_animeEnemyHit, 2, 28, 601 + x, 202, k);
            AnimePieces.Place(_animeEnemyRim, 2, 28, 601 + x, 202, k);
            AnimePieces.Place(_animeEnemyHpFill, 249, 124, 310 + x, 32, k);
            _animeEnemyStunFill.sprite = AnimePieces.Get(boss ? "combat_faint_fill_boss" : "combat_faint_fill");
            AnimePieces.Place(_animeEnemyStunFill, 258, 175, 300 + x, 30, k);
            AnimePieces.Place(_animeEnemyName, 200, 66, 250 + x, 46, k);
            AnimePieces.Place(_animeEnemyMeta, 400 + x, 66, 160, 46, k);
            AnimePieces.Place(_animeEnemyLife, 262, 120, 286 + x, 40, k);
            AnimePieces.Place(_animeEnemyFaint, 272, 172, 276 + x, 34, k);
            _animeEnemyBossWord.enabled = boss && _plaqueSlain <= 0;
            _plaqueMore = -1;   // its plate moves out with the bars' end
        }
        private static TextMeshProUGUI _animeEnemyMoreText;
        private static CommonStates _plaqueWho;
        private static double _plaqueLife, _plaqueMaxLife, _plaqueFaint, _plaqueMaxFaint;
        private static int _plaqueMore = -1, _plaqueLoc = -1, _plaqueRank = -1;

        // THE PLAQUE PICKS ITS ENEMY (Forgejo #106): a press and release on its
        // frame makes the one it shows the target, the way Tab and a map pin pick
        // someone (TargetPerson) - so the dock and the world marker follow. The one
        // who is the target already stays it: the click does nothing, where Tab's
        // second press would let them go. Under the pointer the frame wears a
        // tile's hover rim and pressed it dims, as the docks' tiles do (AnimeHud).
        // The hit is a raycast target, so the game's OnUI keeps that click from
        // walking or swinging; off it, the world has the pointer as before.
        private static readonly Color PlaqueRimHot = new Color(1f, 1f, 1f, .5f), PlaquePressed = new Color(.84f, .82f, .8f, 1f);
        private static Image _animeEnemyHit, _animeEnemyRim, _animeEnemyPlate;
        private static KitTooltip _animeEnemyTip;
        private static bool _plaquePressed;

        private static void ReadPlaqueInput(CommonStates who)
        {
            if (_animeEnemyHit == null) { return; }
            bool hot = who != null && PlayerVitalHot(_animeEnemyHit.rectTransform);
            if (Input.GetMouseButtonDown(0)) { _plaquePressed = hot; }
            bool down = hot && _plaquePressed && Input.GetMouseButton(0);
            if (Input.GetMouseButtonUp(0))
            {
                if (_plaquePressed && hot && !ReferenceEquals(_heldRef, who) && _mn != null && _mn.gameMN != null && _mn.gameMN.pMove != null)
                {
                    TargetPerson(who, "plaque");
                }
                _plaquePressed = false;
            }
            bool rim = hot && !down;
            if (_animeEnemyRim.enabled != rim) { _animeEnemyRim.enabled = rim; }
            if (rim) { _animeEnemyRim.color = PlaqueRimHot; }
            Color tint = down ? PlaquePressed : Color.white;
            if (_animeEnemyPlate.color != tint) { _animeEnemyPlate.color = tint; }
            string tip = !hot ? null
                : "<b>" + who.charaName + "</b>\n" + (ReferenceEquals(_heldRef, who)
                    ? Shared.Loc.Get("hud.enemy.targeted", "Your target")
                    : Shared.Loc.Get("hud.enemy.target", "Click to target"));
            _animeEnemyTip.Tick(tip, null);
        }

        // "+3 in combat": the enemies of the list that neither the plaque nor a
        // marker shows
        private static void PaintMoreInCombat(int more)
        {
            bool on = more > 0;
            if (_animeEnemyMore.gameObject.activeSelf != on) { _animeEnemyMore.gameObject.SetActive(on); }
            if (!on || more == _plaqueMore) { return; }
            _plaqueMore = more;
            _animeEnemyMoreText.text = Shared.Loc.Plural("combat.more", "+{0} in combat", "+{0} in combat", more);
            float w = Mathf.Ceil(_animeEnemyMoreText.GetPreferredValues(_animeEnemyMoreText.text).x) + 10f;
            // under the tracks' right end, its top behind the plate's lower edge
            const float k = CombatScale;
            float right = (559f + _plaqueExtra) / AnimePieces.Px * k, top = 222f / AnimePieces.Px * k;
            Theme.Place(_animeEnemyMore, right - w, top, w, 11f);
            Theme.Place(_animeEnemyMoreText, 0f, 1.2f, w, 9f);
        }

        // THE CAUTION BANNER (FrontCanvas/CautionText, "can't travel, enemies
        // nearby") stands 37-87 units from the top, centred: across the plaque.
        // It is a moment's message and the plaque the fight's reading, so while
        // the plaque is up the banner steps down to just under it, and back when
        // it goes. Vanilla only ever sets its text and switches it on and off.
        private static TextMeshProUGUI _caution;
        private static Vector2 _cautionHome;
        private static bool _cautionMoved;
        private static readonly Vector3[] PlaqueCorners = new Vector3[4];
        private const float CautionGap = 3f;

        private static void PlaceCaution(bool plaque)
        {
            if (_caution == null)
            {
                EventManager ev = _mn != null ? _mn.eventMN : null;
                if (ev == null || ev.cautionText == null) { return; }
                _caution = ev.cautionText;
                _cautionHome = _caution.rectTransform.anchoredPosition;
                _cautionMoved = false;
            }
            if (!plaque)
            {
                RestoreCaution();
                return;
            }
            RectTransform banner = _caution.rectTransform;
            var parent = banner.parent as RectTransform;
            if (parent == null) { return; }
            // the plaque's lowest edge, its "+N" plate included, in the banner's space
            RectTransform low = _animeEnemyMore.gameObject.activeSelf ? _animeEnemyMore.rectTransform : _animeEnemy.rectTransform;
            low.GetWorldCorners(PlaqueCorners);   // 0 = bottom-left
            float bottom = parent.InverseTransformPoint(PlaqueCorners[0]).y;
            // its words are one line in the middle of its box: their middle goes
            // half a line under the plaque, and the box's anchored point with it
            float middle = bottom - CautionGap - _caution.fontSize * .6f;
            float anchor = parent.rect.yMin + banner.anchorMin.y * parent.rect.height;
            float y = Mathf.Min(_cautionHome.y, middle - anchor - (.5f - banner.pivot.y) * banner.rect.height);
            var at = new Vector2(_cautionHome.x, y);
            if (banner.anchoredPosition != at) { banner.anchoredPosition = at; }
            _cautionMoved = true;
        }

        private static void RestoreCaution()
        {
            if (_caution != null && _cautionMoved) { _caution.rectTransform.anchoredPosition = _cautionHome; }
            _cautionMoved = false;
        }

        // THE CONTROL PROMPT (FrontCanvas/ControlDescText, "[Left Click] Continue
        // ...  [R] Leave." through an assault, a scene, a placement) hangs from the
        // top centre, across the plaque. Like the caution banner it steps down to
        // just under the plaque while both are up, and back when the plaque goes;
        // vanilla only sets its words and switches it on and off. The story hints
        // beside it follow it (StoryChoices).
        private static TextMeshProUGUI _prompt;
        private static Vector2 _promptHome;
        private static bool _promptMoved;

        private static void PlacePrompt(bool plaque)
        {
            if (_prompt == null)
            {
                UIManager ui = _mn != null ? _mn.uiMN : null;
                if (ui == null || ui.controlDescText == null) { return; }
                _prompt = ui.controlDescText;
                _promptHome = _prompt.rectTransform.anchoredPosition;
                _promptMoved = false;
            }
            if (!plaque || !_prompt.gameObject.activeInHierarchy)
            {
                RestorePrompt();
                return;
            }
            RectTransform box = _prompt.rectTransform;
            var parent = box.parent as RectTransform;
            if (parent == null) { return; }
            RectTransform low = _animeEnemyMore.gameObject.activeSelf ? _animeEnemyMore.rectTransform : _animeEnemy.rectTransform;
            low.GetWorldCorners(PlaqueCorners);   // 0 = bottom-left
            float bottom = parent.InverseTransformPoint(PlaqueCorners[0]).y;
            // the box's top goes a gap under the plaque, the anchored point with it
            float anchor = parent.rect.yMin + box.anchorMin.y * parent.rect.height;
            float y = Mathf.Min(_promptHome.y, bottom - CautionGap - anchor - (1f - box.pivot.y) * box.rect.height);
            var at = new Vector2(_promptHome.x, y);
            if (box.anchoredPosition != at) { box.anchoredPosition = at; }
            _promptMoved = true;
        }

        private static void RestorePrompt()
        {
            if (_prompt != null && _promptMoved) { _prompt.rectTransform.anchoredPosition = _promptHome; }
            _promptMoved = false;
        }

        // UI-only harness probe: the existing combat display lifecycle, with no
        // damage, hostility changes, or modification of character statistics.
        internal static string PreviewAnimeCombat()
        {
            var who = Focus() as CommonStates;
            if (who == null || _mn == null || _mn.gameMN == null) return "select a character first";
            _mn.gameMN.StartCoroutine(_mn.gameMN.EnemyLifeChange(who));
            return "combat display preview: " + who.charaName;
        }

        // ...and vanilla's own bar shown again, `drop` canvas units under the
        // plaque, to set the two side by side ("on 120"); "off" hides it where it was
        private static Vector2 _vanillaBarHome;
        private static bool _vanillaBarShown;

        internal static string HarnessVanillaBar(string how)
        {
            var rect = _enemyOriginal as RectTransform;
            CanvasGroup group = rect != null ? rect.GetComponent<CanvasGroup>() : null;
            if (group == null) return "no plaque yet";
            string[] words = (how ?? "").Split(' ');
            if (words[0] == "on")
            {
                float drop = words.Length > 1 && float.TryParse(words[1], out float d) ? d : 120f;
                if (!_vanillaBarShown) { _vanillaBarHome = rect.anchoredPosition; _vanillaBarShown = true; }
                rect.anchoredPosition = _vanillaBarHome + new Vector2(0f, -drop);
                group.alpha = 1f;
            }
            else if (_vanillaBarShown)
            {
                rect.anchoredPosition = _vanillaBarHome;
                group.alpha = 0f;
                _vanillaBarShown = false;
            }
            return "vanilla bar " + (_vanillaBarShown ? "shown" : "hidden") + " at " + rect.anchoredPosition;
        }
    }
}
