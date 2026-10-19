using IslandUI.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI
{
    // What the thing you are holding down is about to do, said at the cursor.
    //
    // The click map has verbs a player cannot see: a held modifier changes what
    // the next click means, and nothing on screen says so. That is the same
    // disease D25 cured for the right-click TARGET — an action whose meaning
    // depends on state with no representation — and it wants the same cure:
    // move the invisible thing to where the eyes already are.
    //
    // TEXT, NOT ART, and deliberately. We have no icon set, and the one attempt
    // at generating a look (a Slate skin) was thrown out for looking worse
    // than the game it sat in. A caption-font glyph inherits the parchment HUD
    // instead of competing with it, and real icons can replace glyphs later
    // without any of the grammar changing.
    //
    // TWO RULES it must not break:
    //
    //  1. It says what will happen TO THE THING UNDER THE CURSOR, not that a key
    //     is down. Alt over an empty cell means nothing, so the badge must not
    //     appear — a badge promising an action that will not fire is the same
    //     failure as an invisible one, only louder.
    //  2. It is silent when the answer is obvious. The D25 holder line shows only
    //     when the holder is NOT you, because "this feeds you" on every item you
    //     own is noise, and noise is what stops a label being read at all.
    //
    // Separate from ReasonTip rather than folded into it: that answers "why can't
    // I press this", this answers "what will pressing do". Both are transient and
    // both live at the cursor, so they sit on opposite sides of it — the tip
    // above-right, the badge below-right — and neither has to arbitrate with the
    // other for a slot that only one can hold.
    internal static class CursorBadge
    {
        private const float PadX = 5f;
        private const float PadY = 2f;
        private const float CursorGap = 14f;

        private const float IconSize = 8f;
        private const float IconGap = 3f;

        private static GameObject _go;
        private static RectTransform _rect;
        private static Image _plate;
        private static Image _icon;
        private static TextMeshProUGUI _label;
        private static bool _wanted;
        private static string _text;
        private static Sprite _wantedIcon;
        private static Sprite _shownIcon;

        internal static void ResetForSceneLoad()
        {
            _go = null;
            _rect = null;
            _plate = null;
            _icon = null;
            _label = null;
            _wanted = false;
            _text = null;
            _wantedIcon = null;
            _shownIcon = null;
        }

        // Called by whatever is under the cursor, every frame it wants the badge.
        // Nothing calls Hide: not being asked is what hides it, so a surface that
        // stops being hovered cannot leave a promise behind.
        //
        // THE ICON SAYS WHAT HAPPENS, THE WORDS SAY HOW - descriptive text and a
        // badge/icon both: the kit's line glyphs
        // exist now (HudGlyphs), which is what the text-only rule above waited for.
        internal static void Show(string text, Sprite icon = null)
        {
            _wanted = true;
            _text = text;
            _wantedIcon = icon;
        }

        // Harness door: where the badge sits after a commit, against the canvas it
        // hangs in, so "is it on screen" can be asked without holding a modifier.
        internal static string Where()
        {
            if (_rect == null)
            {
                return "not built";
            }
            Rect bounds = ((RectTransform)_rect.parent).rect;
            Vector2 at = _rect.anchoredPosition;
            bool inside = at.x >= 0f && at.y >= 0f && at.x + _rect.sizeDelta.x <= bounds.width
                && at.y + _rect.sizeDelta.y <= bounds.height;
            return $"at {at} size {_rect.sizeDelta} in {bounds.width}x{bounds.height} active={_go.activeSelf} onScreen={inside} mouse={Input.mousePosition}";
        }

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
            if (_label.text != _text || !ReferenceEquals(_shownIcon, _wantedIcon))
            {
                _label.text = _text;
                _shownIcon = _wantedIcon;
                float lead = _shownIcon != null ? IconSize + IconGap : 0f;
                Vector2 size = _label.GetPreferredValues(_text);
                float h = Mathf.Max(size.y, _shownIcon != null ? IconSize : 0f) + 2f * PadY;
                _rect.sizeDelta = new Vector2(size.x + lead + 2f * PadX, h);
                _label.rectTransform.offsetMin = new Vector2(PadX + lead, PadY);
                _icon.sprite = _shownIcon;
                _icon.enabled = _shownIcon != null;
                RectTransform ir = _icon.rectTransform;
                ir.anchoredPosition = new Vector2(PadX, 0f);
            }
            _wantedIcon = null;
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
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parent, Input.mousePosition, cam, out local))
            {
                return;
            }
            // BELOW-right, so it never lands where ReasonTip does. Then pulled back
            // inside the canvas, because a label that explains a control must not
            // fall off the screen doing it.
            // The badge hangs from the canvas's bottom-left corner (its anchors), but
            // the pointer's local point is measured from the canvas's pivot, its
            // centre - so it is moved to the corner first. Without that the badge
            // was placed half a screen away, below the bottom edge, and nobody ever
            // saw it.
            Vector2 size = _rect.sizeDelta;
            Rect bounds = parent.rect;
            float px = local.x - bounds.xMin;
            float py = local.y - bounds.yMin;
            float x = px + CursorGap;
            float y = py - CursorGap - size.y;
            if (x + size.x > bounds.width)
            {
                x = px - CursorGap - size.x;
            }
            if (y < 0f)
            {
                y = py + CursorGap;
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
            _go = Owned.New("IslandUI_CursorBadge");
            _go.transform.SetParent(canvasRoot, false);
            _rect = (RectTransform)_go.transform;
            _rect.anchorMin = Vector2.zero;
            _rect.anchorMax = Vector2.zero;
            _rect.pivot = Vector2.zero;

            _plate = _go.AddComponent<Image>();
            // sorted above the canvas rather than by sibling order: the hover card
            // lifts itself to the top on every item (HoverCard.Render), and a badge
            // under it is a badge nobody reads
            Canvas sort = _go.AddComponent<Canvas>();
            sort.overrideSorting = true;
            sort.sortingOrder = 3;
            _plate.sprite = Theme.RoundedFill;
            _plate.type = Image.Type.Sliced;
            _plate.color = Theme.PlateDark;
            _plate.raycastTarget = false;

            _label = Theme.MakeText(_go.transform, "Text", Theme.FontCaption,
                Theme.TextCream, TextAlignmentOptions.Midline);
            _label.enableWordWrapping = false;
            RectTransform lr = _label.rectTransform;
            lr.anchorMin = Vector2.zero;
            lr.anchorMax = Vector2.one;
            lr.offsetMin = new Vector2(PadX, PadY);
            lr.offsetMax = new Vector2(-PadX, -PadY);

            var iconGo = Owned.New("Icon");
            iconGo.transform.SetParent(_go.transform, false);
            _icon = iconGo.AddComponent<Image>();
            _icon.raycastTarget = false;
            _icon.color = Theme.TextCream;
            _icon.enabled = false;
            RectTransform ir = _icon.rectTransform;
            ir.anchorMin = new Vector2(0f, 0.5f);
            ir.anchorMax = new Vector2(0f, 0.5f);
            ir.pivot = new Vector2(0f, 0.5f);
            ir.sizeDelta = new Vector2(IconSize, IconSize);
            return true;
        }
    }
}
