using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI.UI
{
    // A themed button with POLLED input: the owner calls Tick with the pointer's
    // local position in the button's parent space (EventSystem raycasts proved
    // unreliable inside our panels — polling is the pattern that works, same as
    // the craft grid's cells).
    internal sealed class UIButton
    {
        public GameObject Go;
        public RectTransform Rect;
        public Image Background;
        public TextMeshProUGUI Label; // null on template-cloned buttons
        public Action OnClick;
        public bool Interactable = true;
        public Vector2 Size; // EFFECTIVE size (rect × scale) — use for layout/hit

        private bool _hovered;
        private bool _pressed;
        private CanvasGroup _group;   // template-cloned buttons fade as a whole
        private Graphic[] _tintTargets;  // press darkening, vanilla-style
        private Color[] _tintOriginals;
        private Vector2 _basePos;
        private bool _basePosSet;
        private TextMeshProUGUI _hint; // modifier feedback ("x10", "Max")

        public static UIButton Create(Transform parent, string label, float x, float y,
            float width, float height, Action onClick)
        {
            var button = new UIButton();
            button.Go = Owned.New($"Button_{label}");
            button.Go.transform.SetParent(parent, false);
            // solid dark bar like the game's tab buttons — the slot sprite's
            // decorative corners fall apart when sliced to button proportions
            button.Background = button.Go.AddComponent<Image>();
            button.Background.raycastTarget = false;
            button.Rect = Theme.Place(button.Go.GetComponent<RectTransform>(), x, y, width, height);
            button.Size = new Vector2(width, height);

            button.Label = Theme.MakeText(button.Go.transform, "Label",
                Theme.FontBody, Theme.TextCream, TextAlignmentOptions.Center);
            button.Label.text = label;
            button.Label.enableWordWrapping = false;
            var labelRect = (RectTransform)button.Label.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            button.OnClick = onClick;
            button.Refresh();
            return button;
        }

        // clone an existing game button's VISUALS (native look for free); falls
        // back to a text button when the template is missing
        public static UIButton CreateFromTemplate(Transform parent, GameObject template,
            string fallbackLabel, float x, float y, Action onClick, float scale = 1f)
        {
            if (template == null)
            {
                return Create(parent, fallbackLabel, x, y, 42f, 18f, onClick);
            }
            var button = new UIButton();
            button.Go = Owned.Instantiate(template, parent, false);
            button.Go.name = "TemplateButton";
            StripInteractive(button.Go);
            foreach (Transform child in button.Go.transform)
            {
                StripInteractive(child.gameObject);
            }
            button.Go.SetActive(true);
            button.Background = button.Go.GetComponent<Image>();
            Vector2 size = ((RectTransform)button.Go.transform).sizeDelta;
            button.Rect = Theme.Place(button.Go.GetComponent<RectTransform>(), x, y, size.x, size.y);
            // scale via transform (fixed-size children shrink with it); layout
            // and hit-testing use the EFFECTIVE size
            button.Rect.localScale = new Vector3(scale, scale, 1f);
            button.Size = size * scale;
            button._group = button.Go.GetComponent<CanvasGroup>() ?? button.Go.AddComponent<CanvasGroup>();
            button._group.alpha = 1f;
            button._group.interactable = true;
            button._group.blocksRaycasts = false;
            // vanilla-style press: the whole button darkens while held
            button._tintTargets = button.Go.GetComponentsInChildren<Graphic>(true);
            button._tintOriginals = new Color[button._tintTargets.Length];
            for (int i = 0; i < button._tintTargets.Length; i++)
            {
                button._tintOriginals[i] = button._tintTargets[i].color;
            }
            button.OnClick = onClick;
            button.Refresh();
            return button;
        }

        private static void StripInteractive(GameObject go)
        {
            foreach (Component comp in go.GetComponents<Component>())
            {
                bool keep = comp is RectTransform || comp is CanvasRenderer || comp is Image
                    || comp is Shadow // includes Outline
                    || comp is TextMeshProUGUI || comp is UnityEngine.UI.Text
                    || comp is CanvasGroup;
                if (!keep)
                {
                    UnityEngine.Object.DestroyImmediate(comp);
                }
            }
        }

        public void SetInteractable(bool on)
        {
            if (Interactable != on)
            {
                Interactable = on;
                Refresh();
            }
        }

        // top-left placement (y positive down, same convention as Theme.Place);
        // the press feedback offsets from this base
        public void SetPosition(Vector2 topLeft)
        {
            _basePos = new Vector2(topLeft.x, -topLeft.y);
            _basePosSet = true;
            Rect.anchoredPosition = _basePos;
        }

        // modifier feedback shown over the button ("x10", "Max"); null hides
        public void SetHint(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                if (_hint != null && _hint.gameObject.activeSelf)
                {
                    _hint.gameObject.SetActive(false);
                }
                return;
            }
            if (_hint == null)
            {
                _hint = Theme.MakeText(Go.transform, "Hint", Theme.FontHeader,
                    Theme.AccentGold, TextAlignmentOptions.Center);
                _hint.fontStyle = TMPro.FontStyles.Bold;
                _hint.enableWordWrapping = false;
                // centered on the button but WIDER than it: long values (x100)
                // overflow the icon instead of clipping
                _hint.overflowMode = TMPro.TextOverflowModes.Overflow;
                var rect = (RectTransform)_hint.transform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(80f, 16f);
            }
            _hint.text = text;
            if (!_hint.gameObject.activeSelf)
            {
                _hint.gameObject.SetActive(true);
            }
        }

        // localPoint: pointer position in the button's PARENT rect space (top-left
        // negative-y convention, same as Theme.Place). Vanilla-style interaction:
        // press shows the depress+shadow, the ACTION fires on mouse UP inside;
        // dragging off while held cancels.
        public void Tick(Vector2 localPoint, bool mouseDown, bool mouseHeld, bool mouseUp)
        {
            Vector2 pos = _basePosSet ? _basePos : Rect.anchoredPosition; // (x, -y)
            bool inside = localPoint.x >= pos.x && localPoint.x <= pos.x + Size.x
                && localPoint.y <= pos.y && localPoint.y >= pos.y - Size.y;
            if (inside != _hovered)
            {
                _hovered = inside;
                Refresh();
            }
            if (inside && mouseDown && Interactable)
            {
                SetPressed(true);
            }
            if (mouseUp)
            {
                bool fire = _pressed && inside && Interactable && OnClick != null;
                SetPressed(false);
                if (fire)
                {
                    OnClick();
                }
            }
            else if (_pressed && (!inside || !mouseHeld))
            {
                SetPressed(false); // dragged off / lost the press
            }
        }

        private void SetPressed(bool pressed)
        {
            if (_pressed == pressed)
            {
                return;
            }
            _pressed = pressed;
            if (_tintTargets != null)
            {
                // multiply toward dark, exactly the vanilla button feel
                for (int i = 0; i < _tintTargets.Length; i++)
                {
                    if (_tintTargets[i] == null)
                    {
                        continue;
                    }
                    Color original = _tintOriginals[i];
                    _tintTargets[i].color = pressed
                        ? new Color(original.r * 0.6f, original.g * 0.6f, original.b * 0.6f, original.a)
                        : original;
                }
            }
            if (!pressed)
            {
                Refresh(); // hover tint may need re-applying on the background
            }
        }

        private void Refresh()
        {
            if (_group != null)
            {
                // template-cloned: keep the native colors; fade whole for disabled,
                // warm tint on hover
                _group.alpha = Interactable ? 1f : 0.45f;
                if (Background != null)
                {
                    Background.color = Interactable && _hovered
                        ? new Color(1f, 0.93f, 0.78f, 1f)
                        : Color.white;
                }
                return;
            }
            if (!Interactable)
            {
                Background.color = Theme.ButtonDisabled;
                if (Label != null) Label.color = Theme.TextDim;
                return;
            }
            Background.color = _hovered ? Theme.ButtonHover : Theme.ButtonFace;
            if (Label != null) Label.color = _hovered ? Theme.AccentGold : Theme.TextCream;
        }
    }
}
