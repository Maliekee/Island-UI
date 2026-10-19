using System;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI.UI
{
    // The kit's CHECKBOX (docs/ui-kit.md §3): an oak chip carrying an ink
    // well (kit_band tinted ink), and a cream check in the well when on. Polled
    // like the rest of the kit: the owner calls Tick each frame. Hover lightens
    // the well from ink to the kit's dark; a press darkens the chip like any
    // wooden button.
    internal sealed class KitCheckbox
    {
        public const float Size = 10f;   // kit_chip_oak is 32px
        private const float WellInset = 2.2f;
        private const float CheckInset = 1.6f;
        private static readonly Color Dark = Theme.ParseColor("#3E2C1E", Theme.KitInk);

        public RectTransform Rect;
        public Action<bool> OnChanged;
        public bool On { get; private set; }

        private Image _chip;
        private Image _well;
        private Image _check;
        private bool _hovered;
        private bool _pressed;

        public static KitCheckbox Create(Transform parent, string name, float x, float y,
            bool on, Action<bool> onChanged)
        {
            var c = new KitCheckbox();
            c.On = on;
            c.OnChanged = onChanged;
            c._chip = Theme.KitImage(parent, name, "kit_chip_oak", 0, Color.white);
            c._chip.raycastTarget = true;
            c.Rect = Theme.Place(c._chip, x, y, Size, Size);
            c._well = Theme.KitImage(c._chip.transform, "Well", "kit_band", 10, Theme.KitInk);
            Theme.Place(c._well, WellInset, WellInset, Size - 2f * WellInset, Size - 2f * WellInset);
            c._check = Theme.KitImage(c._chip.transform, "Check", "kit_check", 0, Theme.KitCream);
            Theme.Place(c._check, CheckInset, CheckInset, Size - 2f * CheckInset, Size - 2f * CheckInset);
            c.Paint();
            return c;
        }

        public void Set(bool on)
        {
            On = on;
            Paint();
        }

        // Returns true while the pointer is over the box.
        public bool Tick(Camera cam)
        {
            bool over = Rect.gameObject.activeInHierarchy
                && RectTransformUtility.RectangleContainsScreenPoint(Rect, Input.mousePosition, cam);
            bool repaint = over != _hovered;
            _hovered = over;
            if (Input.GetMouseButtonDown(0) && over)
            {
                _pressed = true;
                repaint = true;
            }
            if (Input.GetMouseButtonUp(0))
            {
                if (_pressed && over)
                {
                    On = !On;
                    if (OnChanged != null)
                    {
                        OnChanged(On);
                    }
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
            float k = _pressed ? 0.8f : 1f;
            _chip.color = new Color(k, k, k, 1f);
            _well.color = _hovered ? Dark : Theme.KitInk;
            if (_check.enabled != On)
            {
                _check.enabled = On;
            }
        }
    }
}
