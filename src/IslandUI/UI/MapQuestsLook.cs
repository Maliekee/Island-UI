using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI.UI
{
    // THE MAP AND THE QUESTS IN THE HUD'S CLOTHES (Forgejo #56, look B).
    // Both stay the game's own screens - its layout,
    // right-drag panning, the wheel's zoom, the brightness slider, the quest rows - and
    // only wear the HUD's look:
    //   * laid over them, ours: a cocoa rail around the screen, a title tab top left
    //     (the HUD's map or scroll icon and the screen's name) and the HUD's chamfered
    //     close top right, which closes through the game's own toggle;
    //   * the game's pieces re-tinted from the table below: the dims cocoa instead of
    //     black at the game's strength, the slider on the HUD's track with a brass coin,
    //     the quest tags in the HUD's enamels, the rows' title bars brown and their
    //     result bars warm, their text paper.
    // The map's fog stays the game's black. Its sprite is white, but the game draws it
    // with Spine's pixel-lit sprite material, which comes out black whatever it is told
    // (the Image's colour, the material's, emission, Spine's unlit variant, all tried);
    // the UI's default material does take a colour, but drops the material's alpha clip
    // at 0.9, and the fog's soft alpha then half-showed unexplored land at every edge.
    // Applied as a screen opens: postfixes on its toggle, the only place vanilla shows
    // either (MapQuestExclusion). Pieces come from UIManager's own fields where it has
    // them, else by the names the game itself looks them up by; a missing one keeps
    // the game's look. Every colour, sprite and rect replaced is remembered, so
    // Restore puts the game's back (the hot reload's teardown). A screen built again -
    // a scene load - takes our overlay with it and is dressed afresh on its next open.
    [HarmonyPatch(typeof(UIManager))]
    internal static class MapQuestsLook
    {
        // ---- the table --------------------------------------------------------------
        private static readonly Color Cocoa = AnimeHud.Hex("49392F");          // the dims, at the game's alpha
        private static readonly Color Paper = AnimeHud.Hex("F6ECD8");          // the slider's fill, row titles
        private static readonly Color ResultText = AnimeHud.Hex("ECE0C8");
        private static readonly Color Ink = new Color32(26, 16, 12, 255);
        private static readonly Color[] Tags =                                 // Yona, Common, Man:
        {
            AnimeHud.Hex("C44054"),                                            // the rose tile
            AnimeHud.Hex("2C8860"),                                            // jade
            AnimeHud.Hex("2868B0"),                                            // cerulean
        };
        // Image.color over the game's sprites: ui_frame is pale beige, so its brown is
        // the action tile's (70 50 3A) divided by the frame's own colour
        private static readonly Color TitleBar = new Color(.455f, .372f, .319f, 1f);
        private static readonly Color ResultBar = new Color(.36f, .27f, .2f, 1f);

        // ---- the overlay's measures, in canvas units (3.2 px each at 1440p) ----------
        private const float Inset = 2f;            // the rail from the screen's edges
        private const float TabX = 11f, TabH = 20f;
        private const float CloseSize = 17.5f, CloseTop = 1.25f, CloseRight = 2f;
        private static readonly Color RimHot = new Color(1f, 1f, 1f, .5f), Pressed = new Color(.84f, .82f, .8f, 1f);

        private sealed class Close
        {
            public Image Hit, Face, Cross, Rim;
        }

        private sealed class Tab
        {
            public Image Plate, Icon;
            public TextMeshProUGUI Word;
        }

        private struct SpriteWas
        {
            public Sprite Sprite;
            public Image.Type Type;
            public bool PreserveAspect, FillCenter;
            public float Multiplier;
        }

        private static readonly Dictionary<Graphic, Color> ColourWas = new Dictionary<Graphic, Color>();
        private static readonly Dictionary<Image, SpriteWas> SpritesWas = new Dictionary<Image, SpriteWas>();
        private static readonly Dictionary<RectTransform, Vector4> OffsetsWas = new Dictionary<RectTransform, Vector4>();
        private static RectTransform _mapOver, _questUnder, _questOver;
        private static Close _mapClose, _questClose;
        private static Tab _mapTab, _questTab;

        [HarmonyPostfix]
        [HarmonyPatch(nameof(UIManager.MapView))]
        private static void AfterMap(UIManager __instance)
        {
            if (__instance.mapCanvas != null && __instance.mapCanvas.activeSelf)
                DressMap(__instance);
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(UIManager.QuestDiaryVisible))]
        private static void AfterQuests(UIManager __instance)
        {
            if (__instance.questDiaryPanel != null && __instance.questDiaryPanel.activeSelf)
                DressQuests(__instance);
        }

        // ---- the map ----------------------------------------------------------------

        private static void DressMap(UIManager ui)
        {
            Transform canvas = ui.mapCanvas.transform;
            // MapGrp: the band's dim, and the Mask the island pans inside (UIManager.Start
            // finds it by name)
            var band = canvas.Find("MapGrp") as RectTransform;
            if (band != null)
                Dim(band.GetComponent<Image>());
            DressSlider(ui.mapBrightSlider);
            if (_mapOver == null || _mapOver.parent != canvas)
            {
                _mapOver = Layer(canvas, "IslandUI_MapLook");
                if (band != null)
                {
                    // over the band and under the slider, on the band's own rect
                    _mapOver.anchorMin = band.anchorMin; _mapOver.anchorMax = band.anchorMax;
                    _mapOver.pivot = band.pivot;
                    _mapOver.offsetMin = band.offsetMin; _mapOver.offsetMax = band.offsetMax;
                    _mapOver.SetSiblingIndex(band.GetSiblingIndex() + 1);
                }
                Rail(_mapOver);
                _mapTab = MakeTab(_mapOver, "map");
                _mapClose = MakeClose(_mapOver, () => ui.MapView());
            }
            PaintTab(_mapTab, Shared.Loc.Get("hud.chip.map", "Map"));
        }

        // The brightness slider on the HUD's track: the track piece behind, a paper fill
        // inside its rim, the brass coin for a knob. Its own Images, through the Slider.
        private static void DressSlider(Slider slider)
        {
            if (slider == null)
                return;
            Image fill = slider.fillRect != null ? slider.fillRect.GetComponent<Image>() : null;
            Image knob = slider.handleRect != null ? slider.handleRect.GetComponent<Image>() : null;
            foreach (var image in slider.GetComponentsInChildren<Image>(true))
            {
                if (image == fill || image == knob)
                    continue;
                Skin(image, AnimePieces.Get("track", new Vector4(20, 20, 20, 20)), 6.4f);
                Tint(image, Color.white);
                break;
            }
            if (fill != null)
            {
                Skin(fill, AnimeHud.Shape("mask"), 13f);
                Tint(fill, Paper);
                Offsets(fill.rectTransform, 1.2f);
            }
            if (knob != null)
            {
                Skin(knob, AnimePieces.Get("coin"), 1f);
                knob.preserveAspect = true;
                Tint(knob, Color.white);
            }
        }

        // ---- the quests ---------------------------------------------------------------

        private static readonly string[] TagNames = { "yonaNameBack", "commonBack", "manNameBack" };
        private static readonly string[] Columns = { "Yona", "Common", "Man" };

        private static void DressQuests(UIManager ui)
        {
            Transform diary = ui.questDiaryPanel.transform;
            Dim(diary.GetComponent<Image>());
            for (int i = 0; i < 3; i++)
            {
                Transform tag = diary.Find(TagNames[i]);
                if (tag != null)
                    Tint(tag.GetComponent<Image>(), Tags[i]);
                // the rows StoryManager instantiates into each column at load: a title
                // bar, a result bar (both "Back", in that order), and the two texts it
                // writes by name
                Transform column = diary.Find(Columns[i]);
                if (column == null)
                    continue;
                foreach (Transform row in column)
                    DressRow(row);
            }
            if (_questUnder == null || _questUnder.parent != diary)
            {
                // the rail behind the columns, so the tags hang over it; tab and close on top
                _questUnder = Layer(diary, "IslandUI_QuestLookRail");
                _questUnder.SetAsFirstSibling();
                Rail(_questUnder);
                _questOver = Layer(diary, "IslandUI_QuestLook");
                _questTab = MakeTab(_questOver, "quests");
                _questClose = MakeClose(_questOver, () => ui.QuestDiaryVisible());
            }
            _questOver.SetAsLastSibling();
            PaintTab(_questTab, Shared.Loc.Get("hud.chip.quests", "Quests"));
        }

        private static void DressRow(Transform row)
        {
            int bars = 0;
            foreach (Transform child in row)
            {
                var image = child.GetComponent<Image>();
                if (image == null || bars > 1)
                    continue;
                Tint(image, bars == 0 ? TitleBar : ResultBar);
                bars++;
            }
            Transform name = row.Find("questNameText"), result = row.Find("resultText");
            if (name != null)
                Tint(name.GetComponent<TMP_Text>(), Paper);
            if (result != null)
                Tint(result.GetComponent<TMP_Text>(), ResultText);
        }

        // ---- the overlay's pieces -------------------------------------------------------

        private static RectTransform Layer(Transform parent, string name)
        {
            var rect = (RectTransform)Owned.New(name).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static void Rail(RectTransform layer)
        {
            var rail = AnimePieces.Image(layer, "Rail", "screen_rail");
            AnimePieces.Sliced(rail, "screen_rail", new Vector4(64, 64, 64, 64));
            rail.pixelsPerUnitMultiplier = 6.4f;     // the piece is drawn at 2x a 1440p screen
            rail.fillCenter = false;
            var rect = rail.rectTransform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(Inset, Inset * .5f); rect.offsetMax = new Vector2(-Inset, -Inset);
        }

        private static Tab MakeTab(RectTransform layer, string icon)
        {
            var tab = new Tab();
            var sprig = AnimeHud.Art(layer, "Leaves", "leaf");
            Theme.Place(sprig, TabX - 7.5f, TabH - 8f, 15f, 10.5f);
            sprig.rectTransform.localEulerAngles = new Vector3(0, 0, 28f);
            tab.Plate = AnimeHud.Image(layer, "TitleTab", "rail");
            tab.Plate.pixelsPerUnitMultiplier = 12f;
            tab.Icon = AnimeHud.Art(tab.Plate.transform, "Icon", icon);
            Theme.Place(tab.Icon, 5f, 2.2f, 15.6f, 15.6f);
            tab.Word = Theme.MakeText(tab.Plate.transform, "Title", 10.6f, Paper, TextAlignmentOptions.MidlineLeft);
            TMP_FontAsset serif = InventoryWindow.HudStrongFont;
            if (serif != null)
            {
                tab.Word.font = serif;
                tab.Word.fontStyle = FontStyles.Normal;
            }
            tab.Word.enableWordWrapping = false;
            tab.Word.overflowMode = TextOverflowModes.Overflow;
            tab.Word.outlineWidth = .15f; tab.Word.outlineColor = Ink;
            return tab;
        }

        // The title follows the language: its width is measured each time a screen opens.
        private static void PaintTab(Tab tab, string title)
        {
            if (tab == null || tab.Word == null)
                return;
            if (tab.Word.text != title)
                tab.Word.text = title;
            float word = tab.Word.GetPreferredValues(title).x;
            Theme.Place(tab.Word, 24.4f, 0f, word + 2f, TabH);
            Theme.Place(tab.Plate, TabX, 0f, 24.4f + word + 7.5f, TabH);
        }

        private static Close MakeClose(RectTransform layer, System.Action close)
        {
            var c = new Close();
            c.Hit = AnimeHud.Image(layer, "Close", "mask");
            c.Hit.color = Color.clear;
            c.Hit.raycastTarget = true;
            var rect = c.Hit.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(CloseSize, CloseSize);
            rect.anchoredPosition = new Vector2(-CloseRight, -CloseTop);
            c.Face = AnimeHud.Image(c.Hit.transform, "Face", "tile");
            c.Face.pixelsPerUnitMultiplier = 12.5f;
            c.Cross = AnimePieces.Image(c.Face.transform, "Cross", "screen_close_x");
            float x = CloseSize * .6f;
            Theme.Place(c.Cross, (CloseSize - x) * .5f, (CloseSize - x) * .5f, x, x);
            c.Rim = AnimePieces.Image(c.Hit.transform, "Rim", "rim_on");
            AnimePieces.Sliced(c.Rim, "rim_on", new Vector4(24, 24, 24, 24));
            Theme.Place(c.Rim, -.4f, -.4f, CloseSize + .8f, CloseSize + .8f);
            c.Rim.enabled = false;
            var button = c.Hit.gameObject.AddComponent<Button>();
            button.targetGraphic = c.Hit;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => close());
            PaintClose(c);
            return c;
        }

        // The HUD tiles' states (AnimeCell): the pointer over it is the faint "On" rim,
        // a press sinks the tile and lowers its light. Polled, as the HUD's are.
        internal static void Tick()
        {
            PaintClose(_mapClose);
            PaintClose(_questClose);
        }

        private static void PaintClose(Close c)
        {
            if (c == null || c.Hit == null || !c.Hit.gameObject.activeInHierarchy)
                return;
            bool hot = RectTransformUtility.RectangleContainsScreenPoint(c.Hit.rectTransform, Input.mousePosition, null);
            bool down = hot && Input.GetMouseButton(0);
            Theme.Place(c.Face, 0f, down ? .25f : 0f, CloseSize, CloseSize);
            c.Face.color = down ? Pressed : Color.white;
            c.Rim.enabled = hot && !down;
            c.Rim.color = RimHot;
        }

        // ---- remembering the game's look --------------------------------------------------

        private static void Dim(Image dim)
        {
            if (dim == null)
                return;
            float strength = ColourWas.TryGetValue(dim, out Color was) ? was.a : dim.color.a;
            Tint(dim, new Color(Cocoa.r, Cocoa.g, Cocoa.b, strength));
        }

        private static void Tint(Graphic g, Color c)
        {
            if (g == null)
                return;
            if (!ColourWas.ContainsKey(g))
                ColourWas[g] = g.color;
            if (g.color != c)
                g.color = c;
        }

        private static void Skin(Image image, Sprite sprite, float multiplier)
        {
            if (sprite == null)
                return;
            if (!SpritesWas.ContainsKey(image))
                SpritesWas[image] = new SpriteWas { Sprite = image.sprite, Type = image.type, PreserveAspect = image.preserveAspect,
                    FillCenter = image.fillCenter, Multiplier = image.pixelsPerUnitMultiplier };
            image.sprite = sprite;
            image.type = sprite.border == Vector4.zero ? Image.Type.Simple : Image.Type.Sliced;
            image.fillCenter = true;
            image.pixelsPerUnitMultiplier = multiplier;
        }

        // pulls a rect in from its edges, once
        private static void Offsets(RectTransform rect, float inset)
        {
            if (rect == null || OffsetsWas.ContainsKey(rect))
                return;
            OffsetsWas[rect] = new Vector4(rect.offsetMin.x, rect.offsetMin.y, rect.offsetMax.x, rect.offsetMax.y);
            rect.offsetMin += new Vector2(inset, inset);
            rect.offsetMax -= new Vector2(inset, inset);
        }

        // The game's colours, sprites and rects back, and our overlays gone.
        internal static void Restore()
        {
            foreach (var kv in ColourWas)
                if (kv.Key != null)
                    kv.Key.color = kv.Value;
            foreach (var kv in SpritesWas)
            {
                if (kv.Key == null)
                    continue;
                kv.Key.sprite = kv.Value.Sprite; kv.Key.type = kv.Value.Type;
                kv.Key.preserveAspect = kv.Value.PreserveAspect; kv.Key.fillCenter = kv.Value.FillCenter;
                kv.Key.pixelsPerUnitMultiplier = kv.Value.Multiplier;
            }
            foreach (var kv in OffsetsWas)
            {
                if (kv.Key == null)
                    continue;
                kv.Key.offsetMin = new Vector2(kv.Value.x, kv.Value.y);
                kv.Key.offsetMax = new Vector2(kv.Value.z, kv.Value.w);
            }
            ColourWas.Clear(); SpritesWas.Clear(); OffsetsWas.Clear();
            foreach (var layer in new[] { _mapOver, _questUnder, _questOver })
                if (layer != null)
                    Object.Destroy(layer.gameObject);
            _mapOver = _questUnder = _questOver = null;
            _mapClose = _questClose = null;
            _mapTab = _questTab = null;
        }
    }
}
