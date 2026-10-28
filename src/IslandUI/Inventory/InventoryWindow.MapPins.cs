using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using IslandUI.UI;

namespace IslandUI
{
    // FAVOURITES ON THE MAP AS PHOTO PINS (Forgejo #14, the mockup's D).
    // The game marks each favourite with a 2-unit dot,
    // Favorite_00, under MapIcons (UIManager.FriendFavCheck), moved to the person once
    // a second (UIManager.MapUpdate), named on hover and walked to on a click
    // (FavoriteInfo). We keep that object, hide its dot, and hang the pin under it: a
    // square photo in the roster's paper, on a pointer whose tip is the person.
    //
    // A CLICK SELECTS, it does not walk: the person becomes the target, as a roster
    // pick makes them (MapPinClick), and their pin is framed in gold and laid over
    // the other pins. The travel flags lie over every pin, so a flag under a crowd
    // is still a click to travel.
    //
    // CONSTANT SIZE. A pin is laid out in canvas units and counter-scaled by the
    // map's zoom, so it stays PinPx on screen (30 px at 1440p) however far in.
    //
    // CLOSE ONES MAKE ROOM. A plate rests over its person; plates that would touch
    // push each other apart, away from each other's middles, until a gap stands
    // between every two (PackMapPins). A plate keeps its place from frame to frame,
    // carried with its person, and only creeps back home when there is room, so a
    // crowd settles instead of jittering. Never merged into one pin.
    //
    // THE POINTER is a wedge drawn as a mesh (MapPinStems), not a sprite: from inside
    // the plate to the person, the kit's width where it leaves the plate, tapering to
    // the tip. All the pointers are one layer under all the plates, so none crosses a
    // photo. A plate pushed far off points with a wedge of capped length and a hair of
    // ink on to a small paper dot at the person.
    //
    // THE PHOTO is the roster's snapshot (Shed.cs), taken while the person is in a
    // loaded area and kept; until there is one, the kit's star. A dead favourite's
    // pin and photo go grey.
    //
    // The plate is the one raycast target: the game's EventTrigger and Button on the
    // parent take its enter, exit and click, so the name on hover and the walk on a
    // click are the game's own. The map closing, and a hot reload, put the game's
    // dots back and take the pins away.
    internal static partial class InventoryWindow
    {
        private const float PinPx = 30f;                    // THE size: the plate, screen px at 1440p
        private const float PinHair = 0.3125f;              // one screen pixel at 1440p, canvas units
        private const float PinSize = PinPx * PinHair;      // the plate, canvas units
        private const float PinBorder = PinSize * 0.09f;    // the paper round the photo
        private const float PinStem = PinSize * 0.34f;      // the pointer below the plate, at rest
        private const float PinStemW = PinSize * 0.4f;      // its width where it leaves the plate
        private const float PinStemMax = PinSize * 0.8f;    // the longest wedge; past it, a leader line
        private const float PinRoom = PinSize + 4f * PinHair;   // two plates' middles, at least: 4 px apart
        private static readonly Color PinPaper = new Color32(246, 240, 226, 255);
        private static readonly Color PinAshPaper = new Color32(196, 192, 186, 255);
        private static readonly Color PinInk = new Color32(40, 27, 18, 255);
        private static readonly Color PinAshInk = new Color32(70, 66, 62, 255);
        private static readonly Color PinWalnut = new Color32(58, 40, 27, 255);
        private static readonly Color PinAsh = new Color32(128, 122, 116, 255);
        private static readonly Color PinAshStar = new Color32(170, 166, 160, 255);

        private sealed class MapPin
        {
            internal FavoriteInfo Fav;           // null on a harness ghost
            internal CommonStates Ghost;         // a ghost's person
            internal Transform Anchor;           // the favourite's icon, or the ghost's stand-in
            internal Image Dot;                  // the game's own, hidden while we draw
            internal RectTransform Root;
            internal Image PlateInk, Plate, Back, StarEdge, Star;
            internal RawImage Snap;
            internal Vector2 At;                 // the person, canvas units from MapIcons' origin
            internal Vector2 Offset;             // the plate's middle from the person, same units
            internal bool Placed;                // Offset is last frame's, not the rest place
            internal bool Dead;
            internal bool Picked;                // the target: framed in gold, over the rest
            internal CommonStates Who => Fav != null ? Fav.favoriteNPC : Ghost;
        }

        private static readonly List<MapPin> MapPins = new List<MapPin>();
        private static readonly Dictionary<CommonStates, Texture2D> GreySnaps = new Dictionary<CommonStates, Texture2D>();
        private static readonly Dictionary<CommonStates, Texture2D> GreyFrom = new Dictionary<CommonStates, Texture2D>();

