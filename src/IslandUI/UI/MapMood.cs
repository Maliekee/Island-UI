using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI.UI
{
    // MOOD ON THE MAP. Every drain on a villager's mood is about WHERE they are -
    // bad company is two areas overlapping, a night's sleep needs a house door inside
    // the area (Model/MoodCheck.cs) - so the map says what the roster cannot: where to
    // move people.
    //
    // PINS AND YARN. A square pin per villager at the centre of their area, in their
    // mood's colour, slashed when they cannot sleep; red yarn between a pair who cannot
    // stand each other, amber between a pair souring. Hover a pin and only that
    // villager's area is drawn - it is a circle because the game's area is one
    // (NPCManager.NPCDistanceCheck), the one round thing here, and only on demand -
    // with the causes in the map's own hover text (DescriptionViewString, the one that
    // names favourite NPCs on it). A "Mood" chip on the map turns the layer off.
    //
    // WHERE ON THE MAP. The game places its icons at (x / 2, z / 2) under MapIcons
    // (UIManager.MapUpdate); the layer is MapIcons' LAST child, so it shares that space
    // and zooms and pans with the map, over the game's own icons - under them, a whole
    // household's pins hid beneath the base flag. An area set while its
    // villager stood indoors is in the interior's coordinates - and an interior sits
    // under its house (DoorNPC.underOffset), so x and z are the house's anyway.
    //
    // The pins keep their size on screen as the map zooms: every mark is counter-scaled
    // by the zoom, and re-laid only when the zoom changes.
    internal static class MapMood
    {
        private const float PinSize = 6f;        // canvas units, whatever the zoom
        private const float BadgeSize = 9f;
        private const float YarnWidth = 1.1f;
        private const float RingWidth = 0.9f;
        private const float Refresh = 1f;        // seconds between re-reading the moods

        private static readonly Color Rebel = new Color32(150, 24, 20, 255);
        private static readonly Color Unhappy = Theme.ParseColor("#E0392B", Color.red);
        private static readonly Color Risk = Theme.ParseColor("#E8A33A", Color.yellow);
        private static readonly Color Content = Theme.ParseColor("#6BBF59", Color.green);
        private static readonly Color PinRim = new Color32(40, 26, 16, 255);
        private static readonly Color YarnBad = new Color32(212, 44, 36, 255);
        private static readonly Color YarnWarn = new Color32(214, 140, 30, 255);
        private static readonly Color[] RankTint = { Rebel, Unhappy, Risk, Content };

        private sealed class Pin
        {
            public CommonStates Who;
            public int Rank;             // 0 rebelling · 1 unhappy · 2 at risk · 3 content
            public int Badge = -1;       // the crowd badge it is drawn as, or -1
            public string Line;          // its line in a crowd's hover
            public Vector2 At;           // map units: the area's centre
            public Vector2 Shown;        // ...and where the pin is drawn, fanned out from its neighbours
            public float Radius;         // map units
            public Color Tint;
            public bool Sleepless;
            public string Tip;
            public RectTransform Rect;
            public Image Face;
            public Image Slash;
        }

        private sealed class Badge
        {
            public Vector2 At;
            public string Tip;
            public RectTransform Rect;
            public Image Face;
            public TextMeshProUGUI Word;
        }

        private struct Yarn
        {
            public Pin A, B;
            public Color Tint;
        }

        private static RectTransform _layer;
        private static RectTransform _icons;
        private static RectTransform _counts;   // the crowd counts, on the canvas itself
        private static RectTransform _mapView;  // the map's masked frame, which clips them
        private static Image _chip, _chipBox, _chipMark;
        private static TextMeshProUGUI _chipWord;
        private static readonly List<Pin> Pins = new List<Pin>();
        private static readonly List<Yarn> Yarns = new List<Yarn>();
        private static readonly List<Image> Segments = new List<Image>();
        private static readonly List<Image> Ring = new List<Image>();
        private static Image _disc;
        private static float _readAt = -10f;
        private static float _laidZoom = -1f;
        private static Pin _hover;
        private static Badge _hoverBadge;
        private static bool _tipShown;

        internal static void Tick(ManagersScript mn)
        {
            UIManager ui = mn != null && mn.allSet ? mn.uiMN : null;
            if (ui == null || ui.mapCanvas == null || !ui.mapCanvas.activeSelf || GameManager.nowEvent)
            {
                Leave(ui);
                return;
            }
            if (!Ensure(ui))
            {
                return;
            }
            PaintChip();
            bool on = Settings.MapMood.Value;
            if (_layer.gameObject.activeSelf != on)
            {
                _layer.gameObject.SetActive(on);
                _counts.gameObject.SetActive(on);
            }
            if (!on)
            {
                Leave(ui);
                return;
            }
            if (Time.unscaledTime - _readAt > Refresh)
            {
                _readAt = Time.unscaledTime;
                Read(mn);
                _laidZoom = -1f;
            }
            float zoom = Zoom();
            if (!Mathf.Approximately(zoom, _laidZoom))
            {
                _laidZoom = zoom;
                // the game stops the zoom at 6 (UIManager.MapScale): there, everyone
                bool closest = ui.mapPivot != null && ui.mapPivot.transform.localScale.x >= MaxZoom - 0.05f;
                Lay(zoom, closest);
            }
            Follow();
            HoverTick(ui, zoom);
        }

        // the counts over their badges, as the map pans; none off the map's frame
        private static void Follow()
        {
            _counts.SetAsLastSibling();
            foreach (Badge b in Badges)
            {
                bool shown = b.Rect.gameObject.activeInHierarchy;
                if (shown && _mapView != null)
                {
                    Vector2 at = RectTransformUtility.WorldToScreenPoint(null, b.Rect.position);
                    shown = RectTransformUtility.RectangleContainsScreenPoint(_mapView, at, null);
                }
                if (b.Word.gameObject.activeSelf != shown) { b.Word.gameObject.SetActive(shown); }
                if (shown) { b.Word.transform.position = b.Rect.position; }
            }
        }

        // the map closed or the layer went off: no tip of ours left standing
        private static void Leave(UIManager ui)
        {
            if (_tipShown && ui != null)
            {
                ui.DescriptionHide();
            }
            _tipShown = false;
            _hover = null;
            _laidZoom = -1f;
            _readAt = -10f;
        }

        // ---------- the layer and the chip ----------

        private static bool Ensure(UIManager ui)
        {
            if (_layer != null && _icons != null)
            {
                return true;
            }
            Transform icons = ui.mapCanvas.transform.Find("MapGrp/MapImage/MapIcons");
            if (icons == null)
            {
                return false;
            }
            _icons = (RectTransform)icons;
            // a new scene's map: the old one's marks went with it
            Pins.Clear();
            Badges.Clear();   // their counts went with the old canvas too
            Segments.Clear();
            Ring.Clear();
            _disc = null;
            GameObject go = Owned.New("IslandUI_MoodLayer");
            _layer = (RectTransform)go.transform;
            _layer.SetParent(_icons, false);
            _layer.anchorMin = _layer.anchorMax = _layer.pivot = new Vector2(0.5f, 0.5f);
            _layer.sizeDelta = Vector2.zero;
            _layer.localPosition = Vector3.zero;
            _layer.localScale = Vector3.one;
            _mapView = ui.mapCanvas.transform.Find("MapGrp") as RectTransform;
            GameObject counts = Owned.New("IslandUI_MoodCounts");
            _counts = (RectTransform)counts.transform;
            _counts.SetParent(ui.mapCanvas.transform, false);
            _counts.anchorMin = Vector2.zero;
            _counts.anchorMax = Vector2.one;
            _counts.offsetMin = _counts.offsetMax = Vector2.zero;

            // the chip: top-left of the map canvas, the HUD's own walnut
            _chip = Theme.KitImage(ui.mapCanvas.transform, "IslandUI_MoodChip", "kit_chip_walnut", 0, Color.white);
            _chip.type = Image.Type.Sliced;
            _chip.raycastTarget = true;
            Theme.Place(_chip, 10f, 10f, 42f, 13f);
            var button = _chip.gameObject.AddComponent<Button>();
            button.targetGraphic = _chip;
            button.onClick.AddListener(() => Settings.MapMood.Value = !Settings.MapMood.Value);
            _chipBox = Theme.KitImage(_chip.transform, "Box", "kit_dossier_check", 0, Theme.KitCream);
            _chipBox.raycastTarget = false;
            Theme.Place(_chipBox, 3f, 3f, 7f, 7f);
            _chipMark = Theme.KitImage(_chip.transform, "Mark", "kit_check", 0, Theme.KitCream);
            _chipMark.raycastTarget = false;
            Theme.Place(_chipMark, 4f, 4f, 5f, 5f);
            _chipWord = Theme.MakeKitText(_chip.transform, "Word", 6.5f, Theme.KitCream, TextAlignmentOptions.MidlineLeft);
            _chipWord.text = "Mood";
            _chipWord.raycastTarget = false;
            Theme.Place(_chipWord, 13f, 0f, 28f, 13f);
            return true;
        }

        private static void PaintChip()
        {
            bool on = Settings.MapMood.Value;
            if (_chipMark.enabled != on)
            {
                _chipMark.enabled = on;
            }
            _chip.transform.SetAsLastSibling();   // over the map's full-screen catcher
        }

        // the map's zoom against the canvas: what one map unit is worth on screen
        private static float Zoom()
        {
            Canvas canvas = _icons.GetComponentInParent<Canvas>();
            float root = canvas != null ? canvas.rootCanvas.transform.lossyScale.x : 1f;
            return root > 0f ? _icons.lossyScale.x / root : 1f;
        }

        // ---------- reading who is where, and how they feel ----------

        private static void Read(ManagersScript mn)
        {
            var people = new List<CommonStates>();
            if (mn.npcMN != null && mn.npcMN.friendList != null)
            {
                foreach (CommonStates who in mn.npcMN.friendList)
                {
                    if (who != null && who.nMove != null && mn.npcMN.IsActiveFriend(who))
                    {
                        people.Add(who);
                    }
                }
            }
            CommonStates partner = InventoryWindow.Partner(mn);
            if (partner != null && partner.nMove != null)
            {
                people.Add(partner);
            }
            int used = 0;
            var pinOf = new Dictionary<CommonStates, Pin>();
            var clashes = new Dictionary<long, Yarn>();
            foreach (CommonStates who in people)
            {
                List<MoodFinding> found = MoodCheck.Check(mn, who);
                Pin pin = TakePin(used++);
                pin.Who = who;
                Vector3 t = who.nMove.territory;
                pin.At = new Vector2(t.x / 2f, t.z / 2f);
                pin.Radius = who.nMove.territoryRad / 2f;
                pin.Rank = Rank(who, found);
                pin.Tint = RankTint[pin.Rank];
                string word = MoodCheck.Word(mn, who, found);
                pin.Line = (who.charaName ?? "?") + "  " + who.moral.ToString("0") + (string.IsNullOrEmpty(word) ? string.Empty : "  " + word);
                pin.Sleepless = false;
                foreach (MoodFinding f in found)
                {
                    if (f.Key == "sleep" && f.Severity != MoodSeverity.Good) { pin.Sleepless = true; }
                }
                pin.Tip = TipFor(who, found);
                pinOf[who] = pin;
                foreach (MoodFinding f in found)
                {
                    if (f.Key != "company" || f.Other == null || f.Severity < MoodSeverity.Warn)
                    {
                        continue;
                    }
                    int a = Mathf.Min(who.friendID, f.Other.friendID), b = Mathf.Max(who.friendID, f.Other.friendID);
                    long key = ((long)a << 32) | (uint)b;
                    bool bad = f.Severity == MoodSeverity.Bad;
                    if (!clashes.TryGetValue(key, out Yarn y) || bad)
                    {
                        clashes[key] = new Yarn { Tint = bad ? YarnBad : YarnWarn };
                    }
                }
            }
            for (int i = used; i < Pins.Count; i++)
            {
                Pins[i].Rect.gameObject.SetActive(false);
                Pins[i].Who = null;
                Pins[i].Badge = -1;
            }
            Yarns.Clear();
            foreach (KeyValuePair<long, Yarn> pair in clashes)
            {
                CommonStates a = FindPinned(pinOf, (int)(pair.Key >> 32));
                CommonStates b = FindPinned(pinOf, (int)(pair.Key & 0xffffffff));
                if (a == null || b == null)
                {
                    continue;
                }
                Yarns.Add(new Yarn { A = pinOf[a], B = pinOf[b], Tint = pair.Value.Tint });
            }
        }

        private static CommonStates FindPinned(Dictionary<CommonStates, Pin> pinOf, int friendID)
        {
            foreach (CommonStates who in pinOf.Keys)
            {
                if (who.friendID == friendID) { return who; }
            }
            return null;
        }

        private static int Rank(CommonStates who, List<MoodFinding> found)
        {
            int level = who.debuff != null ? who.debuff.discontent : 0;
            if (level >= 3) { return 0; }
            if (MoodCheck.Unhappy(who)) { return 1; }
            return MoodCheck.Headline(found, out _) != null ? 2 : 3;
        }

        // the map's hover text: the name and mood, then what moves it
        private static string TipFor(CommonStates who, List<MoodFinding> found)
        {
            var lines = new List<string> { (who.charaName ?? "?") + "  " + who.moral.ToString("0") };
            int shown = 0;
            foreach (MoodFinding f in found)
            {
                if (f.Short == null || f.Severity == MoodSeverity.Info || shown >= 4)
                {
                    continue;
                }
                string s = f.Short;
                lines.Add(char.ToUpperInvariant(s[0]) + s.Substring(1) + (f.Effect != null ? "  " + f.Effect : string.Empty));
                shown++;
            }
            return string.Join("\n", lines.ToArray());
        }

        // ---------- laying the marks out at this zoom ----------

        private const float MaxZoom = 6f;   // UIManager.MapScale's clamp

        private static void Lay(float zoom, bool closest)
        {
            float k = zoom > 0.0001f ? 1f / zoom : 1f;   // canvas units -> map units
            _layer.SetAsLastSibling();
            int badges = Cluster(k, closest);
            int seg = 0;
            foreach (Yarn y in Yarns)
            {
                // a clash inside one badge is in that badge's hover, not a knot on it
                if (y.A.Badge >= 0 && y.A.Badge == y.B.Badge)
                {
                    continue;
                }
                // a string between two pins sags a little under its own weight
                Vector2 a = y.A.Shown, b = y.B.Shown;
                Vector2 mid = (a + b) * 0.5f + new Vector2(0f, -0.12f * Vector2.Distance(a, b));
                Vector2 prev = a;
                const int steps = 6;
                for (int s = 1; s <= steps; s++)
                {
                    float t = s / (float)steps;
                    Vector2 p = (1 - t) * (1 - t) * a + 2 * (1 - t) * t * mid + t * t * b;
                    Segment(seg++, prev, p, YarnWidth * k, y.Tint);
                    prev = p;
                }
            }
            for (int i = seg; i < Segments.Count; i++)
            {
                if (Segments[i].gameObject.activeSelf) { Segments[i].gameObject.SetActive(false); }
            }
            foreach (Pin pin in Pins)
            {
                bool shown = pin.Who != null && pin.Badge < 0;
                if (pin.Rect.gameObject.activeSelf != shown) { pin.Rect.gameObject.SetActive(shown); }
                if (!shown) { continue; }
                pin.Rect.localPosition = new Vector3(pin.Shown.x, pin.Shown.y, 0f);
                pin.Rect.localScale = new Vector3(k, k, 1f);
                pin.Face.color = RankTint[pin.Rank];
                pin.Slash.enabled = pin.Sleepless;
                pin.Rect.SetAsLastSibling();   // pins over the yarn
            }
            for (int i = 0; i < Badges.Count; i++)
            {
                Badge b = Badges[i];
                bool shown = i < badges;
                if (b.Rect.gameObject.activeSelf != shown) { b.Rect.gameObject.SetActive(shown); }
                if (!shown) { continue; }
                b.Rect.localPosition = new Vector3(b.At.x, b.At.y, 0f);
                b.Rect.localScale = new Vector3(k, k, 1f);
                b.Rect.SetAsLastSibling();
            }
            _hover = null;   // the ring is re-drawn at this zoom
            _hoverBadge = null;
            HideRing();
        }

        // A BASE IS A HANDFUL OF PINS ON A FEW SPOTS (at the
        // map's own zoom thirty villagers were one blob over the base flag). Villagers
        // whose areas sit within a few pins' widths of each other on screen are one
        // group. A small group fans out into a grid, a pin each, so every pin is seen
        // and has its own end for the yarn; a crowd becomes a single badge - the head
        // count, in the worst mood among them - that splits into pins as you zoom in.
        // Returns the badges in use.
        private const int CrowdAt = 5;

        private static int Cluster(float k, bool everyone)
        {
            float step = PinSize * 1.15f * k;   // a pin's width, in map units at this zoom
            float reach = step * 3f;
            var live = new List<Pin>();
            foreach (Pin pin in Pins)
            {
                if (pin.Who != null) { pin.Badge = -1; live.Add(pin); }
            }
            // single linkage: a pin within reach of any member joins the group
            var group = new int[live.Count];
            for (int i = 0; i < live.Count; i++) { group[i] = i; }
            int Root(int i) { while (group[i] != i) { i = group[i] = group[group[i]]; } return i; }
            for (int i = 0; i < live.Count; i++)
            {
                for (int j = i + 1; j < live.Count; j++)
                {
                    if (Vector2.Distance(live[i].At, live[j].At) < reach)
                    {
                        group[Root(i)] = Root(j);
                    }
                }
            }
            var groups = new Dictionary<int, List<Pin>>();
            for (int i = 0; i < live.Count; i++)
            {
                int r = Root(i);
                if (!groups.TryGetValue(r, out List<Pin> g)) { groups[r] = g = new List<Pin>(); }
                g.Add(live[i]);
            }
            // A crowd is a badge - except at the closest zoom, which the game stops
            // short of splitting a household: zoom is capped, so at the closest
            // zoom everyone is always shown. There
            // every group is a matrix of pins.
            int badges = 0;
            var fans = new List<List<Pin>>();
            foreach (List<Pin> g in groups.Values)
            {
                if (!everyone && g.Count >= CrowdAt)
                {
                    Badge b = TakeBadge(badges);
                    FillBadge(b, g, Centre(g), badges);
                    badges++;
                }
                else
                {
                    fans.Add(g);
                }
            }
            // two matrices that would touch become one, until none do
            for (bool merged = true; merged;)
            {
                merged = false;
                for (int i = 0; i < fans.Count && !merged; i++)
                {
                    for (int j = i + 1; j < fans.Count; j++)
                    {
                        if (Touch(fans[i], fans[j], step))
                        {
                            fans[i].AddRange(fans[j]);
                            fans.RemoveAt(j);
                            merged = true;
                            break;
                        }
                    }
                }
            }
            foreach (List<Pin> g in fans)
            {
                // worst first, from the top-left: the eye lands on who needs it
                g.Sort((x, y) => x.Rank != y.Rank ? x.Rank.CompareTo(y.Rank) : x.Who.moral.CompareTo(y.Who.moral));
                Vector2 centre = Centre(g);
                Grid(g.Count, out int cols, out int rows);
                for (int i = 0; i < g.Count; i++)
                {
                    float cx = (i % cols) - (cols - 1) * 0.5f;
                    float cy = (rows - 1) * 0.5f - (i / cols);
                    g[i].Shown = centre + new Vector2(cx, cy) * step;
                }
            }
            return badges;
        }

        private static Vector2 Centre(List<Pin> g)
        {
            Vector2 c = Vector2.zero;
            foreach (Pin p in g) { c += p.At; }
            return c / g.Count;
        }

        private static void Grid(int n, out int cols, out int rows)
        {
            cols = Mathf.CeilToInt(Mathf.Sqrt(n));
            rows = Mathf.CeilToInt(n / (float)cols);
        }

        // would the two matrices, laid out around their centres, touch?
        private static bool Touch(List<Pin> a, List<Pin> b, float step)
        {
            Grid(a.Count, out int ac, out int ar);
            Grid(b.Count, out int bc, out int br);
            Vector2 d = Centre(a) - Centre(b);
            float gap = step * 0.35f;
            return Mathf.Abs(d.x) < (ac + bc) * step * 0.5f + gap
                && Mathf.Abs(d.y) < (ar + br) * step * 0.5f + gap;
        }

        private static void FillBadge(Badge b, List<Pin> g, Vector2 centre, int index)
        {
            g.Sort((x, y) => x.Rank != y.Rank ? x.Rank.CompareTo(y.Rank) : x.Who.moral.CompareTo(y.Who.moral));
            b.At = centre;
            int unhappy = 0, clashes = 0;
            foreach (Pin p in g)
            {
                p.Badge = index;
                p.Shown = centre;
                if (p.Rank <= 1) { unhappy++; }
            }
            foreach (Yarn y in Yarns)
            {
                if (y.A.Badge == index && y.B.Badge == index) { clashes++; }
            }
            b.Face.color = RankTint[g[0].Rank];
            string count = g.Count.ToString();
            if (b.Word.text != count) { b.Word.text = count; }
            var lines = new List<string>
            {
                g.Count + " villagers here" + (unhappy > 0 ? ", " + unhappy + " unhappy" : string.Empty)
                    + (clashes > 0 ? ", " + clashes + (clashes == 1 ? " clash" : " clashes") : string.Empty),
            };
            for (int i = 0; i < g.Count && i < 8; i++)
            {
                lines.Add(g[i].Line);
            }
            if (g.Count > 8) { lines.Add("... and " + (g.Count - 8) + " more - zoom in"); }
            b.Tip = string.Join("\n", lines.ToArray());
        }

        private static readonly List<Badge> Badges = new List<Badge>();

        private static Badge TakeBadge(int i)
        {
            while (Badges.Count <= i)
            {
                GameObject go = Owned.New("MoodCrowd");
                var rect = (RectTransform)go.transform;
                rect.SetParent(_layer, false);
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(BadgeSize, BadgeSize);
                var rim = go.AddComponent<Image>();
                rim.color = PinRim;
                rim.raycastTarget = false;
                Image face = Child(rect, "Face", BadgeSize - 1.6f, BadgeSize - 1.6f);
                // The count lives on the canvas, placed over its badge every frame
                // (Follow) and hidden off the map - kept flat and upright whatever the
                // map's zoom does to the badge.
                TextMeshProUGUI word = Theme.MakeKitText(_counts, "Count", 6.5f, Theme.KitCream, TextAlignmentOptions.Center, bold: true);
                word.raycastTarget = false;
                word.enableWordWrapping = false;
                // a 6.5 line is ~9.7 tall: in the badge's own 9 it truncated to nothing
                word.overflowMode = TextOverflowModes.Overflow;
                var wr = word.rectTransform;
                wr.anchorMin = wr.anchorMax = wr.pivot = new Vector2(0.5f, 0.5f);
                wr.sizeDelta = new Vector2(BadgeSize * 2f, BadgeSize * 1.5f);
                Badges.Add(new Badge { Rect = rect, Face = face, Word = word });
            }
            return Badges[i];
        }

        private static Pin TakePin(int i)
        {
            while (Pins.Count <= i)
            {
                GameObject go = Owned.New("MoodPin");
                var rect = (RectTransform)go.transform;
                rect.SetParent(_layer, false);
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(PinSize, PinSize);
                var rim = go.AddComponent<Image>();
                rim.color = PinRim;
                rim.raycastTarget = false;
                Image face = Child(rect, "Face", PinSize - 1.6f, PinSize - 1.6f);
                Image shine = Child(rect, "Shine", 1.2f, 1.2f);
                shine.color = new Color(1f, 1f, 1f, 0.8f);
                shine.rectTransform.anchoredPosition = new Vector2(-0.9f, 0.9f);
                Image slash = Child(rect, "Slash", PinSize * 1.25f, 1.1f);
                slash.color = PinRim;
                slash.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                Pins.Add(new Pin { Rect = rect, Face = face, Slash = slash });
            }
            return Pins[i];
        }

        private static Image Child(RectTransform parent, string name, float w, float h)
        {
            GameObject go = Owned.New(name);
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(w, h);
            rect.anchoredPosition = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.raycastTarget = false;
            return img;
        }

        // a straight piece of line in map units, from a to b
        private static void Segment(int i, Vector2 a, Vector2 b, float width, Color tint)
        {
            while (Segments.Count <= i)
            {
                GameObject go = Owned.New("MoodLine");
                var rect = (RectTransform)go.transform;
                rect.SetParent(_layer, false);
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0f, 0.5f);
                var img = go.AddComponent<Image>();
                img.raycastTarget = false;
                Segments.Add(img);
            }
            Image line = Segments[i];
            RectTransform r = line.rectTransform;
            Vector2 d = b - a;
            r.localPosition = new Vector3(a.x, a.y, 0f);
            r.sizeDelta = new Vector2(d.magnitude + width * 0.5f, width);
            r.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            r.localScale = Vector3.one;
            if (line.color != tint) { line.color = tint; }
            if (!line.gameObject.activeSelf) { line.gameObject.SetActive(true); }
        }

        // ---------- the hover: one area, and its causes ----------

        private static void HoverTick(UIManager ui, float zoom)
        {
            Pin hit = null;
            float best = 12f * 12f;   // screen px
            Vector2 mouse = Input.mousePosition;
            foreach (Pin pin in Pins)
            {
                if (pin.Who == null || !pin.Rect.gameObject.activeInHierarchy) { continue; }
                Vector2 at = RectTransformUtility.WorldToScreenPoint(null, pin.Rect.position);
                float d = (at - mouse).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    hit = pin;
                }
            }
            Badge crowd = null;
            foreach (Badge b in Badges)
            {
                if (!b.Rect.gameObject.activeInHierarchy) { continue; }
                Vector2 at = RectTransformUtility.WorldToScreenPoint(null, b.Rect.position);
                float d = (at - mouse).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    crowd = b;
                    hit = null;
                }
            }
            if (!ReferenceEquals(hit, _hover) || !ReferenceEquals(crowd, _hoverBadge))
            {
                _hover = hit;
                _hoverBadge = crowd;
                if (hit != null) { DrawRing(hit, zoom); } else { HideRing(); }
            }
            if (hit != null || crowd != null)
            {
                ui.DescriptionViewString(hit != null ? hit.Tip : crowd.Tip);
                _tipShown = true;
            }
            else if (_tipShown)
            {
                ui.DescriptionHide();
                _tipShown = false;
            }
        }

        private static void DrawRing(Pin pin, float zoom)
        {
            float k = zoom > 0.0001f ? 1f / zoom : 1f;
            float r = Mathf.Max(pin.Radius, 0.5f);
            if (_disc == null)
            {
                _disc = Child(_layer, "MoodArea", 1f, 1f);
                _disc.sprite = Disc();
            }
            _disc.rectTransform.localPosition = new Vector3(pin.At.x, pin.At.y, 0f);
            _disc.rectTransform.sizeDelta = new Vector2(2f * r, 2f * r);
            _disc.color = new Color(pin.Tint.r, pin.Tint.g, pin.Tint.b, 0.16f);
            _disc.rectTransform.SetAsFirstSibling();
            _disc.gameObject.SetActive(true);
            const int steps = 40;
            for (int s = 0; s < steps; s++)
            {
                while (Ring.Count <= s)
                {
                    GameObject go = Owned.New("MoodRing");
                    var rect = (RectTransform)go.transform;
                    rect.SetParent(_layer, false);
                    rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                    rect.pivot = new Vector2(0f, 0.5f);
                    var img = go.AddComponent<Image>();
                    img.raycastTarget = false;
                    Ring.Add(img);
                }
                float a0 = s * Mathf.PI * 2f / steps, a1 = (s + 1) * Mathf.PI * 2f / steps;
                Vector2 p0 = pin.At + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * r;
                Vector2 p1 = pin.At + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * r;
                Image seg = Ring[s];
                RectTransform rt = seg.rectTransform;
                Vector2 d = p1 - p0;
                rt.localPosition = new Vector3(p0.x, p0.y, 0f);
                rt.sizeDelta = new Vector2(d.magnitude + RingWidth * k * 0.5f, RingWidth * k);
                rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                seg.color = pin.Tint;
                rt.SetSiblingIndex(1);
                seg.gameObject.SetActive(true);
            }
        }

        private static void HideRing()
        {
            if (_disc != null && _disc.gameObject.activeSelf) { _disc.gameObject.SetActive(false); }
            foreach (Image seg in Ring)
            {
                if (seg.gameObject.activeSelf) { seg.gameObject.SetActive(false); }
            }
        }

        private static Sprite _discSprite;

        // a soft-edged white disc, made once, tinted per area
        private static Sprite Disc()
        {
            if (_discSprite != null)
            {
                return _discSprite;
            }
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            float c = (n - 1) / 2f;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    byte a = (byte)(255f * Mathf.Clamp01(c - d));
                    px[y * n + x] = new Color32(255, 255, 255, a);
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            _discSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
            return _discSprite;
        }
    }
}
