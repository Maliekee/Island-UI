using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI.UI
{
    // The kit's TOOLTIP (docs/ui-kit.md §3): a sheet of kit_paper with a
    // kit_paper_rim over it, in two colour pairs - light (cream paper, ink rim,
    // ink text) and dark (dark paper #3A2A1C, cream rim, cream text). One
    // prefab, two palettes, which is why the paper is two layers.
    //
    // HELPER TEXT AS AN OVERLAY, NOT A CAPTION ROW at the bottom. The deleted vicinity
    // popup spent a row on a caption that named whatever the pointer was over;
    // this says the same thing beside the pointer, costs the window no height,
    // and can carry a sentence rather than a phrase.
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
        private const float Width = 110f;
        private const float PadX = 6f;
        private const float PadY = 5f;
        private const float OffsetX = 6f;   // from the pointer's hotspot
        private const float OffsetY = 10f;
        // docs/ui-kit.md §4: dark paper for the dark tooltip
        private static readonly Color DarkPaper = Theme.ParseColor("#3A2A1C", Theme.PlateDark);

        private readonly RectTransform _root;
        private readonly RectTransform _rect;
        private readonly Image _paper;
        private readonly Image _rim;
        private readonly TextMeshProUGUI _text;
        private string _showing;
        private string _pending;
        private float _restSince;

        public bool Visible => _rect.gameObject.activeSelf;

        private KitTooltip(RectTransform root, bool dark, SettingsArt settingsArt)
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
            _paper.color = dark ? DarkPaper : Theme.KitCream;
            _rim = Theme.KitImage(go.transform, "Rim", "kit_paper_rim", 12,
                dark ? Theme.KitCream : Theme.KitInk);
            Stretch(_rim.rectTransform);
            _text = Theme.MakeKitText(go.transform, "Text", Theme.FontCaption,
                dark ? Theme.KitCream : Theme.KitInk, TextAlignmentOptions.TopLeft);
            if (settingsArt != null) { settingsArt.Type(_text); }
            _text.enableWordWrapping = true;
            _text.overflowMode = TextOverflowModes.Overflow;
            Theme.FlattenType(_text);
            Theme.Place(_text, PadX, PadY, Width - 2f * PadX, 10f);
            go.SetActive(false);
        }

        // `root` must be a full-screen rect (anchors 0..1) on the canvas the
        // pointer is measured against; the tip is placed and clamped within it.
        public static KitTooltip Create(RectTransform root, bool dark, SettingsArt settingsArt = null)
        {
            return new KitTooltip(root, dark, settingsArt);
        }

        public void Tick(string text, Camera cam)
        {
            if (string.IsNullOrEmpty(text))
            {
                _pending = null;
                Hide();
                return;
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

        public void Hide()
        {
            _showing = null;
            if (_rect.gameObject.activeSelf)
            {
                _rect.gameObject.SetActive(false);
            }
        }

        private void Show(string text)
        {
            _showing = text;
            _text.text = text;
            _text.ForceMeshUpdate();
            float h = Mathf.Max(10f, _text.preferredHeight);
            Theme.Place(_text, PadX, PadY, Width - 2f * PadX, h);
            _rect.sizeDelta = new Vector2(Width, h + 2f * PadY);
            _rect.gameObject.SetActive(true);
            _rect.SetAsLastSibling();
        }

        // Below and to the right of the pointer, flipped or clamped to stay on
        // the root. Root-local coordinates are measured from the root's pivot, so
        // they are shifted to its bottom-left corner first.
        private void Follow(Camera cam)
        {
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
