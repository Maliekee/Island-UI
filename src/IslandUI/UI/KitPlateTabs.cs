using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI.UI
{
    // THE SLOT'S TAB ROW, in two drawings:
    //
    //   Plates (T14): the active window wears the oak nail plate the title wore,
    //                 the others smaller walnut plates beside it, all half in
    //                 over the rail. The first cut.
    //   Tabs   (T11): the kit's own top tabs (docs/ui-kit.md §3, "tabs D") - the
    //                 inactive ones walnut, sitting low, the active one pine and
    //                 taller, all rising from the rail with their feet tucked
    //                 under the field, no rail of their own. The row is drawn BEHIND the field so the tuck
    //                 is what makes a tab grow out of the frame.
    //
    // Polled like every kit control: the owner calls Tick each frame with the
    // canvas camera, and a release over a tab that is not the active one fires
    // OnPick. Hover swaps walnut for pine; a press darkens the wood.
    internal sealed class KitPlateTabs
    {
        public enum Style { Plates, Tabs }

        // Plates
        public const float ActiveW = 76f;    // kit_title_oak_nails at 0.8
        public const float ActiveH = 16f;
        public const float InactiveW = 56f;  // kit_plank_walnut, sliced
        public const float InactiveH = 12f;
        // Tabs (T11, grown from its 100x46 px after the first look: "too small")
        public const float TabW = 22f;
        public const float TabActiveH = 18f;
        public const float TabInactiveH = 14f;
        // How far a tab's flat foot runs UNDER the field. The row is drawn behind
        // the frame, as T11 was: the field hides the feet and the rail runs across
        // them, the active tab standing taller and the inactive a step lower.
        // (Drawn over the rail instead, the feet lay on the field and looked off,
        // and the first cut's rounded feet peeked out below;
        // the flat-footed sprites are what fixed that, not the draw order.)
        // Deeper than the drawing's 4 / 8 px: the plank sprites carry a 3px
        // transparent margin and the rail sits a unit inside the frame's rect,
        // and at 1.5 / 3 the feet stopped short of the field and looked detached.
        // Net of those, this is T11's stance.
        public const float ActiveSink = 3f;     // hidden, so only "enough" matters
        public const float InactiveSink = 5f;
        public const float Tuck = 0f;
        public const float Gap = 4f;
        private static readonly Color Pale = Theme.ParseColor("#CDB996", Theme.KitCream);

        public RectTransform Rect;
        public Action<int> OnPick;
        public int Active { get; private set; }
        public Style Look { get; private set; }
        // EACH TAB WEARS THE WOOD OF THE WINDOW IT LEADS TO (coloured by the
        // background: the crafting menu darker, the vicinity window lighter): walnut for the bench, oak for
        // the parchment. Height says which is active; colour says what you get.
        private string[] _woods;
        // what the owner adds to the frame's top edge to place the row's anchor
        public float YOffset => Look == Style.Tabs ? -Tuck : 0f;

        private readonly List<Tab> _tabs = new List<Tab>();
        private KitTabHint _tip;
        private int _hot = -1;
        private int _pressed = -1;

        private sealed class Tab
        {
            public RectTransform Rect;
            public Image Plate;
            public TextMeshProUGUI Text;
            public string Name;
            public Image Glyph;
        }

        // Plates: the row's rect hangs from the parent's top-left with its middle
        // on the parent's top edge, so (x, 0) puts the plates half in over the
        // rail. Tabs: the row stands on its bottom edge, so (x, -Tuck) puts the
        // tabs' feet just under the field's top edge.
        public static KitPlateTabs Create(Transform parent, string[] names, int active,
            Action<int> onPick, Style look = Style.Tabs, string[] woods = null)
        {
            var row = new KitPlateTabs();
            row.OnPick = onPick;
            row.Look = look;
            row._woods = woods;
            row.Active = Mathf.Clamp(active, 0, names.Length - 1);
            var go = Owned.New("PlateTabs");
            go.transform.SetParent(parent, false);
            row.Rect = (RectTransform)go.transform;
            row.Rect.anchorMin = new Vector2(0f, 1f);
            row.Rect.anchorMax = new Vector2(0f, 1f);
            float py = look == Style.Tabs ? 0f : 0.5f;
            row.Rect.pivot = new Vector2(0f, py);
            for (int i = 0; i < names.Length; i++)
            {
                var tab = new Tab { Name = names[i] };
                var tgo = Owned.New("Tab_" + names[i]);
                tgo.transform.SetParent(go.transform, false);
                tab.Rect = (RectTransform)tgo.transform;
                tab.Rect.anchorMin = new Vector2(0f, py);
                tab.Rect.anchorMax = new Vector2(0f, py);
                tab.Rect.pivot = new Vector2(0f, py);
                tab.Plate = tgo.AddComponent<Image>();
                tab.Plate.raycastTarget = true;
                tab.Text = Theme.MakeKitText(tgo.transform, "Text", 9f, Theme.KitCream,
                    TextAlignmentOptions.Center, bold: true);
                tab.Text.enableWordWrapping = false;
                tab.Text.text = names[i];
                if (names.Length == 3 && names[0] == "Crafting")
                {
                    tab.Glyph = Theme.KitImage(tgo.transform, "Glyph", "kit_check", 0, Theme.KitCream);
                    tab.Glyph.sprite = HudGlyphs.Get(i == 0 ? "craft" : i == 1 ? "vicinity" : "roster"); tab.Glyph.raycastTarget = false;
                    tab.Text.gameObject.SetActive(false);
                }
                row._tabs.Add(tab);
            }
            row._tip = new KitTabHint(row.Rect);
            row.Paint();
            return row;
        }

        public void SetActive(int index)
        {
            Active = Mathf.Clamp(index, 0, _tabs.Count - 1);
            Paint();
        }

        // Returns true while the pointer is over any tab.
        public bool Tick(Camera cam)
        {
            int hot = -1;
            if (Rect.gameObject.activeInHierarchy)
            {
                for (int i = 0; i < _tabs.Count; i++)
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
            _tip.Show(hot >= 0 ? _tabs[hot].Name : null);
            return hot >= 0;
        }

        private void Paint()
        {
            float x = 0f;
            float rowH = Look == Style.Tabs ? TabActiveH + Tuck : ActiveH;
            for (int i = 0; i < _tabs.Count; i++)
            {
                Tab t = _tabs[i];
                bool on = i == Active;
                bool hot = i == _hot && !on;
                bool down = i == _pressed && i == _hot && !on;
                float w, h;
                if (Look == Style.Tabs)
                {
                    w = TabW;
                    float sink = on ? ActiveSink : InactiveSink;
                    h = (on ? TabActiveH : TabInactiveH) + sink;
                    string wood = _woods != null && i < _woods.Length && _woods[i] != null
                        ? _woods[i] : (on ? "kit_plank_pine" : "kit_plank_walnut");
                    // the flat-footed tab sprite of that wood: kit_plank_X -> kit_tab_X
                    string sprite = (hot ? "kit_plank_pine" : wood).Replace("kit_plank_", "kit_tab_");
                    Theme.KitSkin(t.Plate, sprite, 14);
                    t.Plate.pixelsPerUnitMultiplier = 14f / ((h - sink) / 3f);
                    // the text sits in the part above the field
                    Theme.Place(t.Text, 0f, 0f, w, h - sink);
                    // the foot stands in the field: the tab's bottom is `sink` below
                    // the row's baseline, which the owner puts on the frame's top edge
                    t.Rect.sizeDelta = new Vector2(w, h);
                    t.Rect.anchoredPosition = new Vector2(x, -sink);
                    float k2 = down ? 0.8f : (on || hot ? 1f : 0.88f);
                    t.Plate.color = new Color(k2, k2, k2, 1f);
                    if (t.Glyph != null) { Theme.Place(t.Glyph, (w - 12) / 2, 3, 12, 12); t.Glyph.color = on ? Theme.KitCream : Pale; }
                    t.Text.fontSize = 8f;
                    t.Text.color = on ? Theme.KitCream : Pale;
                    x += w + Gap;
                    continue;
                }
                else
                {
                    w = on ? ActiveW : InactiveW;
                    h = on ? ActiveH : InactiveH;
                    if (on)
                    {
                        Theme.KitSkin(t.Plate, "kit_title_oak_nails", 0);
                    }
                    else
                    {
                        Theme.KitSkin(t.Plate, hot ? "kit_plank_pine" : "kit_plank_walnut", 18);
                        t.Plate.pixelsPerUnitMultiplier = 18f / (h / 3f);
                    }
                    var tr = t.Text.rectTransform;
                    tr.anchorMin = Vector2.zero;
                    tr.anchorMax = Vector2.one;
                    tr.offsetMin = Vector2.zero;
                    tr.offsetMax = Vector2.zero;
                }
                // an inactive tab sits low AND a shade dimmer, so the wood alone
                // never has to say which one is up
                float k = down ? 0.8f : (on || hot || Look != Style.Tabs ? 1f : 0.88f);
                t.Plate.color = new Color(k, k, k, 1f);
                t.Text.fontSize = Look == Style.Tabs ? 7f : (on ? 9f : 7f);
                t.Text.color = on ? Theme.KitCream : Pale;
                t.Rect.sizeDelta = new Vector2(w, h);
                t.Rect.anchoredPosition = new Vector2(x, 0f);
                x += w + Gap;
            }
            Rect.sizeDelta = new Vector2(Mathf.Max(0f, x - Gap), rowH);
        }
    }
}
