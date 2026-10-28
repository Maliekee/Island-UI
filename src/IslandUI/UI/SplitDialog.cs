using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace IslandUI.UI
{
    // THE SPLIT DIALOG: Shift+click an item to open it, and the mouse wheel adjusts
    // the item count in it. It replaced the wheel splitter on the cells,
    // which moved one item per notch and fought the list scroll for the same wheel.
    //
    // A kit plank over the cell: [-] count [+] [Take]. The wheel steps the count by
    // one, by ten with Shift, and digits type it (Backspace takes one off). Take or
    // Enter takes it; a click anywhere outside, a right-click or Escape cancels
    // (the button is the one to push; clicks elsewhere used to confirm instead).
    // What "take" does is the opener's business: the dialog only chooses a number
    // between 1 and one less than the stack.
    internal static class SplitDialog
    {
        private const float W = 132f;
        private const float H = 24f;
        private const float Chip = 16f;

        private static RectTransform _root;
        private static Image _panel;
        private static Image _less;
        private static Image _more;
        private static Image _take;
        private static TextMeshProUGUI _count;
        private static TextMeshProUGUI _takeLabel;
        private static int _n;
        private static int _stack;
        private static string _typed;   // the digits typed since the last step, or null
        private static Action<int> _onTake;
        private static Image _pressed;

        private static Canvas _sort;
        internal static bool IsOpen { get; private set; }

        internal static void ResetForSceneLoad()
        {
            _root = null;
            _panel = null;
            _sort = null;
            IsOpen = false;
            _onTake = null;
        }

        internal static void Open(RectTransform root, RectTransform anchor, int stack, Action<int> onTake)
        {
            if (root == null || anchor == null || stack < 2 || onTake == null || !Ensure(root))
            {
                return;
            }
            _stack = stack;
            _n = Mathf.Max(1, stack / 2);
            _typed = null;
            _onTake = onTake;
            _pressed = null;
            IsOpen = true;
            _panel.gameObject.SetActive(true);
            _panel.transform.SetAsLastSibling();
            // sorted above the canvas, not by sibling order (the dialog fell into
            // the background over the vicinity window and under a hover): the hover card lifts itself to the top on every
            // item and the other windows are not this root's children. Asserted
            // here because Unity drops overrideSorting set on an inactive object.
            _sort.overrideSorting = true;
            _sort.sortingOrder = 4; // over the cursor badge and the reason tip (3)
            // centred just above the cell, then kept on screen
            Vector3[] corners = new Vector3[4];
            anchor.GetWorldCorners(corners);
            Vector3 topMid = (corners[1] + corners[2]) * 0.5f;
            Vector2 at = root.InverseTransformPoint(topMid);
            Rect bounds = root.rect;
            at.x = Mathf.Clamp(at.x, bounds.xMin + W * 0.5f + 4f, bounds.xMax - W * 0.5f - 4f);
            at.y = Mathf.Clamp(at.y + 3f, bounds.yMin + 4f, bounds.yMax - H - 4f);
            RectTransform r = _panel.rectTransform;
            r.anchorMin = r.anchorMax = root.pivot;
            r.pivot = new Vector2(0.5f, 0f);
            r.sizeDelta = new Vector2(W, H);
            r.anchoredPosition = at;
            Paint(null);
        }

        internal static void Close()
        {
            IsOpen = false;
            _onTake = null;
            _pressed = null;
            if (_panel != null)
            {
                _panel.gameObject.SetActive(false);
            }
        }

        // Escape cancels, asked from the pause's prefix (TargetPatches), the one
        // place the game reads the key - so the press neither pauses nor
        // reaches the target or the item card behind the dialog.
        internal static bool EscapeCancels()
        {
            if (!IsOpen || !Input.GetKeyDown(KeyCode.Escape))
            {
                return false;
            }
            Close();
            return true;
        }

        // Every frame while open; true means the pointer is the dialog's this frame.
        internal static bool Tick(Camera cam)
        {
            if (!IsOpen)
            {
                return false;
            }
            if (_panel == null || !_panel.gameObject.activeInHierarchy)
            {
                Close();
                return false;
            }
            Image hot = Over(_less, cam) ? _less : Over(_more, cam) ? _more : Over(_take, cam) ? _take : null;
            bool overPanel = Over(_panel, cam);
            float wheel = Input.mouseScrollDelta.y;
            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (Mathf.Abs(wheel) > 0.01f && (overPanel || overUi))
            {
                Step((int)Mathf.Sign(wheel) * (ClickMap.Shift ? 10 : 1));
            }
            Type(Input.inputString);
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                Take();
                return true;
            }
            if (Input.GetMouseButtonDown(1))
            {
                Close();
                return true;
            }
            if (Input.GetMouseButtonDown(0))
            {
                if (!overPanel)
                {
                    Close();   // outside cancels, and the press does nothing else
                    return true;
                }
                _pressed = hot;
            }
            if (Input.GetMouseButtonUp(0))
            {
                Image released = _pressed;
                _pressed = null;
                if (released != null && ReferenceEquals(released, hot))
                {
                    if (released == _less) { Step(-1); }
                    else if (released == _more) { Step(1); }
                    else if (released == _take) { Take(); return true; }
                }
            }
            Paint(hot);
            return true;
        }

        private static void Step(int by)
        {
            _typed = null;
            _n = Mathf.Clamp(_n + by, 1, _stack - 1);
        }

        // Digits build a number from nothing - the first one replaces the count -
        // and Backspace takes the last one off. The count is kept in range as it is
        // typed; the digits themselves are shown as typed until the next step.
        private static void Type(string chars)
        {
            if (string.IsNullOrEmpty(chars))
            {
                return;
            }
            foreach (char ch in chars)
            {
                if (ch >= '0' && ch <= '9')
                {
                    _typed = (_typed ?? string.Empty) + ch;
                    if (_typed.Length > 6)
                    {
                        _typed = _typed.Substring(_typed.Length - 6);
                    }
                }
                else if (ch == '\b' && !string.IsNullOrEmpty(_typed))
                {
                    _typed = _typed.Substring(0, _typed.Length - 1);
                }
                else
                {
                    continue;
                }
                int typed = string.IsNullOrEmpty(_typed) ? 1 : int.Parse(_typed);
                _n = Mathf.Clamp(typed, 1, _stack - 1);
            }
        }

        // Harness door: what Enter does, since /ui/click cannot type.
        internal static bool TakeNow()
        {
            if (!IsOpen)
            {
                return false;
            }
            Take();
            return true;
        }

        private static void Take()
        {
            Action<int> take = _onTake;
            int n = _n;
            Close();
            take?.Invoke(n);
        }

        private static void Paint(Image hot)
        {
            _count.text = (_typed != null ? _typed + "|" : _n.ToString()) + " / " + _stack;
            _takeLabel.text = Shared.Loc.Get("split.take", "Take {0}", _n);
            Theme.FitLanguage(_takeLabel, 8f);
            Theme.KitSkin(_less, hot == _less ? "kit_chip_oak" : "kit_chip_walnut", 0);
            Theme.KitSkin(_more, hot == _more ? "kit_chip_oak" : "kit_chip_walnut", 0);
            Theme.KitSkin(_take, hot == _take ? "kit_plank_pine" : "kit_plank_oak", 18);
            _less.color = _n > 1 ? Color.white : new Color(1f, 1f, 1f, 0.45f);
            _more.color = _n < _stack - 1 ? Color.white : new Color(1f, 1f, 1f, 0.45f);
        }

        private static bool Over(Image image, Camera cam)
        {
            return image != null && image.gameObject.activeInHierarchy
                && RectTransformUtility.RectangleContainsScreenPoint(image.rectTransform, Input.mousePosition, cam);
        }

        private static bool Ensure(RectTransform root)
        {
            if (_panel != null && ReferenceEquals(_root, root))
            {
                return true;
            }
            if (Theme.Font == null)
            {
                return false;
            }
            _root = root;
            _panel = Theme.KitImage(root, "IslandUI_SplitDialog", "kit_plank_walnut", 18, Color.white);
            _panel.raycastTarget = true;
            _sort = _panel.gameObject.AddComponent<Canvas>();
            // a nested canvas raycasts for itself, or clicks fall through the plank
            _panel.gameObject.AddComponent<GraphicRaycaster>();
            _less = MakeChip(_panel.transform, "Less", "kit_minus", 4f);
            _count = Theme.MakeKitText(_panel.transform, "Count", 9, Theme.KitCream, TextAlignmentOptions.Center);
            _count.raycastTarget = false;
            Theme.Place(_count, 4f + Chip, 4f, 48f, Chip);
            _more = MakeChip(_panel.transform, "More", "kit_plus", 4f + Chip + 48f);
            _take = Theme.KitImage(_panel.transform, "Take", "kit_plank_oak", 18, Color.white);
            _take.raycastTarget = true;
            Theme.Place(_take, 2f * Chip + 56f, 3f, W - 2f * Chip - 60f, H - 6f);
            _takeLabel = Theme.MakeKitText(_take.transform, "Label", 8, Theme.KitInk, TextAlignmentOptions.Center);
            _takeLabel.raycastTarget = false;
            Theme.Place(_takeLabel, 0f, 0f, W - 2f * Chip - 60f, H - 6f);
            _panel.gameObject.SetActive(false);
            return true;
        }

        private static Image MakeChip(Transform parent, string name, string glyph, float x)
        {
            Image chip = Theme.KitImage(parent, name, "kit_chip_walnut", 0, Color.white);
            chip.raycastTarget = true;
            Theme.Place(chip, x, 4f, Chip, Chip);
            Image mark = Theme.KitImage(chip.transform, "Glyph", glyph, 0, Theme.KitCream);
            mark.raycastTarget = false;
            Theme.Place(mark, 3f, 3f, Chip - 6f, Chip - 6f);
            return chip;
        }
    }
}
