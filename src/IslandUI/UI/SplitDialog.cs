using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI.UI
{
    // THE SPLIT DIALOG: Shift+click a stack and an ivory slip grows out of the slot,
    // its tail on the stack. "Type amount" and the range on top; [-] [field] [+]
    // [Take n] below. Design: art/ref/split-dialog-v2-brief (round two - round one's
    // walnut plank read as a menu frame, and its count as a label nobody typed into).
    //
    // The field behaves as a text field: it opens on half the stack, selected, so the
    // first digit replaces it; a click puts the caret, a double-click selects it all;
    // Left/Right/Home/End move, Backspace/Delete remove. An amount out of 1..stack-1
    // stays as typed, the field turns red and Take and Enter refuse it - nothing is
    // ever confirmed as a number the player did not see. [-]/[+] and the wheel over
    // the slip step from the last valid amount (Shift: by ten) and select the result.
    // Take or Enter takes; a click outside, a right-click or Escape cancels.
    // Not taken from the design's contract: Tab focus (Tab is the target key) and
    // Ctrl+A / Ctrl+V (A walks, V is the vicinity key).
    //
    // What "take" does is the opener's business: the dialog only chooses a number.
    internal static class SplitDialog
    {
        // The pieces are drawn in 1440p pixels (export_split_v2.py);
        // one canvas unit is 3.2 of them, so positions below are pixels / Px.
        private const float Px = Theme.KitPpu;
        private const float BodyW = 404f, BodyH = 88f, TailW = 24f, TailTop = 76f, TailH = 22f, FullH = 98f;
        private static readonly Vector4 Edge10 = new Vector4(10f, 10f, 10f, 10f);
        private static readonly Vector4 Edge6 = new Vector4(6f, 6f, 6f, 6f);

        private static readonly Color Ink = Hex("49392F");
        private static readonly Color Muted = Hex("74614D");
        private static readonly Color Error = Hex("9D3940");
        private static readonly Color Selection = Hex("BDD8C8");
        private static readonly Color GlyphOff = Hex("B4A58F");
        private static readonly Color TakeInk = Hex("30271F");

        private static RectTransform _root;      // the window root the slip is placed in
        private static RectTransform _slip;      // body + tail, pivot at the tail's tip
        private static Image _body, _tail, _field, _less, _more, _take, _sel, _caret;
        private static Image _lessBar, _moreBar, _moreStem;
        private static TextMeshProUGUI _prompt, _range, _value, _takeLabel;
        private static Canvas _sort;

        private static int _stack;
        private static string _text;     // the digits in the field
        private static int _caretAt;     // caret index into _text
        private static bool _all;        // the whole amount is selected
        private static int _lastValid;
        private static Action<int> _onTake;
        private static Image _pressed;
        private static float _lastFieldClick = -1f;

        internal static bool IsOpen { get; private set; }

        internal static void ResetForSceneLoad()
        {
            _root = null;
            _slip = null;
            _body = null;
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
            _lastValid = Mathf.Max(1, stack / 2);
            _text = _lastValid.ToString();
            _caretAt = _text.Length;
            _all = true;
            _onTake = onTake;
            _pressed = null;
            IsOpen = true;
            _slip.gameObject.SetActive(true);
            _slip.SetAsLastSibling();
            // sorted above the canvas, not by sibling order: the hover card lifts
            // itself to the top on every item and the other windows are not this
            // root's children. Asserted here because Unity drops overrideSorting set
            // on an inactive object.
            _sort.overrideSorting = true;
            _sort.sortingOrder = 4; // over the cursor badge and the reason tip (3)

            // the tail's tip just above the cell's top middle; the body kept on
            // screen, the tail sliding along it so it still points at the stack
            Vector3[] corners = new Vector3[4];
            anchor.GetWorldCorners(corners);
            Vector2 tip = root.InverseTransformPoint((corners[1] + corners[2]) * 0.5f);
            Rect bounds = root.rect;
            float w = BodyW / Px, h = FullH / Px, half = w * 0.5f;
            float x = Mathf.Clamp(tip.x, bounds.xMin + half + 2f, bounds.xMax - half - 2f);
            float y = Mathf.Clamp(tip.y + 0.5f, bounds.yMin + 2f, bounds.yMax - h - 2f);
            _slip.anchorMin = _slip.anchorMax = root.pivot;
            _slip.pivot = new Vector2(0.5f, 0f);
            _slip.sizeDelta = new Vector2(w, h);
            _slip.anchoredPosition = new Vector2(x, y);
            float tailX = Mathf.Clamp((BodyW - TailW) * 0.5f + (tip.x - x) * Px, 12f, BodyW - TailW - 12f);
            PlacePx(_tail, tailX, TailTop, TailW, TailH);
            Paint(null);
        }

        internal static void Close()
        {
            IsOpen = false;
            _onTake = null;
            _pressed = null;
            if (_slip != null)
            {
                _slip.gameObject.SetActive(false);
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
            if (_slip == null || !_slip.gameObject.activeInHierarchy)
            {
                Close();
                return false;
            }
            bool valid = TryValue(out int value);
            Image hot = Over(_less, cam) && CanStep(-1) ? _less
                : Over(_more, cam) && CanStep(1) ? _more
                : Over(_take, cam) && valid ? _take : null;
            bool overSlip = Over(_body, cam) || Over(_tail, cam);

            float wheel = Input.mouseScrollDelta.y;
            if (overSlip && Mathf.Abs(wheel) > 0.01f)
            {
                Step((int)Mathf.Sign(wheel) * (ClickMap.Shift ? 10 : 1));
            }
            Type(Input.inputString);
            Keys();
            if (RawInput.GetKeyDown(KeyCode.Return) || RawInput.GetKeyDown(KeyCode.KeypadEnter))
            {
                if (TryValue(out _))
                {
                    Take();
                }
                return true;
            }
            if (Input.GetMouseButtonDown(1))
            {
                Close();
                return true;
            }
            if (Input.GetMouseButtonDown(0))
            {
                if (!overSlip)
                {
                    Close();   // outside cancels, and the press does nothing else
                    return true;
                }
                if (Over(_field, cam))
                {
                    ClickField(cam);
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

        // ---- the amount ----------------------------------------------------

        private static bool TryValue(out int value)
        {
            value = 0;
            return !string.IsNullOrEmpty(_text) && int.TryParse(_text, out value)
                && value >= 1 && value <= _stack - 1;
        }

        private static bool CanStep(int by)
        {
            int from = TryValue(out int v) ? v : _lastValid;
            return by < 0 ? from > 1 : from < _stack - 1;
        }

        private static void Step(int by)
        {
            int from = TryValue(out int v) ? v : _lastValid;
            int n = Mathf.Clamp(from + by, 1, _stack - 1);
            Set(n);
        }

        private static void Set(int n)
        {
            _lastValid = n;
            _text = n.ToString();
            _caretAt = _text.Length;
            _all = true;
        }

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
                    if (_all)
                    {
                        _text = string.Empty;
                        _caretAt = 0;
                        _all = false;
                    }
                    if (_text.Length >= 6)
                    {
                        continue;
                    }
                    _text = _text.Insert(_caretAt, ch.ToString());
                    _caretAt++;
                }
                else if (ch == '\b')
                {
                    if (_all)
                    {
                        _text = string.Empty;
                        _caretAt = 0;
                        _all = false;
                    }
                    else if (_caretAt > 0)
                    {
                        _text = _text.Remove(_caretAt - 1, 1);
                        _caretAt--;
                    }
                }
                Remember();
            }
        }

        private static void Keys()
        {
            if (RawInput.GetKeyDown(KeyCode.Delete))
            {
                if (_all)
                {
                    _text = string.Empty;
                    _caretAt = 0;
                    _all = false;
                }
                else if (_caretAt < _text.Length)
                {
                    _text = _text.Remove(_caretAt, 1);
                }
                Remember();
            }
            if (RawInput.GetKeyDown(KeyCode.LeftArrow))
            {
                _caretAt = _all ? 0 : Mathf.Max(0, _caretAt - 1);
                _all = false;
            }
            if (RawInput.GetKeyDown(KeyCode.RightArrow))
            {
                _caretAt = _all ? _text.Length : Mathf.Min(_text.Length, _caretAt + 1);
                _all = false;
            }
            if (RawInput.GetKeyDown(KeyCode.Home))
            {
                _caretAt = 0;
                _all = false;
            }
            if (RawInput.GetKeyDown(KeyCode.End))
            {
                _caretAt = _text.Length;
                _all = false;
            }
        }

        private static void Remember()
        {
            if (TryValue(out int v))
            {
                _lastValid = v;
            }
        }

        // A click puts the caret at the nearest gap between digits; a second click
        // soon after selects the whole amount.
        private static void ClickField(Camera cam)
        {
            float now = Time.unscaledTime;
            if (now - _lastFieldClick < 0.35f)
            {
                _all = true;
                _caretAt = _text.Length;
                _lastFieldClick = -1f;
                return;
            }
            _lastFieldClick = now;
            _all = false;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _value.rectTransform, Input.mousePosition, cam, out Vector2 local))
            {
                _caretAt = _text.Length;
                return;
            }
            float x = local.x - _value.rectTransform.rect.xMin;
            int best = 0;
            float bestGap = float.MaxValue;
            for (int i = 0; i <= _text.Length; i++)
            {
                float gap = Mathf.Abs(Width(_text.Substring(0, i)) - x);
                if (gap < bestGap)
                {
                    bestGap = gap;
                    best = i;
                }
            }
            _caretAt = best;
        }

        // Harness door: what Enter does, since /ui/click cannot type.
        internal static bool TakeNow()
        {
            if (!IsOpen || !TryValue(out _))
            {
                return false;
            }
            Take();
            return true;
        }

        private static void Take()
        {
            if (!TryValue(out int n))
            {
                return;
            }
            Action<int> take = _onTake;
            Close();
            take?.Invoke(n);
        }

        // ---- drawing ---------------------------------------------------------

        private static void Paint(Image hot)
        {
            bool valid = TryValue(out int value);
            bool canLess = CanStep(-1), canMore = CanStep(1);
            bool held = Input.GetMouseButton(0);

            _prompt.text = Shared.Loc.Get("split.prompt", "Type amount");
            string range = "1–" + (_stack - 1);
            _range.text = valid ? range : "!  " + range;
            _range.color = valid ? Muted : Error;

            Skin(_field, valid ? "split_field_normal" : "split_field_invalid", Edge6);
            Skin(_less, StepState(_less, canLess, hot, held), Edge6);
            Skin(_more, StepState(_more, canMore, hot, held), Edge6);
            _lessBar.color = canLess ? Ink : GlyphOff;
            _moreBar.color = _moreStem.color = canMore ? Ink : GlyphOff;
            Skin(_take, !valid ? "split_take_disabled"
                : ReferenceEquals(_pressed, _take) && held && hot == _take ? "split_take_pressed"
                : hot == _take ? "split_take_hover" : "split_take_normal", Edge6);
            _takeLabel.text = valid ? Shared.Loc.Get("split.take", "Take {0}", value)
                : Shared.Loc.Get("split.take.bare", "Take");
            _takeLabel.color = valid ? TakeInk : Muted;
            Theme.FitLanguage(_takeLabel, 21f / Px);
            Theme.FitLanguage(_prompt, 18f / Px);

            _value.text = _text;
            float left = _value.rectTransform.anchoredPosition.x;
            if (_all && _text.Length > 0)
            {
                _sel.enabled = true;
                PlaceUnits(_sel, left - 2f / Px, 7f / Px, Width(_text) + 4f / Px, 28f / Px);
            }
            else
            {
                _sel.enabled = false;
            }
            bool blink = (Time.unscaledTime % 1f) < 0.55f;
            _caret.enabled = !_all && blink;
            if (_caret.enabled)
            {
                PlaceUnits(_caret, left + Width(_text.Substring(0, Mathf.Min(_caretAt, _text.Length))) + 1f / Px,
                    8f / Px, 2f / Px, 26f / Px);
            }
        }

        private static string StepState(Image step, bool can, Image hot, bool held)
        {
            if (!can) return "split_step_disabled";
            if (hot == step) return ReferenceEquals(_pressed, step) && held ? "split_step_pressed" : "split_step_hover";
            return "split_step_normal";
        }

        private static float Width(string s)
        {
            return string.IsNullOrEmpty(s) ? 0f : _value.GetPreferredValues(s).x;
        }

        private static void Skin(Image image, string piece, Vector4 border)
        {
            Sprite sprite = AnimePieces.Get(piece, border);
            if (!ReferenceEquals(image.sprite, sprite))
            {
                image.sprite = sprite;
            }
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = Px;
        }

        private static bool Over(Image image, Camera cam)
        {
            return image != null && image.gameObject.activeInHierarchy
                && RectTransformUtility.RectangleContainsScreenPoint(image.rectTransform, Input.mousePosition, cam);
        }

        // ---- building --------------------------------------------------------

        private static bool Ensure(RectTransform root)
        {
            if (_slip != null && ReferenceEquals(_root, root))
            {
                return true;
            }
            if (Theme.Font == null || AnimePieces.Get("split_body", Edge10) == null)
            {
                return false;
            }
            _root = root;
            var go = Owned.New("IslandUI_SplitDialog");
            go.transform.SetParent(root, false);
            _slip = (RectTransform)go.transform;
            _sort = go.AddComponent<Canvas>();
            // a nested canvas raycasts for itself, or clicks fall through the slip
            go.AddComponent<GraphicRaycaster>();

            _body = Piece(_slip, "Body", 0f, 0f, BodyW, BodyH);
            Skin(_body, "split_body", Edge10);
            _body.raycastTarget = true;
            _tail = Piece(_slip, "Tail", (BodyW - TailW) * 0.5f, TailTop, TailW, TailH);
            _tail.sprite = AnimePieces.Get("split_tail");
            _tail.type = Image.Type.Simple;
            _tail.raycastTarget = true;

            _prompt = Text(_slip, "Prompt", 18f, Ink, TextAlignmentOptions.MidlineLeft, true);
            PlacePx(_prompt, 12f, 3f, 210f, 26f);
            _range = Text(_slip, "Range", 17f, Muted, TextAlignmentOptions.MidlineRight, false);
            PlacePx(_range, 226f, 3f, 166f, 26f);

            _less = Piece(_slip, "Less", 12f, 34f, 38f, 42f);
            _lessBar = Bar(_less, 12f, 20f, 14f, 2f);
            _field = Piece(_slip, "Field", 58f, 34f, 112f, 42f);
            _sel = Bar(_field, 0f, 0f, 0f, 0f);
            _sel.color = Selection;
            _value = Text(_field.transform, "Value", 24f, Ink, TextAlignmentOptions.MidlineLeft, true);
            PlacePx(_value, 14f, 0f, 92f, 42f);
            _value.overflowMode = TextOverflowModes.Overflow;
            _caret = Bar(_field, 0f, 0f, 0f, 0f);
            _caret.color = Ink;
            _more = Piece(_slip, "More", 178f, 34f, 38f, 42f);
            _moreBar = Bar(_more, 12f, 20f, 14f, 2f);
            _moreStem = Bar(_more, 18f, 14f, 2f, 14f);
            _take = Piece(_slip, "Take", 232f, 34f, 160f, 42f);
            _takeLabel = Text(_take.transform, "Label", 21f, TakeInk, TextAlignmentOptions.Center, true);
            PlacePx(_takeLabel, 6f, 0f, 148f, 42f);

            go.SetActive(false);
            return true;
        }

        private static Image Piece(Transform parent, string name, float x, float y, float w, float h)
        {
            var go = Owned.New(name);
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.raycastTarget = false;
            PlacePx(image, x, y, w, h);
            return image;
        }

        // a flat ink stroke: the glyphs, the selection and the caret need no texture
        private static Image Bar(Image parent, float x, float y, float w, float h)
        {
            Image bar = Piece(parent.transform, "Bar", x, y, w, h);
            bar.color = Ink;
            return bar;
        }

        private static TextMeshProUGUI Text(Transform parent, string name, float px, Color color,
            TextAlignmentOptions align, bool bold)
        {
            TextMeshProUGUI text = Theme.MakeText(parent, name, px / Px, color, align);
            text.enableWordWrapping = false;
            text.raycastTarget = false;
            // the game font's material draws a hard black outline: on the ivory
            // slip it haloed every glyph (Theme.FlattenType)
            Theme.FlattenType(text);
            if (bold)
            {
                text.fontStyle |= FontStyles.Bold;
            }
            return text;
        }

        private static void PlacePx(Component c, float x, float y, float w, float h)
        {
            Theme.Place(c, x / Px, y / Px, w / Px, h / Px);
        }

        private static void PlaceUnits(Component c, float x, float y, float w, float h)
        {
            Theme.Place(c, x, y, w, h);
        }

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out Color c);
            return c;
        }
    }
}
