using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI.UI
{
    // THE DOSSIER'S TABS. A character
    // sheet is a stack of kraft folders, one per page, and the page you are on is
    // the front one. The frame IS that front folder; this row draws the rest of the
    // stack BEHIND it - one folder per other page, each lifted a step so its top
    // edge shows - and every page's tab: a folder tab at a FIXED place along the
    // top, so choosing a page brings its folder forward and never moves a tab.
    //
    // The contents are a printed form; the frame and tabs are the dossier.
    //
    // The tabs carry the page's glyph and its name, since a folder's label is what
    // you read to find it. Placed at the frame's top-left and drawn behind every
    // plate, like KitFootTabs; polled the same way - the owner calls Tick with the
    // canvas camera, and a release over another page's tab fires OnPick.
    internal sealed class DossierTabs
    {
        public const float TabH = 15f;        // kit_dossier_tab is 48 px tall = 15 at KitPpu
        public const float TabW = 68f;
        public const float Gap = 4f;
        public const float Margin = 5.5f;     // the first tab's distance in from the frame's left edge
        public const float Lift = 3.6f;       // each folder further back sits this much higher
        public const float Sink = 3.2f;       // a tab's foot runs this far down behind its folder's edge
        public const float Reserve = 24f;     // what the row needs to the right, for the close chip
        private const float IconSize = 8.5f;
        private const float TextSize = 7.4f;
        private const float Slope = 5f;       // the sprite's sloped side, 16 px
        // the stack behind, darker the further back it lies
        private static readonly Color[] BackTint =
        {
            new Color(0.93f, 0.92f, 0.90f, 1f),
            new Color(0.86f, 0.85f, 0.82f, 1f),
            new Color(0.80f, 0.79f, 0.76f, 1f),
        };

        public RectTransform Rect;
        public Action<int> OnPick;
        public int Active { get; private set; }
        public int Count { get; private set; }

        private readonly List<Image> _backs = new List<Image>();
        private readonly List<Tab> _tabs = new List<Tab>();
        private RectTransform _tagLayer;   // over every tab: a tag reaches past its tab's slope
        private int _hot = -1;
        private int _pressed = -1;
        private float _frameW;
        private float _frameH;
        private float _tabW = TabW;

        private sealed class Tab
        {
            public RectTransform Rect;
            public Image Plate;
            public Image Icon;
            public TextMeshProUGUI Label;
            // points to spend on this page (PointsTag): clipped to the tab's top-right
            public PointsTag Tag;
            public string TagText;
            public PointsTag.Kind TagKind;
        }

        // The row's origin is the FRAME's top-left corner; the owner anchors it in
        // the column's space, pivot at its bottom-left.
        public static DossierTabs Create(Transform parent)
        {
            var row = new DossierTabs();
            var go = Owned.New("DossierTabs");
            go.transform.SetParent(parent, false);
            row.Rect = (RectTransform)go.transform;
            row.Rect.anchorMin = Vector2.zero;
            row.Rect.anchorMax = Vector2.zero;
            row.Rect.pivot = Vector2.zero;
            return row;
        }

        // One glyph and one name per page, which is up, and the frame it stands on -
        // and per page, the points it has to spend (null: none, no tag).
        public void Set(Sprite[] icons, string[] labels, int active, float frameW, float frameH,
            string[] tags = null, PointsTag.Kind[] tagKinds = null)
        {
            Count = icons.Length;
            Active = Mathf.Clamp(active, 0, Mathf.Max(0, Count - 1));
            _frameW = frameW;
            _frameH = frameH;
            _tabW = Count > 0
                ? Mathf.Min(TabW, (frameW - Margin - Reserve - Gap * (Count - 1)) / Count) : TabW;
            while (_tabs.Count < Count)
            {
                _tabs.Add(MakeTab());
            }
            while (_backs.Count < Mathf.Max(0, Count - 1))
            {
                _backs.Add(MakeBack());
            }
            for (int i = 0; i < _tabs.Count; i++)
            {
                bool on = i < Count;
                if (_tabs[i].Rect.gameObject.activeSelf != on)
                {
                    _tabs[i].Rect.gameObject.SetActive(on);
                }
                if (!on)
                {
                    continue;
                }
                _tabs[i].Icon.sprite = icons[i];
                _tabs[i].Icon.enabled = icons[i] != null;
                string label = labels != null && i < labels.Length ? labels[i] : string.Empty;
                if (_tabs[i].Label.text != label)
                {
                    _tabs[i].Label.text = label;
                }
                _tabs[i].TagText = tags != null && i < tags.Length ? tags[i] : null;
                _tabs[i].TagKind = tagKinds != null && i < tagKinds.Length ? tagKinds[i] : PointsTag.Kind.Attribute;
            }
            for (int i = 0; i < _backs.Count; i++)
            {
                bool on = i < Count - 1;
                if (_backs[i].gameObject.activeSelf != on)
                {
                    _backs[i].gameObject.SetActive(on);
                }
            }
            Paint();
        }

        private Image MakeBack()
        {
            Image back = Theme.KitImage(Rect, "Folder", "kit_dossier_folder", 16, Color.white);
            var r = back.rectTransform;
            r.anchorMin = r.anchorMax = Vector2.zero;
            r.pivot = Vector2.zero;
            return back;
        }

        private Tab MakeTab()
        {
            var tab = new Tab();
            tab.Plate = Theme.KitImage(Rect, "Tab", "kit_dossier_tab", 20, Color.white);
            tab.Plate.raycastTarget = true;
            tab.Rect = tab.Plate.rectTransform;
            tab.Rect.anchorMin = tab.Rect.anchorMax = Vector2.zero;
            tab.Rect.pivot = Vector2.zero;
            var igo = Owned.New("Icon");
            igo.transform.SetParent(tab.Rect, false);
            tab.Icon = igo.AddComponent<Image>();
            tab.Icon.raycastTarget = false;
            tab.Icon.preserveAspect = true;
            tab.Label = Theme.MakeKitText(tab.Rect, "Label", TextSize, Theme.DossierInk,
                TextAlignmentOptions.MidlineLeft, bold: true);
            tab.Label.enableWordWrapping = false;
            tab.Label.overflowMode = TextOverflowModes.Overflow;
            return tab;
        }

        // Returns true while the pointer is over a tab.
        public bool Tick(Camera cam)
        {
            int hot = -1;
            if (Rect.gameObject.activeInHierarchy)
            {
                // front first: the active tab stands over the others' feet
                if (Active < Count && Contains(_tabs[Active].Rect, cam))
                {
                    hot = Active;
                }
                for (int i = 0; hot < 0 && i < Count; i++)
                {
                    if (Contains(_tabs[i].Rect, cam))
                    {
                        hot = i;
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
            return hot >= 0;
        }

        // How far back page i lies: 0 for the front, then 1, 2... with the HIGHER
        // pages further back - the order the dossier art is drawn in.
        private int Depth(int i)
        {
            if (i == Active)
            {
                return 0;
            }
            int deeper = 0;
            for (int k = 0; k < Count; k++)
            {
                if (k != Active && k < i)
                {
                    deeper++;
                }
            }
            return deeper + 1;
        }

        private void Paint()
        {
            // the stack: the farthest folder first, each tab before its own folder
            // so the folder's edge covers the tab's foot
            int sibling = 0;
            for (int depth = Count - 1; depth >= 1; depth--)
            {
                int i = -1;
                for (int k = 0; k < Count; k++)
                {
                    if (Depth(k) == depth) { i = k; break; }
                }
                if (i < 0)
                {
                    continue;
                }
                Color tint = BackTint[Mathf.Min(depth - 1, BackTint.Length - 1)];
                PaintTab(i, depth, tint);
                _tabs[i].Rect.SetSiblingIndex(sibling++);
                Image back = _backs[depth - 1];
                back.color = tint;
                back.rectTransform.sizeDelta = new Vector2(_frameW, _frameH);
                back.rectTransform.anchoredPosition = new Vector2(0f, depth * Lift - _frameH);
                back.rectTransform.SetSiblingIndex(sibling++);
            }
            if (Active < Count)
            {
                PaintTab(Active, 0, Color.white);
                _tabs[Active].Rect.SetSiblingIndex(sibling);
            }
            Rect.sizeDelta = new Vector2(_frameW, TabH + (Count - 1) * Lift);
            PaintTags();
        }

        // Centred a little past where the tab's flat top meets its right slope and
        // standing mostly above the edge, so it clips the tab without covering its word.
        private void PaintTags()
        {
            bool any = false;
            for (int i = 0; i < _tabs.Count; i++)
            {
                Tab t = _tabs[i];
                bool on = i < Count && !string.IsNullOrEmpty(t.TagText);
                if (!on)
                {
                    if (t.Tag != null) { t.Tag.Hide(); }
                    continue;
                }
                if (_tagLayer == null)
                {
                    var go = Owned.New("Tags");
                    go.transform.SetParent(Rect, false);
                    _tagLayer = (RectTransform)go.transform;
                    Place(_tagLayer, 0f, 0f, 0f, 0f);
                }
                if (t.Tag == null)
                {
                    t.Tag = PointsTag.Create(_tagLayer, "Tag" + i);
                }
                t.Tag.Show(t.TagKind, t.TagText);
                Vector2 at = t.Rect.anchoredPosition;
                float cx = at.x + _tabW - Slope + 2f;
                float cy = at.y + TabH + 1.5f;
                Place(t.Tag.Rect, cx - t.Tag.Width * 0.5f, cy - t.Tag.H * 0.5f, t.Tag.Width, t.Tag.H);
                any = true;
            }
            if (any && _tagLayer.GetSiblingIndex() != Rect.childCount - 1)
            {
                _tagLayer.SetAsLastSibling();
            }
        }

        private void PaintTab(int i, int depth, Color tint)
        {
            Tab t = _tabs[i];
            bool on = depth == 0;
            bool hot = i == _hot && !on;
            bool down = hot && i == _pressed;
            float k = down ? 0.9f : hot ? 1.06f : 1f;
            t.Plate.color = new Color(Mathf.Min(1f, tint.r * k), Mathf.Min(1f, tint.g * k), Mathf.Min(1f, tint.b * k), 1f);
            t.Rect.sizeDelta = new Vector2(_tabW, TabH);
            t.Rect.anchoredPosition = new Vector2(Margin + i * (_tabW + Gap), depth * Lift - Sink);
            Color ink = on ? Theme.DossierInk : Theme.DossierFaded;
            t.Icon.color = ink;
            t.Label.color = ink;
            // the glyph and the word as one group, centred on the tab's top
            float textW = t.Label.GetPreferredValues(t.Label.text, 200f, TabH).x;
            float groupW = (t.Icon.enabled ? IconSize + 2f : 0f) + textW;
            float x = Mathf.Max(Slope, (_tabW - groupW) * 0.5f);
            float cy = Sink + (TabH - Sink) * 0.5f;
            if (t.Icon.enabled)
            {
                Place(t.Icon.rectTransform, x, cy - IconSize * 0.5f, IconSize, IconSize);
                x += IconSize + 2f;
            }
            Place(t.Label.rectTransform, x, cy - TabH * 0.5f, Mathf.Max(4f, _tabW - x - Slope), TabH);
        }

        // bottom-left placement inside the tab, which is what its pivot is
        private static void Place(RectTransform r, float x, float y, float w, float h)
        {
            r.anchorMin = r.anchorMax = Vector2.zero;
            r.pivot = Vector2.zero;
            r.anchoredPosition = new Vector2(x, y);
            r.sizeDelta = new Vector2(w, h);
        }

        private static bool Contains(RectTransform rect, Camera cam)
        {
            return rect != null && rect.gameObject.activeInHierarchy
                && RectTransformUtility.RectangleContainsScreenPoint(rect, Input.mousePosition, cam);
        }
    }
}
