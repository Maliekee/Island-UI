using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI.UI
{
    // THE DOSSIER'S TABS. A character
    // sheet is a stack of sheets on a clipboard, one per page, and the page you are
    // on is the front one. The frame IS that front sheet; this row draws the rest of
    // the stack BEHIND it - each sheet further back a step higher, so its top edge
    // shows - and every page's tab at a FIXED place along the top, so choosing a
    // page brings its sheet forward and never moves a tab.
    //
    // Look 04 (Forgejo #146): the pieces are cut from the approved proof
    // and stand where the proof has them, one
    // screen pixel a sprite pixel at 1440p. A tab is cut per depth - the front one,
    // one back, two back - with the stacked sheets painted under its foot, so the
    // stack is drawn first and every tab over it, the front one last.
    //
    // The contents are a printed form; the frame and tabs are the dossier.
    //
    // The tabs carry the page's glyph and its name, since a folder's label is what
    // you read to find it. Placed at the frame's top-left and drawn behind every
    // plate, like KitFootTabs; polled the same way - the owner calls Tick with the
    // canvas camera, and a release over another page's tab fires OnPick.
    internal sealed class DossierTabs
    {
        // THE SLOTS: where a page's tab was before look 04, and where it still is
        // when the proof's places do not fit (fewer or narrower tabs). The points
        // tags keep to these, so they stand where they always stood.
        public const float TabH = 15f;
        public const float TabW = 68f;
        public const float Gap = 4f;
        public const float Margin = 5.5f;     // the first tab's distance in from the frame's left edge
        public const float Lift = 3.6f;       // each sheet further back sat this much higher
        public const float Sink = 3.2f;       // a tab's foot ran this far down behind its sheet's edge
        public const float Reserve = 24f;     // what the row needs to the right, for the close chip
        private const float IconSize = 8.5f;
        private const float Slope = 5f;       // the old tab's sloped side, 16 px

        // LOOK 04, in the proof's pixels (Px a unit's worth at 1440p)
        private const float Px = 1f / Theme.KitPpu;
        // the tab per depth: front, one back, two back - each from its painted top
        // down to Foot under the frame's top edge, where the board's lip begins
        private static readonly string[] TabArt = { "kit_sheet_tab", "kit_sheet_tab_back", "kit_sheet_tab_back2" };
        private static readonly Vector4[] TabBorder =
        {
            new Vector4(25f, 0f, 14f, 0f), new Vector4(24f, 0f, 16f, 0f), new Vector4(25f, 0f, 17f, 0f),
        };
        private static readonly float[] TabArtH = { 46f, 54f, 66f };
        // ...and how much of that is the tab's own face: below it each back tab
        // paints its drop shadow and the sheets in front, which a hover must not
        // shade (it darkened a band down over the paper)
        private static readonly float[] TabFaceH = { 46f, 37f, 43f };
        private const float Foot = 6f;
        // the three tabs' places in the proof: each one's left edge from the frame's
        // RIGHT edge (on a whole pixel, where the left one is not) and its width
        private static readonly float[] FaceFromRight = { 743f, 513f, 280f };
        private static readonly float[] FaceWidth = { 218f, 201f, 210f };
        // the stacked sheets: the near one from 12 px above the frame's top down to
        // the lip, the far one the 12 px above it, both as wide as the board
        private const string BackNear = "kit_sheet_back_near";
        private const string BackFar = "kit_sheet_back_far";
        private static readonly Vector4 BackBorder = new Vector4(28f, 0f, 11f, 0f);
        private const float BackW = 771f;
        private const float NearH = 18f;
        private const float FarH = 12f;
        // the words: the HUD's Noto Serif at 26 px, their middle 22 px under the
        // tab's top, the glyph 3 units before them, all in the form's ink
        private const float TextSize = 26f * Px;
        private const float LabelDrop = 22f * Px;
        private const float IconGap = 3f;
        private const float LabelDilate = 0.12f;
        private const float Hover = 0.93f;    // a tab further back shades under the pointer
        private const float Press = 0.85f;    // (the art is the proof's own, it cannot light)

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
        private float _tabW = TabW;
        private float _reserve = Reserve;

        private sealed class Tab
        {
            public RectTransform Rect;
            public Image Plate;
            public RectTransform ShadeClip;   // the face alone (TabFaceH)
            public Image Shade;               // the hover's and the press's shade over it
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
            string[] tags = null, PointsTag.Kind[] tagKinds = null, float reserve = Reserve)
        {
            Count = icons.Length;
            Active = Mathf.Clamp(active, 0, Mathf.Max(0, Count - 1));
            _frameW = frameW;
            _reserve = reserve;
            _tabW = Count > 0
                ? Mathf.Min(TabW, (frameW - Margin - reserve - Gap * (Count - 1)) / Count) : TabW;
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

        // a stacked sheet; Paint gives it its depth's sprite and place
        private Image MakeBack()
        {
            Image back = Theme.KitImage(Rect, "Sheet", null, 0, Color.white);
            Theme.KitSkin(back, BackNear, BackBorder);
            var r = back.rectTransform;
            r.anchorMin = r.anchorMax = Vector2.zero;
            r.pivot = Vector2.zero;
            return back;
        }

        private Tab MakeTab()
        {
            var tab = new Tab();
            tab.Plate = Theme.KitImage(Rect, "Tab", null, 0, Color.white);
            Theme.KitSkin(tab.Plate, TabArt[0], TabBorder[0]);
            tab.Plate.raycastTarget = true;
            tab.Rect = tab.Plate.rectTransform;
            tab.Rect.anchorMin = tab.Rect.anchorMax = Vector2.zero;
            tab.Rect.pivot = Vector2.zero;
            var clip = Owned.New("ShadeClip");
            clip.transform.SetParent(tab.Rect, false);
            clip.AddComponent<RectMask2D>();
            tab.ShadeClip = (RectTransform)clip.transform;
            tab.ShadeClip.anchorMin = new Vector2(0f, 1f);
            tab.ShadeClip.anchorMax = Vector2.one;
            tab.ShadeClip.pivot = new Vector2(0.5f, 1f);
            tab.Shade = Theme.KitImage(tab.ShadeClip, "Shade", null, 0, Color.clear);
            tab.Shade.raycastTarget = false;
            RectTransform sr = tab.Shade.rectTransform;
            sr.anchorMin = new Vector2(0f, 1f);
            sr.anchorMax = Vector2.one;
            sr.pivot = new Vector2(0.5f, 1f);
            sr.anchoredPosition = Vector2.zero;
            tab.Shade.gameObject.SetActive(false);
            var igo = Owned.New("Icon");
            igo.transform.SetParent(tab.Rect, false);
            tab.Icon = igo.AddComponent<Image>();
            tab.Icon.raycastTarget = false;
            tab.Icon.preserveAspect = true;
            tab.Label = Theme.MakeKitText(tab.Rect, "Label", TextSize, Theme.DossierInk,
                TextAlignmentOptions.MidlineLeft);
            // the HUD's serif, as the proof letters it (the kit's Georgia if it
            // cannot be built)
            TMP_FontAsset serif = IslandUI.InventoryWindow.HudBodyFont;
            if (serif != null)
            {
                tab.Label.font = serif;
                tab.Label.fontStyle = FontStyles.Normal;
                // flat - the face's shared material wears the outline other HUD
                // words put on it - and a shade heavier than the face, the weight
                // the proof letters its tabs in
                Theme.FlattenType(tab.Label);
                if (tab.Label.fontSharedMaterial != null)
                {
                    tab.Label.fontMaterial.SetFloat(ShaderUtilities.ID_FaceDilate, LabelDilate);
                }
            }
            tab.Label.enableWordWrapping = false;
            tab.Label.overflowMode = TextOverflowModes.Overflow;
            return tab;
        }

        // Returns true while the pointer is over a tab.
        // a press that became a drag of the whole sheet: no page is picked on release
        public void CancelPress()
        {
            if (_pressed >= 0)
            {
                _pressed = -1;
                Paint();
            }
        }

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
            // the stacked sheets, the farthest first; then the tabs over them, the
            // farthest first and the front one last - a tab carries the sheets under
            // its own foot, painted
            int sibling = 0;
            float backX = Mathf.Max(0f, _frameW - BackW * Px);
            for (int depth = Count - 1; depth >= 1; depth--)
            {
                Image back = _backs[depth - 1];
                bool near = depth == 1;
                Sprite sheet = Theme.Kit(near ? BackNear : BackFar, BackBorder);
                if (back.sprite != sheet)
                {
                    back.sprite = sheet;
                }
                // a fourth page's sheet would stand another far sheet higher
                float bottom = near ? -Foot : NearH - Foot + (depth - 2) * FarH;
                back.rectTransform.anchoredPosition = new Vector2(backX, bottom * Px);
                back.rectTransform.sizeDelta = new Vector2(_frameW - backX, (near ? NearH : FarH) * Px);
                back.rectTransform.SetSiblingIndex(sibling++);
            }
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
                PaintTab(i, depth);
                _tabs[i].Rect.SetSiblingIndex(sibling++);
            }
            if (Active < Count)
            {
                PaintTab(Active, 0);
                _tabs[Active].Rect.SetSiblingIndex(sibling);
            }
            Rect.sizeDelta = new Vector2(_frameW, TabH + (Count - 1) * Lift);
            PaintTags();
        }

        // Page i's tab across the top: the proof's place while there are its three
        // tabs and nothing else wants the row (a target's dock chip), the old slot
        // otherwise
        private void Slot(int i, out float x, out float w)
        {
            if (Count == FaceWidth.Length && _reserve <= Reserve && _frameW >= FaceFromRight[0] * Px)
            {
                x = _frameW - FaceFromRight[i] * Px;
                w = FaceWidth[i] * Px;
                return;
            }
            x = Margin + i * (_tabW + Gap);
            w = _tabW;
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
                // the tab's old slot, not its art: the proof keeps the tags there
                float cx = Margin + i * (_tabW + Gap) + _tabW - Slope + 2f;
                float cy = Depth(i) * Lift - Sink + TabH + 1.5f;
                Place(t.Tag.Rect, cx - t.Tag.Width * 0.5f, cy - t.Tag.H * 0.5f, t.Tag.Width, t.Tag.H);
                any = true;
            }
            if (any && _tagLayer.GetSiblingIndex() != Rect.childCount - 1)
            {
                _tagLayer.SetAsLastSibling();
            }
        }

        private void PaintTab(int i, int depth)
        {
            Tab t = _tabs[i];
            bool on = depth == 0;
            bool hot = i == _hot && !on;
            bool down = hot && i == _pressed;
            float k = down ? Press : hot ? Hover : 1f;
            int art = Mathf.Min(depth, TabArt.Length - 1);
            Sprite face = Theme.Kit(TabArt[art], TabBorder[art]);
            if (t.Plate.sprite != face)
            {
                t.Plate.sprite = face;
            }
            Slot(i, out float tx, out float tw);
            float th = TabArtH[art] * Px;
            // the shade: the face's own art in black, cut off where the face ends,
            // so the shadow and the sheets the tab paints under it stay as they are
            bool shade = k < 1f;
            if (t.Shade.gameObject.activeSelf != shade) { t.Shade.gameObject.SetActive(shade); }
            if (shade)
            {
                if (t.Shade.sprite != face)
                {
                    t.Shade.sprite = face;
                    t.Shade.type = Image.Type.Sliced;
                    t.Shade.pixelsPerUnitMultiplier = t.Plate.pixelsPerUnitMultiplier;
                }
                t.Shade.color = new Color(0f, 0f, 0f, 1f - k);
                t.ShadeClip.sizeDelta = new Vector2(0f, TabFaceH[art] * Px);
                t.Shade.rectTransform.sizeDelta = new Vector2(0f, th);
            }
            // a fourth page's tab would stand a far sheet higher again
            float lift = depth > 2 ? (depth - 2) * FarH * Px : 0f;
            t.Rect.sizeDelta = new Vector2(tw, th);
            t.Rect.anchoredPosition = new Vector2(tx, lift - Foot * Px);
            // every tab is lettered alike in the proof, front or back
            t.Icon.color = Theme.DossierInk;
            t.Label.color = Theme.DossierInk;
            // the glyph and the word as one group, centred on the tab
            float textW = t.Label.GetPreferredValues(t.Label.text, 200f, TabH).x;
            // the glyph as wide as it draws, so the gap to its word is the gap you see
            Sprite glyph = t.Icon.sprite;
            float iconW = glyph != null && glyph.rect.height > 0f
                ? IconSize * Mathf.Min(1f, glyph.rect.width / glyph.rect.height) : IconSize;
            float groupW = (t.Icon.enabled ? iconW + IconGap : 0f) + textW;
            float x = Mathf.Max(Slope, (tw - groupW) * 0.5f);
            float cy = th - LabelDrop;
            if (t.Icon.enabled)
            {
                Place(t.Icon.rectTransform, x, cy - IconSize * 0.5f, iconW, IconSize);
                x += iconW + IconGap;
            }
            // the tabs are as wide as the English needs: a longer language's word
            // shrinks to its tab instead of running under the next one
            bool fit = Shared.Loc.Language != "en";
            if (t.Label.enableAutoSizing != fit)
            {
                t.Label.enableAutoSizing = fit;
                t.Label.fontSizeMax = TextSize;
                t.Label.fontSizeMin = TextSize * 0.6f;
                t.Label.fontSize = TextSize;      // auto-size leaves its last size behind
            }
            Place(t.Label.rectTransform, x, cy - TabH * 0.5f, Mathf.Max(4f, tw - x - Slope), TabH);
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
