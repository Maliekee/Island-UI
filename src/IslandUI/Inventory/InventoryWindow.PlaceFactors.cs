using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using IslandUI.UI;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI
{
    // THE PLACEMENT FACTORS (Forgejo #10): while the Orders page's "Set place"
    // pointer is up, what the spot under it would mean for the person who is to
    // live there. Vanilla draws the spot (the green point) and the two circles,
    // territory and combat area; this adds what the game itself counts from that
    // spot, each with the game's own radius:
    //  - FOOD and WATER: the stores and wells that would keep them fed;
    //  - HOUSE: the house they would sleep in, the night that gives mood back;
    //  - NEIGHBOURS: the people whose areas would overlap theirs, and what each
    //    love tick would do between them - the Social arcs' rule (Links.cs).
    // Each found thing gets an arc from the spot, in the factor's colour (solid:
    // it counts from the spot; dotted: only from the edge of their area), and
    // the Orders window, while the pick is up, is the placement panel that
    // says the lot, including what is missing - a spot with no well says so
    // (PlacePanel.cs). The pointer keeps only a chip with the keys.
    //
    // ADDING A FACTOR is one method of the PlaceFactor shape and one entry in
    // PlaceFactors: it reads the spot, adds its arcs and adds its rows.
    //
    // The pick itself is vanilla's coroutine (NPCManager.SetTerritory), run
    // inside one of ours only so its end is known: placed, cancelled with a
    // right-click or Escape, or the target gone. It is armed from the Orders page
    // or the dock's Set place tile, and lives while they are the target, sheet or
    // no sheet (Forgejo #166); the indicators go with it and with the generation
    // (Owned).
    //
    // WHILE THE PICK IS UP (Forgejo #20) Escape cancels it, and the wheel sizes
    // the territory circle instead of zooming the camera.
    //
    // A CANCEL PUTS THINGS BACK. Vanilla's loop writes only on a left click, but
    // every step it draws the point and both circles at the pointer, and nothing
    // draws them back: a right-click (or, in round 1, our Escape) left the marks
    // where the pointer was, which reads exactly as a placement. So the person's
    // place, house link and radius are taken when the pick is armed, and any end
    // that did not place - Escape, right-click, the target gone - writes them
    // back and redraws the marks there (the radius too: the wheel moved it live).
    internal static partial class InventoryWindow
    {
        private sealed class PlaceSpot
        {
            public ManagersScript Mn;
            public CommonStates Who;
            public Vector3 At;
            public float Rad;        // their territory radius, as the slider has it now
        }

        private sealed class PlaceHit
        {
            public Vector3 To;       // where the mark stands, when Body is null
            public Transform Body;   // a person at home: the mark follows them
            public Color Tint;       // a store's, a well's, the house's pin
            public bool Dotted;      // ...counted only from the area's edge
            public bool Far;         // ...out of reach, shown so you can find it
            public string Icon;      // ...its glyph (st_food, st_water, st_house)
            public string Name;      // ...and its word on an edge tag
            public CommonStates Who; // a neighbour: a strand, or a dot when plain
            public int There, Back;  // ...and what a tick does, to them and back
        }

        // WHAT A SPOT MEANS, AS DATA: the placement panel lays it out (PlacePanel.cs)
        // and the harness prints it. It was a string for the tip at the pointer,
        // which had no room for every neighbour.
        private sealed class PlaceRow
        {
            public string Label;     // Food, Water, House
            public Color Tint;       // ...in its arcs' colour
            public string Text;      // what the spot gives; what is wrong in the sheets' red
            public string Note;      // a second line: what is nearest, and what would fix it
        }

        private struct PlaceNear
        {
            public CommonStates Who;
            public int There;        // what one love tick does to their love for them...
            public int Back;         // ...and to theirs back
        }

        private sealed class PlaceReading
        {
            public readonly List<PlaceRow> Rows = new List<PlaceRow>();
            public readonly List<PlaceNear> Near = new List<PlaceNear>();
            public string NearNote;  // in place of the list: love off, or nobody near
        }

        private delegate void PlaceFactor(PlaceSpot spot, List<PlaceHit> hits, PlaceReading read);

        private static readonly PlaceFactor[] PlaceFactors = { PlaceFood, PlaceWater, PlaceHouse, PlaceNeighbours };

        private static readonly Color PlaceHouseTint = Theme.ParseColor("#C98B4F", new Color(0.79f, 0.55f, 0.31f));
        private const string PlaceBadInk = "#B0322A";   // the sheets' red, on the paper
        private const int PlaceStrandsMax = 8;

        // ---------- the factors ----------

        // FOOD (GameManager.FoodStorageCheck, every breed tick): a food store
        // tops up anyone of yours standing within its reach from what it holds -
        // 20 m for foodstorage_02, 10 for any other.
        private static void PlaceFood(PlaceSpot spot, List<PlaceHit> hits, PlaceReading read)
        {
            PlaceStores(spot, hits, read, BreedInfo.BreedType.Food, false, FoodTint,
                key => key == "foodstorage_02" ? 20f : 10f);
        }

        // WATER (GameManager.WaterStorageCheck): a well, the same way - 10 m for
        // well_01, 20 for well_02, and nobody for any other key.
        private static void PlaceWater(PlaceSpot spot, List<PlaceHit> hits, PlaceReading read)
        {
            PlaceStores(spot, hits, read, BreedInfo.BreedType.Water, true, WaterTint,
                key => key == "well_01" ? 10f : key == "well_02" ? 20f : 0f);
        }

        // how far around the spot a store or house out of reach still gets its
        // pin: the arcs draw it on the screen and tag it within a screen of it
        private const float PlacePinsWithin = 60f;

        // The game measures from where the person STANDS, and they wander their
        // area: a store whose reach takes in the spot counts (solid), one that
        // reaches only into the area counts while they are at that edge (dotted).
        // One out of reach is pinned too, faded: where the food and water are is
        // what you are choosing by.
        private static void PlaceStores(PlaceSpot spot, List<PlaceHit> hits, PlaceReading read, BreedInfo.BreedType type,
            bool wells, Color tint, System.Func<string, float> reachOf)
        {
            // the legend's word, and the pin's glyph and word on an edge tag
            string label = wells ? Shared.Loc.Get("place.water", "Water") : Shared.Loc.Get("place.food", "Food");
            string icon = wells ? "st_water" : "st_food";
            BuildManager build = spot.Mn.buildMN;
            int near = 0, edge = 0;
            float held = 0f, gap = float.MaxValue, gapReach = 0f;
            for (int i = 0; build != null && build.breedList != null && i < build.breedList.Count; i++)
            {
                BreedInfo store = build.breedList[i];
                if (store == null || store.breedType != type)
                {
                    continue;
                }
                ItemInfo info = store.GetComponent<ItemInfo>();
                float reach = reachOf(info != null ? info.itemKey : null);
                if (reach <= 0f)
                {
                    continue;
                }
                Vector3 at = store.transform.position;
                float d = Vector3.Distance(spot.At, at);
                if (d <= reach || d <= reach + spot.Rad)
                {
                    bool inReach = d <= reach;
                    if (inReach) { near++; } else { edge++; }
                    InventorySlot inv = store.GetComponent<InventorySlot>();
                    if (inReach && inv != null && inv.slots != null && inv.slots.Length > 0 && inv.slots[0] != null)
                    {
                        held += Mathf.Max(0f, inv.slots[0].itemLife);
                    }
                    hits.Add(new PlaceHit { To = PinTop(store.transform, 2.2f), Tint = tint, Dotted = !inReach, Icon = icon, Name = label });
                }
                else
                {
                    if (d - reach < gap)
                    {
                        gap = d - reach;
                        gapReach = reach;
                    }
                    if (d <= PlacePinsWithin)
                    {
                        hits.Add(new PlaceHit { To = PinTop(store.transform, 2.2f), Tint = tint, Far = true, Icon = icon, Name = label });
                    }
                }
            }
            var row = new PlaceRow
            {
                Label = label,
                Tint = tint,
            };
            if (near > 0)
            {
                string heldText = held.ToString("0", CultureInfo.InvariantCulture);
                row.Text = wells
                    ? Shared.Loc.Plural("place.water.held", "{0} well, {1} held", "{0} wells, {1} held", near, heldText)
                    : Shared.Loc.Plural("place.food.held", "{0} store, {1} held", "{0} stores, {1} held", near, heldText);
                if (edge > 0)
                {
                    row.Text = Shared.Loc.Get("place.stores.and_edge", "{0}, +{1} at the edge", row.Text, edge);
                }
            }
            else if (edge > 0)
            {
                row.Text = wells
                    ? Shared.Loc.Plural("place.water.edge_only", "only at the area's edge ({0} well)", "only at the area's edge ({0} wells)", edge)
                    : Shared.Loc.Plural("place.food.edge_only", "only at the area's edge ({0} store)", "only at the area's edge ({0} stores)", edge);
            }
            else
            {
                row.Text = PlaceBad(wells
                    ? Shared.Loc.Get("place.water.none", "no well in reach")
                    : Shared.Loc.Get("place.food.none", "no store in reach"));
                if (gap < float.MaxValue)
                {
                    row.Note = Shared.Loc.Get("place.stores.nearest", "nearest {0} away, its reach {1}", Meters(gap + gapReach), Meters(gapReach));
                }
            }
            read.Rows.Add(row);
        }

        // HOUSE (NPCMove.Sleep -> NPCManager.FindFriendHouse): at night a villager
        // goes to the nearest house within their territory radius of the centre,
        // and a night indoors is the one thing that gives mood back (+10 at dawn,
        // MoodCheck). Placed from inside a house, the game ties them to that one
        // (SetTerritory: fixedHouseID = tmpHouse).
        private static void PlaceHouse(PlaceSpot spot, List<PlaceHit> hits, PlaceReading read)
        {
            BuildManager build = spot.Mn.buildMN;
            var row = new PlaceRow { Label = Shared.Loc.Get("place.house", "House"), Tint = PlaceHouseTint };
            read.Rows.Add(row);
            if (spot.Mn.gameMN != null && spot.Mn.gameMN.tmpHouse != 0)
            {
                row.Text = Shared.Loc.Get("place.house.inside", "this one: placed inside, they live here");
                return;
            }
            ItemInfo best = null, closest = null;
            float bestD = float.MaxValue, nearest = float.MaxValue;
            for (int i = 0; build != null && build.houses != null && i < build.houses.Count; i++)
            {
                ItemInfo house = build.houses[i];
                if (house == null || house.GetComponent<DoorNPC>() == null)
                {
                    continue;
                }
                float d = Vector3.Distance(spot.At, house.transform.position);
                if (d < nearest)
                {
                    nearest = d;
                    closest = house;
                }
                if (d <= spot.Rad && d < bestD)
                {
                    best = house;
                    bestD = d;
                }
            }
            bool barred = spot.Who.sexInfo != null && spot.Who.sexInfo.Length > 7 && spot.Who.sexInfo[7] != 0;
            if (best != null)
            {
                hits.Add(new PlaceHit { To = HousePin(best), Tint = PlaceHouseTint, Dotted = barred, Icon = "st_house", Name = row.Label });
                row.Text = barred
                    ? Shared.Loc.Get("place.house.barred", "one {0} away, {1}", Meters(bestD),
                        PlaceBad(Shared.Loc.Get("place.house.barred.why", "but they are not allowed into houses")))
                    : Shared.Loc.Get("place.house.near", "{0} away: +10 mood at dawn", Meters(bestD));
                return;
            }
            row.Text = PlaceBad(Shared.Loc.Get("place.house.none", "none within {0}: no mood at dawn", Meters(spot.Rad)));
            if (closest != null && nearest <= PlacePinsWithin)
            {
                // the one the wheel would reach first, faded
                hits.Add(new PlaceHit { To = HousePin(closest), Tint = PlaceHouseTint, Far = true, Icon = "st_house", Name = row.Label });
            }
            if (nearest < float.MaxValue)
            {
                // what the wheel would have to do: the area is whole metres
                Slider ter = spot.Mn.uiMN != null ? spot.Mn.uiMN.territorySlider : null;
                int widen = Mathf.CeilToInt(nearest - spot.Rad - 0.001f);
                bool can = ter != null && spot.Rad + widen <= ter.maxValue;
                row.Note = can
                    ? Shared.Loc.Get("place.house.widen", "nearest {0} away: widen the area by {1}", Meters(nearest), widen)
                    : Shared.Loc.Get("place.house.past_widest", "nearest {0} away, past the widest area", Meters(nearest));
            }
        }

        // NEIGHBOURS (GameManager.LoveCheck, six ticks a day): everyone of yours
        // whose area would overlap theirs (NPCManager.NPCDistanceCheck: centres no
        // further apart than the two radii) - and what each tick does both ways,
        // +10 to a liked type, -5 to a disliked one, +5 otherwise (RuleOf). A
        // neighbour's centre is their outdoor one: indoors, the game moves it to
        // the interior until morning (preTerritory keeps it).
        private static void PlaceNeighbours(PlaceSpot spot, List<PlaceHit> hits, PlaceReading read)
        {
            NPCManager npcs = spot.Mn.npcMN;
            if (LoveOff(spot.Who))
            {
                read.NearNote = Shared.Loc.Get("place.neighbours.love_off", "Love is off for them: nobody here counts");
                return;
            }
            var plainArcs = new List<PlaceHit>();
            for (int i = 0; npcs != null && npcs.friendList != null && i < npcs.friendList.Count; i++)
            {
                CommonStates other = npcs.friendList[i];
                if (other == null || ReferenceEquals(other, spot.Who) || other.nMove == null || !npcs.IsActiveFriend(other)
                    || LoveOff(other))
                {
                    continue;
                }
                NPCMove move = other.nMove;
                Vector3 centre = move.house != null ? move.preTerritory : move.territory;
                if (Vector3.Distance(spot.At, centre) > spot.Rad + move.territoryRad)
                {
                    continue;
                }
                int there = RuleOf(spot.Who, other), back = RuleOf(other, spot.Who);
                // A MARK ON EVERY NEIGHBOUR (Forgejo #16: "arcs to the villagers
                // within the vicinity"): a strand from the spot to a liked or
                // disliked type, a dot over a plain +5 both ways (DrawPlaceArcs).
                // It stands over them while they are at home, else over their
                // centre - where they live is what overlaps.
                bool plain = there == 5 && back == 5;
                bool home = ArcDrawable(other) && Vector3.Distance(other.transform.position, centre) <= move.territoryRad;
                PlaceHit hit = home
                    ? new PlaceHit { Body = other.transform, Who = other, There = there, Back = back }
                    : new PlaceHit { To = centre + Vector3.up * 0.3f, Who = other, There = there, Back = back };
                (plain ? plainArcs : hits).Add(hit);
                read.Near.Add(new PlaceNear { Who = other, There = there, Back = back });
            }
            hits.AddRange(plainArcs);
            if (read.Near.Count == 0)
            {
                read.NearNote = Shared.Loc.Get("place.neighbours.none", "Nobody's area meets theirs here: no love changes");
                return;
            }
            // trouble first: the lower of the two changes, then the sum
            read.Near.Sort((a, b) => PlaceWorst(a).CompareTo(PlaceWorst(b)));
        }

        private static int PlaceWorst(PlaceNear n)
        {
            return Mathf.Min(n.There, n.Back) * 100 + n.There + n.Back;
        }

        private static string PlaceBad(string text)
        {
            return "<color=" + PlaceBadInk + ">" + text + "</color>";
        }

        private static string PlaceSigned(int v)
        {
            return v > 0 ? "+" + v : "−" + (-v);
        }

        private static string Meters(float d)
        {
            return Shared.Loc.Get("settings.unit.metres", "{0} m", d.ToString("0", CultureInfo.InvariantCulture));
        }

        // `who` living at `at`: what it means, its arcs into `hits`
        private static PlaceReading ReadPlace(ManagersScript mn, CommonStates who, Vector3 at, List<PlaceHit> hits)
        {
            var spot = new PlaceSpot { Mn = mn, Who = who, At = at, Rad = who.nMove != null ? who.nMove.territoryRad : 0f };
            var read = new PlaceReading();
            for (int i = 0; i < PlaceFactors.Length; i++)
            {
                try
                {
                    PlaceFactors[i](spot, hits, read);
                }
                catch (System.Exception e)
                {
                    // one factor's fault leaves the others standing
                    Plugin.Log.LogWarning("Placement factor " + i + ": " + e.Message);
                }
            }
            return read;
        }

        // the reading in plain words, for the harness
        private static string PlaceText(PlaceReading read)
        {
            var sb = new StringBuilder();
            foreach (PlaceRow row in read.Rows)
            {
                sb.Append(row.Label).Append("  ").Append(row.Text);
                if (row.Note != null) { sb.Append(" (").Append(row.Note).Append(')'); }
                sb.Append('\n');
            }
            if (read.NearNote != null)
            {
                sb.Append("Neighbours  ").Append(read.NearNote).Append('\n');
            }
            else
            {
                sb.Append("Neighbours  ").Append(read.Near.Count).Append(", a tick: to them / back\n");
                foreach (PlaceNear n in read.Near)
                {
                    sb.Append(n.Who.charaName).Append("  ").Append(PlaceSigned(n.There)).Append(" / ").Append(PlaceSigned(n.Back)).Append('\n');
                }
            }
            return System.Text.RegularExpressions.Regex.Replace(sb.ToString(), "<[^>]+>", string.Empty).TrimEnd('\n');
        }

        // ---------- the pick's lifetime ----------

        private static CommonStates _placing;
        private static Coroutine _placeRoutine;
        private static int _placeToken;
        private static bool _placeEscaped;     // Escape asked; the wrapper ends the pick on its next step
        private static bool _placeEscapeFake;  // harness: the next PauseGame hears Escape

        // what the pick may change, as it was when it was armed
        private static Vector3 _placeWasTerritory;
        private static Vector3 _placeWasPreTerritory;
        private static int _placeWasHouse;
        private static float _placeWasRad;
        private static CommonStates _placeLastWho;   // harness: whom PlaceUndo puts back

        internal static bool PlacingNow
        {
            get { return _placing != null && _placeRoutine != null; }
        }

        // ONE TOGGLE, TWO GATES (Forgejo #166): the Orders page's Set place and
        // the dock's tile arm the same pick, each shows it, and either ends it -
        // the page's Cancel, or the tile pressed again, as Escape does
        internal static bool PlacingFor(CommonStates who)
        {
            return PlacingNow && ReferenceEquals(_placing, who) && !_placeEscaped;
        }

        private static void TogglePlacing(CommonStates who)
        {
            if (PlacingNow && ReferenceEquals(_placing, who))
            {
                _placeEscaped = true;   // a cancel already asked waits for the wrapper's step
                return;
            }
            StandingAction(who, 0);
        }

        // THE MOUSE IS THE PICK'S while it is up - a click places, a right-click
        // cancels - and stays the pick's until both buttons are let go, so the
        // press that placed or cancelled is not then read as a walk or a guard
        private static bool _placeHeldMouse;

        internal static bool PickHoldsTheMouse
        {
            get
            {
                if (PlacingNow)
                {
                    _placeHeldMouse = true;
                }
                else if (_placeHeldMouse && !Input.GetMouseButton(0) && !Input.GetMouseButton(1) && !Input.GetKey(KeyCode.Z))
                {
                    _placeHeldMouse = false;
                }
                return _placeHeldMouse;
            }
        }

        // StandingAction's "Set place": vanilla's SetTerritory, as SetTerritoryButton
        // starts it, inside a wrapper that says when it ends
        private static void StartPlacing(CommonStates who)
        {
            NPCManager npcs = _mn.npcMN;
            int token = ++_placeToken;
            _placing = who;
            _placeEscaped = false;
            _placeRead = -10f;
            if (who.nMove != null)
            {
                _placeWasTerritory = who.nMove.territory;
                _placeWasPreTerritory = who.nMove.preTerritory;
                _placeWasHouse = who.nMove.fixedHouseID;
                _placeWasRad = who.nMove.territoryRad;
                _placeLastWho = who;
            }
            // SET PLACE MAKES THEM OURS: a clicked person's menu ends the moment
            // you move (D158), and the pick now invites the walk - so the hold
            // becomes a Tab's or a pick's, which outlives it (KeepPastVanillaEnd)
            if (_heldVar == Held.Menu && ReferenceEquals(_heldRef, who))
            {
                _heldHow = Begun.Roster;
            }
            // ARMED WITHOUT THE ORDERS PAGE (Forgejo #166) vanilla's rings at the
            // pointer still need their size: MoveSetting gives it as the page's
            // preview opens, and with no page up they are whoever's was last
            UIManager ui = _mn.uiMN;
            if (!ReferenceEquals(_previewFor, who) && ui != null && who.nMove != null
                && ui.territorySlider != null && ui.chaseSlider != null)
            {
                ui.territorySlider.SetValueWithoutNotify(who.nMove.territoryRad);
                ui.chaseSlider.SetValueWithoutNotify(who.nMove.chaseDist);
                ui.SetCircleRad(0, who.nMove.territoryRad);
                ui.SetCircleRad(1, who.nMove.chaseDist + who.nMove.territoryRad + 0.05f);
            }
            _placeRoutine = npcs.StartCoroutine(Placing(who, npcs.SetTerritory(), token));
            _unitBoundAt = 0f;   // the dock's tile lights
        }

        private static IEnumerator Placing(CommonStates who, IEnumerator pick, int token)
        {
            PlayerMove player = _mn != null && _mn.gameMN != null ? _mn.gameMN.pMove : null;
            bool could = player != null && player.controllable;
            bool stepped = false;
            while (!(_placeEscaped && token == _placeToken) && pick.MoveNext())
            {
                // YOU STILL WALK WHILE PLACING (Forgejo #20: "you cant move your
                // character, when you start the relocation state"). Vanilla's
                // first step takes the player's control for the whole pick; it
                // is handed straight back, and the pick keeps only the mouse
                // (PlacePatches.PlayerMove_Update), Escape and the wheel.
                if (!stepped)
                {
                    stepped = true;
                    if (player != null) { player.controllable = could; }
                }
                yield return pick.Current;
            }
            if (token == _placeToken && !PlacedSince(who))
            {
                UnplaceTo(who);
            }
            // (Escape ends it from outside vanilla's loop, which is never stepped
            // again: nothing is written, and its tail - the 0.2 s wait, then the
            // control back - has nothing to give back.)
            if (token == _placeToken)
            {
                HidePickMarks();
                _placing = null;
                _placeRoutine = null;
                _forceRefresh = true;   // the panel goes back to being the Orders page
                _nextRefresh = 0f;
                _unitBoundAt = 0f;      // ...and the dock's tile goes out
            }
        }

        // VANILLA'S MARKS COME DOWN WITH THE PICK when no Orders page holds them:
        // its loop draws the point and both rings at the pointer and leaves them
        // there, and only MoveSetting, the page's preview, takes them down - and
        // while it is up, for them or whoever the target is now, they are its
        private static void HidePickMarks()
        {
            UIManager ui = _mn != null ? _mn.uiMN : null;
            if (ui == null || ui.pointGreen == null
                || (_previewFor != null && ui.territoryPanel != null && ui.territoryPanel.activeSelf))
            {
                return;
            }
            ui.SetPoint(false);
            ui.SetCircle(0, false);
            ui.SetCircle(1, false);
        }

        // ESCAPE CANCELS THE PICK, asked from the pause's prefix (TargetPatches)
        // after the split dialog: the one press neither pauses nor ends the target,
        // so the sheet stays open on Orders.
        internal static bool EscapeCancelsPlacing()
        {
            bool pressed = Input.GetKeyDown(KeyCode.Escape) || _placeEscapeFake;
            _placeEscapeFake = false;
            // a focused field, the console and Settings keep the key, as for the target
            if (!pressed || !PlacingNow || _placeEscaped
                || FilterInput.AnyFocused || DevConsole.IsOpen || SettingsWindow.IsOpen)
            {
                return false;
            }
            _placeEscaped = true;
            return true;
        }

        // a left click is the only write vanilla's loop makes, and it writes the
        // place, preTerritory and house link together
        private static bool PlacedSince(CommonStates who)
        {
            NPCMove move = who != null ? who.nMove : null;
            return move != null && (move.territory != _placeWasTerritory || move.preTerritory != _placeWasPreTerritory
                || move.fixedHouseID != _placeWasHouse);
        }

        // THE CANCEL: the fields as they were when the pick was armed, and - when
        // the sheet still has them - vanilla's marks and slider redrawn there, as
        // MoveSetting first drew them. The radius is set exactly, not through the
        // whole-metre slider, which would round a saved 2.5 to 3.
        private static void UnplaceTo(CommonStates who)
        {
            NPCMove move = who != null ? who.nMove : null;
            if (move == null)
            {
                return;
            }
            move.territory = _placeWasTerritory;
            move.preTerritory = _placeWasPreTerritory;
            move.fixedHouseID = _placeWasHouse;
            move.territoryRad = _placeWasRad;
            UIManager ui = _mn != null ? _mn.uiMN : null;
            // the marks are MoveSetting's while its panel is up; with it shut they are already gone
            if (ui == null || _mn.gameMN == null || !ReferenceEquals(_mn.gameMN.menuNPC, who)
                || ui.territoryPanel == null || !ui.territoryPanel.activeSelf)
            {
                return;
            }
            ui.territorySlider.SetValueWithoutNotify(_placeWasRad);
            ui.SetCircleRad(0, _placeWasRad);
            ui.SetCircleRad(1, ui.chaseSlider.value + _placeWasRad + 0.05f);
            ui.SetPoint(true, _placeWasTerritory);
            ui.SetCircle(0, true, _placeWasTerritory);
            ui.SetCircle(1, true, _placeWasTerritory);
            _nextSheetPaint = 0f;
        }

        // THE WHEEL SIZES THE CIRCLE: one step on the territory radius through the
        // Orders slider's own path (SetStandingSlider: vanilla's slider, then its
        // handler), so the world circles, the field and our slider agree, within
        // the slider's min and max. Up grows it.
        private const float PlaceWheelSteps = 20f;   // notches from the slider's min to its max

        private static void PlaceWheel(CommonStates who, float notches)
        {
            Slider ter = _mn != null && _mn.uiMN != null ? _mn.uiMN.territorySlider : null;
            if (notches == 0f || ter == null || who.nMove == null || ter.maxValue <= ter.minValue)
            {
                return;
            }
            float step = Mathf.Clamp(notches, -3f, 3f) * (ter.maxValue - ter.minValue) / PlaceWheelSteps;
            if (ter.wholeNumbers)
            {
                // vanilla's slider is whole metres (0..10): a half-metre notch
                // would round back to where it was
                step = Mathf.Sign(step) * Mathf.Max(1f, Mathf.Round(Mathf.Abs(step)));
            }
            float frac = Mathf.InverseLerp(ter.minValue, ter.maxValue, who.nMove.territoryRad + step);
            SetStandingSlider(who, SliderTerritory, frac);
        }

        // ---------- drawing ----------

        private static readonly List<PlaceHit> PlaceHitsNow = new List<PlaceHit>();
        private static PlaceReading _placeReading;   // the spot under the pointer, for the panel
        private static Vector3 _placeAt;
        private static float _placeRad;
        private static float _placeRead = -10f;
        private static Vector3? _placeProbe;   // harness: a spot to read instead of the pointer's
        private const float PlaceReadEvery = 0.25f;
        private static GameObject _placeTipRoot;
        private static KitTooltip _placeTip;

        // LateSync: after the coroutines, so the green point is this frame's
        private static void TickPlaceFactors()
        {
            ManagersScript mn = _mn;
            CommonStates who = _placing;
            GameObject point = mn != null && mn.uiMN != null ? mn.uiMN.pointGreen : null;
            bool ours = who != null && who.nMove != null && mn != null && mn.gameMN != null
                && ReferenceEquals(mn.gameMN.menuNPC, who) && ReferenceEquals(Focus() as CommonStates, who);
            // THE PICK ENDS WITH ITS PERSON, not with its page (Forgejo #166). While
            // they are the target it is in sight - the dock's tile lit, the Orders
            // page the placement panel whenever it is up - so the sheet may open,
            // shut or turn pages. With the keys the game's again, Tab or a deselect
            // can take the target elsewhere, and an unseen pick would still place
            // them on the next click: that is a cancel.
            if (!ours && PlacingNow && !_placeEscaped)
            {
                _placeEscaped = true;
            }
            // HOVERING SET PLACE SHOWS WHERE THEY ARE: the pick's arcs, read from
            // the place they have now - vanilla's point stands there while the
            // Orders page is up (MoveSetting) - with no wheel and no keys chip
            bool peek = !ours && !PlacingNow && _sheetHot != null && _sheetHot.DrewKind == LineStand && _sheetHotSub == 1
                && _focusShown && _focusAspect == Aspect.Orders && mn != null && mn.gameMN != null
                && (who = _target as CommonStates) != null && who.nMove != null && ReferenceEquals(mn.gameMN.menuNPC, who);
            if (peek)
            {
                PeekPlace(mn, who, point);
                return;
            }
            bool on = ours && point != null;
            if (on && _placeProbe.HasValue)
            {
                // the harness's spot, drawn as vanilla draws the pointer's
                mn.uiMN.SetPoint(true, _placeProbe.Value);
                mn.uiMN.SetCircle(0, true, _placeProbe.Value);
                mn.uiMN.SetCircle(1, true, _placeProbe.Value);
            }
            if (!on || !point.activeInHierarchy)
            {
                HidePlaceFactors();
                return;
            }
            // over a window the wheel is the window's scroll
            bool overUi = OverAnyUi() || mn.gameMN.OnUI;
            if (!overUi)
            {
                PlaceWheel(who, Input.mouseScrollDelta.y);
            }
            Vector3 at = point.transform.position;
            float rad = who.nMove.territoryRad;
            // OVER A WINDOW THE SPOT HOLDS: vanilla's point follows the ground behind
            // the window, but that is not where anyone is pointing - and the panel,
            // re-read, would change height and move its own Cancel from under the
            // pointer on its way there.
            if (overUi && _placeReading != null && !_placeProbe.HasValue)
            {
                at = _placeAt;
            }
            if ((at - _placeAt).sqrMagnitude > 0.04f || rad != _placeRad || Time.unscaledTime - _placeRead > PlaceReadEvery)
            {
                _placeAt = at;
                _placeRad = rad;
                _placeRead = Time.unscaledTime;
                PlaceHitsNow.Clear();
                _placeReading = ReadPlace(mn, who, at, PlaceHitsNow);
                if (_focusShown && _focusAspect == Aspect.Orders)
                {
                    _forceRefresh = true;   // the panel's rows are written on a rebind
                }
            }
            DrawPlaceArcs();
            // the keys only: what the spot means is on the panel. Not over a window.
            PlaceTip(overUi ? null : PlaceKeysTip);
        }

        // the reading is the panel's only while the pick is up: here the Orders
        // page stays as it is, and no rebind takes the hover from under the pointer
        private static void PeekPlace(ManagersScript mn, CommonStates who, GameObject point)
        {
            Vector3 at = point != null && point.activeInHierarchy ? point.transform.position : who.nMove.territory;
            float rad = who.nMove.territoryRad;
            if ((at - _placeAt).sqrMagnitude > 0.04f || rad != _placeRad || Time.unscaledTime - _placeRead > PlaceReadEvery)
            {
                _placeAt = at;
                _placeRad = rad;
                _placeRead = Time.unscaledTime;
                PlaceHitsNow.Clear();
                _placeReading = ReadPlace(mn, who, at, PlaceHitsNow);
            }
            DrawPlaceArcs();
            PlaceTip(null);
        }

        // EVERY FRAME, the marks of the spot's reading (FeelingArcs.cs): a pin on
        // each store, well and house, its glyph in the panel's legend colour, a
        // strand to it when it counts (dashed from the area's edge), faded out of
        // reach, a tag on the screen's edge within a screen of it; a dot over each
        // plain neighbour; a strand to each who likes or dislikes a type, the
        // dislikes first, PlaceStrandsMax at most - the panel lists everyone.
        private static readonly List<ArcMark> PlaceArcMarks = new List<ArcMark>();
        private static readonly List<PlaceHit> PlaceStrong = new List<PlaceHit>();

        private static void DrawPlaceArcs()
        {
            Camera cam = Camera.main;
            if (cam == null || (_placeArcs = ArcSetFor(_placeArcs, "Place")) == null)
            {
                return;
            }
            if (MenuOverMarker())
            {
                _placeArcs.Hide();
                return;
            }
            PlaceArcMarks.Clear();
            PlaceStrong.Clear();
            foreach (PlaceHit hit in PlaceHitsNow)
            {
                if (hit.Who == null)
                {
                    PlaceArcMarks.Add(new ArcMark
                    {
                        Kind = ArcKind.Pin, At = hit.To, Tint = hit.Tint, Faint = hit.Dotted, Far = hit.Far,
                        Icon = hit.Icon, Name = hit.Name, Metres = Flat(_placeAt, hit.To),
                    });
                }
                else if (hit.There == 5 && hit.Back == 5)
                {
                    PlaceArcMarks.Add(new ArcMark { Kind = ArcKind.Dot, At = PlaceHead(hit) });
                }
                else
                {
                    PlaceStrong.Add(hit);
                }
            }
            // stable: past the cap the same ones stay out from frame to frame
            PlaceStrong.Sort((a, b) => PlaceRank(a) != PlaceRank(b) ? PlaceRank(a).CompareTo(PlaceRank(b))
                : PlaceHitsNow.IndexOf(a).CompareTo(PlaceHitsNow.IndexOf(b)));
            for (int i = 0; i < PlaceStrong.Count && i < PlaceStrandsMax; i++)
            {
                PlaceHit hit = PlaceStrong[i];
                Vector3 where = hit.Body != null ? hit.Body.position : hit.To;
                PlaceArcMarks.Add(new ArcMark
                {
                    Kind = ArcKind.Strand, At = PlaceHead(hit), There = hit.There, Back = hit.Back,
                    Name = hit.Who.charaName, Metres = Flat(_placeAt, where),
                });
            }
            _placeArcs.Draw(cam, _placeAt + Vector3.up * 0.3f, PlaceArcMarks, ArcSafe());
        }

        // WHERE A PIN'S TIP STANDS on a store, a well or a house: on top of what is
        // drawn of it, no higher than `most` over its foot (a cabin's roof would
        // lift the pin off the screen). Things do not grow, so once a thing.
        private static readonly Dictionary<Transform, float> PinHeights = new Dictionary<Transform, float>();

        private static Vector3 PinTop(Transform t, float most)
        {
            if (!PinHeights.TryGetValue(t, out float rise))
            {
                float top = t.position.y + 0.8f;
                foreach (Renderer r in t.GetComponentsInChildren<Renderer>())
                {
                    if (r.enabled && (r is SpriteRenderer || r is MeshRenderer))
                    {
                        top = Mathf.Max(top, r.bounds.max.y);
                    }
                }
                if (PinHeights.Count > 256)
                {
                    PinHeights.Clear();
                }
                rise = top - t.position.y;
                PinHeights[t] = rise;
            }
            return t.position + Vector3.up * Mathf.Min(rise, most);
        }

        // a house's pin stands over its door, where they go in: the house's own
        // origin is the middle of a footprint that can reach past the screen
        private static Vector3 HousePin(ItemInfo house)
        {
            DoorNPC door = house.GetComponent<DoorNPC>();
            Transform at = door != null && door.door != null ? door.door.transform : house.transform;
            return at.position + Vector3.up * 2f;
        }

        // over their head at home; over the middle of their area when away, a
        // standing height above the ground
        private static Vector3 PlaceHead(PlaceHit hit)
        {
            return hit.Body != null ? OverHead(hit.Who) : hit.To + Vector3.up * 1.7f;
        }

        // the worse of the two first, then the stronger liking
        private static int PlaceRank(PlaceHit hit)
        {
            return Mathf.Min(hit.There, hit.Back) * 100 - Mathf.Max(hit.There, hit.Back);
        }

        private static void HidePlaceFactors()
        {
            if (_placeArcs != null) { _placeArcs.Hide(); }
            PlaceHitsNow.Clear();
            _placeReading = null;
            _placeRead = -10f;
            if (_placeTip != null) { _placeTip.Hide(); }
        }

        // THE CHIP AT THE POINTER: the kit's tip, on a root of its own as the arc's
        // label is (Links.cs). It held the whole readout until the panel took it
        // (Forgejo #20: "a comprehensive tooltip, where there is not enough
        // space"); the keys stay with the pointer, where the eyes are.
        private static string PlaceKeysTip =>
            Shared.Loc.Get("place.keys.size", "{0} size", "<b>wheel</b>")
            + "  ·  " + Shared.Loc.Get("place.keys.cancel", "{0} cancel", "<b>Esc</b>");

        private static void PlaceTip(string text)
        {
            if (text == null)
            {
                if (_placeTip != null) { _placeTip.Hide(); }
                return;
            }
            if (_placeTipRoot == null)
            {
                Canvas canvas = _mn != null && _mn.uiMN != null && _mn.uiMN.itemDescPanel != null
                    ? _mn.uiMN.itemDescPanel.GetComponentInParent<Canvas>() : null;
                if (canvas == null)
                {
                    return;
                }
                _placeTipRoot = Owned.New("IslandUI_PlaceTip");
                _placeTipRoot.transform.SetParent(canvas.rootCanvas.transform, false);
                var r = (RectTransform)_placeTipRoot.transform;
                r.anchorMin = Vector2.zero;
                r.anchorMax = Vector2.one;
                r.offsetMin = Vector2.zero;
                r.offsetMax = Vector2.zero;
                CanvasIsolation.Isolate(_placeTipRoot);
                _placeTip = KitTooltip.Create(r);
            }
            Canvas root = _placeTipRoot.GetComponentInParent<Canvas>();
            Camera cam = root != null && root.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? root.rootCanvas.worldCamera : null;
            _placeTipRoot.transform.SetAsLastSibling();
            _placeTip.Tick(text, cam, live: true);
        }

        // ---------- harness ----------

        // harness: "Set place" for whoever the sheet shows, as its button does
        internal static string PlaceStart()
        {
            CommonStates who = Focus() as CommonStates;
            if (who == null || _mn == null || _mn.gameMN == null || !ReferenceEquals(_mn.gameMN.menuNPC, who))
            {
                return "no villager on the sheet";
            }
            StandingAction(who, 0);
            return "placing: " + who.charaName;
        }

        // harness: read the spot "x y z" instead of the pointer's ("" lets go)
        internal static string PlaceProbe(string arg)
        {
            string[] p = (arg ?? string.Empty).Split(new[] { ' ', ';', '|' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (p.Length == 3 && float.TryParse(p[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x)
                && float.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y)
                && float.TryParse(p[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
            {
                _placeProbe = new Vector3(x, y, z);
                return "probe at " + _placeProbe.Value;
            }
            _placeProbe = null;
            return "probe off";
        }

        // harness: the readout and arcs now, or for the spot "x y z" if given (the harness splits arguments at commas)
        internal static string PlaceDump(string arg)
        {
            CommonStates who = _placing ?? Focus() as CommonStates;
            if (who == null || _mn == null)
            {
                return "no one";
            }
            Vector3 at = _placeAt;
            string[] p = (arg ?? string.Empty).Split(new[] { ' ', ';', '|' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (p.Length == 3)
            {
                at = new Vector3(float.Parse(p[0], CultureInfo.InvariantCulture), float.Parse(p[1], CultureInfo.InvariantCulture),
                    float.Parse(p[2], CultureInfo.InvariantCulture));
            }
            var hits = new List<PlaceHit>();
            string text = PlaceText(ReadPlace(_mn, who, at, hits));
            bool shown = _placeArcs != null && _placeArcs.Shown;
            Vector3 screen = Camera.main != null ? Camera.main.WorldToScreenPoint(at) : Vector3.zero;
            return "placing " + (_placing != null ? _placing.charaName : "-") + " at " + at + " (screen " + screen.x.ToString("0")
                + "," + (Screen.height - screen.y).ToString("0") + " of " + Screen.width + "x" + Screen.height + "), " + hits.Count + " marks ("
                + (shown ? "drawn" : "hidden") + "), tip " + (_placeTip != null && _placeTip.Visible ? "up" : "down") + "\n" + text;
        }

        // harness: Escape, as the game hears it - PauseGame, whose prefix asks the
        // pick first; Input.GetKeyDown cannot be faked, so the prefix is told
        internal static string PlaceEscape()
        {
            if (_mn == null || _mn.gameMN == null)
            {
                return "no managers";
            }
            bool was = PlacingNow;
            _placeEscapeFake = true;
            _mn.gameMN.PauseGame();
            _placeEscapeFake = false;
            return (was ? "was placing" : "was not placing") + ", escaped " + _placeEscaped + ", timeScale " + Time.timeScale
                + ", sheet " + (_focusShown ? _focusAspect.ToString() : "closed");
        }

        // harness: the pick and its person, to show what a cancel left alone
        internal static string PlaceState()
        {
            CommonStates who = _placing ?? Focus() as CommonStates;
            GameManager game = _mn != null ? _mn.gameMN : null;
            UIManager ui = _mn != null ? _mn.uiMN : null;
            return "placing " + PlacingNow + ", escaped " + _placeEscaped + ", controllable "
                + (game != null && game.pMove != null && game.pMove.controllable) + ", timeScale " + Time.timeScale
                + ", sheet " + (_focusShown ? _focusAspect.ToString() : "closed")
                + (who != null && who.nMove != null ? ", " + who.charaName + " territory " + who.nMove.territory.ToString("F4")
                    + " pre " + who.nMove.preTerritory.ToString("F4") + " rad " + who.nMove.territoryRad.ToString("0.000")
                    + " house " + who.nMove.fixedHouseID : "")
                + (ui != null && ui.pointGreen != null ? ", point " + (ui.pointGreen.activeSelf ? ui.pointGreen.transform.position.ToString("F2") : "off")
                    + " slider " + ui.territorySlider.value.ToString("0.0") : "");
        }

        // harness: put the last pick's person back as it was armed, placed or not -
        // a test that really placed someone undoes it here
        internal static string PlaceUndo()
        {
            CommonStates who = _placeLastWho;
            if (who == null || PlacingNow)
            {
                return who == null ? "no pick yet" : "still placing";
            }
            UnplaceTo(who);
            return "back: " + who.charaName + " " + who.nMove.territory.ToString("F4") + " rad " + who.nMove.territoryRad.ToString("0.000")
                + " house " + who.nMove.fixedHouseID;
        }

        // harness: wheel notches (+ grows), as the tick takes them from the mouse
        internal static string PlaceWheelStep(string arg)
        {
            CommonStates who = _placing;
            if (who == null || who.nMove == null)
            {
                return "not placing";
            }
            float before = who.nMove.territoryRad;
            PlaceWheel(who, float.Parse(arg, CultureInfo.InvariantCulture));
            Slider ter = _mn.uiMN.territorySlider;
            return "radius " + before.ToString("0.00") + " -> " + who.nMove.territoryRad.ToString("0.00") + " (slider "
                + ter.value.ToString("0.00") + " of " + ter.minValue.ToString("0.00") + ".." + ter.maxValue.ToString("0.00") + ")";
        }

        // harness: end the pick, as a right-click does; "orphan" also ends a pick
        // started outside the wrapper, by letting go of menuNPC for vanilla's loop
        internal static string PlaceCancel(string arg)
        {
            if (_mn == null || _mn.npcMN == null)
            {
                return "no managers";
            }
            if (arg == "release" && _mn.gameMN != null)
            {
                // vanilla's own way out: its loop ends when menuNPC goes, and the
                // wrapper's end clears the factors (the drop also loses the target)
                _placeProbe = null;
                _mn.gameMN.menuNPC = null;
                return "released";
            }
            if (_placeRoutine != null)
            {
                _mn.npcMN.StopCoroutine(_placeRoutine);
                if (!PlacedSince(_placing))
                {
                    UnplaceTo(_placing);
                }
            }
            if (arg == "orphan" && _mn.gameMN != null)
            {
                _mn.gameMN.menuNPC = null;
            }
            _placeRoutine = null;
            _placing = null;
            _placeProbe = null;
            if (_mn.gameMN != null && _mn.gameMN.pMove != null) { _mn.gameMN.pMove.controllable = true; }
            HidePlaceFactors();
            HidePickMarks();
            _unitBoundAt = 0f;
            return "cancelled";
        }
    }

    // THE CAMERA LEAVES THE WHEEL TO THE PICK (Forgejo #20). The rig's
    // FollowTarget.Update zooms on the wheel whenever the pointer is off UI -
    // exactly where the pick now sizes the circle - so its one read of the axis
    // says nothing while the pick is up.
    [HarmonyPatch]
    internal static class PlacePatches
    {
        [HarmonyPatch(typeof(FollowTarget), "Update")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> FollowTarget_Update(IEnumerable<CodeInstruction> code)
        {
            MethodInfo getAxis = AccessTools.Method(typeof(Input), nameof(Input.GetAxis), new[] { typeof(string) });
            MethodInfo ours = AccessTools.Method(typeof(PlacePatches), nameof(CameraWheel));
            var list = new List<CodeInstruction>(code);
            int swapped = 0;
            for (int i = 1; i < list.Count; i++)
            {
                if (list[i].Calls(getAxis) && list[i - 1].opcode == OpCodes.Ldstr && (string)list[i - 1].operand == "Mouse ScrollWheel")
                {
                    list[i].operand = ours;   // same signature, labels kept
                    swapped++;
                }
            }
            if (swapped != 1)
            {
                Plugin.Log.LogWarning("[place] FollowTarget.Update: " + swapped + " wheel reads replaced, 1 expected");
            }
            return list;
        }

        private static float CameraWheel(string axis)
        {
            return InventoryWindow.PlacingNow ? 0f : Input.GetAxis(axis);
        }

        // THE BODY LEAVES THE MOUSE TO THE PICK. With the player's control kept
        // through the pick (InventoryWindow.Placing), PlayerMove.Update runs - and
        // its two mouse reads would make the click that places also walk to the
        // spot, and the right-click that cancels also raise a guard (or cast the
        // rod, dig, draw the bow). Those two read nothing while the pick holds the
        // mouse; WASD, E, Space, F, Q and the rest are the game's as ever.
        [HarmonyPatch(typeof(PlayerMove), "Update")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> PlayerMove_Update(IEnumerable<CodeInstruction> code)
        {
            MethodInfo rightClick = AccessTools.Method(typeof(GameManager), nameof(GameManager.IsRightClick));
            MethodInfo button = AccessTools.Method(typeof(Input), nameof(Input.GetMouseButton), new[] { typeof(int) });
            MethodInfo ourRight = AccessTools.Method(typeof(PlacePatches), nameof(BodyRightClick));
            MethodInfo ourButton = AccessTools.Method(typeof(PlacePatches), nameof(BodyMouseButton));
            var list = new List<CodeInstruction>(code);
            int rights = 0, buttons = 0;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Calls(rightClick))
                {
                    // the instance stays on the stack as the first argument; labels kept
                    list[i].opcode = OpCodes.Call;
                    list[i].operand = ourRight;
                    rights++;
                }
                else if (list[i].Calls(button))
                {
                    list[i].operand = ourButton;
                    buttons++;
                }
            }
            if (rights != 1 || buttons != 1)
            {
                Plugin.Log.LogWarning("[place] PlayerMove.Update: " + rights + " right-click and " + buttons
                    + " button reads replaced, 1 and 1 expected");
            }
            return list;
        }

        private static bool BodyRightClick(GameManager game, GameManager.ClickType type)
        {
            return !InventoryWindow.PickHoldsTheMouse && game.IsRightClick(type);
        }

        private static bool BodyMouseButton(int button)
        {
            return !InventoryWindow.PickHoldsTheMouse && Input.GetMouseButton(button);
        }
    }
}
