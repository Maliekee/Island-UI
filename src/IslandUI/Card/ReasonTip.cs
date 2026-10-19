using IslandUI.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI
{
    // A one-line floating answer to "why can't I press this?", shown at the
    // cursor while a disabled control is under it.
    //
    // Floating rather than a line inside the card (the alternative considered):
    // the card shrink-wraps to its content, so a message that appears on hover
    // would resize the card under the pointer, and a message that reserves a row
    // costs that row forever. A tip owes nothing to the layout it explains.
    //
    // Deliberately NOT the item card: this answers a question about an ACTION,
    // and lives and dies within one hover. The item card is about an item and
    // persists (§9.2). Conflating them would put a transient sentence inside a
    // panel the reader expects to stay put.
    internal static class ReasonTip
    {
        private const float PadX = 6f;
        private const float PadY = 3f;
        private const float CursorGap = 14f;

        private static GameObject _go;
        private static RectTransform _rect;
        private static Image _plate;
        private static TextMeshProUGUI _label;
        private static bool _wanted;
        private static string _text;

        internal static void ResetForSceneLoad()
        {
            _go = null;
            _rect = null;
            _plate = null;
            _label = null;
            _wanted = false;
            _text = null;
        }

        // Called from whatever is under the cursor, every frame it wants the tip.
        // Nothing calls Hide: not being asked for is what hides it, so a control
        // that stops being hovered — or stops existing — cannot leave a tip behind.
        internal static void Show(string text)
        {
            _wanted = true;
            _text = text;
        }

        // Called once per frame after everything has had its say.
        internal static void Commit(Transform canvasRoot, Camera cam)
        {
            if (!_wanted || string.IsNullOrEmpty(_text))
            {
                if (_go != null && _go.activeSelf)
                {
                    _go.SetActive(false);
                }
                _wanted = false;
                return;
            }
            if (!EnsureBuilt(canvasRoot))
            {
                _wanted = false;
                return;
            }
            if (_label.text != _text)
            {
                _label.text = _text;
                Vector2 size = _label.GetPreferredValues(_text);
                _rect.sizeDelta = new Vector2(size.x + 2f * PadX, size.y + 2f * PadY);
            }
            if (!_go.activeSelf)
            {
                _go.SetActive(true);
            }
            Place(cam);
            _wanted = false;
        }

        private static void Place(Camera cam)
        {
            var parent = (RectTransform)_rect.parent;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parent, Input.mousePosition, cam, out Vector2 local))
            {
                return;
            }
            // above-right of the cursor, then pulled back inside the canvas —
            // a tip that explains a control must not fall off the screen doing it
            // measured from the corner it hangs from, not the canvas's centre - the
            // same mismatch that kept CursorBadge off screen
            Vector2 size = _rect.sizeDelta;
            Rect bounds = parent.rect;
            float px = local.x - bounds.xMin;
            float py = local.y - bounds.yMin;
            float x = px + CursorGap;
            float y = py + CursorGap;
            if (x + size.x > bounds.width)
            {
                x = px - CursorGap - size.x;
            }
            if (y + size.y > bounds.height)
            {
                y = py - CursorGap - size.y;
            }
            _rect.anchoredPosition = new Vector2(x, y);
        }

        private static bool EnsureBuilt(Transform canvasRoot)
        {
            if (_go != null)
            {
                return true;
            }
            if (canvasRoot == null || Theme.Font == null)
            {
                return false;
            }
            _go = Owned.New("IslandUI_ReasonTip");
            _go.transform.SetParent(canvasRoot, false);
            _rect = (RectTransform)_go.transform;
            _rect.anchorMin = new Vector2(0f, 0f);
            _rect.anchorMax = new Vector2(0f, 0f);
            _rect.pivot = new Vector2(0f, 0f);

            _plate = _go.AddComponent<Image>();
            _plate.sprite = Theme.RoundedFill;
            _plate.type = Image.Type.Sliced;
            _plate.color = Theme.PlateDark;
            _plate.raycastTarget = false;
            // above the hover card, as CursorBadge is
            Canvas sort = _go.AddComponent<Canvas>();
            sort.overrideSorting = true;
            sort.sortingOrder = 3;

            _label = Theme.MakeText(_go.transform, "Text", Theme.FontCaption,
                Theme.AccentGold, TextAlignmentOptions.Midline);
            _label.enableWordWrapping = false;
            RectTransform lr = _label.rectTransform;
            lr.anchorMin = Vector2.zero;
            lr.anchorMax = Vector2.one;
            lr.offsetMin = new Vector2(PadX, PadY);
            lr.offsetMax = new Vector2(-PadX, -PadY);
            return true;
        }
    }
}