        internal static void TickMapPins(ManagersScript mn)
        {
            UIManager ui = mn != null && mn.allSet ? mn.uiMN : null;
            if (ui == null || ui.mapCanvas == null || !ui.mapCanvas.activeSelf || ui.favoriteList == null)
            {
                MapPinsOff();
                return;
            }
            Transform icons = ui.mapCanvas.transform.Find("MapGrp/MapImage/MapIcons");
            Canvas canvas = icons != null ? icons.GetComponentInParent<Canvas>() : null;
            if (canvas == null)
            {
                return;
            }
            float root = canvas.rootCanvas.transform.lossyScale.x;
            float zoom = root > 0f ? icons.lossyScale.x / root : 1f;   // canvas units per map unit

            // the pins follow the list: a favourite unstarred took its dot and our pin
            // with it; a new one gets a pin
            MapPins.RemoveAll(p => p.Anchor == null || p.Root == null);
            foreach (FavoriteInfo fav in ui.favoriteList)
            {
                if (fav != null && MapPins.Find(p => p.Fav == fav) == null)
                {
                    MapPins.Add(BuildMapPin(fav, fav.transform, null));
                }
            }
            TickGhosts(mn, icons);

            // the target's pin over the others
            object focus = Focus();
            foreach (MapPin pin in MapPins)
            {
                pin.Picked = focus != null && ReferenceEquals(focus, pin.Who);
            }
            MapPin picked = MapPins.Find(p => p.Picked);
            if (picked != null && MapPins[MapPins.Count - 1] != picked)
            {
                MapPins.Remove(picked);
                MapPins.Add(picked);
            }

            // MapIcons' last children, top first: the travel flags, a click to travel
            // that no pin may cover; the favourites, the target's last; the pointers'
            // layer, under every plate
            MapPinStems stems = PinStemLayer(icons);
            int last = icons.childCount - 1;
            foreach (Transform flag in TravelFlags(ui, icons))
            {
                if (flag.GetSiblingIndex() != last)
                {
                    flag.SetSiblingIndex(last);
                }
                last--;
            }
            for (int i = MapPins.Count - 1; i >= 0; i--, last--)
            {
                Transform t = MapPins[i].Anchor;
                if (t.parent == icons && t.GetSiblingIndex() != last)
                {
                    t.SetSiblingIndex(last);
                }
            }
            if (stems.transform.GetSiblingIndex() != last)
            {
                stems.transform.SetSiblingIndex(last);
            }

            foreach (MapPin pin in MapPins)
            {
                CommonStates who = pin.Who;
                bool shown = who != null && pin.Anchor.gameObject.activeInHierarchy;
                SetShown(pin.Root, shown);
                if (!shown)
                {
                    continue;
                }
                pin.At = (Vector2)pin.Anchor.localPosition * zoom;
                PaintMapPin(pin, who);
            }
            PackMapPins();
            stems.Clear();
            foreach (MapPin pin in MapPins)
            {
                if (pin.Root.gameObject.activeSelf)
                {
                    LayMapPin(pin, root);
                    stems.Add(pin.At + pin.Offset, pin.At, pin.Dead ? PinAshInk : PinInk, pin.Dead ? PinAshPaper : PinPaper);
                }
            }
            stems.Lay(zoom);
            // one portrait a frame, as the roster does it; the roster's own tick
            // only runs while the inventory is up
            TickSnapshots();
        }

        // the fast-travel flags on the map: the base's and one per placed flag
        // (UIManager.CreateFastTravel), the ones that hang under MapIcons
        private static List<Transform> TravelFlags(UIManager ui, Transform icons)
        {
            var flags = new List<Transform>();
            if (ui.travelPointBaseObj != null && ui.travelPointBaseObj.transform.parent == icons)
            {
                flags.Add(ui.travelPointBaseObj.transform);
            }
            if (ui.travelPoint != null)
            {
                foreach (NPCSpawner spawner in ui.travelPoint)
                {
                    GameObject icon = spawner != null ? spawner.travelIcon : null;
                    if (icon != null && icon.transform.parent == icons)
                    {
                        flags.Add(icon.transform);
                    }
                }
            }
            return flags;
        }

        // a pin's click: the person becomes the target
        internal static void SelectFromMap(CommonStates who)
        {
            if (who == null || _mn == null || _mn.gameMN == null)
            {
                return;
            }
            TargetPerson(who, "map");
        }

        // the map closed, or the mod is going: the game's dots back, the pins gone
        internal static void MapPinsOff()
        {
            if (MapPins.Count == 0 && _pinStems == null)
            {
                return;
            }
            foreach (MapPin pin in MapPins)
            {
                if (pin.Dot != null)
                {
                    pin.Dot.enabled = true;
                }
                if (pin.Root != null)
                {
                    pin.Root.SetParent(null, false);
                    Object.Destroy(pin.Root.gameObject);
                }
                if (pin.Fav == null && pin.Anchor != null)
                {
                    Object.Destroy(pin.Anchor.gameObject);
                }
            }
            MapPins.Clear();
            GhostsWanted = 0;
            if (_pinStems != null)
            {
                _pinStems.transform.SetParent(null, false);
                Object.Destroy(_pinStems.gameObject);
            }
            _pinStems = null;
        }

        // ---------- making room ----------

