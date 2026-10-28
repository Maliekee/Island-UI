using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI.UI
{
    // The kit's TOOLTIP (docs/ui-kit.md §3): a sheet of kit_paper with a
    // kit_paper_rim over it, on LIGHT PAPER - cream paper, ink rim, ink text, as
    // the sheets are printed. It was dark paper (#3A2A1C) with cream text.
    //
    // HELPER TEXT AS AN OVERLAY, NOT A CAPTION ROW at the bottom. The deleted vicinity
    // popup spent a row on a caption that named whatever the pointer was over;
    // this says the same thing beside the pointer, costs the window no height,
    // and can carry a sentence rather than a phrase.
    //
    // A TIP CARRIES STRUCTURE, read from its plain text, so a tip is still one
    // string wherever it is written:
    //   <b>Title</b>      a first line all in bold is the title, over a hairline
    //   Label  value      a line split by one run of two or more spaces is a row
    //                     of the tip's table: the label in the label's ink, every
    //                     value starting at one column
    //   (an empty line)   a gap between groups
    //   Fix: what to do   the remedy, in green
    //   anything else     a sentence, wrapped
    // The tip is as wide as it needs, up to MaxWidth.
    //
    // Driven by the owner: Tick(text) every frame with the text for whatever the
    // pointer rests on, or null. It appears after a short rest, follows the
    // pointer while the text stays the same, and vanishes the frame the text
    // goes. Positioned inside a full-screen root so it can float over any frame,
    // and clamped to that root so it never leaves the screen.
    internal sealed class KitTooltip
    {
        // NO REST: the hover appears immediately rather than after a delay. The rest existed so a pointer crossing a dense
        // sheet did not flicker tips on the way past; the flicker is the lesser
        // cost against a readout you have to wait for.
        private const float Delay = 0f;

        // a line starting with this is the remedy: a marker, never shown, so it stays
        // English in every language; the word shown is tip.fix
        internal const string FixMark = "Fix: ";
        private const float MaxWidth = 130f;
        private const float MinWidth = 40f;
        private const float PadX = 6f;
        private const float PadY = 5f;
        private const float OffsetX = 6f;   // from the pointer's hotspot
        private const float OffsetY = 10f;
        private const float LabelGap = 5f;  // from the widest label to the values' column
        private static readonly string LabelHex = "#" + ColorUtility.ToHtmlStringRGB(Theme.DossierLabel);
        private const string FixHex = "#4A8A3A";   // the sheets' good green on paper

        private readonly RectTransform _root;
        private readonly RectTransform _rect;
        private readonly Image _paper;
        private readonly Image _rim;
        private readonly TextMeshProUGUI _title;
        private readonly Image _rule;
        private readonly TextMeshProUGUI _text;
        private string _showing;
        private string _pending;
        private float _restSince;

        public bool Visible => _rect != null && _rect.gameObject.activeSelf;

        // the widest the tip grows; a readout of rows (the placement factors) wants more
        public float Width = MaxWidth;

        private KitTooltip(RectTransform root, SettingsArt settingsArt)
        {
            _root = root;
            var go = Owned.New("KitTooltip");
            go.transform.SetParent(root, false);
            _rect = (RectTransform)go.transform;
            _rect.anchorMin = Vector2.zero;
            _rect.anchorMax = Vector2.zero;
            _rect.pivot = new Vector2(0f, 1f);
            _paper = go.AddComponent<Image>();
            _paper.raycastTarget = false;
            Theme.KitSkin(_paper, "kit_paper", 12);
            _paper.color = Theme.KitCream;
            _rim = Theme.KitImage(go.transform, "Rim", "kit_paper_rim", 12, Theme.KitInk);
            Stretch(_rim.rectTransform);
            _title = Theme.MakeKitText(go.transform, "Title", Theme.FontCaption * 1.1f, Theme.KitInk,
                TextAlignmentOptions.TopLeft, bold: true);
            if (settingsArt != null) { settingsArt.Type(_title, bold: true); }
            Wrap(_title);
            _rule = Theme.KitImage(go.transform, "Rule", null, 0, Theme.DossierSoft, stretch: true);
            _text = Theme.MakeKitText(go.transform, "Text", Theme.FontCaption, Theme.KitInk, TextAlignmentOptions.TopLeft);
            if (settingsArt != null) { settingsArt.Type(_text); }
            Wrap(_text);
            go.SetActive(false);
        }

        private static void Wrap(TextMeshProUGUI text)
        {
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Overflow;
            Theme.FlattenType(text);
        }

        // `root` must be a full-screen rect (anchors 0..1) on the canvas the
        // pointer is measured against; the tip is placed and clamped within it.
        public static KitTooltip Create(RectTransform root, SettingsArt settingsArt = null)
        {
            return new KitTooltip(root, settingsArt);
        }

        // `live`: a readout whose text changes as the pointer moves (the placement
        // factors) is redrawn in place, not hidden for a frame and rested again
        public void Tick(string text, Camera cam, bool live = false)
        {
            if (string.IsNullOrEmpty(text))
            {
                _pending = null;
                Hide();
                return;
            }
            if (live)
            {
                _pending = text;
            }
            if (!ReferenceEquals(text, _pending) && text != _pending)
            {
                _pending = text;
                _restSince = Time.unscaledTime;
                if (Visible)
                {
                    Hide(); // a new target restarts the rest
                }
                return;
            }
            if (!Visible && Time.unscaledTime - _restSince < Delay)
            {
                return;
            }
            if (!Visible || _showing != text)
            {
                Show(text);
            }
            Follow(cam);
        }

        // A tip on a scene's canvas goes with the scene (a save loaded): its owner
        // builds a new one when it next shows text, but hides the old one every
        // frame until then, and a hidden tip that is gone is hidden enough.
        public void Hide()
        {
            _showing = null;
            if (_rect == null)
            {
                return;
            }
            if (_rect.gameObject.activeSelf)
            {
                _rect.gameObject.SetActive(false);
            }
        }

        private void Show(string text)
        {
            _showing = text;
            // icons inside the text (InlineIcons): the sheet is built on first use
            if (text.IndexOf("<sprite", System.StringComparison.Ordinal) >= 0)
            {
                TMP_SpriteAsset icons = InlineIcons.Asset;
                if (_text.spriteAsset != icons) { _text.spriteAsset = icons; }
                if (_title.spriteAsset != icons) { _title.spriteAsset = icons; }
            }
            string body = Compose(text, out string title);
            float inner = Width - 2f * PadX;
            // as wide as the longest line wants, up to Width
            float want = _text.GetPreferredValues(body, 10000f, 10000f).x;
            if (title != null)
            {
                title = "<b>" + title + "</b>";
                want = Mathf.Max(want, _title.GetPreferredValues(title, 10000f, 10000f).x);
            }
            float w = Mathf.Clamp(want + 1f, MinWidth - 2f * PadX, inner);
            float y = PadY;
            bool titled = title != null;
            if (_title.gameObject.activeSelf != titled) { _title.gameObject.SetActive(titled); }
            if (_rule.gameObject.activeSelf != titled) { _rule.gameObject.SetActive(titled); }
            if (titled)
            {
                _title.text = title;
                float th = _title.GetPreferredValues(title, w, 10000f).y;
                Theme.Place(_title, PadX, y, w, th);
                y += th + 1.5f;
                Theme.Place(_rule, PadX, y, w, 0.5f);
                y += 2.5f;
            }
            bool hasBody = body.Length > 0;
            if (_text.gameObject.activeSelf != hasBody) { _text.gameObject.SetActive(hasBody); }
            float h = 0f;
            if (hasBody)
            {
                _text.text = body;
                h = _text.GetPreferredValues(body, w, 10000f).y;
                Theme.Place(_text, PadX, y, w, h);
            }
            else
            {
                y -= 2.5f;
            }
            _rect.sizeDelta = new Vector2(w + 2f * PadX, y + h + PadY);
            _rect.gameObject.SetActive(true);
            _rect.SetAsLastSibling();
            Raise();
        }

        // ABOVE EVERY HUD PIECE, not only above its own root's siblings: a tip
        // raised last inside the window's root still drew under the target dock
        // and the action bar, later children of MainCanvas - a personality tip on
        // the sheet was cut by the dock's portrait (Forgejo #183). So the tip is
        // a canvas of its own, at the tips' order (3: over the HUD, the dock and
        // its fan (2), with the reason tip and the cursor badge), or just above a
        // root that is itself sorted higher (the HUD lifted over the map).
        // Asserted while shown: Unity drops overrideSorting set on an inactive
        // object, and the root's own order can change (LiftOverMap).
        private const int TipOrder = 3;
        private Canvas _sort;

        private void Raise()
        {
            if (_sort == null)
            {
                _sort = _rect.gameObject.AddComponent<Canvas>();
            }
            Transform parent = _rect.parent;
            Canvas under = parent != null ? parent.GetComponentInParent<Canvas>() : null;
            int order = under != null ? Mathf.Max(TipOrder, under.sortingOrder + 1) : TipOrder;
            if (!_sort.overrideSorting || _sort.sortingOrder != order
                || (under != null && _sort.sortingLayerID != under.sortingLayerID))
            {
                _sort.overrideSorting = true;
                _sort.sortingOrder = order;
                if (under != null)
                {
                    _sort.sortingLayerID = under.sortingLayerID;
                }
            }
        }

        // the tip's plain text as its title and its body in rich text (the class's
        // note says what each line can be)
        private string Compose(string text, out string title)
        {
            title = null;
            string[] lines = text.Split('\n');
            int start = 0;
            if (lines.Length > 1 && IsTitle(lines[0]))
            {
                title = lines[0].Substring(3, lines[0].Length - 7);
                start = 1;
                while (start < lines.Length && lines[start].Length == 0) { start++; }
            }
            float column = 0f;
            for (int i = start; i < lines.Length; i++)
            {
                if (Pair(lines[i], out string label, out string _))
                {
                    column = Mathf.Max(column, _text.GetPreferredValues(LabelText(label), 10000f, 10000f).x);
                }
            }
            var sb = new System.Text.StringBuilder();
            for (int i = start; i < lines.Length; i++)
            {
                string line = lines[i];
                if (i > start) { sb.Append('\n'); }
                if (line.Length == 0)
                {
                    sb.Append("<size=45%> </size>");
                }
                else if (line.StartsWith(FixMark, System.StringComparison.Ordinal))
                {
                    sb.Append("<color=").Append(FixHex).Append("><b>").Append(Shared.Loc.Get("tip.fix", "Fix"))
                        .Append("</b>  ").Append(line.Substring(FixMark.Length)).Append("</color>");
                }
                else if (Pair(line, out string label, out string value))
                {
                    // an indent, not a position: a value that wraps hangs at its column
                    sb.Append(LabelText(label)).Append("<indent=")
                        .Append((column + LabelGap).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture))
                        .Append('>').Append(value).Append("</indent>");
                }
                else
                {
                    sb.Append(line);
                }
            }
            return sb.ToString();
        }

        private static string LabelText(string label)
        {
            return "<size=88%><color=" + LabelHex + ">" + label + "</color></size>";
        }

        private static bool IsTitle(string line)
        {
            return line.Length > 7 && line.StartsWith("<b>", System.StringComparison.Ordinal)
                && line.EndsWith("</b>", System.StringComparison.Ordinal)
                && line.IndexOf("<b>", 3, System.StringComparison.Ordinal) < 0;
        }

        // "Label  value": one run of two or more spaces, a short label before it
        private static bool Pair(string line, out string label, out string value)
        {
            label = value = null;
            int gap = line.IndexOf("  ", System.StringComparison.Ordinal);
            if (gap <= 0 || gap > 28 || line.IndexOf("<indent", System.StringComparison.Ordinal) >= 0)
            {
                return false;
            }
            int rest = gap;
            while (rest < line.Length && line[rest] == ' ') { rest++; }
            if (rest >= line.Length || line.IndexOf("  ", rest, System.StringComparison.Ordinal) >= 0)
            {
                return false;
            }
            label = line.Substring(0, gap);
            value = line.Substring(rest);
            return true;
        }

        // Below and to the right of the pointer, flipped or clamped to stay on
        // the root. Root-local coordinates are measured from the root's pivot, so
        // they are shifted to its bottom-left corner first.
        private void Follow(Camera cam)
        {
            // IN FRONT for as long as it shows, not only when it appears: a
            // window raised after that (the left windows, the floating sheet
            // re-raise themselves) left the tip behind it - the roster's tips
            // went into the background
            Transform parent = _rect.parent;
            if (parent != null && _rect.GetSiblingIndex() != parent.childCount - 1)
            {
                _rect.SetAsLastSibling();
            }
            Raise();
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _root, Input.mousePosition, cam, out local))
            {
                return;
            }
            Rect rr = _root.rect;
            float px = local.x - rr.xMin;
            float py = local.y - rr.yMin;
            Vector2 size = _rect.sizeDelta;
            float x = px + OffsetX;
            float y = py - OffsetY; // top edge, growing down
            if (x + size.x > rr.width)
            {
                x = px - OffsetX - size.x;
            }
            if (y - size.y < 0f)
            {
                y = py + OffsetY + size.y;
            }
            x = Mathf.Clamp(x, 0f, Mathf.Max(0f, rr.width - size.x));
            y = Mathf.Clamp(y, size.y, rr.height);
            _rect.anchoredPosition = new Vector2(x, y);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
