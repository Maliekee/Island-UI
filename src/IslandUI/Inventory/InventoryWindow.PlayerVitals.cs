using IslandUI.UI;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace IslandUI
{
    // Navigation and strong health share an illustrated plate. EXP stays below
    // the action bar, with independent character and skill rails.
    internal static partial class InventoryWindow
    {
        private static RectTransform _playerVitals, _vanillaVitals;
        private static Image _playerHp, _playerHpFill, _playerStun, _playerStunFill;
        private static Image _playerXp, _playerXpFill, _playerXpHit;
        private static Image _playerSkillXp, _playerSkillXpFill, _playerSkillXpHit;
        private static TextMeshProUGUI _playerName, _playerLevel, _playerHpText;
        private static RectTransform _playerCoin;
        private static Image _playerFood, _playerWater;
        private static readonly Image[] PlayerHearts = new Image[3];
        private static readonly Image[] PlayerXpTicks = new Image[9];
        private static readonly Image[] PlayerSkillXpTicks = new Image[9];
        private static readonly List<RaycastResult> PlayerVitalHits = new List<RaycastResult>();
        private static KitTooltip _playerVitalsTip;
        private const float PlayerRailH = 3.5f;
        private static float _panelPreviewUntil;
        private static Vector4 _panelPreview;
        private static int _panelPreviewLives;

        // Harness-only visual probe. Changes no game values and expires automatically.
        internal static void PreviewPlayerPanel(float health,float stun,float food,float water,int lives)
        {
            _panelPreview=new Vector4(Mathf.Clamp01(health),Mathf.Clamp01(stun),Mathf.Clamp01(food),Mathf.Clamp01(water));
            _panelPreviewLives=Mathf.Clamp(lives,0,3);
            _panelPreviewUntil=Time.realtimeSinceStartup+15f;
        }
        internal static void EndPlayerPanelPreview() { _panelPreviewUntil=0; }

        private static void ResetPlayerVitals()
        {
            // The owned children die with _hud; ResetHud restores the vanilla
            // CanvasGroup before destroying them, including on a hot reload.
            ResetAnimeEnemy();
            ResetLowVitals();
            _panelPreviewUntil=0;
            _playerVitals = _vanillaVitals = null;
            _playerVitalsCanvas = null;
            _playerVitalsRaycaster = null;
            _playerHp = _playerHpFill = _playerStun = _playerStunFill = null;
            _playerXp = _playerXpFill = _playerXpHit = null;
            _playerSkillXp = _playerSkillXpFill = _playerSkillXpHit = null;
            _playerVitalsTip = null;
            _playerName = _playerLevel = _playerHpText = null;
            _playerCoin = null;
            ClearBurstVisuals();
            _playerFood = _playerWater = null;
            System.Array.Clear(PlayerHearts, 0, PlayerHearts.Length);
            System.Array.Clear(PlayerXpTicks, 0, PlayerXpTicks.Length);
            System.Array.Clear(PlayerSkillXpTicks, 0, PlayerSkillXpTicks.Length);
            PlayerVitalHits.Clear();
        }

        private static TextMeshProUGUI PlayerVitalText(Transform parent, string name,
            float size, bool bold = false, TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft)
        {
            var text = Theme.MakeKitText(parent, name, size, MarkerCream, align, bold);
            SetMarkerFont(text, bold);
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            return text;
        }

        private static Image PlayerVitalTrack(Transform parent, string name, out Image fill)
        {
            var track = Theme.KitImage(parent, name, "kit_emblem_track", 8, Color.white);
            fill = Theme.KitImage(track.transform, "Fill", "kit_emblem_bar", 8, Color.white);
            return track;
        }

        // An EXP rail on the HUD: a clear box that takes the hover, its track ruled at
        // every tenth, and the level-up's shine behind it (InventoryWindow.LevelUp.cs).
        private static Image PlayerXpRail(string name, Image[] ticks, LevelBurst burst, out Image track, out Image fill)
        {
            var hit = Theme.KitImage(_hud, name, "kit_band", 0, Color.clear);
            hit.raycastTarget = true;
            burst.Shine = PlainImage(hit.transform, "LevelUpShine", Color.clear);
            track = PlayerVitalTrack(hit.transform, "Experience", out fill);
            for (int i = 0; i < ticks.Length; i++)
            {
                ticks[i] = PlainImage(track.transform, "TenPercent" + (i + 1), StripMajor);
                ticks[i].gameObject.SetActive(true);
            }
            return hit;
        }

        // Vanilla colours its own food and water icons (orange, blue) and recolours
        // them for a condition such as food poisoning. The painted icons carry their
        // own colour, so vanilla's is applied only when it has left its usual hue -
        // multiplied always, it turned the cyan drop navy.
        private static Color NeedTint(Color vanilla, float usualHue)
        {
            Color.RGBToHSV(vanilla, out float hue, out float saturation, out _);
            float off = Mathf.Abs(hue - usualHue); off = Mathf.Min(off, 1f - off);
            return saturation < .2f || off < .08f ? new Color(1f, 1f, 1f, vanilla.a) : vanilla;
        }

        private static Image PlayerNeed(Transform parent, string name)
        {
            string key=name.ToLowerInvariant();
            Rect r=PlayerPanelArt.FillRect(key);float k=PlayerPanelArt.Scale;
            var back=PlayerPanelArt.Image(parent,name,null);back.color=Color.clear;
            Theme.Place(back,4+r.x*k,r.y*k-28.5f,r.width*k,r.height*k);
            var fill=PlayerPanelArt.Image(back.transform,"Fill",PlayerPanelArt.Fill(key));
            fill.type=Image.Type.Filled;fill.fillMethod=Image.FillMethod.Vertical;fill.fillOrigin=0;
            Theme.Place(fill,0,0,r.width*k,r.height*k);
            return fill;
        }

        private static Image PanelBar(Transform parent,string name,string key,out Image fill)
        {
            var track=PlayerPanelArt.Image(parent,name,null);track.color=Color.clear;
            Rect r=PlayerPanelArt.FillRect(key);float k=PlayerPanelArt.Scale;
            float x=4+r.x*k,y=r.y*k-28.5f,w=r.width*k,h=r.height*k;
            Theme.Place(track,x,y,w,h);
            fill=PlayerPanelArt.Image(track.transform,"Fill",PlayerPanelArt.Fill(key));
            Theme.Place(fill,0,0,w,h);
            fill.type=Image.Type.Filled;fill.fillMethod=Image.FillMethod.Horizontal;fill.fillOrigin=0;
            return track;
        }

        private static bool BuildPlayerVitals(GameManager gm)
        {
            var vanilla = _hud.parent.Find("StateBGImage") as RectTransform;
            if (vanilla == null || gm.foodImage == null || gm.waterImage == null) { return false; }
            EnsureMarkerFonts();
            _vanillaVitals = vanilla;
            var go = Owned.New("PlayerVitals");
            go.transform.SetParent(_hud, false);
            _playerVitals = (RectTransform)go.transform;
            _playerVitals.anchorMin = _playerVitals.anchorMax = Vector2.zero;
            _playerVitals.pivot = Vector2.zero;
            _playerVitals.anchoredPosition = new Vector2(4f, 4f);
            _playerVitals.sizeDelta = new Vector2(178f, 36f);
            float k=PlayerPanelArt.Scale;
            var plate = PlayerPanelArt.Image(go.transform, "AnimeVitals", PlayerPanelArt.Bottom);
            Theme.Place(plate,4,PlayerPanelArt.Split*k-28.5f,174,(2048f*724/2172-PlayerPanelArt.Split)*k);
            _playerVitals.SetAsFirstSibling();
            // a canvas of its own to rise over the map with the dock (PlaceNavigationAboveMap),
            // and a raycaster, or the plate's hover and its catch of clicks went with it
            _playerVitalsCanvas = go.AddComponent<Canvas>();
            _playerVitalsRaycaster = go.AddComponent<GraphicRaycaster>();
            plate.raycastTarget = true;
            _playerName = PlayerVitalText(go.transform, "Name", 8f, true);
            _playerName.gameObject.SetActive(false);
            _playerHp=PanelBar(go.transform,"Health","health",out _playerHpFill);
            _playerStun=PanelBar(go.transform,"Stun","stun",out _playerStunFill);
            _playerHpText=PlayerVitalText(go.transform,"HealthReading",7.5f,true,TextAlignmentOptions.Center);
            Theme.Place(_playerHpText,30,2.5f,104,9);
            _playerHpText.outlineWidth=.15f;_playerHpText.outlineColor=new Color32(30,15,16,255);
            for(int i=0;i<PlayerHearts.Length;i++)
            {
                PlayerHearts[i]=PlayerPanelArt.Image(go.transform,"Life"+i,PlayerPanelArt.Heart(i));
                Theme.Place(PlayerHearts[i],4+(1588+125*i)*k,365*k-28.5f,112*k,99*k);
            }
            _playerFood=PlayerNeed(go.transform,"Food");
            _playerWater=PlayerNeed(go.transform,"Water");
            var coin=PlayerPanelArt.Image(go.transform,"Level",null);coin.color=Color.clear;
            Theme.Place(coin,8,0,19,18);
            _playerCoin=coin.rectTransform;ScaleFromMiddle(_playerCoin,1);
            _playerLevel=PlayerVitalText(coin.transform,"LevelReading",10,true,TextAlignmentOptions.Center);
            _playerLevel.color=AnimeHud.Hex("302015");
            _playerLevel.enableAutoSizing=true;_playerLevel.fontSizeMin=6;_playerLevel.fontSizeMax=10;
            Theme.Place(_playerLevel,0,0,19,18);

            _playerXpHit = PlayerXpRail("PlayerExperience", PlayerXpTicks, CharacterBurst, out _playerXp, out _playerXpFill);
            _playerSkillXpHit = PlayerXpRail("SkillExperience", PlayerSkillXpTicks, SkillBurst, out _playerSkillXp, out _playerSkillXpFill);
            _playerVitalsTip = KitTooltip.Create(_hud);

            // Fade the original, leaving its objects and event-driven values alive.
            // Food poisoning still drives the original icon's tint/animation.
            var group = vanilla.GetComponent<CanvasGroup>();
            bool added = group == null;
            if (added) { group = vanilla.gameObject.AddComponent<CanvasGroup>(); }
            HiddenButtons.Add(new HiddenHud { Group = group, Added = added, Alpha = group.alpha,
                Interactable = group.interactable, Raycasts = group.blocksRaycasts });
            group.alpha = 0f;
            group.interactable = group.blocksRaycasts = false;
            return true;
        }

        internal static void TickPlayerVitals(ManagersScript mn)
        {
            if (_hud == null || mn == null || !mn.allSet || mn.gameMN == null) { return; }
            var gm = mn.gameMN;
            CombatList.Tick(gm, mn.npcMN);
            TickAnimeEnemy(gm);
            TickPortraitSkies();
            // Same selected character as vanilla, including character switches.
            var players = gm.playerCommons;
            int selected = GameManager.selectPlayer;
            var who = players != null && selected >= 0 && selected < players.Length
                ? players[selected] : null;
            if (who == null)
            {
                if (_playerVitals != null) { _playerVitals.gameObject.SetActive(false); }
                if (_playerXpHit != null) { _playerXpHit.gameObject.SetActive(false); }
                if (_playerSkillXpHit != null) { _playerSkillXpHit.gameObject.SetActive(false); }
                if (_playerVitalsTip != null) { _playerVitalsTip.Tick(null, null); }
                return;
            }
            if (_playerVitals == null && !BuildPlayerVitals(gm)) { return; }
            bool visible = _hud.gameObject.activeInHierarchy && _vanillaVitals != null
                && _vanillaVitals.gameObject.activeInHierarchy;
            _playerVitals.gameObject.SetActive(visible);
            _playerXpHit.gameObject.SetActive(visible);
            _playerSkillXpHit.gameObject.SetActive(visible);
            if (!visible) { _playerVitalsTip.Tick(null, null); return; }

            _playerName.text = who.charaName;
            _playerLevel.text = who.level.ToString();
            float coin = PopScale(CharacterBurst, .4f);
            _playerCoin.localScale = new Vector3(coin, coin, 1f);
            _playerHpText.text = who.life.ToString("0") + " / " + who.maxLife.ToString("0");
            _playerHpFill.fillAmount=who.maxLife>0?Mathf.Clamp01((float)(who.life/who.maxLife)):0;
            _playerStunFill.fillAmount=who.maxFaint>0?Mathf.Clamp01((float)(who.faint/who.maxFaint)):0;
            for(int i=0;i<PlayerHearts.Length;i++)
                PlayerHearts[i].enabled=i<gm.lives;
            _playerFood.fillAmount = who.maxFood > 0 ? Mathf.Clamp01(who.food / who.maxFood) : 0f;
            _playerWater.fillAmount = who.maxWater > 0 ? Mathf.Clamp01(who.water / who.maxWater) : 0f;
            _playerFood.color = NeedTint(gm.foodImage.color, .097f);
            _playerWater.color = NeedTint(gm.waterImage.color, .56f);
            bool preview=Time.realtimeSinceStartup<_panelPreviewUntil;
            if(preview)
            {
                _playerHpFill.fillAmount=_panelPreview.x;_playerStunFill.fillAmount=_panelPreview.y;
                _playerFood.fillAmount=_panelPreview.z;_playerWater.fillAmount=_panelPreview.w;
                _playerHpText.text=Mathf.RoundToInt(_panelPreview.x*200)+" / 200";
                for(int i=0;i<PlayerHearts.Length;i++) PlayerHearts[i].enabled=i<_panelPreviewLives;
            }
            // the fills, which are the preview's when one runs; a maximum of nothing is no reading
            TickLowVitals(preview||who.maxLife>0?_playerHpFill.fillAmount:-1f,
                preview||who.maxFood>0?_playerFood.fillAmount:-1f,preview||who.maxWater>0?_playerWater.fillAmount:-1f);
            SkillManager sk = mn.skillMN;
            PlacePlayerExperience(who, sk);

            string tip = null;
            if (PlayerVitalHot(_playerXpHit.rectTransform))
                tip = "<b>" + Shared.Loc.Get("player.level", "Character level {0}", who.level) + "</b>\n"
                    + Shared.Loc.Get("player.xp", "XP") + "  " + who.exp.ToString("0") + " / " + who.maxExp.ToString("0")
                    + " (" + (who.maxExp > 0 ? who.exp / who.maxExp : 0).ToString("P0") + ")";
            else if (sk != null && PlayerVitalHot(_playerSkillXpHit.rectTransform))
                tip = "<b>" + Shared.Loc.Get("player.skill_level", "Skill level {0}", sk.skillLevel) + "</b>\n"
                    + Shared.Loc.Get("player.xp", "XP") + "  " + sk.exp.ToString("0") + " / " + sk.maxExp.ToString("0")
                    + " (" + (sk.maxExp > 0 ? sk.exp / sk.maxExp : 0).ToString("P0") + ")";
            else if (PlayerVitalHot(_playerVitals))
                tip = "<b>" + Shared.Loc.Get("player.name_level", "{0}  ·  Character level {1}", who.charaName, who.level) + "</b>"
                    + "\n" + Shared.Loc.Get("player.hp", "HP") + "  " + LowWord(0, who.life.ToString("0") + " / " + who.maxLife.ToString("0"))
                    + "\n" + Shared.Loc.Get("player.stun", "Stun") + "  " + who.faint.ToString("0") + " / " + who.maxFaint.ToString("0")
                    + "\n" + Shared.Loc.Get("player.lives", "Lives") + "  " + gm.lives + " / 3"
                    + "\n" + Shared.Loc.Get("player.food", "Food") + "  " + LowWord(1, who.food.ToString("0") + " / " + who.maxFood.ToString("0"))
                    + "\n" + Shared.Loc.Get("player.water", "Water") + "  " + LowWord(2, who.water.ToString("0") + " / " + who.maxWater.ToString("0"))
                    // GameManager: no healing at an empty need, and each hunger tick then costs health
                    + (who.food <= 0f || who.water <= 0f ? "\n<color=#ff6a5a>"
                        + Shared.Loc.Get("player.needs_empty", "Food or water empty: no healing, and health drains") + "</color>" : "");
            _playerVitalsTip.Tick(tip, null);
        }

        private static bool PlayerVitalHot(RectTransform rect)
        {
            if (!rect.gameObject.activeInHierarchy || EventSystem.current == null
                || !RectTransformUtility.RectangleContainsScreenPoint(rect, Input.mousePosition, null)) { return false; }
            // A map, sheet or dialog covering the HUD owns the hover.
            PlayerVitalHits.Clear();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current)
                { position = Input.mousePosition }, PlayerVitalHits);
            return PlayerVitalHits.Count > 0 && PlayerVitalHits[0].gameObject.transform.IsChildOf(rect);
        }

        // The two rails hang from the action bar's bottom edge, as wide as it: the
        // character level's first, the skill level's beneath it. ActionBar.RailRoom
        // lifts the bar by the second rail.
        private static void PlacePlayerExperience(CommonStates who, SkillManager sk)
        {
            var bar = ActionBar.Rect;
            float width = 227f;
            float x = (_hud.rect.width - width) * .5f;
            float top = .5f + 2f * PlayerRailH;
            if (bar != null && bar.gameObject.activeInHierarchy)
            {
                var left = _hud.InverseTransformPoint(bar.TransformPoint(new Vector3(bar.rect.xMin, bar.rect.yMin, 0)));
                var right = _hud.InverseTransformPoint(bar.TransformPoint(new Vector3(bar.rect.xMax, bar.rect.yMin, 0)));
                width = right.x - left.x;
                x = left.x - _hud.rect.xMin;
                top = left.y - _hud.rect.yMin;
            }
            PlaceXpRail(_playerXpHit, _playerXp, _playerXpFill, PlayerXpTicks, CharacterBurst,
                x, Mathf.Max(.5f + PlayerRailH, top - PlayerRailH), width,
                who.maxExp > 0 ? (float)(who.exp / who.maxExp) : 0f, Theme.DossierExp);
            PlaceXpRail(_playerSkillXpHit, _playerSkillXp, _playerSkillXpFill, PlayerSkillXpTicks, SkillBurst,
                x, Mathf.Max(.5f, top - 2f * PlayerRailH), width,
                sk != null && sk.maxExp > 0 ? (float)(sk.exp / sk.maxExp) : 0f, Theme.DossierSkillExp);
            PlaceLevelTags(_playerSkillXpHit.rectTransform);
        }

        private static void PlaceXpRail(Image hit, Image track, Image fill, Image[] ticks, LevelBurst burst,
            float x, float y, float width, float fraction, Color color)
        {
            var rect = hit.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, PlayerRailH);
            BurstRail(burst, ref fraction, ref color, width);
            PaintMarkerBar(track, fill, 0, 1f, width, PlayerRailH - 1f, fraction, color);
            // the marker's painted fills carry their own colour and PaintMarkerBar
            // whitens them; the rails' plain bar takes its blue or green here
            fill.color = color;
            for (int i = 0; i < ticks.Length; i++)
                Theme.Place(ticks[i], .6f + (width - 1.2f) * (i + 1) / 10f, .6f, .5f, 1.3f);
        }
    }
}