        // Every plate starts where it stood last frame, carried with its person (a new
        // one at rest over its person). If its rest place is open it goes there.
        // Otherwise, if it is far out or its pointer would pass under another plate,
        // it jumps once to the nearest open place round its person in plain sight;
        // else it creeps a step in toward its person, round whatever is in the way,
        // never into a hidden pointer. Then plates that touch push each other apart
        // along the line between their middles, half each, until every two stand
        // PinRoom apart, and every plate is pushed off the people: none covers anyone,
        // and its own stays a short pointer away. Plates on one spot part by their
        // people's places, or failing that by a fixed angle per pair, so a crowd closes
        // round its people as a ring rather than a row.
        private static void PackMapPins()
        {
            var live = MapPins.FindAll(p => p.Root.gameObject.activeSelf);
            int n = live.Count;
            var at = new Vector2[n];
            var home = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                home[i] = live[i].At + new Vector2(0f, PinStem + PinSize * 0.5f);
                at[i] = live[i].Placed ? live[i].At + live[i].Offset : home[i];
            }
            for (int i = 0; i < n; i++)
            {
                if ((home[i] - at[i]).sqrMagnitude < 0.0001f)
                {
                    continue;
                }
                if (PlateFree(at, live, i, home[i]))
                {
                    at[i] = home[i];
                    continue;
                }
                Vector2 way = live[i].At - at[i];
                float far = way.magnitude;
                bool hidden = SightBlocked(at, i, at[i], live[i].At);
                // stuck out beyond the others, or pointing under another's photo:
                // once, to the nearest open place round its person in plain sight,
                // if that is nearer (or, to come out from under, not much further)
                if ((far > PinRoom * 1.2f || hidden) && PlateEscape(at, live, i, out Vector2 open)
                    && (live[i].At - open).magnitude < far * (hidden ? 1.6f : 0.8f))
                {
                    at[i] = open;
                    continue;
                }
                // a step in toward its person, straight or round whatever is in the
                // way; only ever nearer, so a crowd closes round its people and stays
                float step = Mathf.Min(PinCreep, far);
                foreach (float turn in CreepTurns)
                {
                    Vector2 t = at[i] + (Vector2)(Quaternion.Euler(0f, 0f, turn) * (way / far)) * step;
                    if ((live[i].At - t).magnitude < far - 0.01f && PlateFree(at, live, i, t)
                        && (hidden || !SightBlocked(at, i, t, live[i].At)))
                    {
                        at[i] = t;
                        break;
                    }
                }
            }
            for (int pass = 0; pass < 64; pass++)
            {
                bool moved = false;
                for (int i = 0; i < n; i++)
                {
                    for (int j = i + 1; j < n; j++)
                    {
                        Vector2 d = at[j] - at[i];
                        if (Mathf.Max(Mathf.Abs(d.x), Mathf.Abs(d.y)) >= PinRoom - 0.001f)
                        {
                            continue;
                        }
                        Vector2 u = d;
                        if (u.sqrMagnitude < 0.0001f)
                        {
                            u = live[j].At - live[i].At;
                        }
                        if (u.sqrMagnitude < 0.0001f)
                        {
                            float turn = (i * 7 + j * 3) * 2.39996f;   // the golden angle
                            u = new Vector2(Mathf.Cos(turn), Mathf.Sin(turn));
                        }
                        // along u, until the larger axis between them is PinRoom
                        u.Normalize();
                        float axis = Mathf.Max(Mathf.Abs(u.x), Mathf.Abs(u.y));
                        float apart = Vector2.Dot(d, u);
                        float push = (PinRoom / axis - apart) * 0.5f + 0.0005f;
                        at[i] -= u * push;
                        at[j] += u * push;
                        moved = true;
                    }
                }
                // and off the people: none under a plate, its own a pointer away
                for (int i = 0; i < n; i++)
                {
                    for (int j = 0; j < n; j++)
                    {
                        float keep = PinSize * 0.5f + (i == j ? PinStem * 0.7f : 2f * PinHair);
                        Vector2 d = at[i] - live[j].At;
                        if (Mathf.Max(Mathf.Abs(d.x), Mathf.Abs(d.y)) >= keep - 0.001f)
                        {
                            continue;
                        }
                        Vector2 u = d.sqrMagnitude < 0.0001f ? Vector2.up : d.normalized;
                        float axis = Mathf.Max(Mathf.Abs(u.x), Mathf.Abs(u.y));
                        at[i] += u * (keep / axis - Vector2.Dot(d, u) + 0.0005f);
                        moved = true;
                    }
                }
                if (!moved)
                {
                    break;
                }
            }
            for (int i = 0; i < n; i++)
            {
                live[i].Offset = at[i] - live[i].At;
                live[i].Placed = true;
            }
        }

        private const float PinCreep = 1.5f * PinHair;     // a plate's homeward step, a frame
        private static readonly float[] CreepTurns = { 0f, 25f, -25f, 50f, -50f, 75f, -75f };

