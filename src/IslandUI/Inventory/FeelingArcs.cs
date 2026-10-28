using System.Collections.Generic;
using System.Globalization;
using System.Text;
using IslandUI.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI
{
    // THE FEELINGS ARCS (Forgejo #16 round 2; the mockup's
    // direction B): what a love tick does between people, drawn on the HUD's canvas under
    // every window instead of in the world. Round 1's arcs were copies of the game's
    // LinkLineBlue - a LineRenderer 0.025 m wide with no ends - and they proved
    // poor: thin pale lines in rainbow colours, running off the screen, a fan of them from
    // one point in a big camp, and nothing to say whose line it was or what it meant.
    //
    // A STRAND is one soft bow from the source to over the target's head: a core in an ink
    // halo with a shadow, so it reads on sand, grass and timber alike. Red if either
    // dislikes the other's type, green if either likes it, paper if both are plain; dashed
    // when nothing happens at the next tick (not near, or love off). Its head is a PILL with
    // what one tick does, in the placement panel's order, to them | back: a heart +10, a
    // broken heart -5, a dot +5. A target off the screen gets a stub that fades and a TAG on
    // the edge - the pill's two marks, the name, how far - inside a safe inset clear of the
    // log and the docks. A DOT marks a plain neighbour, a SEAL the source.
    //
    // A PIN stands on a food store, a well or a house while Set place is armed (Forgejo #20
    // round 4: "better highlight food boxes and wells ... and
    // prettier"): the kit's pin form, a paper card on a pointer whose tip is the thing, the
    // HUD's painted glyph in a band of the panel's legend colour. A strand in that colour
    // runs to each that counts - dashed when it counts only from the area's edge - and one
    // out of reach stands faded, with none. Off the screen but within a screen of it, a tag
    // on the edge says what and how far, pointing the way.
    //
    // Immediate: the owner hands over this frame's marks and the set lays them out, reusing
    // what it drew last frame and hiding the rest. Everything is in the layer's own space
    // (canvas units, 3.2 px each at 1440p, as the kit is drawn).
    internal enum ArcKind { Strand, Dot, Pin }

    internal struct ArcMark
    {
        internal ArcKind Kind;
        internal Vector3 At;         // in the world: over a head, on a store
        internal int There, Back;    // a strand's tick: their love for them, and theirs back
        internal bool Dotted;        // a strand's: nothing happens at the next tick
        internal Color Tint;         // a pin's band, and its strand
        internal bool Faint;         // a pin's: it counts only from the area's edge (a dashed strand)
        internal bool Far;           // a pin's: out of reach - faded, no strand
        internal string Icon;        // a pin's glyph (AnimePieces: st_food, st_water, st_house)
        internal string Name;
        internal float Metres;
    }

    internal sealed class ArcSet
    {
        internal static readonly Color Ink = Theme.ParseColor("#281A10", Color.black);
        internal static readonly Color Paper = Theme.ParseColor("#F6E9CB", Color.white);
        internal static readonly Color Green = Theme.ParseColor("#54A642", Color.green);
        internal static readonly Color Red = Theme.ParseColor("#CE382A", Color.red);
        private static readonly Color Muted = Theme.ParseColor("#786048", Color.grey);

        // the mockup's pixels at 1440p over 3.2
        private const float Lift = 0.24f, LiftMax = 53f;     // a bow's rise: a share of its length, capped
        private const float CoreFrom = 1.25f, CoreTo = 1.9f;  // the core thickens toward the head
        private const float DashOn = 5f, DashOff = 3.2f;
        private const float StubMax = 47f;                  // toward someone off the screen
        private const float Glyph = 6.2f;                   // a pill's marks
        private const float TagH = 10f, TagGlyph = 5.4f, TagFont = 6f, TagFarFont = 4.8f;
        private const float PinW = 18f, PinRim = 1f, PinNotch = 6f;   // a pin's card, and the pointer under it
        private const float PinCardY = PinNotch - PinRim + (PinW + 2f * PinRim) * 0.5f;   // the card's centre over the tip
        private const float PinFaded = 0.55f, TagIcon = 8f;
        private const int Segments = 28;

        private readonly RectTransform _root;
        private readonly ArcStrands _strands;
        private readonly RectTransform _dotRoot, _pinRoot, _sealRoot, _pillRoot, _tagRoot;
        private readonly Image _sealInk, _sealPaper, _sealPin;
        private readonly List<Image[]> _dots = new List<Image[]>();
        private readonly List<Pin> _pins = new List<Pin>();
        private readonly List<Pill> _pills = new List<Pill>();
        private readonly List<Tag> _tags = new List<Tag>();
        private readonly List<Vector2> _bow = new List<Vector2>(Segments + 1);
        private readonly List<Vector2> _piece = new List<Vector2>(Segments + 1);
        private readonly StringBuilder _dump = new StringBuilder();
        private Camera _cam;

        private sealed class Mark
        {
            internal Image Rim, Face;
        }

        private sealed class Pill
        {
            internal RectTransform Root;
            internal Image Shadow, Rim, Paper, NotchRim, NotchPaper, Rule;
            internal Mark There, Back;
        }

        private sealed class Pin
        {
            internal RectTransform Root;
            internal CanvasGroup Fade;
            internal Image Ground, Shadow, NotchRim, Rim, Band, NotchBand, Paper, Glyph;
        }

        private sealed class Tag
        {
            internal RectTransform Root;
            internal Image Shadow, Rim, Paper, Pointer, Icon;
            internal Mark There, Back;
            internal TextMeshProUGUI Name, Far;
            internal Vector2 Size;
            internal int Side;       // 0 left, 1 right, 2 bottom, 3 top: the edge it points off
        }

        private ArcSet(Transform parent, string name)
        {
            GameObject go = Owned.New(name);
            go.transform.SetParent(parent, false);
            _root = Stretch(go);
            GameObject strands = Owned.New("Strands");
            strands.transform.SetParent(_root, false);
            Stretch(strands);
            _strands = strands.AddComponent<ArcStrands>();
            _strands.raycastTarget = false;
            _dotRoot = Group("Dots");
            _pinRoot = Group("Pins");
            _sealRoot = Group("Seal");
            _sealInk = Img(_sealRoot, "Rim", "kit_dossier_dot", Ink);
            _sealPaper = Img(_sealRoot, "Paper", "kit_dossier_dot", Paper);
            _sealPin = Img(_sealRoot, "Pin", "kit_dossier_dot", Ink);
            _pillRoot = Group("Pills");
            _tagRoot = Group("Tags");
        }

        internal static ArcSet Create(Transform parent, string name)
        {
            return new ArcSet(parent, name);
        }

        internal bool Alive => _root != null;

        internal void Hide()
        {
            if (_root != null && _root.gameObject.activeSelf)
            {
                _root.gameObject.SetActive(false);
            }
        }

        internal bool Shown => _root != null && _root.gameObject.activeSelf;

        // ---------- the frame ----------

        internal void Draw(Camera cam, Vector3 source, IList<ArcMark> marks, Rect safe)
        {
            _cam = cam;
            if (!_root.gameObject.activeSelf)
            {
                _root.gameObject.SetActive(true);
            }
            Rect screen = _root.rect;
            Vector2 from = Local(source, out bool fromBehind);
            _strands.Clear();
            _dump.Length = 0;
            int dots = 0, pins = 0, pills = 0, tags = 0;
            for (int m = 0; m < marks.Count; m++)
            {
                ArcMark mark = marks[m];
                Vector2 at = Local(mark.At, out bool behind);
                bool seen = !behind && Inside(screen, at, 2f);
                if (mark.Kind == ArcKind.Pin)
                {
                    DrawPin(mark, from, fromBehind, at, behind, seen, screen, safe, ref pins, ref tags);
                    continue;
                }
                if (mark.Kind == ArcKind.Dot)
                {
                    if (seen) { PutDot(dots++, at); }
                    continue;
                }
                Color colour = Verdict(mark.There, mark.Back);
                Bow(from, at);
                if (seen && !fromBehind)
                {
                    // the strand stops under the pill's notch
                    Cut(_bow, Length(_bow) - 5f, _piece);
                    if (mark.Dotted) { Dashes(_piece, colour); }
                    else { _strands.Add(_piece, colour, CoreFrom, CoreTo, false); }
                    PutPill(pills++, at, mark.There, mark.Back);
                    _dump.Append("strand ").Append(mark.Name).Append(' ').Append(Signed(mark.There)).Append('|')
                        .Append(Signed(mark.Back)).Append(mark.Dotted ? " dotted " : " solid ").Append(Screen(from))
                        .Append(" -> ").Append(Screen(at)).Append('\n');
                    continue;
                }
                // OFF THE SCREEN: a stub that fades, and the tag where the bow leaves the safe inset
                int exit = Exit(_bow, safe, out Vector2 edge);
                Cut(_bow, exit == 0 ? StubMax : Mathf.Min(StubMax, ArcLengthTo(_bow, exit, edge)), _piece);
                if (_piece.Count > 1 && !fromBehind)
                {
                    _strands.Add(_piece, colour, CoreTo, CoreTo, true);
                }
                Vector2 toward = (at - edge).sqrMagnitude > 0.01f ? (at - edge).normalized : (at - from).normalized;
                PutTag(tags++, edge, toward, safe, mark);
                _dump.Append("tag ").Append(mark.Name).Append(' ').Append(Signed(mark.There)).Append('|')
                    .Append(Signed(mark.Back)).Append(' ').Append(Metres(mark.Metres)).Append(" at ")
                    .Append(Screen(_tags[tags - 1].Root.anchoredPosition)).Append('\n');
            }
            Separate(tags, safe);
            bool sealSeen = !fromBehind && Inside(screen, from, 2f);
            if (_sealRoot.gameObject.activeSelf != sealSeen) { _sealRoot.gameObject.SetActive(sealSeen); }
            if (sealSeen) { PutSeal(from); }
            Rest(_dots, dots);
            for (int i = pins; i < _pins.Count; i++) { Off(_pins[i].Root); }
            for (int i = pills; i < _pills.Count; i++) { Off(_pills[i].Root); }
            for (int i = tags; i < _tags.Count; i++) { Off(_tags[i].Root); }
            _strands.Lay();
            _dump.Insert(0, "seal " + Screen(from) + ", " + dots + " dots, " + pins + " pins; tags within "
                + Screen(new Vector2(safe.xMin, safe.yMax)) + " to " + Screen(new Vector2(safe.xMax, safe.yMin)) + "\n");
        }

        internal string Dump()
        {
            return Shown ? _dump.ToString().TrimEnd('\n') : "hidden";
        }

        internal static Color Verdict(int there, int back)
        {
            return there < 0 || back < 0 ? Red : there > 5 || back > 5 ? Green : Paper;
        }

        // A PIN: on the screen, its card over the thing and - when it counts - a strand to
        // the card in its colour; off the screen but within a screen of it, a tag on the edge
        // (and the stub, when it counts). Further out it is not drawn at all.
        private void DrawPin(ArcMark mark, Vector2 from, bool fromBehind, Vector2 at, bool behind, bool seen,
            Rect screen, Rect safe, ref int pins, ref int tags)
        {
            string how = mark.Far ? " out of reach" : mark.Faint ? " dashed" : " solid";
            if (seen)
            {
                if (!mark.Far && !fromBehind)
                {
                    Bow(from, at + new Vector2(0f, PinCardY));
                    Cut(_bow, Length(_bow) - (PinW * 0.5f + PinRim + 1f), _piece);
                    if (mark.Faint) { Dashes(_piece, mark.Tint); }
                    else { _strands.Add(_piece, mark.Tint, CoreFrom, CoreTo, false); }
                }
                PutPin(pins++, at, mark);
                _dump.Append("pin ").Append(mark.Name).Append(how).Append(" at ").Append(Screen(at)).Append('\n');
                return;
            }
            var near = new Rect(screen.xMin - screen.width, screen.yMin - screen.height, screen.width * 3f, screen.height * 3f);
            if (behind || !near.Contains(at))
            {
                return;
            }
            Bow(from, at);
            int exit = Exit(_bow, safe, out Vector2 edge);
            if (!mark.Far && !fromBehind)
            {
                Cut(_bow, exit == 0 ? StubMax : Mathf.Min(StubMax, ArcLengthTo(_bow, exit, edge)), _piece);
                if (_piece.Count > 1) { _strands.Add(_piece, mark.Tint, CoreTo, CoreTo, true); }
            }
            Vector2 toward = (at - edge).sqrMagnitude > 0.01f ? (at - edge).normalized : (at - from).normalized;
            PutTag(tags++, edge, toward, safe, mark);
            _dump.Append("pin-tag ").Append(mark.Name).Append(how).Append(' ').Append(Metres(mark.Metres)).Append(" at ")
                .Append(Screen(_tags[tags - 1].Root.anchoredPosition)).Append('\n');
        }

        // ---------- geometry ----------

        // the world point in the layer's space; one behind the camera is pushed off the
        // screen on the side it is on, as the target marker does (WorldMarker.OffScreen)
        private Vector2 Local(Vector3 world, out bool behind)
        {
            Vector3 s = _cam.WorldToScreenPoint(world);
            behind = s.z <= 0f;
            if (behind)
            {
                var centre = new Vector2(UnityEngine.Screen.width * .5f, UnityEngine.Screen.height * .5f);
                Vector2 away = centre - (Vector2)s;
                if (away.sqrMagnitude < 1f) { away = Vector2.down; }
                Vector2 far = centre + away.normalized * (UnityEngine.Screen.width + UnityEngine.Screen.height);
                s = new Vector3(far.x, far.y, 1f);
            }
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, s, null, out Vector2 local);
            return local;
        }

        private string Screen(Vector2 local)
        {
            Vector2 s = RectTransformUtility.WorldToScreenPoint(null, _root.TransformPoint(local));
            return s.x.ToString("0", CultureInfo.InvariantCulture) + "," + (UnityEngine.Screen.height - s.y).ToString("0", CultureInfo.InvariantCulture);
        }

        private static bool Inside(Rect r, Vector2 p, float margin)
        {
            return p.x >= r.xMin + margin && p.x <= r.xMax - margin && p.y >= r.yMin + margin && p.y <= r.yMax - margin;
        }

        // one soft bow, always rising, its height a share of its length
        private void Bow(Vector2 a, Vector2 b)
        {
            _bow.Clear();
            Vector2 d = b - a;
            float length = d.magnitude;
            Vector2 n = length > 0.001f ? new Vector2(-d.y, d.x) / length : Vector2.up;
            if (n.y < 0f) { n = -n; }
            Vector2 c = (a + b) * 0.5f + n * Mathf.Min(length * Lift, LiftMax);
            for (int i = 0; i <= Segments; i++)
            {
                float t = i / (float)Segments, u = 1f - t;
                _bow.Add(u * u * a + 2f * u * t * c + t * t * b);
            }
        }

        private static float Length(List<Vector2> pts)
        {
            float s = 0f;
            for (int i = 1; i < pts.Count; i++) { s += (pts[i] - pts[i - 1]).magnitude; }
            return s;
        }

        private static float ArcLengthTo(List<Vector2> pts, int index, Vector2 end)
        {
            float s = 0f;
            for (int i = 1; i < index && i < pts.Count; i++) { s += (pts[i] - pts[i - 1]).magnitude; }
            return s + (index > 0 && index <= pts.Count ? (end - pts[index - 1]).magnitude : 0f);
        }

        // the polyline from its start to arc length `upto`, into `into`
        private static void Cut(List<Vector2> pts, float upto, List<Vector2> into, float from = 0f)
        {
            into.Clear();
            float s = 0f;
            for (int i = 0; i < pts.Count; i++)
            {
                float next = i + 1 < pts.Count ? s + (pts[i + 1] - pts[i]).magnitude : s;
                if (s >= from && s <= upto) { into.Add(pts[i]); }
                if (i + 1 < pts.Count)
                {
                    if (from > s && from < next) { into.Add(Vector2.Lerp(pts[i], pts[i + 1], (from - s) / (next - s))); }
                    if (upto > s && upto < next)
                    {
                        into.Add(Vector2.Lerp(pts[i], pts[i + 1], (upto - s) / (next - s)));
                        return;
                    }
                }
                s = next;
            }
        }

        // where the bow first leaves the safe inset: the index of the first point outside,
        // and the crossing. A source outside the inset already (up by the log, or behind
        // a dock) aims straight from the inset's nearest point to the target instead: the
        // bow's own crossing would put the tag beside the source, wherever that is.
        private static int Exit(List<Vector2> pts, Rect safe, out Vector2 at)
        {
            if (!safe.Contains(pts[0]))
            {
                Vector2 p = Clamp(pts[0], safe);
                Vector2 dir = pts[pts.Count - 1] - p;
                at = dir.sqrMagnitude > 1e-6f ? RayOut(p, dir.normalized, safe) : p;
                return 0;
            }
            for (int i = 1; i < pts.Count; i++)
            {
                if (safe.Contains(pts[i])) { continue; }
                Vector2 a = pts[i - 1], b = pts[i];
                float lo = 0f, hi = 1f;
                for (int k = 0; k < 18; k++)
                {
                    float mid = (lo + hi) * 0.5f;
                    if (safe.Contains(Vector2.Lerp(a, b, mid))) { lo = mid; } else { hi = mid; }
                }
                at = Vector2.Lerp(a, b, lo);
                return i;
            }
            at = pts[pts.Count - 1];
            return pts.Count;
        }

        private static Vector2 Clamp(Vector2 p, Rect r)
        {
            return new Vector2(Mathf.Clamp(p.x, r.xMin, r.xMax), Mathf.Clamp(p.y, r.yMin, r.yMax));
        }

        // from p inside r along dir, to r's edge
        private static Vector2 RayOut(Vector2 p, Vector2 dir, Rect r)
        {
            float t = float.MaxValue;
            if (dir.x > 1e-5f) { t = Mathf.Min(t, (r.xMax - p.x) / dir.x); }
            if (dir.x < -1e-5f) { t = Mathf.Min(t, (r.xMin - p.x) / dir.x); }
            if (dir.y > 1e-5f) { t = Mathf.Min(t, (r.yMax - p.y) / dir.y); }
            if (dir.y < -1e-5f) { t = Mathf.Min(t, (r.yMin - p.y) / dir.y); }
            return t < float.MaxValue ? p + dir * Mathf.Max(0f, t) : p;
        }

        private void Dashes(List<Vector2> pts, Color colour)
        {
            float total = Length(pts);
            var dash = new List<Vector2>(8);
            for (float s = 0f; s < total; s += DashOn + DashOff)
            {
                Cut(pts, Mathf.Min(s + DashOn, total), dash, s);
                if (dash.Count > 1) { _strands.Add(dash, colour, CoreFrom + (CoreTo - CoreFrom) * s / total, CoreTo, false); }
            }
        }

        // ---------- the marks ----------

        private void PutSeal(Vector2 at)
        {
            Place(_sealInk.rectTransform, at, 7.6f, 7.6f);
            Place(_sealPaper.rectTransform, at, 5.2f, 5.2f);
            Place(_sealPin.rectTransform, at, 2.3f, 2.3f);
        }

        private void PutDot(int i, Vector2 at)
        {
            while (_dots.Count <= i)
            {
                RectTransform r = Holder(_dotRoot, "Dot");
                _dots.Add(new[] { Img(r, "Rim", "kit_dossier_dot", Ink), Img(r, "Paper", "kit_dossier_dot", Paper) });
            }
            Image[] d = _dots[i];
            On(d[0].transform.parent);
            Place(d[0].rectTransform, at, 5.2f, 5.2f);
            Place(d[1].rectTransform, at, 3.4f, 3.4f);
        }

        // the card on its pointer, the pointer's tip on the thing (`at`), a shadow on the
        // ground under it; faded out of reach
        private void PutPin(int i, Vector2 at, ArcMark mark)
        {
            while (_pins.Count <= i)
            {
                RectTransform r = Holder(_pinRoot, "Pin");
                var p = new Pin
                {
                    Root = r,
                    Fade = r.gameObject.AddComponent<CanvasGroup>(),
                    Ground = Img(r, "Ground", "kit_dossier_dot", Alpha(Color.black, 0.35f)),
                    Shadow = Img(r, "Shadow", "kit_dossier_chip", Alpha(Color.black, 0.32f), 8),
                    NotchRim = Img(r, "NotchRim", "kit_pointer", Ink),
                    Rim = Img(r, "Rim", "kit_dossier_chip", Ink, 8),
                    Band = Img(r, "Band", "kit_dossier_chip", Color.white, 8),
                    NotchBand = Img(r, "NotchBand", "kit_pointer", Color.white),
                    Paper = Img(r, "Paper", "kit_dossier_chip", Paper, 8),
                    Glyph = Img(r, "Glyph", null, Color.white),
                };
                p.Fade.blocksRaycasts = false;
                p.Fade.interactable = false;
                p.Glyph.preserveAspect = true;
                _pins.Add(p);
            }
            Pin pin = _pins[i];
            On(pin.Root);
            pin.Root.anchoredPosition = at;
            float rimW = PinW + PinRim * 2f;
            var card = new Vector2(0f, PinCardY);
            Place(pin.Ground.rectTransform, Vector2.zero, 6.4f, 2.2f);
            Place(pin.Shadow.rectTransform, card + new Vector2(0f, -1.2f), rimW + 1.2f, rimW + 1.2f);
            Place(pin.NotchRim.rectTransform, new Vector2(0f, PinNotch * 0.5f), 7f, PinNotch);
            Place(pin.Rim.rectTransform, card, rimW, rimW);
            Place(pin.Band.rectTransform, card, PinW, PinW);
            Place(pin.NotchBand.rectTransform, new Vector2(0f, PinNotch * 0.5f + 1.1f), 4.6f, PinNotch - 2f);
            Place(pin.Paper.rectTransform, card, PinW - 3.2f, PinW - 3.2f);
            Place(pin.Glyph.rectTransform, card, PinW - 3.4f, PinW - 3.4f);
            if (pin.Band.color != mark.Tint) { pin.Band.color = mark.Tint; pin.NotchBand.color = mark.Tint; }
            Sprite glyph = AnimePieces.Get(mark.Icon);
            if (pin.Glyph.sprite != glyph) { pin.Glyph.sprite = glyph; }
            float alpha = mark.Far ? PinFaded : 1f;
            if (pin.Fade.alpha != alpha) { pin.Fade.alpha = alpha; }
        }

        private void PutPill(int i, Vector2 at, int there, int back)
        {
            while (_pills.Count <= i)
            {
                RectTransform r = Holder(_pillRoot, "Pill");
                var p = new Pill
                {
                    Root = r,
                    Shadow = Img(r, "Shadow", "kit_dossier_chip", Alpha(Color.black, 0.32f), 8),
                    NotchRim = Img(r, "NotchRim", "kit_pointer", Ink),
                    Rim = Img(r, "Rim", "kit_dossier_chip", Ink, 8),
                    Paper = Img(r, "Paper", "kit_dossier_chip", Paper, 8),
                    NotchPaper = Img(r, "NotchPaper", "kit_pointer", Paper),
                    Rule = Img(r, "Rule", null, Alpha(Muted, 0.7f)),
                };
                p.There = NewMark(r, "There");
                p.Back = NewMark(r, "Back");
                _pills.Add(p);
            }
            Pill pill = _pills[i];
            On(pill.Root);
            pill.Root.anchoredPosition = at;
            float w = Glyph * 2f + 5.5f, h = Glyph + 3f;
            Place(pill.Shadow.rectTransform, new Vector2(0f, -1f), w + 1.6f, h + 1.6f);
            Place(pill.NotchRim.rectTransform, new Vector2(0f, -h * 0.5f - 1.6f), 4.6f, 3.4f);
            Place(pill.Rim.rectTransform, Vector2.zero, w + 1.6f, h + 1.6f);
            Place(pill.Paper.rectTransform, Vector2.zero, w, h);
            Place(pill.NotchPaper.rectTransform, new Vector2(0f, -h * 0.5f - 0.6f), 3f, 2.2f);
            Place(pill.Rule.rectTransform, Vector2.zero, 0.35f, h - 3f);
            SetMark(pill.There, new Vector2(-Glyph * 0.5f - 1.2f, 0f), there, Glyph);
            SetMark(pill.Back, new Vector2(Glyph * 0.5f + 1.2f, 0f), back, Glyph);
        }

        private void PutTag(int i, Vector2 edge, Vector2 toward, Rect safe, ArcMark mark)
        {
            while (_tags.Count <= i)
            {
                RectTransform r = Holder(_tagRoot, "Tag");
                var t = new Tag
                {
                    Root = r,
                    Shadow = Img(r, "Shadow", "kit_dossier_chip", Alpha(Color.black, 0.32f), 8),
                    Pointer = Img(r, "Pointer", "kit_pointer", Ink),
                    Rim = Img(r, "Rim", "kit_dossier_chip", Ink, 8),
                    Paper = Img(r, "Paper", "kit_dossier_chip", Paper, 8),
                };
                t.There = NewMark(r, "There");
                t.Back = NewMark(r, "Back");
                t.Icon = Img(r, "Icon", null, Color.white);
                t.Icon.preserveAspect = true;
                t.Name = Theme.MakeKitText(r, "Name", TagFont, Ink, TextAlignmentOptions.MidlineLeft);
                t.Far = Theme.MakeKitText(r, "Far", TagFarFont, Muted, TextAlignmentOptions.MidlineLeft);
                foreach (TextMeshProUGUI text in new[] { t.Name, t.Far })
                {
                    text.enableWordWrapping = false;
                    text.overflowMode = TextOverflowModes.Overflow;
                    text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                    text.rectTransform.pivot = new Vector2(0f, 0.5f);
                }
                _tags.Add(t);
            }
            Tag tag = _tags[i];
            On(tag.Root);
            string far = Metres(mark.Metres);
            if (tag.Name.text != mark.Name) { tag.Name.text = mark.Name ?? string.Empty; }
            if (tag.Far.text != far) { tag.Far.text = far; }
            float nameW = tag.Name.GetPreferredValues(tag.Name.text).x;
            float farW = tag.Far.GetPreferredValues(far).x;
            // a person's two marks lead, or a pin's glyph
            bool pin = mark.Kind == ArcKind.Pin;
            float lead = pin ? TagIcon : TagGlyph * 2f + 1.5f;
            float w = 3f + lead + 2.5f + nameW + 2f + farW + 3.5f;
            tag.Size = new Vector2(w, TagH);
            // the side it leaves by, and the tag inside the inset with its point to that edge
            tag.Side = Mathf.Abs(toward.x) >= Mathf.Abs(toward.y) ? (toward.x < 0f ? 0 : 1) : (toward.y < 0f ? 2 : 3);
            Vector2 c = edge - toward * (w * 0.5f * Mathf.Abs(toward.x) + TagH * 0.5f * Mathf.Abs(toward.y) + 5f);
            tag.Root.anchoredPosition = InsideBy(c, safe, tag.Size * 0.5f + new Vector2(4f, 4f));
            float x = -w * 0.5f + 3f;
            Place(tag.Shadow.rectTransform, new Vector2(0f, -1f), w + 1.6f, TagH + 1.6f);
            Place(tag.Rim.rectTransform, Vector2.zero, w + 1.6f, TagH + 1.6f);
            Place(tag.Paper.rectTransform, Vector2.zero, w, TagH);
            if (tag.Icon.gameObject.activeSelf != pin) { tag.Icon.gameObject.SetActive(pin); }
            if (pin)
            {
                HideMark(tag.There);
                HideMark(tag.Back);
                Sprite glyph = AnimePieces.Get(mark.Icon);
                if (tag.Icon.sprite != glyph) { tag.Icon.sprite = glyph; }
                Place(tag.Icon.rectTransform, new Vector2(x + TagIcon * 0.5f, 0f), TagIcon, TagIcon);
            }
            else
            {
                On(tag.There.Face.transform);
                On(tag.Back.Face.transform);
                SetMark(tag.There, new Vector2(x + TagGlyph * 0.5f, 0f), mark.There, TagGlyph);
                SetMark(tag.Back, new Vector2(x + TagGlyph * 1.5f + 1.5f, 0f), mark.Back, TagGlyph);
            }
            Color nameInk = mark.Far ? Muted : Ink;
            if (tag.Name.color != nameInk) { tag.Name.color = nameInk; }
            x += lead + 2.5f;
            tag.Name.rectTransform.anchoredPosition = new Vector2(x, 0.2f);
            tag.Name.rectTransform.sizeDelta = new Vector2(nameW + 1f, TagH);
            tag.Far.rectTransform.anchoredPosition = new Vector2(x + nameW + 2f, 0f);
            tag.Far.rectTransform.sizeDelta = new Vector2(farW + 1f, TagH);
            AimPointer(tag);
        }

        // the pointer on the tag's side toward the edge; kit_pointer points down as drawn
        private static void AimPointer(Tag tag)
        {
            float w = tag.Size.x, h = tag.Size.y;
            Vector2 at;
            float angle;
            switch (tag.Side)
            {
                case 0: at = new Vector2(-w * 0.5f - 2.2f, 0f); angle = -90f; break;
                case 1: at = new Vector2(w * 0.5f + 2.2f, 0f); angle = 90f; break;
                case 2: at = new Vector2(0f, -h * 0.5f - 2.2f); angle = 0f; break;
                default: at = new Vector2(0f, h * 0.5f + 2.2f); angle = 180f; break;
            }
            Place(tag.Pointer.rectTransform, at, 5.4f, 3.8f);
            tag.Pointer.rectTransform.localEulerAngles = new Vector3(0f, 0f, angle);
        }

        // tags on the same edge stand apart, along that edge
        private void Separate(int count, Rect safe)
        {
            for (int pass = 0; pass < 3; pass++)
            {
                bool moved = false;
                for (int a = 0; a < count; a++)
                {
                    for (int b = a + 1; b < count; b++)
                    {
                        Tag p = _tags[a], q = _tags[b];
                        Vector2 d = q.Root.anchoredPosition - p.Root.anchoredPosition;
                        Vector2 need = (p.Size + q.Size) * 0.5f + new Vector2(2f, 1.5f);
                        if (Mathf.Abs(d.x) >= need.x || Mathf.Abs(d.y) >= need.y) { continue; }
                        bool vertical = p.Side <= 1;
                        Vector2 push = vertical
                            ? new Vector2(0f, (need.y - Mathf.Abs(d.y)) * 0.5f * (d.y >= 0f ? 1f : -1f))
                            : new Vector2((need.x - Mathf.Abs(d.x)) * 0.5f * (d.x >= 0f ? 1f : -1f), 0f);
                        p.Root.anchoredPosition = InsideBy(p.Root.anchoredPosition - push, safe, p.Size * 0.5f);
                        q.Root.anchoredPosition = InsideBy(q.Root.anchoredPosition + push, safe, q.Size * 0.5f);
                        moved = true;
                    }
                }
                if (!moved) { break; }
            }
        }

        private static Vector2 InsideBy(Vector2 c, Rect safe, Vector2 half)
        {
            float x = safe.width > half.x * 2f ? Mathf.Clamp(c.x, safe.xMin + half.x, safe.xMax - half.x) : safe.center.x;
            float y = safe.height > half.y * 2f ? Mathf.Clamp(c.y, safe.yMin + half.y, safe.yMax - half.y) : safe.center.y;
            return new Vector2(x, y);
        }

        // a heart +10 (green), a broken heart -5 (red, on a whole heart of ink so the
        // crack shows dark), a dot +5 (ink)
        private Mark NewMark(Transform parent, string name)
        {
            return new Mark { Rim = Img(parent, name + "Rim", "kit_heart", Ink), Face = Img(parent, name, "kit_heart", Green) };
        }

        private static void HideMark(Mark mark)
        {
            Off(mark.Rim.transform);
            Off(mark.Face.transform);
        }

        private static void SetMark(Mark mark, Vector2 at, int v, float size)
        {
            bool plain = v >= 0 && v <= 5;
            if (mark.Rim.gameObject.activeSelf == plain) { mark.Rim.gameObject.SetActive(!plain); }
            string face = plain ? "kit_dossier_dot" : v < 0 ? "kit_heart_broken" : "kit_heart";
            Color colour = plain ? Ink : v < 0 ? Red : Green;
            Sprite sprite = Theme.Kit(face, 0);
            if (mark.Face.sprite != sprite) { mark.Face.sprite = sprite; }
            if (mark.Face.color != colour) { mark.Face.color = colour; }
            if (plain)
            {
                Place(mark.Face.rectTransform, at, size * 0.38f, size * 0.38f);
                return;
            }
            Place(mark.Rim.rectTransform, at + new Vector2(0f, -0.15f), size + 1.5f, size + 1.5f);
            Place(mark.Face.rectTransform, at, size, size);
        }

        // ---------- plumbing ----------

        private static RectTransform Stretch(GameObject go)
        {
            var r = (RectTransform)go.transform;   // Owned.New makes it a RectTransform
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = r.offsetMax = Vector2.zero;
            r.pivot = new Vector2(0.5f, 0.5f);
            return r;
        }

        private RectTransform Group(string name)
        {
            GameObject go = Owned.New(name);
            go.transform.SetParent(_root, false);
            return Stretch(go);
        }

        private static RectTransform Holder(RectTransform parent, string name)
        {
            GameObject go = Owned.New(name);
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            r.sizeDelta = Vector2.zero;
            return r;
        }

        private static Image Img(Transform parent, string name, string sprite, Color colour, int border = 0)
        {
            if (sprite == null)
            {
                GameObject go = Owned.New(name);
                go.transform.SetParent(parent, false);
                var plain = go.AddComponent<Image>();
                plain.raycastTarget = false;
                plain.color = colour;
                return plain;
            }
            Image image = Theme.KitImage(parent, name, sprite, border, colour);
            image.preserveAspect = false;
            return image;
        }

        private static void Place(RectTransform r, Vector2 at, float w, float h)
        {
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = at;
            r.sizeDelta = new Vector2(w, h);
        }

        private static void On(Transform t)
        {
            if (!t.gameObject.activeSelf) { t.gameObject.SetActive(true); }
        }

        private static void Off(Transform t)
        {
            if (t.gameObject.activeSelf) { t.gameObject.SetActive(false); }
        }

        private static void Rest(List<Image[]> pool, int used)
        {
            for (int i = used; i < pool.Count; i++) { Off(pool[i][0].transform.parent); }
        }

        private static Color Alpha(Color c, float a)
        {
            c.a = a;
            return c;
        }

        private static string Signed(int v)
        {
            return v > 0 ? "+" + v : v.ToString(CultureInfo.InvariantCulture);
        }

        private static string Metres(float m)
        {
            return Shared.Loc.Get("settings.unit.metres", "{0} m", Mathf.RoundToInt(m).ToString(CultureInfo.InvariantCulture));
        }
    }

    // THE STRANDS, as one mesh: every shadow, then every halo, then every core, so strands
    // that cross read as one network of cords. Each band is a strip across a soft-edged
    // texture (UI meshes have no antialiasing of their own); a stub's alpha falls to nothing
    // along its length.
    internal sealed class ArcStrands : MaskableGraphic
    {
        private const float Halo = 0.8f, ShadowWide = 1.4f, ShadowDrop = 0.7f;

        private struct Run
        {
            internal int From, Count;
            internal Color32 Colour;
            internal float W0, W1;
            internal bool Fade;
        }

        private readonly List<Vector2> _points = new List<Vector2>();
        private readonly List<Run> _runs = new List<Run>();
        private static Texture2D _soft;

        public override Texture mainTexture => Soft();

        internal void Clear()
        {
            _points.Clear();
            _runs.Clear();
        }

        internal void Add(List<Vector2> pts, Color colour, float w0, float w1, bool fade)
        {
            if (pts.Count < 2) { return; }
            _runs.Add(new Run { From = _points.Count, Count = pts.Count, Colour = colour, W0 = w0, W1 = w1, Fade = fade });
            _points.AddRange(pts);
        }

        internal void Lay()
        {
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var ink = (Color32)ArcSet.Ink;
            foreach (Run r in _runs) { Strip(vh, r, ShadowWide + Halo * 2f, new Color32(0, 0, 0, 80), new Vector2(0f, -ShadowDrop), 0.75f); }
            foreach (Run r in _runs) { Strip(vh, r, Halo * 2f, new Color32(ink.r, ink.g, ink.b, 240), Vector2.zero, 0.25f); }
            foreach (Run r in _runs) { Strip(vh, r, 0f, r.Colour, Vector2.zero, 0.25f); }
        }

        private void Strip(VertexHelper vh, Run r, float extra, Color32 colour, Vector2 offset, float row)
        {
            float total = 0f;
            for (int i = 1; i < r.Count; i++) { total += (_points[r.From + i] - _points[r.From + i - 1]).magnitude; }
            if (total < 0.01f) { return; }
            int start = vh.currentVertCount;
            float s = 0f;
            for (int i = 0; i < r.Count; i++)
            {
                Vector2 p = _points[r.From + i];
                if (i > 0) { s += (p - _points[r.From + i - 1]).magnitude; }
                Vector2 a = _points[r.From + Mathf.Max(i - 1, 0)], b = _points[r.From + Mathf.Min(i + 1, r.Count - 1)];
                Vector2 d = (b - a).sqrMagnitude > 1e-6f ? (b - a).normalized : Vector2.right;
                var n = new Vector2(-d.y, d.x);
                float t = s / total;
                float half = (Mathf.Lerp(r.W0, r.W1, t) + extra) * 0.5f;
                Color32 c = colour;
                if (r.Fade) { c.a = (byte)(c.a * Mathf.Pow(1f - t, 1.4f)); }
                vh.AddVert(p + n * half + offset, c, new Vector2(0f, row));
                vh.AddVert(p - n * half + offset, c, new Vector2(1f, row));
                if (i > 0)
                {
                    int k = start + (i - 1) * 2;
                    vh.AddTriangle(k, k + 1, k + 3);
                    vh.AddTriangle(k, k + 3, k + 2);
                }
            }
        }

        // 32 across, two rows: a crisp band with a texel-and-a-half of falloff each side
        // (the halo and the core), and a soft one for the shadow
        private static Texture2D Soft()
        {
            if (_soft != null) { return _soft; }
            _soft = new Texture2D(32, 2, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "IslandUI_ArcBand",
            };
            for (int x = 0; x < 32; x++)
            {
                float d = Mathf.Abs(x + 0.5f - 16f);
                float crisp = Mathf.Clamp01((16f - d) / 3.5f);
                float soft = Mathf.Pow(Mathf.Clamp01(1f - d / 16f), 1.5f);
                _soft.SetPixel(x, 0, new Color(1f, 1f, 1f, crisp));
                _soft.SetPixel(x, 1, new Color(1f, 1f, 1f, soft));
            }
            _soft.Apply();
            return _soft;
        }
    }
}
