using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI.UI
{
    // The options kit's KEY BINDING (art/options-visual-kit-v1): the inset field
    // with the key's name in it. A click makes it LISTEN - the focused field,
    // "Press a key" - and the next key pressed is the binding. Escape gives up,
    // Backspace or Delete unbinds, a click anywhere else gives up.
    //
    // Listening has to own the keyboard, or binding B would also open the bag:
    // KeyRemap.Capturing blanks every other key read in the game and the mods,
    // and this row reads through RawInput.Captured. It holds the keyboard until
    // the chosen key is let go again, because readers later in the same frame -
    // and on the frames it stays down - would otherwise still see it.
    internal sealed class KitKeyBind
    {
        public const float Width = 70f;
        public const float Height = 17f;

        public RectTransform Rect;
        public string Label;
        public KeyCode Key { get; private set; }
        public string ConflictWith { get; private set; }

        private static KitKeyBind _listening;
        private static KeyCode _releasing = KeyCode.None;

        private SettingsArt _art;
        private Image _field;
        private TextMeshProUGUI _text;
        private Action<KeyCode> _onChanged;
        private string _unbound;
        private bool _hovered;

        public static bool AnyListening => _listening != null;

        // `unbound` is what None means for this row, when it is not "no key at all"
        // (the console's None is the key left of 1).
        public static KitKeyBind Create(Transform parent, string name, string label, float x, float y,
            KeyCode key, Action<KeyCode> onChanged, SettingsArt art, string unbound = null)
        {
            var k = new KitKeyBind { Label = label, Key = key, _onChanged = onChanged, _art = art, _unbound = unbound };
            k._field = art.Image(parent, name, art.Field);
            k._field.raycastTarget = true;
            k.Rect = Theme.Place(k._field, x, y, Width, Height);
            k._text = art.Text(k._field.transform, "Key", 8f, Theme.KitInk, TextAlignmentOptions.Center, bold: true);
            k._text.overflowMode = TextOverflowModes.Ellipsis;
            Theme.Place(k._text, 4f, 0f, Width - 8f, Height);
            k.Paint();
            return k;
        }

        public void Set(KeyCode key)
        {
            if (Key != key)
            {
                Key = key;
                Paint();
            }
        }

        public void SetConflict(string with)
        {
            if (ConflictWith != with)
            {
                ConflictWith = with;
                Paint();
            }
        }

        // The window closed, or was torn down: nobody is listening any more.
        public static void StopListening()
        {
            KitKeyBind was = _listening;
            _listening = null;
            _releasing = KeyCode.None;
            KeyRemap.Capturing = false;
            was?.Paint();
        }

        // Once a frame, whatever page is showing: gives the keyboard back when
        // the key that was just bound comes up.
        public static void TickRelease()
        {
            if (_listening == null && _releasing != KeyCode.None
                && !RawInput.Physical(_releasing, down: false))
            {
                _releasing = KeyCode.None;
                KeyRemap.Capturing = false;
            }
        }

        // Returns true while the pointer is over it.
        public bool Tick(Camera cam, bool live)
        {
            bool over = live && Rect.gameObject.activeInHierarchy
                && RectTransformUtility.RectangleContainsScreenPoint(Rect, Input.mousePosition, cam);
            if (over != _hovered)
            {
                _hovered = over;
                Paint();
            }
            bool mine = ReferenceEquals(_listening, this);
            if (Input.GetMouseButtonDown(0))
            {
                if (over && !mine)
                {
                    StopListening();
                    _listening = this;
                    KeyRemap.Capturing = true;
                    Paint();
                    return over;
                }
                if (mine)
                {
                    StopListening();
                    return over;
                }
            }
            if (!mine)
            {
                return over;
            }
            if (!Rect.gameObject.activeInHierarchy)
            {
                StopListening();
                return over;
            }
            KeyCode pressed = RawInput.Captured();
            if (pressed == KeyCode.None)
            {
                return over;
            }
            _listening = null;
            _releasing = pressed;   // Capturing stays on until it comes up: TickRelease
            if (pressed != KeyCode.Escape)
            {
                KeyCode bound = pressed == KeyCode.Backspace || pressed == KeyCode.Delete ? KeyCode.None : pressed;
                if (bound != Key)
                {
                    Key = bound;
                    _onChanged?.Invoke(bound);
                }
            }
            Paint();
            return over;
        }

        private void Paint()
        {
            bool listening = ReferenceEquals(_listening, this);
            SettingsArt.Skin(_field, listening ? _art.FieldFocus
                : ConflictWith != null ? _art.FieldInvalid : _art.Field);
            float k = listening || _hovered ? 1f : 0.95f;
            _field.color = new Color(k, k, k, 1f);
            _text.text = listening ? "Press a key" : Key == KeyCode.None && _unbound != null ? _unbound : Name(Key);
            _text.color = Key == KeyCode.None && !listening
                ? new Color(Theme.KitInk.r, Theme.KitInk.g, Theme.KitInk.b, 0.5f) : Theme.KitInk;
        }

        // KeyCode's names are identifiers; these are the words on the keys.
        public static string Name(KeyCode key)
        {
            if (key == KeyCode.None)
            {
                return "Unbound";
            }
            string s = key.ToString();
            if (s.StartsWith("Alpha"))
            {
                return s.Substring(5);
            }
            if (s.StartsWith("Keypad"))
            {
                return "Num " + s.Substring(6);
            }
            switch (key)
            {
                case KeyCode.BackQuote: return "`";
                case KeyCode.Return: return "Enter";
                case KeyCode.Escape: return "Esc";
                case KeyCode.LeftControl: return "Left Ctrl";
                case KeyCode.RightControl: return "Right Ctrl";
                case KeyCode.PageUp: return "Page Up";
                case KeyCode.PageDown: return "Page Down";
            }
            // LeftShift -> Left Shift, UpArrow -> Up Arrow
            var words = new System.Text.StringBuilder(s.Length + 2);
            for (int i = 0; i < s.Length; i++)
            {
                if (i > 0 && char.IsUpper(s[i]) && !char.IsUpper(s[i - 1]))
                {
                    words.Append(' ');
                }
                words.Append(s[i]);
            }
            return words.ToString();
        }
    }
}