        // the open place nearest the person on square rings round them, from rest
        // distance out to a plate further; up first among equals
        private static bool PlateEscape(Vector2[] at, List<MapPin> live, int i, out Vector2 open)
        {
            open = Vector2.zero;
            float best = float.MaxValue;
            float rest = PinStem + PinSize * 0.5f;
            for (int r = 0; r <= 4; r++)
            {
                float ring = rest + PinRoom * r * 0.25f;
                for (int k = 0; k < 24; k++)
                {
                    float a = (90f + k * 15f) * Mathf.Deg2Rad;
                    var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    Vector2 t = live[i].At + dir * (ring / Mathf.Max(Mathf.Abs(dir.x), Mathf.Abs(dir.y)));
                    float far = (t - live[i].At).magnitude;
                    if (far < best - 0.01f && PlateFree(at, live, i, t) && !SightBlocked(at, i, t, live[i].At))
                    {
                        best = far;
                        open = t;
                    }
                }
            }
            return best < float.MaxValue;
        }

        // would the pointer from a plate at `from` to its person pass under another plate
        private static bool SightBlocked(Vector2[] at, int self, Vector2 from, Vector2 to)
        {
            for (int k = 0; k < at.Length; k++)
            {
                if (k != self && SegmentHitsSquare(from, to, at[k], PinSize * 0.5f))
                {
                    return true;
                }
            }
            return false;
        }

        // the slab test: does a..b cross the square of half-size h about c
        private static bool SegmentHitsSquare(Vector2 a, Vector2 b, Vector2 c, float h)
        {
            float t0 = 0f, t1 = 1f;
            Vector2 d = b - a;
            for (int axis = 0; axis < 2; axis++)
            {
                float lo = c[axis] - h - a[axis], hi = c[axis] + h - a[axis];
                if (Mathf.Abs(d[axis]) < 0.0001f)
                {
                    if (lo > 0f || hi < 0f)
                    {
                        return false;
                    }
                    continue;
                }
                float u0 = lo / d[axis], u1 = hi / d[axis];
                if (u0 > u1)
                {
                    float swap = u0;
                    u0 = u1;
                    u1 = swap;
                }
                t0 = Mathf.Max(t0, u0);
                t1 = Mathf.Min(t1, u1);
                if (t0 > t1)
                {
                    return false;
                }
            }
            return true;
        }

        private static bool PlateFree(Vector2[] at, List<MapPin> live, int self, Vector2 t)
        {
            for (int k = 0; k < at.Length; k++)
            {
                if (k != self && Mathf.Abs(at[k].x - t.x) < PinRoom && Mathf.Abs(at[k].y - t.y) < PinRoom)
                {
                    return false;
                }
                float keep = PinSize * 0.5f + (k == self ? PinStem * 0.7f : 2f * PinHair);
                if (Mathf.Abs(live[k].At.x - t.x) < keep && Mathf.Abs(live[k].At.y - t.y) < keep)
                {
                    return false;
                }
            }
            return true;
        }

        // ---------- the pointers ----------

        private static MapPinStems _pinStems;

        // one layer under MapIcons, scaled back to canvas units, for every pointer
        private static MapPinStems PinStemLayer(Transform icons)
        {
            if (_pinStems != null && _pinStems.transform.parent == icons)
            {
                return _pinStems;
            }
            if (_pinStems != null)
            {
                Object.Destroy(_pinStems.gameObject);
            }
            GameObject go = Owned.New("IslandUI_MapPinStems");
            go.transform.SetParent(icons, false);
            _pinStems = go.AddComponent<MapPinStems>();
            _pinStems.raycastTarget = false;
            var r = _pinStems.rectTransform;
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = new Vector2(100000f, 100000f);   // never culled by the map's clip
            r.localPosition = Vector3.zero;
            PinShadow(go);
            return _pinStems;
        }

        // The wedges as one mesh, in canvas units about MapIcons' origin: all the ink
        // first, then all the paper, so wedges that cross read as one cut. A wedge's
        // base sits inside its plate (the plate hides it), it leaves the plate PinStemW
        // wide wherever it leaves, and its tip is the person, or PinStemMax past the
        // plate's edge with a leader on to the person's dot.
        internal sealed class MapPinStems : MaskableGraphic
        {
            private struct Stem
            {
                internal Vector2 Plate, Person;
                internal Color32 Ink, Paper;
            }

            private readonly List<Stem> _stems = new List<Stem>();
            private readonly List<Stem> _drawn = new List<Stem>();

            internal void Clear()
            {
                _stems.Clear();
            }

            internal void Add(Vector2 plate, Vector2 person, Color ink, Color paper)
            {
                _stems.Add(new Stem { Plate = plate, Person = person, Ink = ink, Paper = paper });
            }

            // the layer's scale undoes the map's zoom; the mesh again only on a change
            internal void Lay(float zoom)
            {
                float k = zoom > 0f ? 1f / zoom : 1f;
                if (!Mathf.Approximately(transform.localScale.x, k))
                {
                    transform.localScale = new Vector3(k, k, 1f);
                }
                bool same = _stems.Count == _drawn.Count;
                for (int i = 0; same && i < _stems.Count; i++)
                {
                    same = (_stems[i].Plate - _drawn[i].Plate).sqrMagnitude < 0.0001f
                        && (_stems[i].Person - _drawn[i].Person).sqrMagnitude < 0.0001f
                        && _stems[i].Ink.Equals(_drawn[i].Ink);
                }
                if (!same)
                {
                    _drawn.Clear();
                    _drawn.AddRange(_stems);
                    SetVerticesDirty();
                }
            }

