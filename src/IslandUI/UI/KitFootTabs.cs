using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace IslandUI.UI
{
    // THE COLUMN'S ASPECT TABS (window-system.md §6.20, D49): icon tabs hanging
    // from a frame's BOTTOM edge - the kit's top tab turned over, so its flat
    // foot tucks up under the field and its rounded end hangs free. One row per
    // dossier column, placed by the inventory's Layout at each frame's
    // bottom-left. The active tab is pine and longer; the others walnut, a step
    // shorter and a shade dimmer; pine on hover, darker while pressed.
    //
    // Icons, not words: the column is bottom-anchored, so its
    // foot is the one edge that never moves, and a glyph reads at a width a word
    // cannot. The tab set is the CARRIER'S (D51) - the owner hands over one
    // sprite per aspect it has, and an aspect it lacks is a tab that is not there.
    //
    // Polled like every kit control: the owner calls Tick each frame with the
    // canvas camera; a release over a tab that is not the active one fires
    // OnPick with the tab's index into the set it was given.
    internal sealed class KitFootTabs
    {
        public const float TabW = 22f;
        public const float ActiveH = 18f;      // how far the active tab hangs below the frame
        public const float InactiveH = 14f;
        public const float ActiveSink = 3f;    // how far a foot runs up under the field
        public const float InactiveSink = 5f;
        public const float Gap = 3f;
        public const float IconSize = 11f;
        // what a column reserves under its frame for the row
        public const float RowH = ActiveH;

        public RectTransform Rect;
        public Action<int> OnPick;
        // ON THE TOP RAIL: the sheet hangs from the unit now, so its
        // top edge is the fixed one and the tabs stand on it, rising, instead of
        // hanging from a foot that moves with every page's height
        public bool Top;
        public string[] Tooltips;
        private KitTabHint _tip;
        public int Active { get; private set; }
        public int Count { get; private set; }

        private readonly List<Tab> _tabs = new List<Tab>();
        private int _hot = -1;
        private int _pressed = -1;
        private float _tabWidth = TabW;

        private sealed class Tab
        {
            public RectTransform Rect;
            public Image Plate;
            public Image Icon;
            public TextMeshProUGUI Label;
        }

        // The row hangs from its top-left, so (x, y) is where the frame's bottom
        // edge is - the owner sets the anchors to match its column's.
        public static KitFootTabs Create(Transform parent)
        {
            var row = new KitFootTabs();
            var go = Owned.New("FootTabs");
            go.transform.SetParent(parent, false);
            row.Rect = (RectTransform)go.transform;
            row.Rect.anchorMin = Vector2.zero;
            row.Rect.anchorMax = Vector2.zero;
            row.Rect.pivot = new Vector2(0f, 1f);
            row._tip = new KitTabHint((RectTransform)go.transform);
            return row;
        }

        // The tab set for this bind: one icon per aspect, and which is up. Grows
        // the pool on demand and hides the surplus, so one row serves whatever
        // carrier the column holds this refresh.
        public void Set(Sprite[] icons, int active, string[] labels = null, float width = 0f)
        {
            _tabWidth = labels != null && icons.Length > 0
                ? Mathf.Max(TabW, (width - Gap * (icons.Length - 1)) / icons.Length) : TabW;
            while (_tabs.Count < icons.Length)
            {
                _tabs.Add(MakeTab());
            }
            Count = icons.Length;
            for (int i = 0; i < _tabs.Count; i++)
            {
                bool on = i < Count;
                if (_tabs[i].Rect.gameObject.activeSelf != on)
                {
                    _tabs[i].Rect.gameObject.SetActive(on);
                }
                if (on)
                {
                    _tabs[i].Icon.sprite = icons[i];
                    _tabs[i].Icon.enabled = icons[i] != null;
                    _tabs[i].Label.text = labels != null && i < labels.Length ? labels[i] : string.Empty;
                    _tabs[i].Label.gameObject.SetActive(labels != null);
                    _tabs[i].Icon.enabled = icons[i] != null && labels == null;
                }
            }
            Active = Mathf.Clamp(active, 0, Mathf.Max(0, Count - 1));
            Paint();
        }

        private Tab MakeTab()
        {
            var tab = new Tab();
            var go = Owned.New("Tab");
            go.transform.SetParent(Rect, false);
            tab.Rect = (RectTransform)go.transform;
            tab.Rect.anchorMin = new Vector2(0f, 1f);
            tab.Rect.anchorMax = new Vector2(0f, 1f);
            // Pivot at the FOOT, then turned over: the kit's tab is rounded at
            // its top and flat at its foot, and hung from a bottom edge it is
            // the same sprite upside down. A negative scale about the foot puts
            // the rounded end below the pivot, which is where the row wants it.
            tab.Rect.pivot = new Vector2(0f, 0f);
            tab.Rect.localScale = new Vector3(1f, -1f, 1f);
            tab.Plate = go.AddComponent<Image>();
            tab.Plate.raycastTarget = true;
            var igo = Owned.New("Icon");
            igo.transform.SetParent(go.transform, false);
            tab.Icon = igo.AddComponent<Image>();
            tab.Icon.raycastTarget = false;
            tab.Icon.preserveAspect = true;
            var ir = tab.Icon.rectTransform;
            ir.anchorMin = new Vector2(0.5f, 0.5f);
            ir.anchorMax = new Vector2(0.5f, 0.5f);
            ir.pivot = new Vector2(0.5f, 0.5f);
            ir.sizeDelta = new Vector2(IconSize, IconSize);
            // ...and the glyph turned back the right way up, about its own centre
            ir.localScale = new Vector3(1f, -1f, 1f);
            tab.Label = Theme.MakeKitText(go.transform, "Label", 8f, Theme.KitCream,
                TextAlignmentOptions.Center, bold: true);
            tab.Label.enableWordWrapping = false;
            tab.Label.enableAutoSizing = true;
            tab.Label.fontSizeMin = 6.5f;
            tab.Label.fontSizeMax = 8f;
            tab.Label.raycastTarget = false;
            return tab;
        }

        public void SetActive(int index)
        {
            Active = Mathf.Clamp(index, 0, Mathf.Max(0, Count - 1));
            Paint();
        }

        // Returns true while the pointer is over any tab.
        public bool Tick(Camera cam)
        {
            int hot = -1;
            if (Rect.gameObject.activeInHierarchy)
            {
                for (int i = 0; i < Count; i++)
                {
                    if (RectTransformUtility.RectangleContainsScreenPoint(_tabs[i].Rect, Input.mousePosition, cam))
                    {
                        hot = i;
                        break;
                    }
                }
            }
            bool repaint = hot != _hot;
            _hot = hot;
            if (Input.GetMouseButtonDown(0))
            {
                _pressed = hot;
                repaint = true;
            }
            if (Input.GetMouseButtonUp(0))
            {
                bool fire = _pressed >= 0 && _pressed == hot && hot != Active;
                _pressed = -1;
                repaint = true;
                if (fire && OnPick != null)
                {
                    OnPick(hot);
                }
            }
            else if (_pressed >= 0 && !Input.GetMouseButton(0))
            {
                _pressed = -1;
                repaint = true;
            }
            if (repaint)
            {
                Paint();
            }
            _tip.Show(hot >= 0 && Tooltips != null && hot < Tooltips.Length ? Tooltips[hot] : null);
            return hot >= 0;
        }

        private void Paint()
        {
            float x = 0f;
            for (int i = 0; i < Count; i++)
            {
                Tab t = _tabs[i];
                bool on = i == Active;
                bool hot = i == _hot && !on;
                bool down = i == _pressed && i == _hot && !on;
                float sink = on ? ActiveSink : InactiveSink;
                float drop = on ? ActiveH : InactiveH;
                float h = drop + sink;
                Theme.KitSkin(t.Plate, on || hot ? "kit_tab_pine" : "kit_tab_walnut", 14);
                t.Plate.pixelsPerUnitMultiplier = 14f / (drop / 3f);
                t.Rect.sizeDelta = new Vector2(_tabWidth, h);
                if (Top)
                {
                    // upright, standing on the row's bottom edge (the frame's top),
                    // sunk into the field by `sink` and rising by `drop`
                    t.Rect.anchorMin = new Vector2(0f, 0f);
                    t.Rect.anchorMax = new Vector2(0f, 0f);
                    t.Rect.localScale = Vector3.one;
                    t.Icon.rectTransform.localScale = Vector3.one;
                    t.Rect.anchoredPosition = new Vector2(x, -sink);
                    t.Icon.rectTransform.anchoredPosition = new Vector2(0f, sink * 0.5f);
                }
                else
                {
                    t.Rect.anchorMin = new Vector2(0f, 1f);
                    t.Rect.anchorMax = new Vector2(0f, 1f);
                    t.Rect.localScale = new Vector3(1f, -1f, 1f);
                    t.Icon.rectTransform.localScale = new Vector3(1f, -1f, 1f);
                    t.Rect.anchoredPosition = new Vector2(x, sink);
                    t.Icon.rectTransform.anchoredPosition = new Vector2(0f, sink * 0.5f);
                }
                float k = down ? 0.8f : (on || hot ? 1f : 0.88f);
                t.Plate.color = new Color(k, k, k, 1f);
                t.Icon.color = on ? Color.white : new Color(1f, 1f, 1f, 0.8f);
                var label = t.Label.rectTransform;
                label.anchorMin = label.anchorMax = new Vector2(0.5f, 0.5f);
                label.pivot = new Vector2(0.5f, 0.5f);
                label.sizeDelta = new Vector2(_tabWidth - 4f, drop - 2f);
                label.localScale = Top ? Vector3.one : new Vector3(1f, -1f, 1f);
                label.anchoredPosition = new Vector2(0f, sink * 0.5f);
                t.Label.color = on ? Theme.KitInk : Theme.KitCream;
                x += _tabWidth + Gap;
            }
            Rect.sizeDelta = new Vector2(Mathf.Max(0f, x - Gap), RowH);
        }
    }
}
