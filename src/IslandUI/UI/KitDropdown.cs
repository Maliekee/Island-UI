using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI.UI
{
    // The kit's DROPDOWN (docs/ui-kit.md §3): an oak plank carrying the
    // current value in cream with a chevron at its right end. Hover swaps the
    // plank for pine; a press darkens the oak and drops the text two pixels.
    // Open, a tall walnut plank hangs beneath it with one cream row per option,
    // and the row under the pointer wears a cream band with ink text.
    //
    // POLLED, like UIButton and every other control this mod draws: the owner
    // calls Tick each frame and the control resolves hover, press and release
    // from the pointer's screen position itself. Two objects rather than one,
    // because the list has to draw over whatever sits beneath the control: the
    // closed plank is placed where the owner says, and the list is a sibling
    // raised to the top of the same parent for as long as it is open.
    internal sealed class KitDropdown
    {
        public const float Height = 17f;      // the plank is 64px = 20 at KitPpu; 20 was slightly too big
        public const float RowHeight = 12f;
        private const float PadX = 6f;
        private const float ListPad = 6f;     // the walnut plank's 18px border
        private const float ChevronW = 7.5f;  // kit_chevron is 24x16px
        private const float ChevronH = 5f;
        private const float PressDrop = 0.6f; // "text 2px down"

        public RectTransform Rect;
        public Action<int> OnChanged;
        public int Index { get; private set; }
        public bool IsOpen { get; private set; }

        private readonly string[] _options;
        private readonly Sprite[] _icons;   // one per option, or null: a glyph before the word
        private bool _printed;
        private SettingsArt _settingsArt;
        private Image _chevron;
        private const float IconW = 11f;
        private Image _face;
        private Image _icon;
        private TextMeshProUGUI _label;
        private RectTransform _list;
        private readonly List<Row> _rows = new List<Row>();
        private bool _hovered;
        private bool _pressed;
        private int _hotRow = -1;
        private int _pressedRow = -1;

        private sealed class Row
        {
            public RectTransform Rect;
            public Image Band;
            public Image Icon;
            public TextMeshProUGUI Text;
        }

        private KitDropdown(string[] options, Sprite[] icons)
        {
            _options = options;
            _icons = icons != null && icons.Length == options.Length ? icons : null;
        }

        // `icons`, when given, is one sprite per option (null entries allowed):
        // the game's own glyph for a job, say, drawn before the word on the face
        // and on each row - the roster's language for the same thing.
        // `listParent`, when given, is where the OPEN LIST lives - a container
        // above everything the list may fall over. Raised among its siblings the
        // list only tops the face's own row; on a sheet the rows below it drew
        // over the list. On open the list is placed under the face by world corners,
        // so the two need not share a parent.
        // `printed`: the dossier's field instead of the oak plank - paper
        // with an ink outline, ink type, the list the same paper beneath it
        public static KitDropdown Create(Transform parent, string name, float x, float y,
            float width, string[] options, int index, Action<int> onChanged, Sprite[] icons = null,
            Transform listParent = null, bool printed = false, SettingsArt settingsArt = null)
        {
            var d = new KitDropdown(options, icons);
            d._printed = printed;
            d._settingsArt = settingsArt;
            d.Index = Mathf.Clamp(index, 0, options.Length - 1);
            d.OnChanged = onChanged;

            var go = Owned.New(name);
            go.transform.SetParent(parent, false);
            d._face = go.AddComponent<Image>();
            d._face.raycastTarget = true;
            if (printed) { Theme.KitSkin(d._face, "kit_dossier_field", 6); }
            else { Theme.KitSkin(d._face, "kit_plank_oak", 18); }
            d.Rect = Theme.Place(d._face, x, y, width, Height);
            Color type = printed ? Theme.DossierInk : Theme.KitCream;

            d._label = Theme.MakeKitText(go.transform, "Value", Theme.FontBody, type,
                TextAlignmentOptions.MidlineLeft, bold: !printed);
            d._label.enableWordWrapping = false;
            Theme.FlattenType(d._label);
            if (settingsArt != null) { settingsArt.Type(d._label); }
            // a longer word shrinks a little before it is cut short
            d._label.enableAutoSizing = true;
            d._label.fontSizeMax = d._label.fontSize;
            d._label.fontSizeMin = d._label.fontSize * 0.8f;
            float textX = PadX;
            if (d._icons != null)
            {
                var igo = Owned.New("Icon");
                igo.transform.SetParent(go.transform, false);
                d._icon = igo.AddComponent<Image>();
                d._icon.raycastTarget = false;
                d._icon.preserveAspect = true;
                Theme.Place(d._icon, PadX, (Height - IconW) * 0.5f, IconW, IconW);
                textX += IconW + 3f;
            }
            Theme.Place(d._label, textX, 0f, width - textX - PadX - ChevronW - Theme.Gap, Height);

            Image chevron = Theme.KitImage(go.transform, "Chevron", "kit_chevron", 0, type);
            d._chevron = chevron;
            Theme.Place(chevron, width - PadX - ChevronW, (Height - ChevronH) * 0.5f, ChevronW, ChevronH);

            var listGo = Owned.New(name + "_List");
            listGo.transform.SetParent(listParent != null ? listParent : parent, false);
            var listBack = listGo.AddComponent<Image>();
            listBack.raycastTarget = true;
            if (printed) { Theme.KitSkin(listBack, "kit_dossier_field", 6); }
            else { Theme.KitSkin(listBack, "kit_plank_walnut_tall", 18); }
            if (settingsArt != null) { SettingsArt.Skin(listBack, settingsArt.ButtonDown); }
            float listH = 2f * ListPad + options.Length * RowHeight;
            // overlapping the plank's bottom edge by a hair, so the list hangs
            // from it rather than floating a gap below it
            d._list = Theme.Place(listBack, x, y + Height - 2f, width, listH);
            for (int i = 0; i < options.Length; i++)
            {
                var row = new Row();
                var rowGo = Owned.New("Row");
                rowGo.transform.SetParent(listGo.transform, false);
                row.Rect = (RectTransform)rowGo.transform;
                Theme.Place(row.Rect, ListPad, ListPad + i * RowHeight, width - 2f * ListPad, RowHeight);
                row.Band = Theme.KitImage(rowGo.transform, "Band", "kit_band", 10,
                    printed ? Theme.DossierRule : Theme.KitCream);
                Stretch(row.Band.rectTransform);
                row.Band.enabled = false;
                float rowTextX = PadX;
                if (d._icons != null)
                {
                    var rigo = Owned.New("Icon");
                    rigo.transform.SetParent(rowGo.transform, false);
                    row.Icon = rigo.AddComponent<Image>();
                    row.Icon.raycastTarget = false;
                    row.Icon.preserveAspect = true;
                    row.Icon.sprite = d._icons[i];
                    row.Icon.enabled = d._icons[i] != null;
                    Theme.Place(row.Icon, PadX, (RowHeight - IconW) * 0.5f, IconW, IconW);
                    rowTextX += IconW + 3f;
                }
                row.Text = Theme.MakeKitText(rowGo.transform, "Text", Theme.FontBody, type,
                    TextAlignmentOptions.MidlineLeft, bold: !printed);
                row.Text.enableWordWrapping = false;
                row.Text.text = options[i];
                Theme.FlattenType(row.Text);
                if (settingsArt != null) { settingsArt.Type(row.Text); }
                Theme.Place(row.Text, rowTextX, 0f, width - 2f * ListPad - rowTextX - PadX, RowHeight);
                d._rows.Add(row);
            }
            listGo.SetActive(false);

            d.PaintFace();
            d.PaintLabel();
            return d;
        }

        // Returns true while the pointer is over the control or its open list,
        // so the owner can keep the world from reading the same click. Ticked
        // at most once a frame: the owner may ask early for an open list (see
        // TickOpen) and again in its own pass, and a second run would see the
        // same mouse-up and toggle the list straight back.
        private int _tickFrame = -1;
        private bool _tickResult;

        public bool Tick(Camera cam)
        {
            if (_tickFrame == Time.frameCount)
            {
                return _tickResult;
            }
            _tickFrame = Time.frameCount;
            _tickResult = TickOnce(cam);
            return _tickResult;
        }

        // AN OPEN LIST GETS FIRST REFUSAL. It may hang over anything - on a sheet
        // it falls over the foot tabs under the frame, which were asked for the
        // pointer before the sheet's own pass and claimed it, so the rows over
        // them could not be picked.
        // The owner asks here before anything else it polls.
        public static bool TickOpen(Camera cam)
        {
            bool taken = false;
            for (int i = OpenLists.Count - 1; i >= 0; i--)
            {
                KitDropdown d = OpenLists[i];
                if (d._face != null && d._list != null && d._face.gameObject.activeInHierarchy && d.Tick(cam))
                {
                    taken = true;
                }
            }
            return taken;
        }

        private bool TickOnce(Camera cam)
        {
            bool over = Contains(Rect, cam);
            bool overList = IsOpen && Contains(_list, cam);
            int hot = -1;
            if (IsOpen)
            {
                for (int i = 0; i < _rows.Count; i++)
                {
                    if (Contains(_rows[i].Rect, cam))
                    {
                        hot = i;
                        break;
                    }
                }
            }
            bool repaint = false;
            if (over != _hovered)
            {
                _hovered = over;
                repaint = true;
            }
            if (hot != _hotRow)
            {
                _hotRow = hot;
                PaintRows();
            }

            if (Input.GetMouseButtonDown(0))
            {
                if (over)
                {
                    _pressed = true;
                    repaint = true;
                }
                else if (IsOpen && hot >= 0)
                {
                    _pressedRow = hot;
                }
                else if (IsOpen && !overList)
                {
                    Close(); // a click anywhere else lets the list go
                }
            }
            if (Input.GetMouseButtonUp(0))
            {
                if (_pressed && over)
                {
                    if (IsOpen)
                    {
                        Close();
                    }
                    else
                    {
                        Open();
                    }
                }
                else if (_pressedRow >= 0 && _pressedRow == hot)
                {
                    Select(hot);
                    Close();
                }
                _pressed = false;
                _pressedRow = -1;
                repaint = true;
            }
            else if (_pressed && !Input.GetMouseButton(0))
            {
                _pressed = false; // lost the press
                repaint = true;
            }
            if (repaint)
            {
                PaintFace();
            }
            return over || overList;
        }

        // The value changed elsewhere (the .cfg, another control): show it
        // without firing OnChanged back at whoever changed it.
        public void SetIndex(int index)
        {
            Index = Mathf.Clamp(index, 0, _options.Length - 1);
            PaintLabel();
            if (_settingsArt != null && IsOpen) { PaintRows(); }
        }

        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }
            IsOpen = false;
            _pressedRow = -1;
            _hotRow = -1;
            OpenLists.Remove(this);
            if (_list != null) { _list.gameObject.SetActive(false); }
            if (_face != null) { PaintFace(); }
        }

        // A list kept away from its face outlives the face's row: when the row
        // is hidden or its window closed with the list up, nothing under the row
        // ticks it shut. The open lists are known, and whoever owns the frame
        // asks once a frame for any whose face has gone dark.
        private static readonly System.Collections.Generic.List<KitDropdown> OpenLists =
            new System.Collections.Generic.List<KitDropdown>();

        public static void CloseOrphans()
        {
            for (int i = OpenLists.Count - 1; i >= 0; i--)
            {
                KitDropdown d = OpenLists[i];
                if (d._face == null || d._list == null || !d._face.gameObject.activeInHierarchy)
                {
                    d.Close();
                }
            }
        }

        private void Open()
        {
            IsOpen = true;
            _hotRow = -1;
            PaintRows();
            if (!ReferenceEquals(_list.parent, _face.transform.parent))
            {
                // a list kept elsewhere hangs from the face wherever the face is
                // now: its top-left two units up from the plank's bottom-left
                var corners = new Vector3[4];
                ((RectTransform)_face.transform).GetWorldCorners(corners);
                _list.position = corners[0] + (corners[1] - corners[0]) * (2f / Height);
            }
            _list.gameObject.SetActive(true);
            _list.SetAsLastSibling();
            if (!OpenLists.Contains(this)) { OpenLists.Add(this); }
            PaintFace();
        }

        // Both halves: the face under its row and the list wherever it lives.
        public void Destroy()
        {
            OpenLists.Remove(this);
            if (_list != null) { UnityEngine.Object.Destroy(_list.gameObject); }
            if (_face != null) { UnityEngine.Object.Destroy(_face.gameObject); }
        }

        private void Select(int i)
        {
            if (i == Index)
            {
                return;
            }
            Index = i;
            PaintLabel();
            if (OnChanged != null)
            {
                OnChanged(i);
            }
        }

        private void PaintLabel()
        {
            _label.text = _options[Index];
            if (_icon != null)
            {
                _icon.sprite = _icons[Index];
                _icon.enabled = _icons[Index] != null;
            }
        }

        // Hover and open are the pine plank; a press is the oak darkened with the
        // text two pixels down. Image.color stays white on wood otherwise - the
        // states are a sprite swap, not a tint (docs/ui-kit.md §6).
        private void PaintFace()
        {
            bool lit = (_hovered || IsOpen) && !_pressed;
            if (_settingsArt != null)
            {
                SettingsArt.Skin(_face, _pressed ? _settingsArt.ButtonDown
                    : lit ? _settingsArt.ButtonHot : _settingsArt.Button);
                _face.color = Color.white;
                _label.color = lit ? Theme.KitInk : Theme.KitCream;
                _chevron.color = _label.color;
            }
            else if (_printed)
            {
                // paper does not light up; it darkens a shade under the pointer
                Theme.KitSkin(_face, "kit_dossier_field", 6);
                float k = _pressed ? 0.86f : lit ? 0.94f : 1f;
                _face.color = new Color(k, k, k, 1f);
            }
            else
            {
                Theme.KitSkin(_face, lit ? "kit_plank_pine" : "kit_plank_oak", 18);
                _face.color = _pressed ? new Color(0.8f, 0.8f, 0.8f, 1f) : Color.white;
            }
            var lr = (RectTransform)_label.transform;
            lr.anchoredPosition = new Vector2(lr.anchoredPosition.x, _pressed ? -PressDrop : 0f);
        }

        private void PaintRows()
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                bool hot = i == _hotRow || (_settingsArt != null && i == Index);
                if (_rows[i].Band.enabled != hot)
                {
                    _rows[i].Band.enabled = hot;
                }
                _rows[i].Text.color = _printed ? Theme.DossierInk : hot ? Theme.KitInk : Theme.KitCream;
            }
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static bool Contains(RectTransform rect, Camera cam)
        {
            return rect != null && rect.gameObject.activeInHierarchy
                && RectTransformUtility.RectangleContainsScreenPoint(rect, Input.mousePosition, cam);
        }
    }
}
