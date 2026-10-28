using System.Collections.Generic;
using IslandUI.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace IslandUI
{
    internal static partial class InventoryWindow
    {
        private static RectTransform _hud;
        private static KitTooltip _hudTip;
        private static RectTransform _collectionsDock;
        private static RectTransform _generalDock;
        private static RectTransform _playerDock;
        private static Canvas _generalDockCanvas, _playerVitalsCanvas;
        private static GraphicRaycaster _playerVitalsRaycaster;
        private sealed class HudEntry
        {
            public string Name; public Image Face, Glyph, Mark; public Button Button; public AnimeCell Cell;
        }
        private static readonly List<HudEntry> HudEntries = new List<HudEntry>();
        private sealed class HiddenHud
        {
            public CanvasGroup Group; public bool Added; public float Alpha; public bool Interactable, Raycasts;
        }
        private static readonly List<HiddenHud> HiddenButtons = new List<HiddenHud>();
        internal static void ResetHud()
        {
            ResetPlayerVitals();
            foreach (var h in HiddenButtons)
            if (h.Group != null)
            {
                if (h.Added)
                    Object.Destroy(h.Group);
                else
                {
                    h.Group.alpha = h.Alpha;
                    h.Group.interactable = h.Interactable;
                    h.Group.blocksRaycasts = h.Raycasts;
                }
            }
            HiddenButtons.Clear();
            HudEntries.Clear();
            if (_hud != null)
                Object.Destroy(_hud.gameObject);
            _hud = null;
            _hudTip = null;
            ResetLogFeed();
            _generalDock = null;
            _playerDock = null;
            _generalDockCanvas = null;
            AnimeHud.Reset();
            PlayerPanelArt.Reset();
            AnimePieces.Reset();
            _dockChevron = null;
        }
        internal static void TickHud(ManagersScript mn)
        {
            if (mn == null || !mn.allSet || mn.uiMN == null || mn.gameMN == null || mn.gameMN.pMove == null)
                return;
            _mn = mn;
            if (_hud == null)
            {
                if (!Theme.Ensure(mn, mn.inventory))
                    return;
                var canvas = mn.uiMN.itemDescPanel.GetComponentInParent<Canvas>().rootCanvas;
                var go = Owned.New("IslandUI_Hud");
                go.transform.SetParent(canvas.transform, false);
                _hud = (RectTransform)go.transform;
                CanvasIsolation.Isolate(go);
                _hud.anchorMin = Vector2.zero;
                _hud.anchorMax = Vector2.one;
                _hud.offsetMin = _hud.offsetMax = Vector2.zero;
                var left = MakeDock("General", false, 5);
                // The map has a separate root canvas and a full-screen raycast
                // catcher. This dock must remain a usable way back out of it.
                _generalDockCanvas = left.gameObject.AddComponent<Canvas>();
                left.gameObject.AddComponent<GraphicRaycaster>();
                var right = MakeDock("Player", true, 4);
                _generalDock = left;
                _playerDock = right;
                AddHud(left, "craft", "Crafting", 0);
                AddHud(left, "vicinity", "Vicinity", 1);
                AddHud(left, "roster", "Roster", 2);
                AddHud(left, "map", "Map", 3);
                AddHud(left, "quests", "Quests", 4);
                // the tool belt's chip first, beside the board it slides out
                AddHud(right, "tools", "Tool belt", 0);
                AddHud(right, "details", "Your character", 1);
                AddHud(right, "bag", "Your bag", 2);
                AddHud(right, "settings", "Settings", 3);
                MakeBoard(right);
                // Collections remains a secondary entry of the crafting window.
                _collectionsDock = MakeDock("Collections", false, 1);
                _collectionsDock.pivot = new Vector2(1, 1);
                _collectionsDock.sizeDelta = new Vector2(55, 12);
                _collectionsDock.GetComponent<Image>().enabled = false;
                var link = Theme.MakeKitText(_collectionsDock, "Collections", 7, Theme.KitCream, TMPro.TextAlignmentOptions.MidlineRight);
                Theme.Place(link, 0, 0, 55, 12);
                UI.LocLabels.Set(link, () => Shared.Loc.Get("hud.collections_link", "Collections") + " >");
                Theme.FitLanguage(link, 7);
                link.raycastTarget = true;
                var collectionButton = link.gameObject.AddComponent<Button>();
                collectionButton.targetGraphic = link;
                collectionButton.onClick.AddListener(() => HudPress("collections"));
                // ...and the log with its +/- button: the feed draws its lines
                // (InventoryWindow.LogFeed.cs), the veiled panel keeps its rect
                string[] names = { "BackpackButton", "EquipButton", "StatusButton", "MapButton", "QuestButton", "ToggleRunButton", "SettingButton", "CraftButton", "CollectionButton", "FriendButton", "LogMask", "LogVisibleButton" };
                // Only the HUD's own row: MainCanvas's direct children and MenuPanel's.
                // A name match anywhere below caught SettingMenu/SettingButton - the
                // Option entry - and with it the Island UI entry cloned from it.
                foreach (var tr in canvas.GetComponentsInChildren<RectTransform>(true))
                if (System.Array.IndexOf(names, tr.name) >= 0
                    && (tr.parent == canvas.transform || (tr.parent.name == "MenuPanel" && tr.parent.parent == canvas.transform)))
                {
                    var c = tr.GetComponent<CanvasGroup>();
                    bool added = c == null;
                    if (added)
                        c = tr.gameObject.AddComponent<CanvasGroup>();
                    HiddenButtons.Add(new HiddenHud { Group = c, Added = added, Alpha = c.alpha, Interactable = c.interactable, Raycasts = c.blocksRaycasts });
                    c.alpha = 0;
                    c.interactable = false;
                    c.blocksRaycasts = false;
                }
                _hudTip = KitTooltip.Create(_hud);
            }
            // The HUD goes when vanilla fades its own - that is what a cutscene does
            // (UIManager.MainCanvasView). It used to go for the whole of a story
            // event too, and a boss fight runs INSIDE one (StoryManager.EventStart
            // around BossBattleCheck), so you fought with no health on screen.
            // During an event vanilla only blocks its menu, backpack
            // and map keys (GameManager.Update), so only the doors to those go:
            // the two chip docks. Your vitals and the tool belt stay.
            _hud.gameObject.SetActive(mn.uiMN.mainCanvasGrp.alpha > 0);
            bool doors = !GameManager.nowEvent;
            if (_generalDock.gameObject.activeSelf != doors)
                _generalDock.gameObject.SetActive(doors);
            if (_playerDock.gameObject.activeSelf != doors)
                _playerDock.gameObject.SetActive(doors);
            PlaceNavigationAboveMap(mn.uiMN.mapCanvas);
            PlaceQuestsAboveWindows(mn.uiMN.questDiaryPanel);
            var craftFrame = BrowserShell.NavigationFrame;
            bool collections = mn.uiMN.craftPanel.activeSelf && craftFrame != null && craftFrame.gameObject.activeInHierarchy;
            _collectionsDock.gameObject.SetActive(collections);
            if (collections)
                _collectionsDock.position = craftFrame.TransformPoint(new Vector3(craftFrame.rect.xMax - 12, craftFrame.rect.yMin - 2, 0));
            string boardTip = TickBoard();
            string tip = null;
            foreach (var e in HudEntries)
            {
                bool open = HudOpen(e.Name);
                bool hot = e.Face.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(e.Face.rectTransform, Input.mousePosition, null);
                bool window = e.Name == "craft" || e.Name == "vicinity" || e.Name == "roster" || e.Name == "bag" || e.Name == "details";
                if(e.Face.transform.parent==_generalDock)
                    e.Cell.PaintPanel(PlayerPanelArt.Button(HudEntries.IndexOf(e)),open,hot,hot && Input.GetMouseButton(0));
                else if (e.Face.transform.parent == _playerDock)
                    e.Cell.PaintPanel(AnimePieces.Get("tile_" + e.Name), open, hot, hot && Input.GetMouseButton(0));
                else
                    e.Cell.Paint(e.Name, null, open && window, false, false, true, hot, hot && Input.GetMouseButton(0), false, open && !window);
                if (hot)
                    tip = e.Name == "roster" ? RosterChipTip(HudLabel(e.Name)) : HudLabel(e.Name);
            }
            // the chevron points the way the tool belt's board slides: out, then back
            if (_dockChevron != null) _dockChevron.rectTransform.localScale = new Vector3(BoardOpen ? -1 : 1, 1, 1);
            TickDockPoints(HudEntries.Find(e => e.Name == "details"));
            TickHudMood(HudEntries.Find(e => e.Name == "roster"));
            _hudTip.Tick(tip ?? boardTip, null);
        }

        private static void PlaceNavigationAboveMap(GameObject map)
        {
            var mapCanvas = map != null ? map.GetComponent<Canvas>() : null;
            bool aboveMap = mapCanvas != null && map.activeInHierarchy;
            _generalDock.GetComponent<Image>().enabled = true;
            // the target dock stays under the map, and the map stops short of the
            // screen's foot: its captions showed below it as a strip of loose words
            var unitCanvas = _unit != null ? _unit.GetComponent<Canvas>() : null;
            if (unitCanvas != null && unitCanvas.enabled == aboveMap) { unitCanvas.enabled = !aboveMap; }
            // The vitals, the plate's lower half, go up with the dock (Forgejo #122):
            // the map's frame runs along the map's foot, across the plate's top edge,
            // and the HUD draws over it as it does over the quests' frame, which sits
            // in MainCanvas under the HUD. The dock stays over the vitals, as it is
            // in the HUD's own order. Only drawn over it: the map still owns their
            // hover and clicks, so their tip does not open under the map
            LiftOverMap(_playerVitalsCanvas, aboveMap ? mapCanvas : null, 1);
            LiftOverMap(_generalDockCanvas, aboveMap ? mapCanvas : null, 2);
            if (_playerVitalsRaycaster != null && _playerVitalsRaycaster.enabled == aboveMap)
                _playerVitalsRaycaster.enabled = !aboveMap;
        }

        // The map is a root canvas of its own at MainCanvas's order, drawn after it,
        // so a piece of the HUD shows over it only on a canvas sorted above it.
        private static void LiftOverMap(Canvas canvas, Canvas map, int above)
        {
            if (canvas == null)
                return;
            canvas.overrideSorting = map != null;
            if (map == null)
                return;
            canvas.sortingLayerID = map.sortingLayerID;
            canvas.sortingOrder = map.sortingOrder + above;
        }

        // The quest diary is an overlay: over every window, under only the dock
        // that toggles it. It shares MainCanvas with our windows, which are its
        // later siblings and lift themselves with SetAsLastSibling as they open,
        // so the order is re-asserted each tick while it is open - and only moved
        // when wrong, since a sibling move rebuilds the canvas.
        private static void PlaceQuestsAboveWindows(GameObject quests)
        {
            if (quests == null || !quests.activeSelf || quests.transform.parent != _hud.parent)
                return;
            int last = _hud.parent.childCount - 1;
            if (_hud.GetSiblingIndex() == last && quests.transform.GetSiblingIndex() == last - 1)
                return;
            quests.transform.SetAsLastSibling();
            _hud.SetAsLastSibling();
        }

        // Where "into your pack" is on screen when no cell of yours shows the item:
        // our bag chip, or vanilla's button when our HUD is not drawn. Vanilla's
        // stays ACTIVE under our HUD, only faded by the CanvasGroup above, so
        // aiming at it flew every closed-bag pickup to an empty spot
        internal static RectTransform BagSpot()
        {
            var entry = HudEntries.Find(e => e.Name == "bag");
            if (entry != null && entry.Face != null && entry.Face.gameObject.activeInHierarchy)
                return entry.Face.rectTransform;
            GameObject vanilla = Theme.BagButton;
            if (vanilla == null || !vanilla.activeInHierarchy)
                return null;
            var faded = vanilla.GetComponent<CanvasGroup>();
            return faded != null && faded.alpha <= 0f ? null : (RectTransform)vanilla.transform;
        }

        // Harness door: raise or lower vanilla's story-event flag, to look at the
        // HUD as a boss fight shows it. Refuses to lower a flag it did not raise.
        private static bool _eventProbeRaised;

        internal static string EventFlagProbe(int on)
        {
            if (on != 0 && !GameManager.nowEvent)
            {
                GameManager.nowEvent = true;
                _eventProbeRaised = true;
            }
            else if (on == 0 && _eventProbeRaised)
            {
                GameManager.nowEvent = false;
                _eventProbeRaised = false;
            }
            return "nowEvent=" + GameManager.nowEvent + (_eventProbeRaised ? " (raised by the probe)" : "");
        }

        // Harness check through the same raycast ordering that a real pointer
        // uses. Direct Button events alone cannot detect an overlay blocking it.
        internal static bool HudButtonReachable(string name)
        {
            if (EventSystem.current == null) return false;
            var entry = HudEntries.Find(e => e.Name == name);
            if (entry == null || !entry.Button.IsActive() || !entry.Button.IsInteractable()) return false;
            return PointerReachable(entry.Face.rectTransform, false);
        }

        internal static bool PointerReachable(RectTransform rect, bool scroll)
        {
            if (rect == null || !rect.gameObject.activeInHierarchy || EventSystem.current == null) return false;
            var canvas = rect.GetComponentInParent<Canvas>();
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var hits = new List<RaycastResult>();
            Vector2[] points = { new Vector2(.5f, .5f), new Vector2(.15f, .15f),
                new Vector2(.15f, .85f), new Vector2(.85f, .15f), new Vector2(.85f, .85f) };
            foreach (var point in points)
            {
                var local = new Vector2(Mathf.Lerp(rect.rect.xMin, rect.rect.xMax, point.x),
                    Mathf.Lerp(rect.rect.yMin, rect.rect.yMax, point.y));
                var screen = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(local));
                var pointer = new PointerEventData(EventSystem.current) { position = screen };
                hits.Clear();
                EventSystem.current.RaycastAll(pointer, hits);
                if (hits.Count == 0) return false;
                var handler = scroll ? ExecuteEvents.GetEventHandler<IScrollHandler>(hits[0].gameObject)
                    : ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
                if (handler != rect.gameObject) return false;
            }
            return true;
        }
        private static RectTransform MakeDock(string name, bool right, int count)
        {
            var face = AnimeHud.Image(_hud, name, "rail");
            face.raycastTarget = true;
            var r = face.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(right ? 1 : 0, 0);
            r.pivot = new Vector2(right ? 1 : 0, 0);
            r.anchoredPosition = new Vector2(right ? -8 : 8, right ? 6 : 35.5f);
            r.sizeDelta = new Vector2(HudX(name, count) + 2, 33);
            if (name == "General")
            {
                float height=PlayerPanelArt.Split*PlayerPanelArt.Scale;
                r.sizeDelta=new Vector2(174,height);
                r.anchoredPosition=new Vector2(8,68.5f-height);
                face.sprite=PlayerPanelArt.Top;face.type=Image.Type.Simple;
            }
            if (name == "Player")
            {
                // the board's own player dock, its tiles lifted out (PlayerDockTiles)
                face.sprite = AnimePieces.Get("dock_player"); face.type = Image.Type.Simple;
                r.sizeDelta = AnimePieces.Size("dock_player");
                r.anchoredPosition = new Vector2(-4, 2);
                EnsureMarkerFonts();
                _dockChevron = AnimePieces.Image(r, "Expansion", "mark_chevron");
                AnimePieces.Place(_dockChevron, 16, 58, 44, 69);
                AnimePieces.Centre(_dockChevron);
                System.Func<string>[] words =
                {
                    () => Shared.Loc.Get("hud.caption.toolbelt", "TOOLBELT"), () => Shared.Loc.Get("unit.caption.character", "CHARACTER"),
                    () => Shared.Loc.Get("unit.caption.inventory", "INVENTORY"), () => Shared.Loc.Get("hud.caption.menu", "MENU"),
                };
                for (int i = 0; i < words.Length; i++)
                {
                    Rect t = PlayerDockTiles[i];
                    var word = PlayerVitalText(r, "Label", 3.3f, true, TMPro.TextAlignmentOptions.Center);
                    UI.LocLabels.Set(word, words[i]); word.overflowMode = TMPro.TextOverflowModes.Overflow;
                    ActRow.CaptionFit(word);
                    AnimePieces.Place(word, t.x - 6, 148, t.width + 12, 32);
                }
            }
            return r;
        }

        // A chip's left edge on its dock: 23 units apart, and 7 more at each group's
        // start - the General dock's map, the Player dock's settings, and its character,
        // which sets the tool belt's chip apart from the menu (a separator between
        // it and the other buttons). HudX(dock,
        // count) is where a chip after the last would start, so + 4 is the width.
        // The player dock's tiles in its painting's pixels (the board's section 4,
        // cut at 940,528): tool belt, character, bag, menu - the board painted the
        // bag first, the target dock's order (Forgejo #116) puts the character there.
        // Every tile is the target dock's one slot, 107 x 111, centred across on the
        // painted tile's outline (make_anime_hud.py player_slot, Forgejo #169).
        private static readonly Rect[] PlayerDockTiles = {
            new Rect(68, 42, 107, 111), new Rect(211, 42, 107, 111),
            new Rect(328, 42, 107, 111), new Rect(465, 42, 107, 111) };
        private static Image _dockChevron;

        private static float HudX(string dock, int index)
        {
            int gaps = dock == "General" ? (index >= 3 ? 1 : 0)
                : dock == "Player" ? (index >= 1 ? 1 : 0) + (index >= 3 ? 1 : 0)
                : 0;
            return 4 + index * (dock == "General" ? 32 : 28) + gaps * 6;
        }

        // A chip's words, its hover tip, read at use. The GameObject keeps the
        // English label AddHud was given, which is what the harness finds.
        private static string HudLabel(string name)
        {
            switch (name)
            {
                case "craft": return Shared.Loc.Get("hud.chip.crafting", "Crafting");
                case "vicinity": return Shared.Loc.Get("hud.chip.vicinity", "Vicinity");
                case "roster": return Shared.Loc.Get("roster.title", "Roster");
                case "map": return Shared.Loc.Get("hud.chip.map", "Map");
                case "quests": return Shared.Loc.Get("hud.chip.quests", "Quests");
                case "tools": return Shared.Loc.Get("hud.chip.tools", "Tool belt");
                case "bag": return Shared.Loc.Get("hud.chip.bag", "Your bag");
                case "details": return Shared.Loc.Get("hud.chip.character", "Your character");
                case "settings": return Shared.Loc.Get("hud.chip.settings", "Settings");
                default: return name;
            }
        }

        private static void AddHud(RectTransform parent, string name, string label, int index)
        {
            var e = new HudEntry { Name = name };
            e.Face = Theme.KitImage(parent, label, "kit_chip_walnut", 0, Color.white);
            e.Face.raycastTarget = true;
            e.Cell = new AnimeCell(e.Face);
            // the harness's state preview: the vicinity button and your bag hovered,
            // the menu pressed, the map focused
            e.Cell.Role = parent.name == "General" ? (index == 1 ? 1 : index == 3 ? 4 : 0)
                : parent.name == "Player" ? (name == "bag" ? 1 : index == 3 ? 2 : 0) : 0;
            e.Cell.Place(HudX(parent.name, index), 4, AnimeHud.CellSize, AnimeHud.CellSize);
            if(parent.name=="General")
            {
                Rect b=PlayerPanelArt.ButtonRect(index);float s=PlayerPanelArt.Scale;
                e.Cell.Place(b.x*s,b.y*s,b.width*s,b.height*s);
            }
            if (parent.name == "Player")
            {
                Rect t = PlayerDockTiles[index];
                e.Cell.Place(t.x / AnimePieces.Px, t.y / AnimePieces.Px, t.width / AnimePieces.Px, t.height / AnimePieces.Px);
            }
            e.Glyph = Theme.KitImage(e.Face.transform, "Glyph", "kit_check", 0, Theme.KitCream);
            e.Glyph.sprite = HudGlyphs.Get(name == "collections" ? "vicinity" : name);
            e.Glyph.raycastTarget = false;
            Theme.Place(e.Glyph, 3, 3, 15, 15);
            e.Mark = Theme.KitImage(e.Face.transform, "Open", "kit_hairline", 0, Theme.KitCream);
            e.Mark.raycastTarget = false;
            Theme.Place(e.Mark, 5, 22, 11, 1);
            e.Glyph.enabled = e.Mark.enabled = false;
            e.Button = e.Face.gameObject.AddComponent<Button>();
            e.Button.targetGraphic = e.Face;
            e.Button.transition = Selectable.Transition.None;
            e.Button.onClick.AddListener(() => HudPress(name));
            HudEntries.Add(e);
        }
        // UI-only harness probe: hover, press, focus and a refused press drawn on
        // chosen buttons of every dock for 15 s (AnimeCell.Role), no input sent
        internal static string PreviewHudStates()
        {
            AnimeCell.PreviewUntil = Time.unscaledTime + 15f;
            return "hud states preview for 15 s";
        }

        internal static bool HudOpen(string name)
        {
            if (_mn == null || _mn.uiMN == null)
                return false;
            var ui = _mn.uiMN;
            switch (name)
            {
                case "craft":
                    return ui.craftPanel.activeSelf || ui.collectionPanel.activeSelf;
                case "collections":
                    return ui.collectionPanel.activeSelf;
                case "vicinity":
                    return _reachOpen && _slotView == ViewReach;
                case "roster":
                    return RosterView;
                case "map":
                    return ui.mapCanvas.activeSelf;
                case "quests":
                    return ui.questDiaryPanel.activeSelf;
                case "bag":
                    return _ownOpen;
                case "details":
                    return _ownSheetOpen;
                case "settings":
                    return ui.settingMenu.activeSelf;
                case "tools":
                    return BoardOpen;
                default:
                    return false;
            }
        }
        internal static void HudPress(string name)
        {
            if (_mn == null || _mn.uiMN == null)
                return;
            var ui = _mn.uiMN;
            switch (name)
            {
                case "craft":
                    if (HudOpen(name))
                        ui.LeftPanelState(0);
                    else
                    {
                        CloseReach();
                        ui.LeftPanelState(1);
                    }
                    break;
                case "vicinity":
                    if (HudOpen(name))
                        CloseReach();
                    else
                    {
                        ui.LeftPanelState(0);
                        ShowSlotView(ViewReach);
                    }
                    break;
                case "roster":
                    ui.LeftPanelState(0);
                    ToggleRoster(_mn);
                    break;
                case "collections":
                    CloseReach();
                    ui.LeftPanelState(2);
                    break;
                case "map":
                    ui.MapView();
                    break;
                case "quests":
                    ui.QuestDiaryVisible();
                    break;
                case "bag":
                    if (_ownOpen)
                        Close();
                    else
                        Open(_mn);
                    break;
                case "details":
                    ToggleStatusSheet(_mn, null);
                    break;
                case "settings":
                    ui.SettingPanelOpen();
                    break;
                case "tools":
                    ToggleBoard();
                    break;
            }
        }
    }
}