            protected override void OnPopulateMesh(VertexHelper vh)
            {
                vh.Clear();
                foreach (Stem s in _drawn)
                {
                    Wedge(vh, s, PinHair, s.Ink);
                }
                foreach (Stem s in _drawn)
                {
                    Wedge(vh, s, 0f, s.Paper);
                }
            }

            // one wedge, grown by `edge` on every side for its ink
            private static void Wedge(VertexHelper vh, Stem s, float edge, Color32 c)
            {
                Vector2 run = s.Person - s.Plate;
                float length = run.magnitude;
                Vector2 u = length > 0.0001f ? run / length : Vector2.down;
                float exit = PinSize * 0.5f / Mathf.Max(Mathf.Abs(u.x), Mathf.Abs(u.y));   // the plate's edge, along u
                if (length <= exit + PinHair)
                {
                    return;                                         // the person under their own plate
                }
                var side = new Vector2(-u.y, u.x);
                float baseAt = PinSize * 0.25f;
                float reach = Mathf.Min(length - exit, PinStemMax);
                float tip = exit + reach;
                float half = Mathf.Min(PinStemW * 0.5f * (tip - baseAt) / reach, PinSize * 0.3f);
                // the ink's tip out by the edge over the wedge's half-angle, capped
                float slope = half / (tip - baseAt);
                float tipOut = edge > 0f ? Mathf.Min(edge * Mathf.Sqrt(1f + 1f / (slope * slope)), 3f * edge) : 0f;
                float halfOut = half + edge * Mathf.Sqrt(1f + slope * slope);
                Vector2 b = s.Plate + u * baseAt;
                Tri(vh, b + side * halfOut, s.Plate + u * (tip + tipOut), b - side * halfOut, c);
                if (length - exit <= PinStemMax)
                {
                    return;
                }
                // the leader: a hair of ink from the wedge on to the person, and the dot
                if (edge > 0f)
                {
                    Quad(vh, s.Plate + u * (tip - PinHair), s.Person, 1.6f * PinHair, c);
                }
                Dot(vh, s.Person, PinHair * 1.6f + edge * 1.2f, c);
            }

            private static void Tri(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Color32 col)
            {
                int i = vh.currentVertCount;
                vh.AddVert(a, col, Vector2.zero);
                vh.AddVert(b, col, Vector2.zero);
                vh.AddVert(c, col, Vector2.zero);
                vh.AddTriangle(i, i + 1, i + 2);
            }

            private static void Quad(VertexHelper vh, Vector2 a, Vector2 b, float w, Color32 col)
            {
                Vector2 d = b - a;
                if (d.sqrMagnitude < 0.0001f)
                {
                    return;
                }
                Vector2 side = new Vector2(-d.y, d.x).normalized * (w * 0.5f);
                int i = vh.currentVertCount;
                vh.AddVert(a + side, col, Vector2.zero);
                vh.AddVert(b + side, col, Vector2.zero);
                vh.AddVert(b - side, col, Vector2.zero);
                vh.AddVert(a - side, col, Vector2.zero);
                vh.AddTriangle(i, i + 1, i + 2);
                vh.AddTriangle(i, i + 2, i + 3);
            }

            private static void Dot(VertexHelper vh, Vector2 at, float r, Color32 col)
            {
                const int steps = 12;
                int o = vh.currentVertCount;
                vh.AddVert(at, col, Vector2.zero);
                for (int k = 0; k < steps; k++)
                {
                    float a = k * Mathf.PI * 2f / steps;
                    vh.AddVert(at + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r, col, Vector2.zero);
                }
                for (int k = 0; k < steps; k++)
                {
                    vh.AddTriangle(o, o + 1 + k, o + 1 + (k + 1) % steps);
                }
            }
        }

        // ---------- one pin ----------

        private static MapPin BuildMapPin(FavoriteInfo fav, Transform anchor, CommonStates ghost)
        {
            var pin = new MapPin { Fav = fav, Ghost = ghost, Anchor = anchor, Dot = fav != null ? fav.GetComponent<Image>() : null };
            if (pin.Dot != null)
            {
                pin.Dot.enabled = false;
            }
            GameObject go = Owned.New("IslandUI_MapPin");
            pin.Root = (RectTransform)go.transform;
            pin.Root.SetParent(anchor, false);
            pin.Root.anchorMin = pin.Root.anchorMax = pin.Root.pivot = new Vector2(0.5f, 0.5f);
            pin.Root.sizeDelta = Vector2.zero;
            pin.Root.localPosition = Vector3.zero;

            // the pointer is the stems layer's (MapPinStems), under every plate
            pin.PlateInk = PinPiece(pin.Root, "PlateInk", "kit_dossier_chip", false);
            PinShadow(pin.PlateInk.gameObject);
            pin.Plate = PinPiece(pin.Root, "Plate", "kit_dossier_chip", false);
            pin.Plate.raycastTarget = true;   // the game's hover and click, through its parent

            pin.Back = PinPiece(pin.Plate.rectTransform, "Photo", "kit_dossier_chip", false);
            pin.Back.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            GameObject snapGo = Owned.New("Snap");
            snapGo.transform.SetParent(pin.Back.transform, false);
            pin.Snap = snapGo.AddComponent<RawImage>();
            pin.Snap.raycastTarget = false;
            Stretch(pin.Snap.rectTransform);
            pin.StarEdge = PinPiece(pin.Back.rectTransform, "StarEdge", "kit_dossier_star", false);
            pin.Star = PinPiece(pin.Back.rectTransform, "Star", "kit_dossier_star", false);

            float photo = PinSize - 2f * PinBorder;
            Size(pin.PlateInk.rectTransform, PinSize + 2f * PinHair);
            Size(pin.Plate.rectTransform, PinSize);
            Size(pin.Back.rectTransform, photo);
            Size(pin.StarEdge.rectTransform, photo * 0.82f + 2f * PinHair);
            Size(pin.Star.rectTransform, photo * 0.82f);
            return pin;
        }

