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
        private const float SectionH = 18f;
        private const float RuleH = 0.35f;
        private const float DropW = 94f;
        private const float LanguageDropW = 136f;   // "Same as the game (now: English)" and its longer translations
        private const float SlipX = 32f;
        private const float SlipW = 85f;
        private const float SlipH = 30f;
        private const float SlipPitch = 36f;
        private const float SlipTop = 54f;
        private const float SlipBottom = 264f;    // where six slips end, at the column rule's foot
        private const float ColumnRuleX = 126f;
        private const float SlipsContentX = 138f;
        private const float ContentTop = 53f;
        // THE BODY IS DRAWN SMALLER THAN THE FRAME (Forgejo #155): the slips and the
        // pages hang in one layer at BodyScale, so their type and controls sit
        // nearer the game's own sizes while the window keeps its size. Inside it
        // everything is laid out in the layer's units, the frame's divided by
        // the scale; the frame's chrome - plaque, close, footer, Done - is not.
        private const float BodyScale = 0.75f;
        // A page's rows sit under its heading, in a view that ends above the footer
        // rule (182 frame units below the content's top). One-line rows: the
        // sentence about a setting is its tooltip.
        private const float HeadH = 49f;
        private const float ViewH = 182f / BodyScale - HeadH;
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
        private static RectTransform _body;   // the slips and the pages, at BodyScale
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
            public string Name;        // the id the code switches on; never shown
            public string Title;
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

        // the generation of Shared.Loc the window was built in: another and it is built again
        private static int _builtIn = -1;

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
            _body = null;
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
            // built in a language no longer wanted (the game's setting, or the harness) too
            if ((book != _book || book == Book.IslandEx || _builtIn != Shared.Loc.Generation) && _root != null)
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

        // UI.LocLabels: the language changed - an open window is built again, on its page
        internal static void LanguageChanged()
        {
            if (IsOpen)
            {
                Open(_book);
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
                _menuButton = CloneMenuButton(mn, ButtonName, ButtonLabel, 1, () => OnMenuButton(Book.IslandUI),
                    ModIcon(typeof(SettingsWindow).Assembly, "IslandUI.icon.png"));
            }
            if (_aiButton == null && PluginConfig(AiGuid) != null)
            {
                _aiButton = CloneMenuButton(mn, AiButtonName, AiButtonLabel, 2, () => OnMenuButton(Book.IslandAI),
                    ModIcon(PluginAssembly(AiGuid), "IslandAI.icon.png"));
            }
            if (_exButton == null && PluginConfig(ExGuid) != null)
            {
                _exButton = CloneMenuButton(mn, ExButtonName, ExButtonLabel, _aiButton != null ? 3 : 2,
                    () => OnMenuButton(Book.IslandEx), ModIcon(PluginAssembly(ExGuid), "IslandEx.icon.png"));
            }
        }

        // `after` is how many places below the game's Option entry the copy stands.
        private static GameObject CloneMenuButton(ManagersScript mn, string name, string label, int after,
            UnityEngine.Events.UnityAction onClick, Sprite icon)
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
            AddMenuIcon(clone, icon);
            clone.SetActive(true);
            Plugin.Log.LogInfo("Settings: menu button '" + label + "' cloned from '" + template.name + "'");
            return clone;
        }

        // The mod's title-screen icon (Shared/TitleBadge), outside the entry's left
        // edge: the vanilla entries are a column of centred words, and the icon
        // marks the ones a mod added without moving their label off that centre.
        private static void AddMenuIcon(GameObject button, Sprite icon)
        {
            if (icon == null) { return; }
            RectTransform box = button.GetComponent<RectTransform>();
            float size = box != null ? box.rect.height * 0.9f : 15f;
            var go = new GameObject("ModIcon", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(button.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = new Vector2(-size * 0.2f, 0f);
            Image image = go.GetComponent<Image>();
            image.sprite = icon;
            image.preserveAspect = true;
            image.raycastTarget = false;
        }

        // an embedded PNG of a mod's assembly, once per session; null when the mod or
        // its icon is missing, and then the entry is only its label
        private static readonly Dictionary<string, Sprite> MenuIcons = new Dictionary<string, Sprite>();

        private static Sprite ModIcon(System.Reflection.Assembly assembly, string resource)
        {
            if (assembly == null) { return null; }
            Sprite sprite;
            if (MenuIcons.TryGetValue(resource, out sprite) && sprite != null) { return sprite; }
            using (System.IO.Stream stream = assembly.GetManifestResourceStream(resource))
            {
                if (stream == null) { return null; }
                using (var bytes = new System.IO.MemoryStream())
                {
                    stream.CopyTo(bytes);
                    // mip-mapped: a 192 px icon shown at 15 px shimmers without them
                    var texture = new Texture2D(2, 2, TextureFormat.RGBA32, true);
                    if (!texture.LoadImage(bytes.ToArray())) { return null; }
                    texture.filterMode = FilterMode.Trilinear;
                    texture.wrapMode = TextureWrapMode.Clamp;
                    sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
                    sprite.name = resource;
                }
            }
            MenuIcons[resource] = sprite;
            return sprite;
        }

        private static System.Reflection.Assembly PluginAssembly(string guid)
        {
            BepInEx.PluginInfo info;
            return BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue(guid, out info) && info.Instance != null
                ? info.Instance.GetType().Assembly
                : null;
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
            _builtIn = Shared.Loc.Generation;
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
            _h = MinH;
            _frame.sizeDelta = new Vector2(W, _h);
            var bodyGo = Owned.New("Body");
            bodyGo.transform.SetParent(_frame, false);
            _body = Theme.Place(bodyGo.transform, 0f, 0f, W / BodyScale, _h / BodyScale);
            _body.localScale = Vector3.one * BodyScale;
            for (int i = 0; i < Pages.Count; i++)
            {
                Page p = Pages[i];
                var contentGo = Owned.New("Page_" + p.Name);
                contentGo.transform.SetParent(_body, false);
                p.Content = (RectTransform)contentGo.transform;
                p.Height = BuildPage(mn, p, contentW / BodyScale);
            }
            for (int i = 0; i < Pages.Count; i++)
            {
                Page p = Pages[i];
                Theme.Place(p.Content, SlipsContentX / BodyScale, ContentTop / BodyScale,
                    contentW / BodyScale, (_h - ContentTop - 48f) / BodyScale);
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
            // the footer at the body's size, centred where the larger one stood
            TextMeshProUGUI note = _art.Text(_frame, "ApplyNote", 7f * BodyScale,
                Theme.ParseColor("#70573D", Theme.KitInk), TextAlignmentOptions.MidlineLeft);
            note.text = Shared.Loc.Get("settings.apply_note", "Changes apply immediately.");
            Theme.Place(note, SlipsContentX, _h - 51f, contentW - 78f, 21f);
            _done = _art.Image(_frame, "Done", _art.Button);
            _done.raycastTarget = true;
            const float doneW = 68f * BodyScale, doneH = 21f * BodyScale;
            Theme.Place(_done, W - Inset - doneW, _h - 51f + (21f - doneH) * 0.5f, doneW, doneH);
            _doneText = _art.Text(_done.transform, "Text", 9f * BodyScale, Theme.KitCream,
                TextAlignmentOptions.Center, bold: true);
            _doneText.text = Shared.Loc.Get("settings.done", "Done");
            Stretch(_doneText.rectTransform);

            _tip = KitTooltip.Create(_rootRect, _art);
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
                Pages.Add(new Page
                {
                    // seven rows since Door assist: taller than the view, so it scrolls as
                    // Effects and Hotkeys do (the seventh ran over the footer)
                    Name = "Steering", Title = Shared.Loc.Get("settings.page.steering", "Steering"), Scrolls = true,
                    About = Shared.Loc.Get("settings.page.steering.about", "How your people and your enemies get around what is in their way."),
                });
                return;
            }
            if (_book == Book.IslandEx)
            {
                Pages.Add(new Page
                {
                    Name = "Effects", Title = Shared.Loc.Get("settings.page.effects", "Effects"), Scrolls = true,
                    About = Shared.Loc.Get("settings.page.effects.about", "Switch an effect on to hide it. An effect is listed once the game has shown it."),
                });
                Pages.Add(new Page
                {
                    Name = "Struggle", Title = Shared.Loc.Get("settings.page.struggle", "Struggle"),
                    About = Shared.Loc.Get("settings.page.struggle.about", "Holding the button in a grapple, instead of hammering it."),
                });
                Pages.Add(new Page
                {
                    Name = "Window", Title = Shared.Loc.Get("settings.page.window", "Window"),
                    About = Shared.Loc.Get("settings.page.window.about", "What the game window does when you look away from it."),
                });
                Pages.Add(new Page
                {
                    Name = "Weather", Title = Shared.Loc.Get("settings.page.weather", "Weather"),
                    About = Shared.Loc.Get("settings.page.weather.about", "Where the weather reaches you."),
                });
                Pages.Add(new Page
                {
                    Name = "Camera", Title = Shared.Loc.Get("settings.page.camera", "Camera"),
                    About = Shared.Loc.Get("settings.page.camera.about", "How far back the camera can stand."),
                });
                Pages.Add(new Page
                {
                    Name = "Tools", Title = Shared.Loc.Get("settings.page.tools", "Tools"),
                    About = Shared.Loc.Get("settings.page.tools.about", "What happens to a tool when it wears out."),
                });
                return;
            }
            // SEVEN PAGES, EACH IN GROUPS (Forgejo #155): nine slips crowded the
            // column, five of them over one or two rows; the small ones are now
            // groups on a page of their kind, under a sub-heading.
            Pages.Add(new Page
            {
                Name = "General", Title = Shared.Loc.Get("settings.page.general", "General"),
                About = Shared.Loc.Get("settings.page.general.about", "Your pointer, and the language the mods speak."),
            });
            Pages.Add(new Page
            {
                Name = "Target", Title = Shared.Loc.Get("settings.page.target", "Target"), Scrolls = true,
                About = Shared.Loc.Get("settings.page.target.about", "How the person or thing you are dealing with is marked."),
            });
            Pages.Add(new Page
            {
                Name = "Combat", Title = Shared.Loc.Get("settings.page.combat", "Combat"), Scrolls = true,
                About = Shared.Loc.Get("settings.page.combat.about", "Who carries a marker in a fight, and whom the plaque follows."),
            });
            Pages.Add(new Page
            {
                Name = "Notices", Title = Shared.Loc.Get("settings.page.notices", "Notices"), Scrolls = true,
                About = Shared.Loc.Get("settings.page.notices.about", "How the screen tells you what happens: low vitals, and what flies where."),
            });
            Pages.Add(new Page
            {
                Name = "Windows", Title = Shared.Loc.Get("settings.page.windows", "Windows"), Scrolls = true,
                About = Shared.Loc.Get("settings.page.windows.about", "Where Island UI's windows stand and what they show."),
            });
            Pages.Add(new Page
            {
                Name = "Story", Title = Shared.Loc.Get("settings.page.story", "Story"),
                About = Shared.Loc.Get("settings.page.story.about", "What the story's choices tell you before you pick."),
            });
            Pages.Add(new Page
            {
                Name = "Hotkeys", Title = Shared.Loc.Get("settings.page.hotkeys", "Hotkeys"), Scrolls = true,
                About = Shared.Loc.Get("settings.page.hotkeys.about", "Click a key, then press the new one. Esc cancels, Backspace clears."),
            });
        }

        private static float BuildPage(ManagersScript mn, Page p, float contentW)
        {
            Transform t = p.Content;
            Section(t, 0f, contentW, p.Title, p.About);
            GameObject viewGo = Owned.New("View");
            viewGo.transform.SetParent(t, false);
            p.View = Theme.Place(viewGo.transform, 0f, HeadH, contentW, ViewH);
            GameObject rowsGo = Owned.New("Rows");
            rowsGo.transform.SetParent(viewGo.transform, false);
            p.Rows = Theme.Place(rowsGo.transform, 0f, 0f, contentW, ViewH);

            Transform r = p.Rows;
            // every page leaves the scrollbar's lane, so the controls of all pages
            // end on one line whether a page scrolls or not
            float w = contentW - ScrollW - Theme.Gap;
            float y = 0f;
            switch (p.Name)
            {
                case "General":
                    y = DropLine(r, p.Content, y, w, Shared.Loc.Get("settings.page.cursor", "Cursor"), CursorStyleOptions(), (int)Settings.CursorStyle.Value,
                        index => { Settings.CursorStyle.Value = (GameCursor.Style)index; },
                        Shared.Loc.Get("settings.cursor.style.help", "Select Off to use the system pointer."));
                    _cursorStyle = Drops[Drops.Count - 1];
                    string[] codes = LanguageCodes();
                    y = DropLine(r, p.Content, y, w, Shared.Loc.Get("settings.language", "Language"), LanguageOptions(codes),
                        System.Array.IndexOf(codes, (Settings.Language.Value ?? "").Trim().ToLowerInvariant()),
                        index => { Settings.Language.Value = codes[index]; },
                        Shared.Loc.Get("settings.language.help", "Same as the game speaks whatever language the game's own options are set to; the brackets show which one that is right now. A fan translation over the game's English still shows as English there, so pick its language from the list. The translations are machine-made; corrections are welcome."),
                        LanguageDropW);
                    break;
                case "Target":
                    y = Group(r, y, w, Shared.Loc.Get("settings.group.marking", "Marking"));
                    y = ToggleRow(r, y, w, Shared.Loc.Get("settings.target.ring", "Ground ring"), Settings.TargetRing, null,
                        Shared.Loc.Get("settings.target.ring.help", "A ring on the ground under your target, in the colour of its marker:\nGreen  yours\nRed  hostile\nAmber  a stranger or a thing\nGrey  a body"));
                    y = SliderRow(r, y, w, Shared.Loc.Get("settings.target.ring_size", "Ring size"), Settings.TargetRingScale, 0.5f, 2f, 0.1f, Percent, Settings.TargetRing,
                        Shared.Loc.Get("settings.target.ring_size.help", "How big the ring is, against what the target's footprint asks for."));
                    y = SliderRow(r, y, w, Shared.Loc.Get("settings.target.ring_opacity", "Ring opacity"), Settings.TargetRingAlpha, 0f, 1f, 0.05f, Percent, Settings.TargetRing,
                        Shared.Loc.Get("settings.target.ring_opacity.help", "How solid the ring is."));
                    y = ToggleRow(r, y, w, Shared.Loc.Get("settings.target.selection_pulse", "Selection pulse"), Settings.SelectionGlow, null,
                        Shared.Loc.Get("settings.target.selection_pulse.help", "The game's own pulsing white glow on the person you selected. Off by default: the marker and the ring already say who it is."));
                    y = ToggleRow(r, y, w, Shared.Loc.Get("settings.target.hover_health", "Health on hover"), Settings.HoverHealth, null,
                        Shared.Loc.Get("settings.target.hover_health.help", "Pointing at anyone with health, in a fight or not, shows the combat marker over their head for as long as the pointer stays on them. Not for one who already carries a marker: your target, or an enemy marked in a fight."));
                    y = Group(r, y, w, Shared.Loc.Get("settings.group.chests", "Chests"));
                    y = ToggleRow(r, y, w, Shared.Loc.Get("settings.target.containers_open", "Chests open at once"), Settings.ContainersOpen, null,
                        Shared.Loc.Get("settings.target.containers_open.help", "Targeting a chest, or anything else that is only storage, opens its inventory at once. Close it, and the next chest you switch to stays closed until you deselect.\nOff  press its Inventory button"));
                    y = ToggleRow(r, y, w, Shared.Loc.Get("settings.target.container_close_untargets", "Closing a chest deselects it"), Settings.ContainerCloseUntargets, null,
                        Shared.Loc.Get("settings.target.container_close_untargets.help", "Closing the inventory of a chest, or anything else that is only storage, also deselects it.\nOff  it stays your target with its inventory closed"));
                    break;
                case "Combat":
                    y = Group(r, y, w, Shared.Loc.Get("settings.group.markers", "Markers"));
                    y = SliderRow(r, y, w, Shared.Loc.Get("settings.target.combat_markers", "Combat markers"), Settings.CombatMarkers, 0, Settings.CombatMarkersAll, Count, null,
                        Shared.Loc.Get("settings.target.combat_markers.help", "In a fight, how many enemies carry a small health and stun marker over their head: the ones you hit last, then the nearest of those on you. Off the screen, the marker waits at its edge and points to them.\nNone  no markers\nAll  every enemy in the fight"));
                    y = ToggleRow(r, y, w, Shared.Loc.Get("settings.target.friendly_markers", "Friendly markers"),
                        () => Settings.FriendlyMarkers.Value, on => Settings.FriendlyMarkers.Value = on, () => Settings.CombatMarkers.Value > 0,
                        Shared.Loc.Get("settings.target.friendly_markers.help", "In a fight, your followers and your people who fight beside you carry the combat marker too, their health bar green. They have their own count, as many as the enemies' above, so an enemy never gives up its marker to one of yours."));
                    y = ToggleRow(r, y, w, Shared.Loc.Get("settings.target.always_friend_markers", "Always on your people"), Settings.AlwaysFriendMarkers, null,
                        Shared.Loc.Get("settings.target.always_friend_markers.help", "Every one of your people on the screen carries the combat marker, in a fight or not, their health bar green. Overrides the count above for them."));
                    y = ToggleRow(r, y, w, Shared.Loc.Get("settings.target.always_enemy_markers", "Always on enemies"), Settings.AlwaysEnemyMarkers, null,
                        Shared.Loc.Get("settings.target.always_enemy_markers.help", "Every hostile one on the screen carries the combat marker, in a fight or not: those that have come for you or yours. Overrides the count above for them."));
                    y = ToggleRow(r, y, w, Shared.Loc.Get("settings.target.always_neutral_markers", "Always on the neutral"), Settings.AlwaysNeutralMarkers, null,
                        Shared.Loc.Get("settings.target.always_neutral_markers.help", "Everyone on the screen who is neither yours nor hostile carries the combat marker, in a fight or not, their health bar amber. Overrides the count above for them."));
                    y = Group(r, y, w, Shared.Loc.Get("settings.group.plaque", "Plaque"));
                    y = ToggleRow(r, y, w, Shared.Loc.Get("settings.target.boss_first", "Boss first"), Settings.BossPriority, null,
                        Shared.Loc.Get("settings.target.boss_first.help", "In a fight, the combat plaque and the markers go to the boss first, from the moment its fight starts, and what it summons comes last, so hitting a summon never takes the plaque off the boss. Within each, the one you hit last, then the nearest on you.\nOff  the one you hit last, whoever it is"));
                    y = SliderRow(r, y, w, Shared.Loc.Get("settings.target.kill_linger", "Downed on the plaque"), Settings.KillLinger, 1f, 10f, 0.5f, Seconds, null,
                        Shared.Loc.Get("settings.target.kill_linger.help", "How long the combat plaque stays on an enemy you downed: its tab says Slain, or the face stays as it was when they only fainted. Anyone still in the fight takes the plaque sooner."));
                    break;
                case "Notices":
                    y = Group(r, y, w, Shared.Loc.Get("settings.page.vitals", "Vitals"));
                    y = ToggleRow(r, y, w, Shared.Loc.Get("settings.vitals.low", "Low warning"), Settings.LowVitals, null,
                        Shared.Loc.Get("settings.vitals.low.help", "A vital running low glows on the panel in its own shape:\nAmber  low, a slow breath\nRed  critical, a quicker pulse\nEach step worse flashes it once. Restored, it fades out."));
                    y = SliderRow(r, y, w, Shared.Loc.Get("settings.vitals.health_at", "Health low at"), Settings.LowHealthAt, 0.1f, 0.6f, 0.05f, Percent, Settings.LowVitals,
                        Shared.Loc.Get("settings.vitals.health_at.help", "Of your maximum health. Critical comes at half of it."));
                    y = SliderRow(r, y, w, Shared.Loc.Get("settings.vitals.needs_at", "Food and water low at"), Settings.LowNeedsAt, 0.1f, 0.5f, 0.05f, Percent, Settings.LowVitals,
                        Shared.Loc.Get("settings.vitals.needs_at.help", "Of the full bar. Critical comes at half of it, and empty flashes once more: the game then stops healing you and drains your health."));
                    y = Group(r, y, w, Shared.Loc.Get("settings.group.flights", "Flights"));
                    y = ToggleRow(r, y, w, Shared.Loc.Get("settings.target.pickup_flight", "Pickup flight"), Settings.PickupFlight, null,
                        Shared.Loc.Get("settings.target.pickup_flight.help", "A picked-up item's notice flies from you to the slot it landed in, instead of waiting at the edge of the screen."));
                    y = SliderRow(r, y, w, Shared.Loc.Get("settings.target.flight_time", "Flight time"), Settings.PickupFlightSeconds, 0.2f, 1.2f, 0.05f, Seconds, Settings.PickupFlight,
                        Shared.Loc.Get("settings.target.flight_time.help", "How long the notice takes to fly.\nUnder about 0.35 s  the count cannot be read in flight\nOver 0.8 s  it feels like waiting"));
                    y = ToggleRow(r, y, w, Shared.Loc.Get("settings.target.npc_use_flight", "Your people eat and drink"), Settings.NpcUseFlight, null,
                        Shared.Loc.Get("settings.target.npc_use_flight.help", "When one of your people eats from a food box or drinks from a well, the food or water icon flies from it to them, as it does to you."));
                    break;
                case "Windows":
                    y = Group(r, y, w, Shared.Loc.Get("settings.page.sheets", "Sheets"));
                    y = ToggleRow(r, y, w, Shared.Loc.Get("settings.sheets.left", "Sheet to the left"), Settings.SheetFloatLeft, null,
                        Shared.Loc.Get("settings.sheets.left.help", "A character's sheet moves to the left side while crafting, the vicinity and the roster are closed, and back when one opens. The tab on the sheet's edge switches this too."));
                    y = Group(r, y, w, Shared.Loc.Get("settings.page.vicinity", "Vicinity"));
                    y = ToggleRow(r, y, w, Shared.Loc.Get("settings.vicinity.compact", "Compact"), Settings.VicinityCompact, null,
                        Shared.Loc.Get("settings.vicinity.compact.help", "Each holder shows its taken slots in their own order, then one count of how many are free, instead of every slot. The Compact box at the foot of the vicinity window sets this too."));
                    y = ToggleRow(r, y, w, Shared.Loc.Get("settings.vicinity.hold_order", "Hold the order"), Settings.VicinityHoldOrder, null,
                        Shared.Loc.Get("settings.vicinity.hold_order.help", "While the pointer is on the list, the rows keep their places, so none moves under the pointer. A row that has come nearer than one above it wears an arrow, and the list re-sorts once the pointer leaves.\nOff  always nearest first"));
                    y = Group(r, y, w, Shared.Loc.Get("settings.group.item_card", "Item card"));
                    y = ToggleRow(r, y, w, Shared.Loc.Get("settings.items.guide", "How to get it"), Settings.ItemGuide, null,
                        Shared.Loc.Get("settings.items.guide.help", "The card says how to get the item: where it is found, what drops it, who trades it. Written from what players have collected, so it covers the dungeon, chests and trades too."));
                    y = Group(r, y, w, Shared.Loc.Get("settings.group.action_bar", "Action bar"));
                    y = ToggleRow(r, y, w, Shared.Loc.Get("settings.action_bar.shift", "Shift to move"), Settings.ActionBarShiftToMove, null,
                        Shared.Loc.Get("settings.action_bar.shift.help", "Moving an action bar slot, or a tool on the belt, needs shift held, so a slip of the mouse can't lose one. Off: drag them freely, and drop one off the bar to remove it. Shift+click removes one either way."));
                    break;
                case "Story":
                    y = DropLine(r, p.Content, y, w, Shared.Loc.Get("settings.story.hints", "Story hints"), HintOptions(), (int)Settings.StoryHints.Value,
                        index => { Settings.StoryHints.Value = (HintMode)index; },
                        Shared.Loc.Get("settings.story.hints.help", "Each option of a story choice, and each key of a scene that waits for one, says what it leads to: affection, who joins, what unlocks. On click, they wait behind a bar until you click it. Deaths, endings and partings are always masked until clicked."));
                    break;
                case "Hotkeys":
                    y = Group(r, y, w, "Island UI");
                    y = KeyLine(r, y, w, 0, Shared.Loc.Get("settings.hotkeys.inventory", "Inventory"), Settings.InventoryKey, Shared.Loc.Get("settings.hotkeys.inventory.help", "Opens and closes the inventory window."));
                    y = KeyLine(r, y, w, 0, Shared.Loc.Get("settings.hotkeys.vicinity", "Vicinity"), Settings.ReachKey, Shared.Loc.Get("settings.hotkeys.vicinity.help", "Opens and closes the vicinity window: the storage within reach."));
                    y = KeyLine(r, y, w, 0, Shared.Loc.Get("settings.hotkeys.crafting", "Crafting"), QoL.OpenCraftingMenuKey, Shared.Loc.Get("settings.hotkeys.crafting.help", "Opens and closes the crafting window."));
                    y = KeyLine(r, y, w, 0, Shared.Loc.Get("settings.hotkeys.quick_save", "Quick save"), QoL.QuickSaveKey, Shared.Loc.Get("settings.hotkeys.quick_save.help", "Saves to the slot this game was loaded from."));
                    y = KeyLine(r, y, w, 0, Shared.Loc.Get("settings.hotkeys.target", "Target"), Settings.TargetKey, Shared.Loc.Get("settings.hotkeys.target.help", "Targets what is nearest the cursor without walking to it; pressed again, the next nearest."));
                    y = KeyLine(r, y, w, 0, Shared.Loc.Get("settings.hotkeys.target_sheet", "Target's sheet"), Settings.SheetKey, Shared.Loc.Get("settings.hotkeys.target_sheet.help", "Opens and closes the sheet of whoever is your target."));
                    y = KeyLine(r, y, w, 0, Shared.Loc.Get("settings.hotkeys.world_focus", "Back to the world"), Settings.WorldFocusKey, Shared.Loc.Get("settings.hotkeys.world_focus.help", "Closes the windows Island UI owns, and the map and quest overlays."));
                    y = KeyLine(r, y, w, 0, Shared.Loc.Get("settings.hotkeys.close_all", "Close everything"), Settings.CloseAllKey, Shared.Loc.Get("settings.hotkeys.close_all.help", "Closes every open window, Island UI's first, then the game's."));
                    y = KeyLine(r, y, w, 0, Shared.Loc.Get("settings.hotkeys.console", "Console"), Settings.ConsoleKey, Shared.Loc.Get("settings.hotkeys.console.help", "Drops the command console down from the top of the screen."),
                        Shared.Loc.Get("settings.hotkeys.console.unbound", "Left of 1"));
                    y = KeyLine(r, y, w, 0, Shared.Loc.Get("settings.hotkeys.quick_stack", "Quick stack"), Settings.QuickStackKey, Shared.Loc.Get("settings.hotkeys.quick_stack.help", "Deposits stackable items into nearby storages set to Stack."));
                    y = KeyLine(r, y, w, 0, Shared.Loc.Get("settings.hotkeys.cursor_cycle", "Next cursor"), Settings.CursorCycleKey, Shared.Loc.Get("settings.hotkeys.cursor_cycle.help", "Steps through the cursor styles, so the choice can be made by looking."));
                    y = Group(r, y + 2f, w, Shared.Loc.Get("settings.hotkeys.group.game", "The game's keys"));
                    for (int i = 0; i < Bindings.All.Count; i++)
                    {
                        Bindings.Binding b = Bindings.All[i];
                        if (!ReferenceEquals(b.Entry, QoL.OpenCraftingMenuKey) && !ReferenceEquals(b.Entry, QoL.QuickSaveKey))
                        {
                            y = KeyLine(r, y, w, 1, GameKeyWords(b.Label), b.Entry, GameKeyHelp(b));
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
                case "Camera":
                case "Tools":
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
                y = ToggleRow(r, y, w, Shared.Loc.Get("settings.steering.enabled", "Island AI"), enabled, null,
                    Shared.Loc.Get("settings.steering.enabled.help", "The whole mod, live: off and everyone moves exactly as in the unmodded game."));
            }
            var key = new BepInEx.Configuration.ConfigDefinition("Steering", "Method");
            if (cfg.ContainsKey(key))
            {
                BepInEx.Configuration.ConfigEntryBase method = cfg[key];
                string[] names = System.Enum.GetNames(method.SettingType);
                var words = new string[names.Length];
                for (int i = 0; i < names.Length; i++)
                {
                    words[i] = MethodWords(names[i]);
                }
                y = DropLine(r, p.Content, y, w, Shared.Loc.Get("settings.steering.method", "Method"), words,
                    System.Array.IndexOf(names, method.BoxedValue.ToString()),
                    index => { method.BoxedValue = System.Enum.Parse(method.SettingType, names[index]); },
                    Shared.Loc.Get("settings.steering.method.help", "How a blocked walker picks its way:\nAlong walls  also tries the direction of the wall it met, and takes the heading with the most room nearest its target; it finds narrow passages\nFan  the first cut: the nearest of eleven fixed headings that is completely clear"));
            }
            if (enemies != null)
            {
                y = ToggleRow(r, y, w, Shared.Loc.Get("settings.steering.enemies", "Enemies steer"), enemies, enabled,
                    Shared.Loc.Get("settings.steering.enemies.help", "Enemies walk around obstacles instead of pushing into them: chasing, wandering, running away. Walls defend less."));
            }
            if (friends != null)
            {
                y = ToggleRow(r, y, w, Shared.Loc.Get("settings.steering.friends", "Your people steer"), friends, enabled,
                    Shared.Loc.Get("settings.steering.friends.help", "Followers and villagers walk around obstacles outdoors: following you, chasing an enemy, wandering, walking to a house at night."));
            }
            if (probe != null)
            {
                y = SliderRow(r, y, w, Shared.Loc.Get("settings.steering.look_ahead", "Look ahead"), probe, 0.5f, 6f, 0.25f, Metres, enabled,
                    Shared.Loc.Get("settings.steering.look_ahead.help", "How far ahead a walker looks for something in its way."));
            }
            if (clearance != null)
            {
                y = SliderRow(r, y, w, Shared.Loc.Get("settings.steering.clearance", "Clearance"), clearance, 0.8f, 1.3f, 0.05f, Percent, enabled,
                    Shared.Loc.Get("settings.steering.clearance.help", "How wide the look-ahead is against the walker's own body.\nAbove 100%  rounds corners with room to spare, refuses tight gaps\nBelow 100%  squeezes into gaps barely wider than the body, brushes corners"));
            }
            if (doors != null)
            {
                y = ToggleRow(r, y, w, Shared.Loc.Get("settings.steering.door_assist", "Door assist"), doors, enabled,
                    Shared.Loc.Get("settings.steering.door_assist.help", "Your people leaving a house get out when they reach its door. The Native House's exit sits mostly inside its wall, and without this they can stand beside it for good."));
            }
            return y;
        }

        // Island Ex's Struggle, Window, Weather, Camera and Tools pages, from its settings file, the same way.
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
                    y = ToggleRow(r, y, w, Shared.Loc.Get("settings.struggle.hold", "Hold to struggle"), hold, null,
                        Shared.Loc.Get("settings.struggle.hold.help", "Hold the attack button in a grapple and it keeps hitting. Unmodded, holding only repeats once skill 9 is levelled; until then you have to hammer the button."));
                }
                if (interval != null)
                {
                    y = SliderRow(r, y, w, Shared.Loc.Get("settings.struggle.interval", "Hit every"), interval, 0.05f, 1f, 0.05f, Seconds, hold,
                        Shared.Loc.Get("settings.struggle.interval.help", "Time between hits while held. Never slower than your skill already allows.\n0.10 s  about a fast hammering, and what the game gives at full skill"));
                }
                return y;
            }
            if (page == "Weather")
            {
                BepInEx.Configuration.ConfigEntry<bool> dry;
                cfg.TryGetEntry("Weather", "Dry Indoors", out dry);
                if (dry != null)
                {
                    y = ToggleRow(r, y, w, Shared.Loc.Get("settings.weather.dry_indoors", "Dry indoors"), dry, null,
                        Shared.Loc.Get("settings.weather.dry_indoors.help", "No rain falls inside a house. Outside it still rains, and you see it again at the door. Unmodded, the rain follows you in."));
                }
                return y;
            }
            if (page == "Camera")
            {
                BepInEx.Configuration.ConfigEntry<float> zoom;
                cfg.TryGetEntry("Camera", "Zoom Out", out zoom);
                if (zoom != null)
                {
                    y = SliderRow(r, y, w, Shared.Loc.Get("settings.camera.zoom_out", "Zoom out"), zoom, 1f, 4f, 0.25f, Percent, null,
                        Shared.Loc.Get("settings.camera.zoom_out.help", "How far the mouse wheel pulls the camera back, against the game's own farthest. The camera draws as far as it stands back, so a far zoom costs frames.\n100%  as in the unmodded game"));
                }
                return y;
            }
            if (page == "Tools")
            {
                BepInEx.Configuration.ConfigEntry<bool> broken;
                cfg.TryGetEntry("Tools", "Keep Broken Tools", out broken);
                if (broken != null)
                {
                    y = ToggleRow(r, y, w, Shared.Loc.Get("settings.tools.keep_broken", "Keep broken tools"), broken, null,
                        Shared.Loc.Get("settings.tools.keep_broken.help", "A tool, weapon or shield worn to nothing stays in your bag, broken: it cannot be equipped until a repair kit mends it. Unmodded, it vanishes."));
                }
                return y;
            }
            cfg.TryGetEntry("Window", "KeepFullscreenOnFocusLoss", out keep);
            cfg.TryGetEntry("Window", "MinimiseShortcuts", out shortcuts);
            if (keep != null)
            {
                y = ToggleRow(r, y, w, Shared.Loc.Get("settings.window.keep_fullscreen", "Stay on screen"), keep, null,
                    Shared.Loc.Get("settings.window.keep_fullscreen.help", "The game stays up when you switch desktop, Alt+Tab or click another monitor. Unmodded, it minimises itself."));
            }
            if (shortcuts != null)
            {
                y = ToggleRow(r, y, w, Shared.Loc.Get("settings.window.minimise_shortcuts", "Win+M minimises"), shortcuts, null,
                    Shared.Loc.Get("settings.window.minimise_shortcuts.help", "Win+M and Win+Down minimise the game, as they do other windows. A taskbar click and Win+D work either way."));
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
                y = ToggleRow(r, y, w, Shared.Loc.Get("settings.effects.filter", "Effect filter"), on, null,
                    Shared.Loc.Get("settings.effects.filter.help", "Off and every effect shows, as in the unmodded game. What is hidden below is kept for when it is on again."));
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
            y = Group(r, y + 4f, w, Shared.Loc.Get("settings.effects.group.hidden", "Hidden"));
            foreach (string name in hidden)
            {
                y = EffectRow(r, y, w, name, blocked, live);
            }
            y = Group(r, y + 4f, w, Shared.Loc.Get("settings.effects.group.shown", "Shown, newest first"));
            if (shown.Count == 0)
            {
                HelpZone zone;
                TextMeshProUGUI none = LineLabel(r, Shared.Loc.Get("settings.effects.none", "None yet. Play on and open this again."), y, w,
                    Shared.Loc.Get("settings.effects.none.help", "Effects are listed the first time the game shows them this session."), out zone);
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
            return ToggleRow(r, y, w, isId ? Shared.Loc.Get("settings.effects.by_id", "Effect {0}", id) : name,
                () => IsHidden(blocked.Value, name),
                hide => blocked.Value = hide ? Hide(blocked.Value, name) : Unhide(blocked.Value, name),
                live,
                isId ? Shared.Loc.Get("settings.effects.by_id.help", "Hidden by its number in the .cfg's [FX] Blocked list.")
                    : Shared.Loc.Get("settings.effects.by_name.help", "The game's own name for this effect. Hiding it changes only what you see."));
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
            return Shared.Loc.Get("settings.unit.percent", "{0}%", Mathf.RoundToInt(v * 100f));
        }

        private static string Count(float v)
        {
            int n = Mathf.RoundToInt(v);
            return n == 0 ? Shared.Loc.Get("settings.unit.none", "None")
                : n >= Settings.CombatMarkersAll ? Shared.Loc.Get("settings.unit.all", "All") : n.ToString();
        }

        private static string Seconds(float v)
        {
            return Shared.Loc.Get("settings.unit.seconds", "{0} s", v.ToString("0.00"));
        }

        private static string Metres(float v)
        {
            return Shared.Loc.Get("settings.unit.metres", "{0} m", v.ToString("0.0#"));
        }

        // The option lists, made with the window so they are in its language. A value
        // the switch does not know shows its identifier, as every one did before.
        private static string[] CursorStyleOptions()
        {
            var styles = (GameCursor.Style[])System.Enum.GetValues(typeof(GameCursor.Style));
            var words = new string[styles.Length];
            for (int i = 0; i < styles.Length; i++)
            {
                switch (styles[i])
                {
                    case GameCursor.Style.Off: words[i] = Shared.Loc.Get("settings.cursor.style.off", "Off"); break;
                    case GameCursor.Style.Arrow: words[i] = Shared.Loc.Get("settings.cursor.style.arrow", "Arrow"); break;
                    case GameCursor.Style.ArrowBold: words[i] = Shared.Loc.Get("settings.cursor.style.arrow_bold", "ArrowBold"); break;
                    case GameCursor.Style.Carved: words[i] = Shared.Loc.Get("settings.cursor.style.carved", "Carved"); break;
                    case GameCursor.Style.Spear: words[i] = Shared.Loc.Get("settings.cursor.style.spear", "Spear"); break;
                    case GameCursor.Style.Claw: words[i] = Shared.Loc.Get("settings.cursor.style.claw", "Claw"); break;
                    case GameCursor.Style.Crosshair: words[i] = Shared.Loc.Get("settings.cursor.style.crosshair", "Crosshair"); break;
                    case GameCursor.Style.Ring: words[i] = Shared.Loc.Get("settings.cursor.style.ring", "Ring"); break;
                    default: words[i] = styles[i].ToString(); break;
                }
            }
            return words;
        }

        // THE LANGUAGES ON OFFER (Forgejo #19): following the game, English, then each folder
        // shipped beside the DLL - ours in a fixed order, one a player dropped in after them -
        // and the override in force if it is none of those (the pseudo-language). The
        // [Language] Override is the code; "" follows the game.
        private static readonly string[] Shipped = { "jp", "cn", "ru", "ptbr", "es", "de" };

        private static string[] LanguageCodes()
        {
            var codes = new List<string> { "", "en" };
            var found = new List<string>();
            try
            {
                string root = System.IO.Path.Combine(System.IO.Path.Combine(BepInEx.Paths.PluginPath, "IslandUI"), "Localization");
                if (System.IO.Directory.Exists(root))
                {
                    foreach (string dir in System.IO.Directory.GetDirectories(root))
                    {
                        string code = System.IO.Path.GetFileName(dir).ToLowerInvariant();
                        if (code != "en" && System.IO.File.Exists(System.IO.Path.Combine(dir, "strings.txt")))
                        {
                            found.Add(code);
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning("Settings: could not list the languages - " + e.Message);
            }
            foreach (string code in Shipped)
            {
                if (found.Remove(code))
                {
                    codes.Add(code);
                }
            }
            found.Sort(System.StringComparer.Ordinal);
            codes.AddRange(found);
            string current = (Settings.Language.Value ?? "").Trim().ToLowerInvariant();
            if (!codes.Contains(current))
            {
                codes.Add(current);
            }
            return codes.ToArray();
        }

        private static string[] LanguageOptions(string[] codes)
        {
            var words = new string[codes.Length];
            for (int i = 0; i < codes.Length; i++)
            {
                words[i] = codes[i].Length == 0
                    ? Shared.Loc.Get("settings.language.same_as_game", "Same as the game (now: {0})", LanguageName(Shared.Loc.GameLanguageCode))
                    : LanguageName(codes[i]);
            }
            return words;
        }

        // each language by its own name, as a player looking for theirs reads it
        private static string LanguageName(string code)
        {
            switch (code)
            {
                case "en": return "English";
                case "jp": return "日本語";
                case "cn": return "简体中文";
                case "kr": return "한국어";
                case "ru": return "Русский";
                case "ptbr": return "Português (BR)";
                case "es": return "Español";
                case "de": return "Deutsch";
                case "fr": return "Français";
                default: return code;
            }
        }

        // in HintMode's order
        private static string[] HintOptions()
        {
            return new[]
            {
                Shared.Loc.Get("settings.story.hints.off", "Off"),
                Shared.Loc.Get("settings.story.hints.click", "On click"),
                Shared.Loc.Get("settings.story.hints.always", "Always"),
            };
        }

        // Island AI's SteerMethod, by name: this mod cannot see that enum
        private static string MethodWords(string name)
        {
            switch (name)
            {
                case "AlongWalls": return Shared.Loc.Get("settings.steering.method.along_walls", "Along walls");
                case "Fan": return Shared.Loc.Get("settings.steering.method.fan", "Fan");
                default: return Words(name);
            }
        }

        // The game's keys are registered under English labels that are also their .cfg
        // keys (QoL.RemapTargets), so they are put into words here, where they are shown.
        private static string GameKeyWords(string label)
        {
            switch (label)
            {
                case "Move Forward": return Shared.Loc.Get("keymap.move_forward", "Move Forward");
                case "Move Left": return Shared.Loc.Get("keymap.move_left", "Move Left");
                case "Move Back": return Shared.Loc.Get("keymap.move_back", "Move Back");
                case "Move Right": return Shared.Loc.Get("keymap.move_right", "Move Right");
                case "Jump / Confirm / Skip Dialog": return Shared.Loc.Get("keymap.jump", "Jump / Confirm / Skip Dialog");
                case "Menu": return Shared.Loc.Get("keymap.menu", "Menu");
                case "Action (hold)": return Shared.Loc.Get("keymap.action", "Action (hold)");
                case "Carry / Pickup": return Shared.Loc.Get("keymap.carry", "Carry / Pickup");
                case "Interact": return Shared.Loc.Get("keymap.interact", "Interact");
                case "Context Action / Mount": return Shared.Loc.Get("keymap.context", "Context Action / Mount");
                case "Disassemble Animal": return Shared.Loc.Get("keymap.disassemble", "Disassemble Animal");
                case "Drop Item / Close Menu": return Shared.Loc.Get("keymap.drop", "Drop Item / Close Menu");
                case "Backpack": return Shared.Loc.Get("keymap.backpack", "Backpack");
                case "Map": return Shared.Loc.Get("keymap.map", "Map");
                case "Toggle Run": return Shared.Loc.Get("keymap.run", "Toggle Run");
                case "Right-Click Substitute": return Shared.Loc.Get("keymap.right_click", "Right-Click Substitute");
                case "Drone: Hide Model": return Shared.Loc.Get("keymap.drone_hide", "Drone: Hide Model");
                case "Prev Character / Slow Time": return Shared.Loc.Get("keymap.prev_character", "Prev Character / Slow Time");
                case "Next Character / Speed Time": return Shared.Loc.Get("keymap.next_character", "Next Character / Speed Time");
                case "Mute Audio": return Shared.Loc.Get("keymap.mute", "Mute Audio");
                case "Modifier: Bulk": return Shared.Loc.Get("keymap.mod_bulk", "Modifier: Bulk");
                case "Modifier: Split/Bulk+": return Shared.Loc.Get("keymap.mod_split", "Modifier: Split/Bulk+");
                case "Modifier: Alt Behavior": return Shared.Loc.Get("keymap.mod_alt", "Modifier: Alt Behavior");
                case "Pause": return Shared.Loc.Get("keymap.pause", "Pause");
                case "Confirm / Submit": return Shared.Loc.Get("keymap.confirm", "Confirm / Submit");
                default: return label;   // another mod's binding, in its own words
            }
        }

        // the .cfg's description, said again in the window's language
        private static string GameKeyHelp(Bindings.Binding b)
        {
            if (b.Entry.Definition.Section != "Keymap")
            {
                return b.Entry.Description.Description;   // another mod's, in its own words
            }
            string help = Shared.Loc.Get("keymap.help", "The game's own key is {0}. None = no key.", b.Entry.DefaultValue);
            switch (b.Label)
            {
                case "Drone: Hide Model": return help + " " + Shared.Loc.Get("keymap.drone_hide.note", "Only used while flying the drone.");
                case "Confirm / Submit": return help + " " + Shared.Loc.Get("keymap.confirm.note", "Changing it can break dialogs.");
                default: return help;
            }
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
            // a longer language shrinks it to the line rather than past the frame
            subtitle.enableWordWrapping = false;
            subtitle.enableAutoSizing = true;
            subtitle.fontSizeMin = 5f;
            subtitle.fontSizeMax = 8f;
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
        // A group after rows stands a little apart from them; the room comes out
        // of the gap under its own rule, so a page is no taller for its groups.
        private static float Group(Transform parent, float y, float w, string title)
        {
            if (y > 0f) { y += 2f; }
            TextMeshProUGUI text = _art.Text(parent, "Group_" + title, 8f,
                Theme.ParseColor("#70573D", Theme.KitInk), TextAlignmentOptions.MidlineLeft, bold: true);
            text.overflowMode = TextOverflowModes.Overflow;
            text.text = title;
            Theme.Place(text, 0f, y, w, 11f);
            Rule(parent, "GroupRule_" + title, 0f, y + 12f, w, vertical: false);
            return y + 12f + LineGap - 2f;
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
            return SliderRow(parent, y, w, label, entry.ConfigFile, () => entry.Value, v => entry.Value = v,
                min, max, step, format, dependsOn, help);
        }

        // ...over a whole number, in steps of one
        private static float SliderRow(Transform parent, float y, float w, string label,
            BepInEx.Configuration.ConfigEntry<int> entry, int min, int max,
            System.Func<float, string> format, BepInEx.Configuration.ConfigEntry<bool> dependsOn, string help)
        {
            return SliderRow(parent, y, w, label, entry.ConfigFile, () => entry.Value, v => entry.Value = Mathf.RoundToInt(v),
                min, max, 1f, format, dependsOn, help);
        }

        private static float SliderRow(Transform parent, float y, float w, string label,
            BepInEx.Configuration.ConfigFile file, System.Func<float> get, System.Action<float> set,
            float min, float max, float step,
            System.Func<float, string> format, BepInEx.Configuration.ConfigEntry<bool> dependsOn, string help)
        {
            HelpZone zone;
            TextMeshProUGUI name = LineLabel(parent, label, y, w - DropW - Theme.Gap, help, out zone);
            KitSlider slider = KitSlider.Create(parent, "Slider_" + label, w - DropW,
                y + (LineH - KitSlider.Height) * 0.5f, DropW, min, max, step, get(), format,
                v =>
                {
                    // BepInEx writes the file on every set; a drag is dozens of them
                    bool saves = file.SaveOnConfigSet;
                    file.SaveOnConfigSet = false;
                    set(v);
                    file.SaveOnConfigSet = saves;
                },
                // let go, a row that hangs on this number greys or wakes
                () => { file.Save(); RefreshAll(); }, _art);
            Sliders.Add(slider);
            Refreshers.Add(() =>
            {
                bool live = dependsOn == null || dependsOn.Value;
                if (!slider.Dragging)
                {
                    slider.Set(get());
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
            string[] options, int index, System.Action<int> onChanged, string help, float dropW = DropW)
        {
            HelpZone zone;
            LineLabel(parent, label, y, w - dropW - Theme.Gap, help, out zone);
            Drops.Add(KitDropdown.Create(parent, "Drop_" + label, w - dropW, y, dropW,
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
                a.Zone.Text = with != null ? Shared.Loc.Get("settings.hotkeys.clash", "Also bound to {0}. {1}", with, a.Help) : a.Help;
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
            go.transform.SetParent(_body, false);
            p.Slip = (RectTransform)go.transform;
            p.SlipPaper = go.AddComponent<Image>();
            p.SlipPaper.raycastTarget = true;
            SettingsArt.Skin(p.SlipPaper, _art.Slip);
            RectTransform r = p.Slip;
            r.anchorMin = new Vector2(0f, 1f);
            r.anchorMax = new Vector2(0f, 1f);
            r.pivot = new Vector2(0.5f, 0.5f);
            // in the body's units: as wide as the column, at their drawn height,
            // tightening only if more than fit run past the rule
            float x = SlipX / BodyScale, w = SlipW / BodyScale, top = SlipTop / BodyScale;
            float h = SlipH;
            float pitch = Pages.Count > 1 ? Mathf.Min(SlipPitch, (SlipBottom / BodyScale - top - h) / (Pages.Count - 1)) : SlipPitch;
            r.sizeDelta = new Vector2(w, h);
            r.anchoredPosition = new Vector2(x + w * 0.5f, -(top + index * pitch + h * 0.5f));
            p.SlipText = _art.Text(r, "Text", 10f, Theme.KitInk,
                TextAlignmentOptions.MidlineLeft, bold: true);
            p.SlipText.enableWordWrapping = false;
            p.SlipText.enableAutoSizing = true;
            p.SlipText.fontSizeMin = 7.5f;
            p.SlipText.fontSizeMax = 10f;
            p.SlipText.text = p.Title;
            Theme.Place(p.SlipText, 13f, 1f, w - 18f, h - 2f);
            Help.Add(new HelpZone { Rect = r, Text = Shared.Loc.Get("settings.page.tip", "{0} settings", p.Title) });
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
