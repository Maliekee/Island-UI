using System.Collections.Generic;
using IslandUI.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI
{
    // The mods' settings, opened from the game's menu: Island UI's own entry, and
    // Island AI's and Island Ex's while those mods are installed. The approved
    // parchment/wood study is the visual base and its control sheet the source of
    // every control here. See art/options-visual-kit-v1/.
    internal static class SettingsWindow
    {
        internal static bool IsOpen { get; private set; }
        internal static bool PointerOver => IsOpen && _pointerOver;

        private const string ButtonName = "IslandUI_SettingsButton";
        private const string ButtonLabel = "Island UI";
        private const string AiButtonName = "IslandUI_AiSettingsButton";
        private const string AiButtonLabel = "Island AI";
        private const string AiGuid = "madisland.islandai";
        private const string ExButtonName = "IslandUI_ExSettingsButton";
        private const string ExButtonLabel = "Island Ex";
        private const string ExGuid = "madisland.islandex";

        // TWO MENU ENTRIES, ONE WINDOW: Island AI has a separate menu item.
        // Island AI ships alone and has no kit, so its
        // options are designed here first, on the kit, and move to that mod once
        // the design is settled. Its settings are reached through
        // BepInEx, never through its assembly: the two DLLs stay strangers. One
        // book is built at a time; opening the other tears the window down.
        // Island Ex (its own menu) is a third book the same way, one
        // page per feature, since the features have nothing to do with each other.
        private enum Book
        {
            IslandUI,
            IslandAI,
            IslandEx,
        }

        private static Book _book;

        // Canvas units (800 x 450 reference canvas). Keep the artwork at its
        // native aspect and shrink the whole window only on smaller canvases.
        private const float W = 450f;
        private const float MinH = 300f;      // approved artwork is 3:2; preserve its proportions
        private const float Inset = 40f;
        private const float RowGap = 12f;
        private const float RowH = KitDropdown.Height;
        private const float SectionH = 18f;
        private const float RuleH = 0.35f;
        private const float DropW = 94f;
        private const float SlipX = 32f;
        private const float SlipW = 85f;
        private const float SlipH = 30f;
        private const float SlipPitch = 36f;
        private const float SlipTop = 54f;
        private const float ColumnRuleX = 126f;
        private const float SlipsContentX = 138f;
        private const float ContentTop = 53f;
        // A page's rows sit under its heading, in a view that ends above the footer
        // rule. One-line rows: the sentence about a setting is its tooltip.
        private const float HeadH = 49f;
        private const float ViewH = 133f;
        private const float LineH = 17f;
        private const float LineGap = 6f;
        private const float ScrollW = 6f;
        private const float ScrollArtPixelsPerUnit = 23f / ScrollW;   // the bar is 23 px wide on the sheet

        private static ManagersScript _mn;
        private static GameObject _menuButton;
        private static GameObject _aiButton;
        private static GameObject _exButton;
        private static GameObject _root;
        private static RectTransform _rootRect;
        private static RectTransform _frame;
        private static float _h;
        private static Image _closeChip;
        private static Image _done;
        private static TextMeshProUGUI _doneText;
        private static SettingsArt _art;
        private static KitDropdown _cursorStyle;
        private static bool _donePressed;
        private static bool _closePressed;
        private static bool _pointerOver;

        private static readonly List<KitDropdown> Drops = new List<KitDropdown>();
        private static readonly List<KitCheckbox> Checks = new List<KitCheckbox>();
        private static readonly List<KitToggle> Toggles = new List<KitToggle>();
        private static readonly List<KitSlider> Sliders = new List<KitSlider>();
        private static readonly List<KeyRow> Keys = new List<KeyRow>();
        // Each row's "show what the setting says now": run when the window opens
        // and after any switch flips, which is also what greys a dependent row.
        private static readonly List<System.Action> Refreshers = new List<System.Action>();
        private static readonly List<HelpZone> Help = new List<HelpZone>();
        private static readonly List<Page> Pages = new List<Page>();
        private static int _active;
        private static Page _hotTab;
        private static Page _pressedTab;
        private static KitTooltip _tip;

        // A category: its rows live in Content; the tab it is picked by is a
        // slip.
        private sealed class Page
        {
            public string Name;
            public string About;
            public RectTransform Content;
            public float Height;
            public RectTransform Slip;
            public Image SlipPaper;
            public TextMeshProUGUI SlipText;
            // the rows, and the view they scroll in when there are too many
            public bool Scrolls;
            public RectTransform View;
            public RectTransform Rows;
            public float RowsH;
            public float Scroll;
            public RectTransform Thumb;
            public float ThumbH;
        }

        // A key row knows its group, so a clash is only called inside one: your
        // keys against each other. Across groups the overlaps are deliberate (V is
        // the vicinity window and, in the drone, the game's).
        private sealed class KeyRow
        {
            public KitKeyBind Control;
            public int Group;
            public HelpZone Zone;
            public string Help;
        }

        // HELP BESIDE THE POINTER. Each row registers the span
        // of its icon and label as a zone with a sentence; resting on it raises
        // the kit's dark tooltip. Dark over parchment for the same reason the
        // item card is: a reading surface laid ON the surface it explains has to
        // separate from it, and cream on cream would not.
        private sealed class HelpZone
        {
            public RectTransform Rect;
            public string Text;
            public bool InRows;   // inside a page's view, so deaf when scrolled out of it
        }

        private static readonly string[] CursorStyleOptions = System.Enum.GetNames(typeof(GameCursor.Style));
        private static readonly string[] SpoilerOptions = { "Click to reveal", "Always show", "Hide" };

        internal static void ResetForSceneLoad()
        {
            if (_root != null)
            {
                Object.Destroy(_root);
            }
            if (_menuButton != null)
            {
                Object.Destroy(_menuButton);
            }
            if (_aiButton != null)
            {
                Object.Destroy(_aiButton);
            }
            if (_exButton != null)
            {
                Object.Destroy(_exButton);
            }
            _root = null;
            _menuButton = null;
            _aiButton = null;
            _exButton = null;
            Forget();
            IsOpen = false;
        }

        private static void Forget()
        {
            KitKeyBind.StopListening();
            _rootRect = null;
            _frame = null;
            Drops.Clear();
            Checks.Clear();
            Toggles.Clear();
            Sliders.Clear();
            Keys.Clear();
            Refreshers.Clear();
            Help.Clear();
            Pages.Clear();
            _tip = null;
            _hotTab = null;
            _pressedTab = null;
            _closeChip = null;
            _done = null;
            _doneText = null;
            _art = null;
            _cursorStyle = null;
            _donePressed = false;
            _closePressed = false;
            _pointerOver = false;
            _thumbDrag = false;
        }

        internal static void Open()
        {
            Open(Book.IslandUI);
        }

        internal static void OpenAi()
        {
            Open(Book.IslandAI);
        }

        private static void Open(Book book)
        {
            // Island Ex's Effects page lists what the game has shown so far, so that
            // book is built fresh at every opening, on the page it was left at
            if ((book != _book || book == Book.IslandEx) && _root != null)
            {
                // the other book is built: the next Tick builds this one
                Object.Destroy(_root);
                _root = null;
                Forget();
                if (book != _book)
                {
                    _active = 0;
                }
            }
            _book = book;
            IsOpen = true;
            if (_root != null)
            {
                _root.SetActive(true);
                _root.transform.SetAsLastSibling();
                RefreshAll();
            }
        }

        internal static void Close()
        {
            IsOpen = false;
            KitKeyBind.StopListening();
            _pointerOver = false;
            _closePressed = false;
            _donePressed = false;
            _pressedTab = null;
            for (int i = 0; i < Drops.Count; i++)
            {
                Drops[i].Close();
            }
            if (_tip != null)
            {
                _tip.Hide();
            }
            if (_root != null)
            {
                _root.SetActive(false);
            }
        }

        // Escape: an open list first, then the window.
        internal static bool TryEscapeClose()
        {
            if (!IsOpen)
            {
                return false;
            }
            KitDropdown open = OpenDrop();
            if (open != null)
            {
                open.Close();
                return true;
            }
            Close();
            return true;
        }

        internal static void Tick(ManagersScript mn)
        {
            _mn = mn;
            if (mn == null || !mn.allSet || mn.uiMN == null)
            {
                return;
            }
            EnsureMenuButtons(mn);
            KitKeyBind.TickRelease();
            if (!IsOpen)
            {
                return;
            }
            if (!Ensure(mn))
            {
                return;
            }
            if (!_root.activeSelf)
            {
                _root.SetActive(true);
                _root.transform.SetAsLastSibling();
            }
            Camera cam = CameraFor(_frame);
            FitToCanvas();
            if (_cursorStyle != null && _cursorStyle.Index != (int)Settings.CursorStyle.Value)
            {
                _cursorStyle.SetIndex((int)Settings.CursorStyle.Value);
            }
            bool over = Contains(_frame, cam);
            // An open list hangs over the rows beneath it, so while one is open
            // it is the only control that hears the pointer: a click on its row
            // must not also press the plank under that row.
            bool overControl = false;
            // a row scrolled out of its view still has a rect, under the footer or
            // the heading: only rows inside the view hear the pointer
            Page page = Pages[_active];
            bool inView = !page.Scrolls || Contains(page.View, cam);
            KitDropdown open = OpenDrop();
            if (open != null)
            {
                overControl = open.Tick(cam);
            }
            else
            {
                for (int i = 0; i < Drops.Count; i++)
                {
                    overControl |= Drops[i].Tick(cam);
                }
                for (int i = 0; i < Checks.Count; i++)
                {
                    overControl |= Checks[i].Tick(cam);
                }
                for (int i = 0; i < Toggles.Count; i++)
                {
                    overControl |= Toggles[i].Tick(cam, inView);
                }
                for (int i = 0; i < Sliders.Count; i++)
                {
                    overControl |= Sliders[i].Tick(cam, inView);
                }
                for (int i = 0; i < Keys.Count; i++)
                {
                    overControl |= Keys[i].Control.Tick(cam, inView);
                }
                TickScroll(page, cam, over);
            }
            // the plate row floats outside the frame, so it is tested on its own
            bool overClose = TickClose(cam);
            bool overDone = IsOpen && TickDone(cam, open == null);
            _pointerOver = IsOpen && (over || overControl || overClose || overDone);
            if (_root == null || !IsOpen)
            {
                return; // the close just fired
            }
            // help for whatever the pointer rests on - none while a list is open,
            // which is its own answer to "what are the choices"
            string help = null;
            if (open == null)
            {
                for (int i = 0; i < Help.Count; i++)
                {
                    if ((inView || !Help[i].InRows) && Contains(Help[i].Rect, cam))
                    {
                        help = Help[i].Text;
                        break;
                    }
                }
            }
            _tip.Tick(help, cam);
            TickTabs(cam, open == null);
        }

        // ---------- the menu button ----------

        private static void EnsureMenuButtons(ManagersScript mn)
        {
            // Unity's fake-null: a scene reload destroys the clones with the menu
            // they lived in, these read null, and the buttons are built again.
            if (_menuButton == null)
            {
                _menuButton = CloneMenuButton(mn, ButtonName, ButtonLabel, 1, () => OnMenuButton(Book.IslandUI));
            }
            if (_aiButton == null && PluginConfig(AiGuid) != null)
            {
                _aiButton = CloneMenuButton(mn, AiButtonName, AiButtonLabel, 2, () => OnMenuButton(Book.IslandAI));
            }
            if (_exButton == null && PluginConfig(ExGuid) != null)
            {
                _exButton = CloneMenuButton(mn, ExButtonName, ExButtonLabel, _aiButton != null ? 3 : 2,
                    () => OnMenuButton(Book.IslandEx));
            }
        }

        // `after` is how many places below the game's Option entry the copy stands.
        private static GameObject CloneMenuButton(ManagersScript mn, string name, string label, int after,
            UnityEngine.Events.UnityAction onClick)
        {
            GameObject menu = mn.uiMN.settingMenu;
            if (menu == null)
            {
                return null;
            }
            Transform template = menu.transform.Find("SettingButton");
            if (template == null)
            {
                foreach (Transform child in menu.transform)
                {
                    if (child.GetComponent<Button>() != null)
                    {
                        template = child;
                        break;
                    }
                }
            }
            if (template == null)
            {
                return null;
            }
            for (int i = menu.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = menu.transform.GetChild(i);
                if (child.name == name)
                {
                    Object.Destroy(child.gameObject);
                }
            }

            GameObject clone = Owned.Instantiate(template.gameObject, menu.transform, false);
            clone.name = name;
            clone.transform.SetSiblingIndex(template.GetSiblingIndex() + after);
            // The template's label is localized through a LocalizeStringEvent
            // that would put "Option" back on it at the next language change.
            foreach (Component c in clone.GetComponentsInChildren<Component>(true))
            {
                if (c != null && c.GetType().Name == "LocalizeStringEvent")
                {
                    Object.DestroyImmediate(c);
                }
            }
            TMP_Text text = clone.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
            {
                text.text = label;
            }
            Button button = clone.GetComponent<Button>();
            if (button != null)
            {
                button.onClick = new Button.ButtonClickedEvent();
                button.onClick.AddListener(onClick);
            }
            // a hidden state on the template must not travel with the copy: the
            // HUD once faded the Option entry, and the clone kept its CanvasGroup
            CanvasGroup group = clone.GetComponent<CanvasGroup>();
            if (group != null)
            {
                group.alpha = 1f;
                group.interactable = true;
                group.blocksRaycasts = true;
            }
            clone.SetActive(true);
            Plugin.Log.LogInfo("Settings: menu button '" + label + "' cloned from '" + template.name + "'");
            return clone;
        }

        private static void OnMenuButton(Book book)
        {
            // the menu folds away as it does for its own entries that open a panel
            if (_mn != null && _mn.uiMN != null && _mn.uiMN.settingMenu != null
                && _mn.uiMN.settingMenu.activeSelf)
            {
                _mn.uiMN.SettingPanelOpen();
            }
            Open(book);
        }

        // Another mod's settings, through BepInEx's list of loaded plugins: null when
        // that mod is not installed, and then it has no menu entry either.
        private static BepInEx.Configuration.ConfigFile PluginConfig(string guid)
        {
            BepInEx.PluginInfo info;
            return BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue(guid, out info) && info.Instance != null
                ? info.Instance.Config
                : null;
        }

        // ---------- the window ----------

        private static bool Ensure(ManagersScript mn)
        {
            if (_root != null)
            {
                return true;
            }
            if (mn.inventory == null || !Theme.Ensure(mn, mn.inventory)
                || mn.uiMN.itemDescPanel == null)
            {
                return false;
            }
            Canvas canvas = mn.uiMN.itemDescPanel.GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                return false;
            }
            _root = Owned.New("IslandUI_Settings");
            _root.transform.SetParent(canvas.rootCanvas.transform, false);
            _rootRect = (RectTransform)_root.transform;
            _rootRect.anchorMin = Vector2.zero;
            _rootRect.anchorMax = Vector2.one;
            _rootRect.offsetMin = Vector2.zero;
            _rootRect.offsetMax = Vector2.zero;
            CanvasIsolation.Isolate(_root);

            // the window's rect, centred; the sheet and the chrome hang off it
            var frameGo = Owned.New("Window");
            frameGo.transform.SetParent(_root.transform, false);
            _frame = (RectTransform)frameGo.transform;
            _frame.anchorMin = new Vector2(0.5f, 0.5f);
            _frame.anchorMax = new Vector2(0.5f, 0.5f);
            _frame.pivot = new Vector2(0.5f, 0.5f);
            _frame.anchoredPosition = Vector2.zero;
            _art = frameGo.AddComponent<SettingsArt>();
            _art.Init();

            ListPages();
            _active = Mathf.Clamp(_active, 0, Pages.Count - 1);

            float contentW = W - SlipsContentX - Inset;
            RectTransform sheet = MakeSheet(_frame, "Sheet");
            for (int i = 0; i < Pages.Count; i++)
            {
                Page p = Pages[i];
                var contentGo = Owned.New("Page_" + p.Name);
                contentGo.transform.SetParent(sheet, false);
                p.Content = (RectTransform)contentGo.transform;
                p.Height = BuildPage(mn, p, contentW);
            }
            _h = MinH;
            _frame.sizeDelta = new Vector2(W, _h);
            for (int i = 0; i < Pages.Count; i++)
            {
                Page p = Pages[i];
                Theme.Place(p.Content, SlipsContentX, ContentTop, contentW, _h - ContentTop - 48f);
                BuildSlip(p, i);
            }
            sheet.sizeDelta = new Vector2(W, _h);
            Rule(_frame, "ColumnRule", ColumnRuleX, ContentTop, _h - ContentTop - 36f, vertical: true);

            // The plaque and empty close backing are already part of the frame.
            // Text stays live; the close glyph comes from the matching parts sheet.
            TextMeshProUGUI title = _art.Text(_frame, "Title", 12f,
                Theme.KitCream, TextAlignmentOptions.Center, bold: true);
            title.text = _book == Book.IslandAI ? AiButtonLabel : _book == Book.IslandEx ? ExButtonLabel : ButtonLabel;
            Theme.Place(title, 38f, 15f, 92f, 20f);
            _closeChip = _art.Image(_frame, "Close", _art.Close);
            _closeChip.raycastTarget = true;
            Theme.Place(_closeChip, 408f, 17f, 22f, 22f);

            Rule(_frame, "FooterRule", SlipsContentX, _h - 61f, contentW, vertical: false);
            TextMeshProUGUI note = _art.Text(_frame, "ApplyNote", 7f,
                Theme.ParseColor("#70573D", Theme.KitInk), TextAlignmentOptions.MidlineLeft);
            note.text = "Changes apply immediately.";
            Theme.Place(note, SlipsContentX, _h - 51f, contentW - 78f, 21f);
            _done = _art.Image(_frame, "Done", _art.Button);
            _done.raycastTarget = true;
            Theme.Place(_done, W - Inset - 68f, _h - 51f, 68f, 21f);
            _doneText = _art.Text(_done.transform, "Text", 9f, Theme.KitCream,
                TextAlignmentOptions.Center, bold: true);
            _doneText.text = "Done";
            Stretch(_doneText.rectTransform);

            _tip = KitTooltip.Create(_rootRect, true, _art);
            Select(_active);
            RefreshAll();
            FitToCanvas();
            _root.SetActive(false);
            Plugin.Log.LogInfo("Settings: window built (" + Pages.Count + " pages)");
            return true;
        }

        // The approved blank frame carries the paper, rail and title backing.
        private static RectTransform MakeSheet(Transform parent, string name)
        {
            var go = Owned.New(name);
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = Vector2.zero;
            Image fieldImage = _art.Image(rect, "Artwork", _art.Frame);
            fieldImage.raycastTarget = true; // a click here is ours, not the world's
            Stretch(fieldImage.rectTransform);
            return rect;
        }

        // ---------- the pages ----------

        private static void ListPages()
        {
            Pages.Clear();
            if (_book == Book.IslandAI)
            {
                Pages.Add(new Page { Name = "Steering", About = "How your people and your enemies get around what is in their way." });
                return;
            }
            if (_book == Book.IslandEx)
            {
                Pages.Add(new Page
                {
                    Name = "Effects", Scrolls = true,
                    About = "Switch an effect on to hide it. An effect is listed once the game has shown it.",
                });
                Pages.Add(new Page { Name = "Struggle", About = "Holding the button in a grapple, instead of hammering it." });
                Pages.Add(new Page { Name = "Window", About = "What the game window does when you look away from it." });
                Pages.Add(new Page { Name = "Weather", About = "Where the weather reaches you." });
                return;
            }
            Pages.Add(new Page { Name = "Cursor", About = "Choose the pointer you use on the island." });
            Pages.Add(new Page { Name = "Target", About = "How the person or thing you are dealing with is marked." });
            Pages.Add(new Page { Name = "Items", About = "What an item's card tells you." });
            Pages.Add(new Page { Name = "Story", About = "What the story's choices tell you before you pick." });
            Pages.Add(new Page
            {
                Name = "Hotkeys", Scrolls = true,
                About = "Click a key, then press the new one. Esc cancels, Backspace clears.",
            });
        }

        private static float BuildPage(ManagersScript mn, Page p, float contentW)
        {
            Transform t = p.Content;
            Section(t, 0f, contentW, p.Name, p.About);
            GameObject viewGo = Owned.New("View");
            viewGo.transform.SetParent(t, false);
            p.View = Theme.Place(viewGo.transform, 0f, HeadH, contentW, ViewH);
            GameObject rowsGo = Owned.New("Rows");
            rowsGo.transform.SetParent(viewGo.transform, false);
            p.Rows = Theme.Place(rowsGo.transform, 0f, 0f, contentW, ViewH);

            Transform r = p.Rows;
            float w = p.Scrolls ? contentW - ScrollW - Theme.Gap : contentW;
            float y = 0f;
            switch (p.Name)
            {
                case "Cursor":
                    y = Row(r, y, w, "Style", CursorStyleOptions, (int)Settings.CursorStyle.Value,
                        index => { Settings.CursorStyle.Value = (GameCursor.Style)index; },
                        "Select Off to use the system pointer.");
                    _cursorStyle = Drops[Drops.Count - 1];
                    break;
                case "Target":
                    y = ToggleRow(r, y, w, "Ground ring", Settings.TargetRing, null,
                        "A ring on the ground under your target, in the colour of its marker: green yours, red hostile, amber a stranger or a thing, grey a body.");
                    y = SliderRow(r, y, w, "Ring size", Settings.TargetRingScale, 0.5f, 2f, 0.1f, Percent, Settings.TargetRing,
                        "How big the ring is, against what the target's footprint asks for.");
                    y = SliderRow(r, y, w, "Ring opacity", Settings.TargetRingAlpha, 0f, 1f, 0.05f, Percent, Settings.TargetRing,
                        "How solid the ring is.");
                    y = ToggleRow(r, y, w, "Selection pulse", Settings.SelectionGlow, null,
                        "The game's own pulsing white glow on the person you selected. Off by default: the marker and the ring already say who it is.");
                    y = ToggleRow(r, y, w, "Pickup flight", Settings.PickupFlight, null,
                        "A picked-up item's notice flies from you to the slot it landed in, instead of waiting at the edge of the screen.");
                    y = SliderRow(r, y, w, "Flight time", Settings.PickupFlightSeconds, 0.2f, 1.2f, 0.05f, Seconds, Settings.PickupFlight,
                        "How long the notice takes to fly. Under about 0.35 s the count cannot be read in flight; over 0.8 s it feels like waiting.");
                    break;
                case "Items":
                    y = ToggleRow(r, y, w, "How to get it", Settings.ItemGuide, null,
                        "The card says how to get the item: where it is found, what drops it, who trades it. Written from what players have collected, so it covers the dungeon, chests and trades too.");
                    break;
                case "Story":
                    y = ToggleRow(r, y, w, "Choice hints", Settings.StoryHints, null,
                        "Each option of a story choice says what it leads to: affection, who joins, what unlocks.");
                    y = ToggleRow(r, y, w, "Scene prompt hints", Settings.StoryPromptHints, null,
                        "Scenes that wait for a key instead of showing buttons say what each key does, and that hidden keys exist.");
                    y = Row(r, y, w, "Spoilers", SpoilerOptions, (int)Settings.StorySpoilers.Value,
                        index => { Settings.StorySpoilers.Value = (SpoilerMode)index; },
                        "Deaths, endings and partings: masked until clicked, always shown, or left out.");
                    break;
                case "Hotkeys":
                    y = Group(r, y, w, "Island UI");
                    y = KeyLine(r, y, w, 0, "Inventory", Settings.InventoryKey, "Opens and closes the inventory window.");
                    y = KeyLine(r, y, w, 0, "Vicinity", Settings.ReachKey, "Opens and closes the vicinity window: the storage within reach.");
                    y = KeyLine(r, y, w, 0, "Crafting", QoL.OpenCraftingMenuKey, "Opens and closes the crafting window.");
                    y = KeyLine(r, y, w, 0, "Quick save", QoL.QuickSaveKey, "Saves to the slot this game was loaded from.");
                    y = KeyLine(r, y, w, 0, "Target", Settings.TargetKey, "Targets what is nearest the cursor without walking to it; pressed again, the next nearest.");
                    y = KeyLine(r, y, w, 0, "Target's sheet", Settings.SheetKey, "Opens and closes the sheet of whoever is your target.");
                    y = KeyLine(r, y, w, 0, "Back to the world", Settings.WorldFocusKey, "Closes the windows Island UI owns, and the map and quest overlays.");
                    y = KeyLine(r, y, w, 0, "Close everything", Settings.CloseAllKey, "Closes every open window, Island UI's first, then the game's.");
                    y = KeyLine(r, y, w, 0, "Console", Settings.ConsoleKey, "Drops the command console down from the top of the screen.", "Left of 1");
                    y = KeyLine(r, y, w, 0, "Quick stack", Settings.QuickStackKey, "Deposits stackable items into nearby storages set to Stack.");
                    y = KeyLine(r, y, w, 0, "Next cursor", Settings.CursorCycleKey, "Steps through the cursor styles, so the choice can be made by looking.");
                    y = Group(r, y + 4f, w, "The game's keys");
                    for (int i = 0; i < Bindings.All.Count; i++)
                    {
                        Bindings.Binding b = Bindings.All[i];
                        if (!ReferenceEquals(b.Entry, QoL.OpenCraftingMenuKey) && !ReferenceEquals(b.Entry, QoL.QuickSaveKey))
                        {
                            y = KeyLine(r, y, w, 1, b.Label, b.Entry, b.Entry.Description.Description);
                        }
                    }
                    MarkClashes();
                    break;
                case "Steering":
                    y = AiRows(r, y, w, p);
                    break;
                case "Effects":
                    y = EffectRows(r, y, w);
                    break;
                case "Struggle":
                case "Window":
                case "Weather":
                    y = ExRows(r, y, w, p.Name);
                    break;
            }
            p.RowsH = Mathf.Max(0f, y - LineGap);
            p.Scrolls = p.Scrolls && p.RowsH > ViewH;
            if (p.Scrolls)
            {
                viewGo.AddComponent<RectMask2D>();
                BuildScrollbar(p, contentW);
            }
            else if (p.RowsH > ViewH + 0.5f)
            {
                Plugin.Log.LogWarning("Settings: page '" + p.Name + "' is " + p.RowsH + " tall in a view of " + ViewH);
            }
            return HeadH + Mathf.Min(p.RowsH, ViewH);
        }

        // Island AI's rows, from its settings file. A setting that build of the mod
        // does not have is a row that is not drawn.
        private static float AiRows(Transform r, float y, float w, Page p)
        {
            BepInEx.Configuration.ConfigFile cfg = PluginConfig(AiGuid);
            if (cfg == null)
            {
                return y;
            }
            BepInEx.Configuration.ConfigEntry<bool> enabled, enemies, friends, doors;
            BepInEx.Configuration.ConfigEntry<float> probe, clearance;
            cfg.TryGetEntry("General", "Enabled", out enabled);
            cfg.TryGetEntry("Steering", "Enemies", out enemies);
            cfg.TryGetEntry("Steering", "Friends", out friends);
            cfg.TryGetEntry("Steering", "Probe Distance", out probe);
            cfg.TryGetEntry("Steering", "Clearance", out clearance);
            cfg.TryGetEntry("Houses", "Door Assist", out doors);
            if (enabled != null)
            {
                y = ToggleRow(r, y, w, "Island AI", enabled, null,
                    "The whole mod, live: off and everyone moves exactly as in the unmodded game.");
            }
            var key = new BepInEx.Configuration.ConfigDefinition("Steering", "Method");
            if (cfg.ContainsKey(key))
            {
                BepInEx.Configuration.ConfigEntryBase method = cfg[key];
                string[] names = System.Enum.GetNames(method.SettingType);
                var words = new string[names.Length];
                for (int i = 0; i < names.Length; i++)
                {
                    words[i] = Words(names[i]);
                }
                y = DropLine(r, p.Content, y, w, "Method", words,
                    System.Array.IndexOf(names, method.BoxedValue.ToString()),
                    index => { method.BoxedValue = System.Enum.Parse(method.SettingType, names[index]); },
                    "Along walls: a blocked walker also tries the direction of the wall it met, and takes the heading with the most room nearest its target; it finds narrow passages. Fan: the first cut, the nearest of eleven fixed headings that is completely clear.");
            }
            if (enemies != null)
            {
                y = ToggleRow(r, y, w, "Enemies steer", enemies, enabled,
                    "Enemies walk around obstacles instead of pushing into them: chasing, wandering, running away. Walls defend less.");
            }
            if (friends != null)
            {
                y = ToggleRow(r, y, w, "Your people steer", friends, enabled,
                    "Followers and villagers walk around obstacles outdoors: following you, chasing an enemy, wandering, walking to a house at night.");
            }
            if (probe != null)
            {
                y = SliderRow(r, y, w, "Look ahead", probe, 0.5f, 6f, 0.25f, Metres, enabled,
                    "How far ahead a walker looks for something in its way.");
            }
            if (clearance != null)
            {
                y = SliderRow(r, y, w, "Clearance", clearance, 0.8f, 1.3f, 0.05f, Percent, enabled,
                    "How wide the look-ahead is against the walker's own body. Above 100% rounds corners with room to spare and refuses tight gaps; below it squeezes into gaps barely wider than the body and brushes corners.");
            }
            if (doors != null)
            {
                y = ToggleRow(r, y, w, "Door assist", doors, enabled,
                    "Your people leaving a house get out when they reach its door. The Native House's exit sits mostly inside its wall, and without this they can stand beside it for good.");
            }
            return y;
        }

        // Island Ex's Struggle, Window and Weather pages, from its settings file, the same way.
        private static float ExRows(Transform r, float y, float w, string page)
        {
            BepInEx.Configuration.ConfigFile cfg = PluginConfig(ExGuid);
            if (cfg == null)
            {
                return y;
            }
            BepInEx.Configuration.ConfigEntry<bool> hold, keep, shortcuts;
            BepInEx.Configuration.ConfigEntry<float> interval;
            if (page == "Struggle")
            {
                cfg.TryGetEntry("Grapple", "HoldToStruggle", out hold);
                cfg.TryGetEntry("Grapple", "HoldInterval", out interval);
                if (hold != null)
                {
                    y = ToggleRow(r, y, w, "Hold to struggle", hold, null,
                        "Hold the attack button in a grapple and it keeps hitting. Unmodded, holding only repeats once skill 9 is levelled; until then you have to hammer the button.");
                }
                if (interval != null)
                {
                    y = SliderRow(r, y, w, "Hit every", interval, 0.05f, 1f, 0.05f, Seconds, hold,
                        "Time between hits while held. 0.10 s is about a fast hammering, and what the game gives at full skill. Never slower than your skill already allows.");
                }
                return y;
            }
            if (page == "Weather")
            {
                BepInEx.Configuration.ConfigEntry<bool> dry;
                cfg.TryGetEntry("Weather", "Dry Indoors", out dry);
                if (dry != null)
                {
                    y = ToggleRow(r, y, w, "Dry indoors", dry, null,
                        "No rain falls inside a house. Outside it still rains, and you see it again at the door. Unmodded, the rain follows you in.");
                }
                return y;
            }
            cfg.TryGetEntry("Window", "KeepFullscreenOnFocusLoss", out keep);
            cfg.TryGetEntry("Window", "MinimiseShortcuts", out shortcuts);
            if (keep != null)
            {
                y = ToggleRow(r, y, w, "Stay on screen", keep, null,
                    "The game stays up when you switch desktop, Alt+Tab or click another monitor. Unmodded, it minimises itself.");
            }
            if (shortcuts != null)
            {
                y = ToggleRow(r, y, w, "Win+M minimises", shortcuts, null,
                    "Win+M and Win+Down minimise the game, as they do other windows. A taskbar click and Win+D work either way.");
            }
            return y;
        }

        // Island Ex's effect filter as rows: one switch per effect, on = hidden.
        // The rows write the same [FX] Blocked list the .cfg has, so a hand-written
        // entry and a switched one are the same thing. The effects the game has
        // shown this session come from Island Ex itself, found by name; a build
        // without that lists only what is already hidden.
        private static float EffectRows(Transform r, float y, float w)
        {
            BepInEx.Configuration.ConfigFile cfg = PluginConfig(ExGuid);
            if (cfg == null)
            {
                return y;
            }
            BepInEx.Configuration.ConfigEntry<bool> on;
            BepInEx.Configuration.ConfigEntry<string> blocked;
            cfg.TryGetEntry("FX", "Enabled", out on);
            cfg.TryGetEntry("FX", "Blocked", out blocked);
            if (on != null)
            {
                y = ToggleRow(r, y, w, "Effect filter", on, null,
                    "Off and every effect shows, as in the unmodded game. What is hidden below is kept for when it is on again.");
            }
            if (blocked == null)
            {
                return y;
            }
            string[] seen = SeenEffects();
            var hidden = new List<string>();
            var shown = new List<string>();
            foreach (string name in seen)
            {
                (IsHidden(blocked.Value, name) ? hidden : shown).Add(name);
            }
            // hidden entries no effect of this session answers to: a name not met
            // yet, or an effect id
            foreach (string entry in BlockedEntries(blocked.Value))
            {
                bool met = false;
                foreach (string name in seen)
                {
                    met |= Hides(entry, name);
                }
                if (!met)
                {
                    hidden.Add(entry);
                }
            }
            System.Func<bool> live = () => on == null || on.Value;
            y = Group(r, y + 4f, w, "Hidden");
            foreach (string name in hidden)
            {
                y = EffectRow(r, y, w, name, blocked, live);
            }
            y = Group(r, y + 4f, w, "Shown, newest first");
            if (shown.Count == 0)
            {
                HelpZone zone;
                TextMeshProUGUI none = LineLabel(r, "None yet. Play on and open this again.", y, w,
                    "Effects are listed the first time the game shows them this session.", out zone);
                none.alpha = 0.6f;
                y += LineH + LineGap;
            }
            foreach (string name in shown)
            {
                y = EffectRow(r, y, w, name, blocked, live);
            }
            return y;
        }

        private static float EffectRow(Transform r, float y, float w, string name,
            BepInEx.Configuration.ConfigEntry<string> blocked, System.Func<bool> live)
        {
            int id;
            bool isId = int.TryParse(name, out id);
            return ToggleRow(r, y, w, isId ? "Effect " + id : name,
                () => IsHidden(blocked.Value, name),
                hide => blocked.Value = hide ? Hide(blocked.Value, name) : Unhide(blocked.Value, name),
                live,
                isId ? "Hidden by its number in the .cfg's [FX] Blocked list."
                    : "The game's own name for this effect. Hiding it changes only what you see.");
        }

        private static string[] SeenEffects()
        {
            try
            {
                BepInEx.PluginInfo info;
                if (!BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue(ExGuid, out info) || info.Instance == null)
                {
                    return new string[0];
                }
                System.Reflection.MethodInfo m = info.Instance.GetType().GetMethod("SeenEffects",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                return (m != null ? m.Invoke(null, null) as string[] : null) ?? new string[0];
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning("Settings: Island Ex's effect list: " + e.Message);
                return new string[0];
            }
        }

        // The rule is Island Ex's: a number is an effect id, anything else hides
        // every effect whose name contains it, ignoring case.
        private static List<string> BlockedEntries(string list)
        {
            var entries = new List<string>();
            foreach (string part in (list ?? "").Split(','))
            {
                string entry = part.Trim();
                if (entry.Length > 0)
                {
                    entries.Add(entry);
                }
            }
            return entries;
        }

        private static bool Hides(string entry, string name)
        {
            int ignored;
            return int.TryParse(entry, out ignored)
                ? entry == name
                : name.IndexOf(entry, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsHidden(string list, string name)
        {
            foreach (string entry in BlockedEntries(list))
            {
                if (Hides(entry, name))
                {
                    return true;
                }
            }
            return false;
        }

        private static string Hide(string list, string name)
        {
            List<string> entries = BlockedEntries(list);
            if (!IsHidden(list, name))
            {
                entries.Add(name);
            }
            return string.Join(", ", entries.ToArray());
        }

        // every entry that hides it goes, which also shows again any other effect
        // that one short entry was hiding
        private static string Unhide(string list, string name)
        {
            List<string> entries = BlockedEntries(list);
            entries.RemoveAll(entry => Hides(entry, name));
            return string.Join(", ", entries.ToArray());
        }

        private static string Percent(float v)
        {
            return Mathf.RoundToInt(v * 100f) + "%";
        }

        private static string Seconds(float v)
        {
            return v.ToString("0.00") + " s";
        }

        private static string Metres(float v)
        {
            return v.ToString("0.0#") + " m";
        }

        // AlongWalls -> Along walls
        private static string Words(string identifier)
        {
            var s = new System.Text.StringBuilder(identifier.Length + 4);
            for (int i = 0; i < identifier.Length; i++)
            {
                char c = identifier[i];
                if (i > 0 && char.IsUpper(c))
                {
                    s.Append(' ').Append(char.ToLowerInvariant(c));
                }
                else
                {
                    s.Append(c);
                }
            }
            return s.ToString();
        }

        // Page title, supporting sentence and quiet separator.
        private static void Section(Transform parent, float y, float contentW, string title, string about)
        {
            TextMeshProUGUI heading = _art.Text(parent, "Section_" + title, 13f,
                Theme.KitInk, TextAlignmentOptions.MidlineLeft, bold: true);
            heading.enableWordWrapping = false;
            // the body face's line is a hair taller than SectionH, and an ellipsis
            // box that cannot fit one line draws nothing at all
            heading.overflowMode = TextOverflowModes.Overflow;
            heading.text = title;
            Theme.Place(heading, 0f, y, contentW, SectionH);
            TextMeshProUGUI subtitle = _art.Text(parent, "Description", 8f,
                Theme.ParseColor("#70573D", Theme.KitInk), TextAlignmentOptions.MidlineLeft);
            subtitle.overflowMode = TextOverflowModes.Overflow;
            subtitle.text = about;
            Theme.Place(subtitle, 0f, y + SectionH + 1f, contentW, 13f);
            Rule(parent, "Rule_" + title, 0f, y + 38f, contentW, vertical: false);
        }

        // Fine ink rules separate navigation, content and footer.
        private static void Rule(Transform parent, string name, float x, float y, float length, bool vertical)
        {
            GameObject go = Owned.New(name);
            go.transform.SetParent(parent, false);
            Image rule = go.AddComponent<Image>();
            rule.raycastTarget = false;
            rule.color = new Color(Theme.KitInk.r, Theme.KitInk.g, Theme.KitInk.b, 0.35f);
            Theme.Place(rule, x, y, vertical ? RuleH : length, vertical ? length : RuleH);
        }

        // A label on the left, a dropdown on the right, one row.
        private static float Row(Transform parent, float y, float contentW, string label,
            string[] options, int index, System.Action<int> onChanged, string help)
        {
            float labelW = contentW - DropW - Theme.Gap;
            TextMeshProUGUI name = Label(parent, label, 0f, y, labelW, RowH);
            _art.Type(name, true);
            TextMeshProUGUI description = _art.Text(parent, "HelpText_" + label, 7f,
                Theme.ParseColor("#70573D", Theme.KitInk), TextAlignmentOptions.TopLeft);
            description.text = help;
            description.enableWordWrapping = true;
            Theme.Place(description, 0f, y + RowH + 1f, labelW - 8f, 26f);
            KitDropdown dropdown = KitDropdown.Create(parent, "Drop_" + label, contentW - DropW, y, DropW,
                options, index, onChanged, settingsArt: _art);
            Drops.Add(dropdown);
            HelpOn(parent, label, 0f, y, labelW, RowH, help);
            return y + RowH + 27f + RowGap;
        }

        // A label on the left, the kit checkbox on the right, bound to a bool
        // entry; the entry's own SettingChanged does whatever applying it takes.
        private static float CheckRow(Transform parent, float y, float contentW, string label,
            BepInEx.Configuration.ConfigEntry<bool> entry, string help)
        {
            float h = Mathf.Max(KitCheckbox.Size, SectionH);
            float labelW = contentW - KitCheckbox.Size - Theme.Gap;
            Label(parent, label, 0f, y, labelW, h);
            Checks.Add(KitCheckbox.Create(parent, "Check_" + label, contentW - KitCheckbox.Size,
                y + (h - KitCheckbox.Size) * 0.5f, entry.Value, on => { entry.Value = on; }));
            HelpOn(parent, label, 0f, y, labelW, h, help);
            return y + h + RowGap;
        }

        private static TextMeshProUGUI Label(Transform parent, string label, float x, float y, float w, float h)
        {
            TextMeshProUGUI text = _art.Text(parent, "Label_" + label, 9f,
                Theme.KitInk, TextAlignmentOptions.MidlineLeft);
            text.enableWordWrapping = false;
            text.text = label;
            Theme.Place(text, x, y, w, h);
            return text;
        }

        // the help zone spans everything in a row that is not the control itself
        private static HelpZone HelpOn(Transform parent, string label, float x, float y, float w, float h, string help)
        {
            var zoneGo = Owned.New("Help_" + label);
            zoneGo.transform.SetParent(parent, false);
            var zone = (RectTransform)zoneGo.transform;
            Theme.Place(zone, x, y, w, h);
            var added = new HelpZone { Rect = zone, Text = help };
            Help.Add(added);
            return added;
        }

        // ---------- one-line rows ----------

        // A sub-heading inside a page's rows, with its own hairline.
        private static float Group(Transform parent, float y, float w, string title)
        {
            TextMeshProUGUI text = _art.Text(parent, "Group_" + title, 8f,
                Theme.ParseColor("#70573D", Theme.KitInk), TextAlignmentOptions.MidlineLeft, bold: true);
            text.overflowMode = TextOverflowModes.Overflow;
            text.text = title;
            Theme.Place(text, 0f, y, w, 11f);
            Rule(parent, "GroupRule_" + title, 0f, y + 12f, w, vertical: false);
            return y + 12f + LineGap;
        }

        private static TextMeshProUGUI LineLabel(Transform parent, string label, float y, float w, string help,
            out HelpZone zone)
        {
            TextMeshProUGUI name = Label(parent, label, 0f, y, w, LineH);
            _art.Type(name, true);
            name.overflowMode = TextOverflowModes.Ellipsis;
            zone = HelpOn(parent, label, 0f, y, w, LineH, help);
            zone.InRows = true;
            return name;
        }

        // `dependsOn`: the switch this row is a detail of. Off, the row stays in
        // its place, greyed, and hears nothing.
        private static float ToggleRow(Transform parent, float y, float w, string label,
            BepInEx.Configuration.ConfigEntry<bool> entry, BepInEx.Configuration.ConfigEntry<bool> dependsOn, string help)
        {
            return ToggleRow(parent, y, w, label, () => entry.Value, on => entry.Value = on,
                () => dependsOn == null || dependsOn.Value, help);
        }

        // A switch over anything that can be read and set, not only a bool setting.
        private static float ToggleRow(Transform parent, float y, float w, string label,
            System.Func<bool> get, System.Action<bool> set, System.Func<bool> dependsOn, string help)
        {
            HelpZone zone;
            TextMeshProUGUI name = LineLabel(parent, label, y, w - KitToggle.Width - Theme.Gap, help, out zone);
            KitToggle toggle = KitToggle.Create(parent, "Toggle_" + label, w - KitToggle.Width,
                y + (LineH - KitToggle.Height) * 0.5f, get(),
                on => { set(on); RefreshAll(); }, _art);
            Toggles.Add(toggle);
            Refreshers.Add(() =>
            {
                bool live = dependsOn();
                toggle.Set(get());
                toggle.SetEnabled(live);
                name.alpha = live ? 1f : 0.4f;
            });
            return y + LineH + LineGap;
        }

        private static float SliderRow(Transform parent, float y, float w, string label,
            BepInEx.Configuration.ConfigEntry<float> entry, float min, float max, float step,
            System.Func<float, string> format, BepInEx.Configuration.ConfigEntry<bool> dependsOn, string help)
        {
            HelpZone zone;
            TextMeshProUGUI name = LineLabel(parent, label, y, w - DropW - Theme.Gap, help, out zone);
            KitSlider slider = KitSlider.Create(parent, "Slider_" + label, w - DropW,
                y + (LineH - KitSlider.Height) * 0.5f, DropW, min, max, step, entry.Value, format,
                v =>
                {
                    // BepInEx writes the file on every set; a drag is dozens of them
                    BepInEx.Configuration.ConfigFile file = entry.ConfigFile;
                    bool saves = file.SaveOnConfigSet;
                    file.SaveOnConfigSet = false;
                    entry.Value = v;
                    file.SaveOnConfigSet = saves;
                },
                () => entry.ConfigFile.Save(), _art);
            Sliders.Add(slider);
            Refreshers.Add(() =>
            {
                bool live = dependsOn == null || dependsOn.Value;
                if (!slider.Dragging)
                {
                    slider.Set(entry.Value);
                }
                slider.SetEnabled(live);
                name.alpha = live ? 1f : 0.4f;
            });
            return y + LineH + LineGap;
        }

        private static float KeyLine(Transform parent, float y, float w, int group, string label,
            BepInEx.Configuration.ConfigEntry<KeyCode> entry, string help, string unbound = null)
        {
            HelpZone zone;
            LineLabel(parent, label, y, w - KitKeyBind.Width - Theme.Gap, help, out zone);
            KitKeyBind control = KitKeyBind.Create(parent, "Key_" + label, label, w - KitKeyBind.Width, y,
                entry.Value, key => { entry.Value = key; MarkClashes(); }, _art, unbound);
            Keys.Add(new KeyRow { Control = control, Group = group, Zone = zone, Help = help });
            Refreshers.Add(() => control.Set(entry.Value));
            return y + LineH + LineGap;
        }

        // The one-line dropdown. Its open list lives on `listParent`, above the
        // rows it falls over.
        private static float DropLine(Transform parent, Transform listParent, float y, float w, string label,
            string[] options, int index, System.Action<int> onChanged, string help)
        {
            HelpZone zone;
            LineLabel(parent, label, y, w - DropW - Theme.Gap, help, out zone);
            Drops.Add(KitDropdown.Create(parent, "Drop_" + label, w - DropW, y, DropW,
                options, Mathf.Max(0, index), onChanged, listParent: listParent, settingsArt: _art));
            return y + LineH + LineGap;
        }

        // Two of your keys on one key: both fields wear the invalid rim, and the
        // tooltip names the other.
        private static void MarkClashes()
        {
            for (int i = 0; i < Keys.Count; i++)
            {
                KeyRow a = Keys[i];
                string with = null;
                if (a.Control.Key != KeyCode.None)
                {
                    for (int j = 0; j < Keys.Count && with == null; j++)
                    {
                        KeyRow b = Keys[j];
                        if (j != i && b.Group == a.Group && b.Control.Key == a.Control.Key)
                        {
                            with = b.Control.Label;
                        }
                    }
                }
                a.Control.SetConflict(with);
                a.Zone.Text = with != null ? "Also bound to " + with + ". " + a.Help : a.Help;
            }
        }

        private static void RefreshAll()
        {
            for (int i = 0; i < Refreshers.Count; i++)
            {
                Refreshers[i]();
            }
            MarkClashes();
            _cursorStyle?.SetIndex((int)Settings.CursorStyle.Value);
        }

        // ---------- scrolling ----------

        private static bool _thumbDrag;
        private static float _thumbGrab;

        private static void BuildScrollbar(Page p, float contentW)
        {
            float x = contentW - ScrollW;
            Image track = _art.Image(p.Content, "ScrollTrack", _art.ScrollTrack);
            SettingsArt.SkinAtScale(track, _art.ScrollTrack, ScrollArtPixelsPerUnit);
            Theme.Place(track, x, HeadH, ScrollW, ViewH);
            // the sheet's bar has its thumb over the top of the track, so the
            // track's top end is its own piece
            Image cap = _art.Image(p.Content, "ScrollCap", _art.ScrollCap);
            Theme.Place(cap, x, HeadH, ScrollW, 34f / ScrollArtPixelsPerUnit);
            Image thumb = _art.Image(p.Content, "ScrollThumb", _art.ScrollThumb);
            thumb.raycastTarget = true;
            p.ThumbH = Mathf.Clamp(ViewH * ViewH / p.RowsH, 14f, ViewH);
            p.Thumb = Theme.Place(thumb, x, HeadH, ScrollW, p.ThumbH);
        }

        // The wheel anywhere over the window, or the thumb dragged.
        private static void TickScroll(Page p, Camera cam, bool overWindow)
        {
            if (!p.Scrolls)
            {
                return;
            }
            float max = p.RowsH - ViewH;
            float wheel = Input.mouseScrollDelta.y;
            if (overWindow && Mathf.Abs(wheel) > 0.01f)
            {
                p.Scroll -= wheel * (LineH + LineGap) * 2f;
            }
            Vector2 local;
            bool inside = RectTransformUtility.ScreenPointToLocalPointInRectangle(p.View, Input.mousePosition, cam, out local);
            float fromTop = -local.y;
            if (Input.GetMouseButtonDown(0) && Contains(p.Thumb, cam))
            {
                _thumbDrag = true;
                _thumbGrab = fromTop - (ViewH - p.ThumbH) * (p.Scroll / max);
            }
            if (_thumbDrag)
            {
                if (!Input.GetMouseButton(0))
                {
                    _thumbDrag = false;
                }
                else if (inside && ViewH > p.ThumbH)
                {
                    p.Scroll = (fromTop - _thumbGrab) / (ViewH - p.ThumbH) * max;
                }
            }
            p.Scroll = Mathf.Clamp(p.Scroll, 0f, max);
            p.Rows.anchoredPosition = new Vector2(0f, p.Scroll);
            Vector2 at = p.Thumb.anchoredPosition;
            p.Thumb.anchoredPosition = new Vector2(at.x, -(HeadH + (ViewH - p.ThumbH) * (p.Scroll / max)));
        }

        // ---------- pinned category tabs ----------

        // The selected paper slip carries its own pin and walnut side marker.
        private static void BuildSlip(Page p, int index)
        {
            var go = Owned.New("Slip_" + p.Name);
            go.transform.SetParent(_frame, false);
            p.Slip = (RectTransform)go.transform;
            p.SlipPaper = go.AddComponent<Image>();
            p.SlipPaper.raycastTarget = true;
            SettingsArt.Skin(p.SlipPaper, _art.Slip);
            RectTransform r = p.Slip;
            r.anchorMin = new Vector2(0f, 1f);
            r.anchorMax = new Vector2(0f, 1f);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = new Vector2(SlipW, SlipH);
            r.anchoredPosition = new Vector2(SlipX + SlipW * 0.5f, -(SlipTop + index * SlipPitch + SlipH * 0.5f));
            p.SlipText = _art.Text(r, "Text", 10f, Theme.KitInk,
                TextAlignmentOptions.MidlineLeft, bold: true);
            p.SlipText.enableWordWrapping = false;
            p.SlipText.text = p.Name;
            Theme.Place(p.SlipText, 13f, 1f, SlipW - 18f, SlipH - 2f);
            Help.Add(new HelpZone { Rect = r, Text = p.Name + " settings" });
        }

        private static void PaintSlips()
        {
            for (int i = 0; i < Pages.Count; i++)
            {
                Page p = Pages[i];
                if (p.Slip == null)
                {
                    continue;
                }
                bool on = i == _active;
                bool hot = ReferenceEquals(p, _hotTab);
                bool down = hot && ReferenceEquals(p, _pressedTab);
                SettingsArt.Skin(p.SlipPaper, on ? _art.SlipActive : _art.Slip);
                float k = down ? 0.85f : on || hot ? 1f : 0.91f;
                p.SlipPaper.color = new Color(k, k, k, 1f);
            }
        }

        // Only the active page's rows show.
        private static void Select(int index)
        {
            _active = index;
            for (int i = 0; i < Pages.Count; i++)
            {
                Page p = Pages[i];
                bool on = i == _active;
                if (p.Content.gameObject.activeSelf != on)
                {
                    p.Content.gameObject.SetActive(on);
                }
            }
            PaintSlips();
        }

        // Slips: hover, press, release, select.
        private static void TickTabs(Camera cam, bool live)
        {
            Page hot = null;
            if (live)
            {
                for (int i = 0; i < Pages.Count; i++)
                {
                    Page p = Pages[i];
                    if (i != _active && Contains(p.Slip, cam))
                    {
                        hot = p;
                        break;
                    }
                }
            }
            bool changed = !ReferenceEquals(hot, _hotTab);
            _hotTab = hot;
            if (Input.GetMouseButtonDown(0))
            {
                _pressedTab = hot;
                changed = true;
            }
            if (Input.GetMouseButtonUp(0))
            {
                if (_pressedTab != null && ReferenceEquals(_pressedTab, hot))
                {
                    Select(Pages.IndexOf(hot));
                }
                _pressedTab = null;
                changed = true;
            }
            if (changed)
            {
                PaintSlips();
            }
        }

        // ---------- close, pointer ----------

        private static bool TickClose(Camera cam)
        {
            bool over = Contains((RectTransform)_closeChip.transform, cam);
            if (Input.GetMouseButtonDown(0) && over)
            {
                _closePressed = true;
            }
            if (Input.GetMouseButtonUp(0))
            {
                bool fire = _closePressed && over;
                _closePressed = false;
                if (fire)
                {
                    Close();
                    return false;
                }
            }
            else if (_closePressed && !Input.GetMouseButton(0))
            {
                _closePressed = false;
            }
            float shade = over && _closePressed ? 0.8f : over ? 1f : 0.92f;
            _closeChip.color = new Color(shade, shade, shade, 1f);
            return over;
        }

        private static bool TickDone(Camera cam, bool live)
        {
            bool over = live && Contains(_done.rectTransform, cam);
            if (Input.GetMouseButtonDown(0)) { _donePressed = over; }
            if (Input.GetMouseButtonUp(0))
            {
                bool fire = _donePressed && over;
                _donePressed = false;
                if (fire) { Close(); return false; }
            }
            else if (!Input.GetMouseButton(0)) { _donePressed = false; }
            bool down = over && _donePressed;
            SettingsArt.Skin(_done, down ? _art.ButtonDown : over ? _art.ButtonHot : _art.Button);
            _doneText.color = over && !down ? Theme.KitInk : Theme.KitCream;
            return over;
        }

        private static void FitToCanvas()
        {
            if (_rootRect == null || _frame == null) { return; }
            Vector2 space = _rootRect.rect.size;
            if (space.x <= 0f || space.y <= 0f) { return; }
            float scale = Mathf.Min(1f, Mathf.Min((space.x - 20f) / W, (space.y - 20f) / _h));
            _frame.localScale = Vector3.one * Mathf.Max(0.1f, scale);
        }

        private static KitDropdown OpenDrop()
        {
            for (int i = 0; i < Drops.Count; i++)
            {
                if (Drops[i].IsOpen)
                {
                    return Drops[i];
                }
            }
            return null;
        }

        private static Camera CameraFor(RectTransform rect)
        {
            Canvas canvas = rect.GetComponentInParent<Canvas>();
            return canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
        }

        private static bool Contains(RectTransform rect, Camera cam)
        {
            return rect != null && rect.gameObject.activeInHierarchy
                && RectTransformUtility.RectangleContainsScreenPoint(rect, Input.mousePosition, cam);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
