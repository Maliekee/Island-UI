using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI.UI
{
    // THE AUTOCAST LOOK (Forgejo #119 round 2): a key
    // whose act can run by itself wears gold corners, and one that is running by
    // itself wears sparks travelling round its edge as well - WoW's pet bar, in our
    // gold. The ask: auto-castable "indicated by the golden corners", set to autocast
    // "indicated by the visual effect running on the borders". It belongs to the
    // act's ActRow.IAuto, so any act that opts in gets it: the dock's tile
    // (AnimeCell) and the plank row's socket draw the same two pieces.
    //
    // THE CORNERS are one image, auto_corners 9-sliced: its four corners are the
    // triangles, its middle empty and not drawn (fillCenter off). They are part of
    // the key's frame - over its rim, under every mark - and shown on and off,
    // near and far: right-click switches the act wherever you stand.
    //
    // THE SPARKS are sixteen auto_spark images moved round the key's rounded edge:
    // four sizes, each at its own pace, each four times a quarter round apart, so
    // they pass one another and the loop twinkles (WoW's AutoCastShine). A frame
    // costs sixteen points on the edge and sixteen positions and alphas - no
    // allocation, and nothing at all while the act is off.
    internal sealed class AutoLook
    {
        // make_autocast.py's numbers, in board px: the 9-slice's border (the PNG is
        // drawn at twice the board's pixels), the tile's corner radius, and the
        // sparks' path in from the edge - on the tile's dark outline
        private const float Edge = 24f, PieceK = 2f, CornerR = 13f, Inset = 2.5f;
        private static readonly float[] Boxes = { 20f, 16f, 12f, 10f };
        private static readonly float[] Rounds = { 4.4f, 6f, 7.6f, 9.2f };
        private static readonly float[] Starts = { 0f, .11f, .06f, .17f };

        private readonly Image _corners;
        private readonly RectTransform _run;
        private readonly Image[] _sparks = new Image[16];
        private float _cornersK = -1f, _sparksK = -1f;

        // Harness only: the sparks drawn at this time instead of the clock's, so
        // shots - a third of a second each - can step through the run at 30 fps.
        // Negative: the clock.
        private static float _frozenAt = -1f;

        internal static string HarnessFreeze(string at)
        {
            _frozenAt = float.Parse(at, CultureInfo.InvariantCulture);
            return _frozenAt < 0f ? "running" : "frozen at " + _frozenAt.ToString("0.000", CultureInfo.InvariantCulture);
        }

        // made under `parent` at `sibling`, the corners first and the sparks over them
        public AutoLook(Transform parent, int sibling)
        {
            _corners = AnimePieces.Image(parent, "AutoCorners", "auto_corners");
            _corners.sprite = AnimePieces.Get("auto_corners", new Vector4(Edge * PieceK, Edge * PieceK, Edge * PieceK, Edge * PieceK));
            _corners.type = Image.Type.Sliced;
            _corners.fillCenter = false;
            var run = Owned.New("AutoRun");
            run.transform.SetParent(parent, false);
            _run = (RectTransform)run.transform;
            for (int i = 0; i < _sparks.Length; i++)
            {
                _sparks[i] = AnimePieces.Image(_run, "Spark", "auto_spark");
                var r = _sparks[i].rectTransform;
                r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
                r.pivot = new Vector2(.5f, .5f);
            }
            _corners.transform.SetSiblingIndex(sibling);
            _run.SetSiblingIndex(sibling + 1);
            Hide();
        }

        public void Hide()
        {
            if (_corners.enabled) { _corners.enabled = false; }
            if (_run.gameObject.activeSelf) { _run.gameObject.SetActive(false); }
        }

        // The key's rect in its parent (top-left based, as Theme.Place), and k, the
        // parent's units to a board pixel: the corners' size and the sparks' scale.
        public void Paint(bool capable, bool on, float x, float y, float w, float h, float k)
        {
            if (_corners.enabled != capable) { _corners.enabled = capable; }
            if (capable)
            {
                Theme.Place(_corners, x, y, w, h);
                if (_cornersK != k)
                {
                    _corners.pixelsPerUnitMultiplier = PieceK / k;
                    _cornersK = k;
                }
            }
            bool running = capable && on;
            if (_run.gameObject.activeSelf != running) { _run.gameObject.SetActive(running); }
            if (!running) { return; }
            Theme.Place(_run, x, y, w, h);
            float t = _frozenAt >= 0f ? _frozenAt : Time.unscaledTime;
            float inset = Inset * k, r = (CornerR - Inset) * k;
            for (int i = 0; i < Boxes.Length; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    Image spark = _sparks[i * 4 + j];
                    var rect = spark.rectTransform;
                    rect.anchoredPosition = EdgePoint(Mathf.Repeat(t / Rounds[i] + Starts[i] + j * .25f, 1f), w, h, inset, r);
                    if (_sparksK != k) { rect.sizeDelta = new Vector2(Boxes[i] * k, Boxes[i] * k); }
                    float twinkle = .5f + .5f * Mathf.Sin(2f * Mathf.PI * (t * (.9f + .23f * i) + .37f * j + .19f * i));
                    spark.color = new Color(1f, 1f, 1f, .45f + .55f * twinkle);
                }
            }
            _sparksK = k;
        }

        // A point u (0..1) round the rounded rect `inset` in from a w x h key,
        // clockwise from the top edge's start; y down the key, returned as the
        // anchored position from its top-left corner (y up).
        private static Vector2 EdgePoint(float u, float w, float h, float inset, float r)
        {
            float x0 = inset, y0 = inset, x1 = w - inset, y1 = h - inset;
            float sw = Mathf.Max(0f, x1 - x0 - 2f * r), sh = Mathf.Max(0f, y1 - y0 - 2f * r);
            float arc = Mathf.PI * r * .5f;
            float s = u * (2f * (sw + sh) + 4f * arc);
            float px = x0 + r, py = y0;
            for (int side = 0; side < 4; side++)
            {
                float straight = side % 2 == 0 ? sw : sh;
                if (s < straight)
                {
                    switch (side)
                    {
                        case 0: px = x0 + r + s; py = y0; break;
                        case 1: px = x1; py = y0 + r + s; break;
                        case 2: px = x1 - r - s; py = y1; break;
                        default: px = x0; py = y1 - r - s; break;
                    }
                    break;
                }
                s -= straight;
                if (s < arc || side == 3)
                {
                    // the corner after this side, turning clockwise
                    float a = r > 0f ? Mathf.Min(s, arc) / r : 0f, sin = Mathf.Sin(a), cos = Mathf.Cos(a);
                    switch (side)
                    {
                        case 0: px = x1 - r + r * sin; py = y0 + r - r * cos; break;
                        case 1: px = x1 - r + r * cos; py = y1 - r + r * sin; break;
                        case 2: px = x0 + r - r * sin; py = y1 - r + r * cos; break;
                        default: px = x0 + r - r * cos; py = y0 + r - r * sin; break;
                    }
                    break;
                }
                s -= arc;
            }
            return new Vector2(px, -py);
        }
    }
}
