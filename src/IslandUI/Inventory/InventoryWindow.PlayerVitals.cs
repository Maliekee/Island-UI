using IslandUI.UI;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace IslandUI
{
    // Selected concept A: the target indicator's enamel and rulers,
    // worn by the player. EXP has two rails beneath the action bar: the character
    // level's blue right under it, the skill level's green under that.
    internal static partial class InventoryWindow
    {
        private static RectTransform _playerVitals, _vanillaVitals;
        private static Image _playerHp, _playerHpFill, _playerStun, _playerStunFill;
        private static Image _playerXp, _playerXpFill, _playerXpHit;
        private static Image _playerSkillXp, _playerSkillXpFill, _playerSkillXpHit;
        private static SheetRow _playerHpRuler, _playerStunRuler;
        private static TextMeshProUGUI _playerName, _playerLevel, _playerHpText;
        private static RectTransform _playerCoin;
        private static Image _playerFood, _playerWater;
        private static readonly Image[] PlayerHearts = new Image[3];
        private static readonly Image[] PlayerXpTicks = new Image[9];
        private static readonly Image[] PlayerSkillXpTicks = new Image[9];
        private static readonly List<RaycastResult> PlayerVitalHits = new List<RaycastResult>();
        private static KitTooltip _playerVitalsTip;
        private const float PlayerBarW = 104f;
        private const float PlayerRailH = 3.5f;

        private static void ResetPlayerVitals()
        {
            // The owned children die with _hud; ResetHud restores the vanilla
            // CanvasGroup before destroying them, including on a hot reload.
            _playerVitals = _vanillaVitals = null;
            _playerHp = _playerHpFill = _playerStun = _playerStunFill = null;
            _playerXp = _playerXpFill = _playerXpHit = null;
            _playerSkillXp = _playerSkillXpFill = _playerSkillXpHit = null;
            _playerHpRuler = _playerStunRuler = null;
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

        // Both needs stand PlayerNeedH tall; the box takes the sprite's own aspect so
        // preserveAspect has nothing to shrink (vanilla: food 24x24, water 17x24).
        private const float PlayerNeedH = 17.5f;

        private static Image PlayerNeed(Transform parent, string name, Image source, float x, out float width)
        {
            var rect = source.sprite != null ? source.sprite.rect : new Rect(0, 0, 1, 1);
            width = rect.height > 0 ? PlayerNeedH * rect.width / rect.height : PlayerNeedH;
            var back = Theme.KitImage(parent, name, "kit_emblem_person", 0, StripTrack);
            back.sprite = source.sprite;
            back.preserveAspect = true;
            Theme.Place(back, x, 13.25f, width, PlayerNeedH);
            var fill = Theme.KitImage(back.transform, "Fill", "kit_emblem_person", 0, source.color);
            fill.sprite = source.sprite;
            fill.preserveAspect = true;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Vertical;
            fill.fillOrigin = 0;
            Theme.Place(fill, 0, 0, width, PlayerNeedH);
            return fill;
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
            var plate = Theme.KitImage(go.transform, "EnamelPlate", "kit_emblem_status_header", 20, Color.white);
            Theme.Place(plate, 11f, 0f, 167f, 36f);
            plate.raycastTarget = true;
            _playerName = PlayerVitalText(go.transform, "Name", 8f, true);
            Theme.Place(_playerName, 36f, 2.5f, 96f, 10f);
            _playerHpText = PlayerVitalText(go.transform, "HealthReading", 5f, false, TextAlignmentOptions.MidlineRight);
            Theme.Place(_playerHpText, 49f, 13f, 88f, 6f);
            var hpLabel = PlayerVitalText(go.transform, "HealthLabel", 4.3f);
            hpLabel.text = "HP";
            hpLabel.color = MarkerMuted;
            Theme.Place(hpLabel, 36f, 13f, 11f, 6f);
            _playerHp = PlayerVitalTrack(go.transform, "Health", out _playerHpFill);
            _playerStun = PlayerVitalTrack(go.transform, "Stun", out _playerStunFill);
            _playerHpRuler = new SheetRow { Go = _playerHp.gameObject };
            _playerStunRuler = new SheetRow { Go = _playerStun.gameObject };
            for (int i = 0; i < PlayerHearts.Length; i++)
            {
                PlayerHearts[i] = Theme.KitImage(go.transform, "Life" + i, "kit_heart", 0, MarkerCream);
                Theme.Place(PlayerHearts[i], 149f + i * 6.5f, 5f, 5.5f, 5.5f);
            }
            var divider = Theme.KitImage(go.transform, "NeedsDivider", "kit_hairline", 0, MarkerMuted);
            Theme.Place(divider, 139.5f, 16f, .4f, 16f);
            _playerFood = PlayerNeed(go.transform, "Food", gm.foodImage, 141.5f, out float foodW);
            _playerWater = PlayerNeed(go.transform, "Water", gm.waterImage, 141.5f + foodW + .75f, out _);

            var rim = Theme.KitImage(go.transform, "PlayerMedallion", "kit_emblem_rim", 0, Color.white);
            rim.raycastTarget = true;
            Theme.Place(rim, 0, 3f, 33f, 33f);
            var enamel = Theme.KitImage(rim.transform, "Enamel", "kit_emblem_enamel", 0,
                Theme.ParseColor("#43699C", Color.blue));
            Theme.Place(enamel, 4.3f, 4.3f, 24.4f, 24.4f);
            var person = Theme.KitImage(rim.transform, "Person", "kit_emblem_person", 0, Color.white);
            Theme.Place(person, 7f, 7f, 19f, 19f);
            var coin = Theme.KitImage(rim.transform, "LevelCoin", "kit_emblem_coin", 0, Color.white);
            Theme.Place(coin, 22f, 23f, 12.5f, 12.5f);
            _playerCoin = coin.rectTransform;
            ScaleFromMiddle(_playerCoin, 1f);
            _playerLevel = PlayerVitalText(coin.transform, "Level", 6.6f, true, TextAlignmentOptions.MidlineGeoAligned);
            _playerLevel.color = EmblemInk;
            _playerLevel.enableAutoSizing = true;
            _playerLevel.fontSizeMin = 4f;
            _playerLevel.fontSizeMax = 6.6f;
            Theme.Place(_playerLevel, .6f, 0, 11.3f, 12.5f);

            _playerXpHit = PlayerXpRail("PlayerExperience", PlayerXpTicks, CharacterBurst, out _playerXp, out _playerXpFill);
            _playerSkillXpHit = PlayerXpRail("SkillExperience", PlayerSkillXpTicks, SkillBurst, out _playerSkillXp, out _playerSkillXpFill);
            _playerVitalsTip = KitTooltip.Create(_hud, true);

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
            PaintMarkerBar(_playerHp, _playerHpFill, 36f, 19.5f, PlayerBarW, 5f,
                who.maxLife > 0 ? (float)(who.life / who.maxLife) : 0f, LifeTint);
            PaintMarkerBar(_playerStun, _playerStunFill, 36f, 27f, PlayerBarW, 3f,
                who.maxFaint > 0 ? (float)(who.faint / who.maxFaint) : 0f, StunTint);
            PaintMarkerRuler(_playerHpRuler, (float)who.maxLife, PlayerBarW, 5f);
            PaintMarkerRuler(_playerStunRuler, (float)who.maxFaint, PlayerBarW, 3f);
            for (int i = 0; i < PlayerHearts.Length; i++)
                PlayerHearts[i].sprite = Theme.Kit(i < gm.lives ? "kit_heart" : "kit_heart_hollow", 0);
            _playerFood.fillAmount = who.maxFood > 0 ? Mathf.Clamp01(who.food / who.maxFood) : 0f;
            _playerWater.fillAmount = who.maxWater > 0 ? Mathf.Clamp01(who.water / who.maxWater) : 0f;
            _playerFood.color = gm.foodImage.color;
            _playerWater.color = gm.waterImage.color;
            SkillManager sk = mn.skillMN;
            PlacePlayerExperience(who, sk);

            string tip = null;
            if (PlayerVitalHot(_playerXpHit.rectTransform))
                tip = "Character level " + who.level + "\nXP  " + who.exp.ToString("0") + " / " + who.maxExp.ToString("0")
                    + " (" + (who.maxExp > 0 ? who.exp / who.maxExp : 0).ToString("P0") + ")";
            else if (sk != null && PlayerVitalHot(_playerSkillXpHit.rectTransform))
                tip = "Skill level " + sk.skillLevel + "\nXP  " + sk.exp.ToString("0") + " / " + sk.maxExp.ToString("0")
                    + " (" + (sk.maxExp > 0 ? sk.exp / sk.maxExp : 0).ToString("P0") + ")";
            else if (PlayerVitalHot(_playerVitals))
                tip = who.charaName + "  ·  Character level " + who.level
                    + "\nHP  " + who.life.ToString("0") + " / " + who.maxLife.ToString("0")
                    + "\nStun  " + who.faint.ToString("0") + " / " + who.maxFaint.ToString("0")
                    + "\nLives  " + gm.lives + " / 3"
                    + "\nFood  " + who.food.ToString("0") + " / " + who.maxFood.ToString("0")
                    + "\nWater  " + who.water.ToString("0") + " / " + who.maxWater.ToString("0");
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
            for (int i = 0; i < ticks.Length; i++)
                Theme.Place(ticks[i], .6f + (width - 1.2f) * (i + 1) / 10f, .6f, .5f, 1.3f);
        }
    }
}
