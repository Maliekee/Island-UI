using System;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI.UI
{
    // The options kit's TOGGLE (art/options-visual-kit-v1): the sheet draws it
    // whole in each state, knob left on walnut for off, knob right for on, so the
    // two are swapped rather than animated. Polled like the rest of the kit.
    internal sealed class KitToggle
    {
        public const float Width = 33f;   // 204 x 92 px at the height of a row's control
        public const float Height = 15f;

        public RectTransform Rect;
        public bool On { get; private set; }

        private SettingsArt _art;
        private Image _image;
        private Action<bool> _onChanged;
        private bool _hovered;
        private bool _pressed;
        private bool _enabled = true;

        public static KitToggle Create(Transform parent, string name, float x, float y,
            bool on, Action<bool> onChanged, SettingsArt art)
        {
            var t = new KitToggle { On = on, _onChanged = onChanged, _art = art };
            t._image = art.Image(parent, name, on ? art.ToggleOn : art.ToggleOff);
            t._image.raycastTarget = true;
            t.Rect = Theme.Place(t._image, x, y, Width, Height);
            t.Paint();
            return t;
        }

        public void Set(bool on)
        {
            if (On != on)
            {
                On = on;
                Paint();
            }
        }

        // A row that depends on a switch that is off: shown, greyed, deaf.
        public void SetEnabled(bool on)
        {
            if (_enabled != on)
            {
                _enabled = on;
                _pressed = false;
                Paint();
            }
        }

        // Returns true while the pointer is over it.
        public bool Tick(Camera cam, bool live)
        {
            bool over = _enabled && live && Rect.gameObject.activeInHierarchy
                && RectTransformUtility.RectangleContainsScreenPoint(Rect, Input.mousePosition, cam);
            bool repaint = over != _hovered;
            _hovered = over;
            if (Input.GetMouseButtonDown(0))
            {
                _pressed = over;
                repaint = true;
            }
            if (Input.GetMouseButtonUp(0))
            {
                if (_pressed && over)
                {
                    On = !On;
                    _onChanged?.Invoke(On);
                }
                _pressed = false;
                repaint = true;
            }
            else if (_pressed && !Input.GetMouseButton(0))
            {
                _pressed = false;
                repaint = true;
            }
            if (repaint)
            {
                Paint();
            }
            return over;
        }

        private void Paint()
        {
            SettingsArt.Skin(_image, On ? _art.ToggleOn : _art.ToggleOff);
            float k = _pressed ? 0.82f : _hovered ? 1f : 0.94f;
            _image.color = new Color(k, k, k, _enabled ? 1f : 0.4f);
        }
    }
}
