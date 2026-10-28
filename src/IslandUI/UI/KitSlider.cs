using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI.UI
{
    // The options kit's SLIDER (art/options-visual-kit-v1): a dark track, the lit
    // run of it left of the knob, the knob, and the value in words to the right.
    // The value moves in steps, so a drag lands on numbers a person would type.
    // OnChanged fires as the knob moves - the setting applies while you look at
    // it - and OnReleased once when the drag ends, which is when it is worth
    // writing to disk. Polled like the rest of the kit.
    internal sealed class KitSlider
    {
        public const float Height = 13f;
        private const float TrackH = 8f;
        private const float Knob = 13f;
        private const float ValueW = 26f;
        private const float ArtPixelsPerUnit = 33f / TrackH;   // the track is 33 px tall on the sheet

        public RectTransform Rect;
        public float Value { get; private set; }
        public bool Enabled { get; private set; } = true;

        private float _min, _max, _step;
        private Func<float, string> _format;
        private Action<float> _onChanged;
        private Action _onReleased;
        private RectTransform _track;
        private RectTransform _fill;
        private RectTransform _knob;
        private Image _knobImage;
        private TextMeshProUGUI _text;
        private CanvasGroup _group;
        private float _trackW;
        private bool _hovered;
        private bool _dragging;
        private bool _moved;

        public bool Dragging => _dragging;

        public static KitSlider Create(Transform parent, string name, float x, float y, float width,
            float min, float max, float step, float value, Func<float, string> format,
            Action<float> onChanged, Action onReleased, SettingsArt art)
        {
            var s = new KitSlider
            {
                _min = min, _max = max, _step = step, _format = format,
                _onChanged = onChanged, _onReleased = onReleased,
            };
            GameObject go = Owned.New(name);
            go.transform.SetParent(parent, false);
            s.Rect = Theme.Place(go.transform, x, y, width, Height);
            s._group = go.AddComponent<CanvasGroup>();

            s._trackW = width - ValueW - Theme.Gap;
            float trackY = (Height - TrackH) * 0.5f;
            Image empty = art.Image(go.transform, "Track", art.SliderEmpty);
            SettingsArt.SkinAtScale(empty, art.SliderEmpty, ArtPixelsPerUnit);
            empty.raycastTarget = true;
            // The dark run was cut from right of the sheet's knob, so its left end
            // is square. It starts half a track in, where the lit run's round end
            // always covers it.
            Theme.Place(empty, TrackH * 0.5f, trackY, s._trackW - TrackH * 0.5f, TrackH);
            GameObject hit = Owned.New("Hit");
            hit.transform.SetParent(go.transform, false);
            s._track = Theme.Place(hit.transform, 0f, trackY, s._trackW, TrackH);
            Image fill = art.Image(go.transform, "Fill", art.SliderFill);
            SettingsArt.SkinAtScale(fill, art.SliderFill, ArtPixelsPerUnit);
            s._fill = Theme.Place(fill, 0f, trackY, Knob, TrackH);
            s._knobImage = art.Image(go.transform, "Knob", art.SliderKnob);
            s._knob = Theme.Place(s._knobImage, 0f, 0f, Knob, Knob);

            s._text = art.Text(go.transform, "Value", 8f, Theme.KitInk, TextAlignmentOptions.MidlineRight);
            s._text.overflowMode = TextOverflowModes.Overflow;
            s._text.enableAutoSizing = true;          // "100 %" and longer units stay off the track
            s._text.fontSizeMin = 6f;
            s._text.fontSizeMax = 8f;
            Theme.Place(s._text, s._trackW + Theme.Gap, 0f, ValueW, Height);

            s.Value = s.Snap(value);
            s.Layout();
            return s;
        }

        public void Set(float value)
        {
            float v = Snap(value);
            if (!Mathf.Approximately(v, Value))
            {
                Value = v;
                Layout();
            }
        }

        // A row that depends on a switch that is off: shown, greyed, deaf.
        public void SetEnabled(bool on)
        {
            Enabled = on;
            _group.alpha = on ? 1f : 0.4f;
            if (!on)
            {
                _dragging = false;
            }
        }

        // Returns true while the pointer is over it or a drag is under way.
        public bool Tick(Camera cam, bool live)
        {
            bool over = Enabled && live && Rect.gameObject.activeInHierarchy
                && (RectTransformUtility.RectangleContainsScreenPoint(_track, Input.mousePosition, cam)
                    || RectTransformUtility.RectangleContainsScreenPoint(_knob, Input.mousePosition, cam));
            if (Input.GetMouseButtonDown(0) && over)
            {
                _dragging = true;
                _moved = false;
            }
            if (_dragging)
            {
                Vector2 local;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_track, Input.mousePosition, cam, out local))
                {
                    // the track's pivot is its top-left corner; the knob's centre runs
                    // from half a knob in to half a knob short of the far end
                    float t = Mathf.InverseLerp(Knob * 0.5f, _trackW - Knob * 0.5f, local.x);
                    float v = Snap(Mathf.Lerp(_min, _max, t));
                    if (!Mathf.Approximately(v, Value))
                    {
                        Value = v;
                        _moved = true;
                        Layout();
                        _onChanged?.Invoke(Value);
                    }
                }
                if (!Input.GetMouseButton(0))
                {
                    _dragging = false;
                    if (_moved)
                    {
                        _onReleased?.Invoke();
                    }
                }
            }
            bool hot = over || _dragging;
            if (hot != _hovered)
            {
                _hovered = hot;
                float k = hot ? 1f : 0.93f;
                _knobImage.color = new Color(k, k, k, 1f);
            }
            return hot;
        }

        private float Snap(float value)
        {
            float v = Mathf.Clamp(value, _min, _max);
            if (_step > 0f)
            {
                v = _min + Mathf.Round((v - _min) / _step) * _step;
            }
            return Mathf.Clamp(v, _min, _max);
        }

        private void Layout()
        {
            float t = Mathf.InverseLerp(_min, _max, Value);
            float centre = Mathf.Lerp(Knob * 0.5f, _trackW - Knob * 0.5f, t);
            _knob.anchoredPosition = new Vector2(centre - Knob * 0.5f, 0f);
            // the lit run ends under the knob, never short of its own round end
            _fill.sizeDelta = new Vector2(Mathf.Max(centre, TrackH), TrackH);
            _text.text = _format != null ? _format(Value) : Value.ToString("0.##");
        }
    }
}