        private static Image PinPiece(Transform parent, string name, string sprite, bool stretch)
        {
            Image image = Theme.KitImage(parent, name, sprite, 0, Color.white, stretch);
            var r = image.rectTransform;
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = Vector2.zero;
            return image;
        }

        private static void PinShadow(GameObject go)
        {
            Shadow drop = go.AddComponent<Shadow>();
            drop.effectColor = new Color(0f, 0f, 0f, 0.43f);
            drop.effectDistance = new Vector2(PinHair, -2f * PinHair);
        }

        private static void Size(RectTransform r, float s)
        {
            r.sizeDelta = new Vector2(s, s);
        }

        // the look: paper or ash, a photo or the star
        private static void PaintMapPin(MapPin pin, CommonStates who)
        {
            bool dead = who.dead != 0;
            Texture2D snap = Snapshot(who);
            if (snap != null && dead)
            {
                snap = GreySnap(who, snap);
            }
            Color paper = pin.Picked ? StarShine : dead ? PinAshPaper : PinPaper;
            Color ink = dead ? PinAshInk : PinInk;
            SetColor(pin.Plate, paper);
            SetColor(pin.PlateInk, ink);
            bool star = snap == null;
            SetColor(pin.Back, star ? (dead ? PinAsh : PinWalnut) : SnapBack);
            if (pin.Snap.texture != snap)
            {
                pin.Snap.texture = snap;
                // the camera frames a head in 160x192: the square round the face
                const float w = 0.74f;
                float h = w * 160f / 192f;
                pin.Snap.uvRect = new Rect((1f - w) * 0.5f, 1f - 0.07f - h, w, h);
            }
            SetShown(pin.Snap, !star);
            SetShown(pin.StarEdge, star);
            SetShown(pin.Star, star);
            if (star)
            {
                SetColor(pin.StarEdge, PinInk);
                SetColor(pin.Star, dead ? PinAshStar : StarShine);
            }
            pin.Dead = dead;
        }

        private static void SetColor(Graphic g, Color c)
        {
            if (g != null && g.color != c)
            {
                g.color = c;
            }
        }

        // a dead favourite's photo, in greys; made again when the snapshot is
        private static Texture2D GreySnap(CommonStates who, Texture2D snap)
        {
            if (GreySnaps.TryGetValue(who, out Texture2D grey) && grey != null
                && GreyFrom.TryGetValue(who, out Texture2D from) && from == snap)
            {
                return grey;
            }
            if (grey == null)
            {
                grey = new Texture2D(snap.width, snap.height, TextureFormat.RGBA32, false);
                GreySnaps[who] = grey;
            }
            Color32[] px = snap.GetPixels32();
            for (int i = 0; i < px.Length; i++)
            {
                byte v = (byte)(90 + (px[i].r * 77 + px[i].g * 150 + px[i].b * 29) / 256 * 140 / 255);
                px[i] = new Color32(v, v, v, px[i].a);
            }
            grey.SetPixels32(px);
            grey.Apply(false);
            GreyFrom[who] = snap;
            return grey;
        }

        // where: the root at the person, counter-scaled to canvas units; the plate at
        // its place from PackMapPins
        private static void LayMapPin(MapPin pin, float root)
        {
            float parent = pin.Anchor.lossyScale.x;
            float k = parent > 0f ? root / parent : 1f;
            if (!Mathf.Approximately(pin.Root.localScale.x, k))
            {
                pin.Root.localScale = new Vector3(k, k, 1f);
            }
            SetPos(pin.Plate.rectTransform, pin.Offset);
            SetPos(pin.PlateInk.rectTransform, pin.Offset);
        }

        private static void SetPos(RectTransform r, Vector2 at)
        {
            if (r.anchoredPosition != at)
            {
                r.anchoredPosition = at;
            }
        }

        // ---------- for the harness ----------

        // "scale[:name|home]" (the harness splits on commas): the map's zoom, as its wheel
        // does it (UIManager.MapScale) but about a favourite's pin or the screen's middle
        internal static string MapZoom(string arg)
        {
            UIManager ui = _mn != null ? _mn.uiMN : null;
            if (ui == null || ui.mapCanvas == null || !ui.mapCanvas.activeSelf)
            {
                return "map closed";
            }
            string[] parts = (arg ?? "").Split(':');
            if (!float.TryParse(parts[0], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float scale))
            {
                return "usage: scale[:name|home]";
            }
            Vector3 about = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
            bool home = parts.Length > 1 && parts[1].Trim() == "home";   // the map as it opens
            if (parts.Length > 1 && !home)
            {
                MapPin pin = MapPins.Find(p => p.Who != null && p.Who.charaName == parts[1].Trim());
                if (pin == null)
                {
                    return "no pin for " + parts[1];
                }
                about = pin.Anchor.position;
            }
            var map = HarmonyLib.Traverse.Create(ui).Field("mapRect").GetValue<RectTransform>();
            Transform was = map.parent;
            ui.mapPivot.transform.position = about;
            map.SetParent(ui.mapPivot.transform);
            scale = Mathf.Clamp(scale, 0.7f, 6f);
            ui.mapPivot.transform.localScale = new Vector3(scale, scale, 1f);
            map.SetParent(was);
            float room = 250f * (scale - 0.2f);
            map.localPosition = new Vector2(Mathf.Clamp(map.localPosition.x, -room, room),
                Mathf.Clamp(map.localPosition.y, -room, room));
            if (home)
            {
                map.localPosition = Vector3.zero;
            }
            return "zoom " + scale;
        }

        // every pin: who, its look, where its plate and its person are on screen, and
        // what a pointer on the plate's middle reaches (the plate; the game's icon
        // above it for enter and click)
        internal static string MapPinsDump()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(MapPins.Count).Append(" pins\n");
            foreach (MapPin pin in MapPins)
            {
                if (pin.Anchor == null || pin.Root == null)
                {
                    continue;
                }
                CommonStates who = pin.Who;
                Vector2 plate = pin.Plate.rectTransform.position;
                Vector2 person = pin.Anchor.position;
                sb.Append(who != null ? who.charaName : "?")
                  .Append(pin.Fav == null ? " ghost" : "")
                  .Append(pin.Dead ? " dead" : "")
                  .Append(pin.Snap.gameObject.activeSelf ? " photo" : " star")
                  .Append(" shown=").Append(pin.Root.gameObject.activeInHierarchy)
                  .Append(" plate=").Append(plate.ToString("F0"))
                  .Append(" person=").Append(person.ToString("F0"))
                  .Append(" size=").Append((pin.Plate.rectTransform.rect.width * pin.Plate.rectTransform.lossyScale.x).ToString("F1"))
                  .Append(" dot=").Append(pin.Dot != null && pin.Dot.enabled ? "on" : "off")
                  .Append(" plate-person=").Append((pin.Offset.magnitude / PinHair).ToString("F0")).Append("px");
                GameObject hit = HitAt(plate);
                sb.Append(" hit=").Append(hit != null ? hit.name : "-");
                if (hit != null)
                {
                    GameObject click = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit);
                    sb.Append(" click->").Append(pin.Fav != null && click == pin.Fav.gameObject ? "favourite" : click != null ? click.name : "-");
                }
                sb.Append('\n');
            }
            // plates closer than a plate (touching), and closer than PinRoom (no gap)
            int touch = 0, tight = 0;
            var live = MapPins.FindAll(p => p.Root != null && p.Root.gameObject.activeSelf);
            for (int i = 0; i < live.Count; i++)
            {
                for (int j = i + 1; j < live.Count; j++)
                {
                    Vector2 d = live[j].At + live[j].Offset - live[i].At - live[i].Offset;
                    float far = Mathf.Max(Mathf.Abs(d.x), Mathf.Abs(d.y));
                    touch += far < PinSize ? 1 : 0;
                    tight += far < PinRoom - 0.01f ? 1 : 0;
                }
            }
            sb.Append("plates overlapping: ").Append(touch).Append(", closer than the gap: ").Append(tight).Append('\n');
            int strays = 0;
            if (_mn != null && _mn.uiMN != null && _mn.uiMN.mapCanvas != null)
            {
                foreach (Transform t in _mn.uiMN.mapCanvas.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "IslandUI_MapPin")
                    {
                        strays++;
                    }
                }
            }
            sb.Append("pin objects under the map: ").Append(strays);
            return sb.ToString();
        }

        // the pointer coming onto a favourite's plate: enter on the hit and each
        // parent, as the input module sends it, so the game's own hover runs
        internal static string MapPinHover(string name)
        {
            MapPin pin = MapPins.Find(p => p.Who != null && p.Who.charaName == name);
            if (pin == null)
            {
                return "no pin for " + name;
            }
            Vector2 at = pin.Plate.rectTransform.position;
            GameObject hit = HitAt(at);
            if (hit == null)
            {
                return "nothing under the plate";
            }
            var data = new PointerEventData(EventSystem.current) { position = at };
            var reached = new System.Text.StringBuilder();
            for (Transform t = hit.transform; t != null; t = t.parent)
            {
                if (ExecuteEvents.Execute(t.gameObject, data, ExecuteEvents.pointerEnterHandler))
                {
                    reached.Append(t.name).Append(' ');
                }
            }
            return "hit " + hit.name + "; enter reached " + reached;
        }

        // GHOSTS: pins on the nearest people who are not favourites, for testing a
        // crowd the save does not have. Harness only, off until asked; they set
        // nothing the save keeps (no Employ.Favorite, no FavoriteInfo), stand on
        // their own icons under MapIcons, and go with the pins on map close and reload.
        private static int GhostsWanted;
        private static float GhostSqueeze = 1f;
        private static float GhostClock;

        // "n[:squeeze]": n ghosts, their spread about their middle times squeeze; 0 clears
        internal static string MapPinsGhost(string arg)
        {
            UIManager ui = _mn != null ? _mn.uiMN : null;
            if (ui == null || ui.mapCanvas == null || !ui.mapCanvas.activeSelf)
            {
                return "map closed";
            }
            string[] parts = (arg ?? "").Split(':');
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            if (!int.TryParse(parts[0], out int n))
            {
                return "usage: n[:squeeze]";
            }
            float squeeze = 1f;
            if (parts.Length > 1 && !float.TryParse(parts[1], System.Globalization.NumberStyles.Float, inv, out squeeze))
            {
                return "usage: n[:squeeze]";
            }
            ClearGhosts();
            GhostsWanted = Mathf.Max(0, n);
            GhostSqueeze = squeeze;
            return "ghosts " + GhostsWanted + " at squeeze " + squeeze.ToString(inv);
        }

        private static void ClearGhosts()
        {
            foreach (MapPin pin in MapPins)
            {
                if (pin.Fav == null && pin.Anchor != null)
                {
                    Object.Destroy(pin.Anchor.gameObject);
                    pin.Anchor = null;
                }
            }
            MapPins.RemoveAll(p => p.Anchor == null);
        }

        // the ghosts made once, the nearest living friends; moved once a second, as
        // the game moves its favourites (UIManager.MapUpdate)
        private static void TickGhosts(ManagersScript mn, Transform icons)
        {
            if (GhostsWanted <= 0)
            {
                return;
            }
            bool fresh = !MapPins.Exists(p => p.Fav == null);
            if (fresh)
            {
                Vector3 me = mn.gameMN.player.transform.position;
                var players = new HashSet<CommonStates>(mn.gameMN.playerCommons ?? new CommonStates[0]);
                var people = new List<CommonStates>();
                foreach (CommonStates c in mn.npcMN != null && mn.npcMN.friendList != null ? mn.npcMN.friendList : new List<CommonStates>())
                {
                    if (c != null && c.dead == 0 && c.gameObject.activeInHierarchy && !players.Contains(c)
                        && !MapPins.Exists(p => p.Who == c))
                    {
                        people.Add(c);
                    }
                }
                people.Sort((a, b) => (a.transform.position - me).sqrMagnitude.CompareTo((b.transform.position - me).sqrMagnitude));
                for (int i = 0; i < people.Count && i < GhostsWanted; i++)
                {
                    GameObject go = Owned.New("IslandUI_MapGhost");
                    var anchor = (RectTransform)go.transform;
                    anchor.SetParent(icons, false);
                    anchor.sizeDelta = new Vector2(2f, 2f);
                    MapPins.Add(BuildMapPin(null, anchor, people[i]));
                }
                GhostClock = 1f;
            }
            GhostClock += Time.deltaTime;
            if (GhostClock < 1f)
            {
                return;
            }
            GhostClock = 0f;
            var ghosts = MapPins.FindAll(p => p.Fav == null && p.Ghost != null);
            if (ghosts.Count == 0)
            {
                return;
            }
            Vector2 middle = Vector2.zero;
            foreach (MapPin g in ghosts)
            {
                Vector3 w = g.Ghost.transform.position;
                middle += new Vector2(w.x / 2f, w.z / 2f);
            }
            middle /= ghosts.Count;
            foreach (MapPin g in ghosts)
            {
                Vector3 w = g.Ghost.transform.position;
                Vector2 at = middle + (new Vector2(w.x / 2f, w.z / 2f) - middle) * GhostSqueeze;
                g.Anchor.localPosition = new Vector3(at.x, at.y, 0f);
            }
        }

        private static GameObject HitAt(Vector2 at)
        {
            if (EventSystem.current == null)
            {
                return null;
            }
            var data = new PointerEventData(EventSystem.current) { position = at };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(data, hits);
            return hits.Count > 0 ? hits[0].gameObject : null;
        }
    }

    // a favourite's pin clicked: the game walks the player to them
    // (FavoriteInfo.TargetFavorite); we make them the target instead
    [HarmonyLib.HarmonyPatch]
    internal static class MapPinClick
    {
        [HarmonyLib.HarmonyPatch(typeof(FavoriteInfo), nameof(FavoriteInfo.TargetFavorite))]
        [HarmonyLib.HarmonyPrefix]
        private static bool TargetFavorite_Prefix(FavoriteInfo __instance)
        {
            if (__instance == null || __instance.favoriteNPC == null)
            {
                return true;
            }
            InventoryWindow.SelectFromMap(__instance.favoriteNPC);
            return false;
        }
    }
}
