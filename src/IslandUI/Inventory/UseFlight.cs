using System.Collections.Generic;
using System.Globalization;
using HarmonyLib;
using IslandUI.UI;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI
{
    // THE STATION'S GIFT FLIES TO YOU (Forgejo #119, the ask: "having the food/water icon
    // fly from the station to the player"): a drink from a well or a meal from a food
    // box sends the HUD's own water drop or drumstick - the vitals panel's st_water
    // and st_food - from the station to the player, whether you pressed for it or
    // auto-use did it for you. The icon is the act's (AutoUse.Kinds), so an act that
    // opts in to auto-usage flies its own.
    //
    // TWO WAYS IN. A press - vanilla's prop panel, or the dock's key, which presses
    // vanilla's own button (PropVerbs.Press) - is UIManager.PropActionButton, whose
    // coroutine drinks (state 3) or eats (state 4) before its first yield, so by the
    // postfix the bar has moved. It flies only when the bar rose or the stock fell:
    // a press at a full bar plays vanilla's sound and moves nothing, and nothing flies.
    // Auto-use calls Launch after its own use (AutoUse.Tick).
    //
    // THE ENDS: from the middle of the station's drawing to over the eater's head,
    // held there, then into their chest, all projected every frame - the camera follows the player, so a screen point taken
    // at launch would be stale by the landing. It flies over the world and under the
    // whole HUD (the root canvas's first child): it is something happening out there.
    //
    // YOUR PEOPLE EAT AND DRINK TOO (Settings.NpcUseFlight): the game feeds them from
    // the food boxes and wells on its breed check, one a frame (NpcUse below), and
    // the same icon flies from the station to the one it fed - only one on the
    // screen, so a village fed out of sight does not take the flights.
    //
    // CHEAP: a few Images made once per canvas and a flight a struct in a fixed array;
    // a frame moves the live ones and allocates nothing. A launch that finds them all
    // flying takes the oldest.
    [HarmonyPatch(typeof(UIManager))]
    internal static class UseFlight
    {
        // THREE PHASES: the flight (its pop at the station included), a hold over the
        // eater's head - so you can tell who it went to, as a pickup shows itself
        // before it flies - and the consume, sinking into their chest as it fades
        private const float FlySeconds = 1f, HoldSeconds = .5f, ConsumeSeconds = .25f;
        private const float Seconds = FlySeconds + HoldSeconds + ConsumeSeconds;
        private const float Pop = .16f;        // of the flight: the icon swells at the station before it leaves
        private const float Size = .04f;       // of the screen's height: half again the vitals icon
        private const float Chest = 1.3f;      // metres above the player's feet
        private const float Head = 2.1f;       // ...and the hold's, just over the head

        private struct Flight
        {
            public bool Live;
            public Vector3 From;
            public Transform To;   // one of your people; null for the player
            public float Start;
            // SIDE BY SIDE: icons that go to one eater at once - a meal and a drink
            // on the same breed check - each hold in a lane of their own over the
            // head, the lanes centred; Offset eases to the lane's place when one joins
            public int Lane;
            public float Offset;
        }

        private static readonly Image[] Icons = new Image[16];
        private static readonly Flight[] Flights = new Flight[16];
        private static int _live;

        // A FEEDING IS A STREAM, as a quick-stack's sweep is (QuickStackFlight): the
        // game feeds your people one a frame, and those launches wait here and leave
        // Stagger apart, the whole burst bounded by MaxBurst however many it fed.
        private const float Stagger = .06f, MaxBurst = .6f;
        private struct Waiting
        {
            public Component Station;
            public string Icon;
            public Transform To;
        }
        private static readonly Queue<Waiting> Queued = new Queue<Waiting>();
        private static float _nextLaunch;

        private static void Enqueue(Component station, string icon, Transform to)
        {
            Queued.Enqueue(new Waiting { Station = station, Icon = icon, To = to });
        }

        private static void LaunchQueued(ManagersScript mn)
        {
            if (Queued.Count == 0 || Now < _nextLaunch)
            {
                return;
            }
            Waiting w = Queued.Dequeue();
            // gone while it waited: the next one takes its turn now
            if (w.Station == null || w.To == null)
            {
                return;
            }
            Launch(mn, w.Station, w.Icon, w.To);
            _nextLaunch = Now + Mathf.Min(Stagger, MaxBurst / (Queued.Count + 1));
        }
        private static RectTransform _canvas;
        private static Camera _uiCam;

        // Harness only: the flights' clock pinned, so shots - a third of a second
        // each - can step a flight at 30 fps. Negative: the clock.
        private static float _frozenAt = -1f;

        private static float Now
        {
            get { return _frozenAt >= 0f ? _frozenAt : Time.unscaledTime; }
        }

        internal static string HarnessFreeze(string at)
        {
            _frozenAt = float.Parse(at, CultureInfo.InvariantCulture);
            return (_frozenAt < 0f ? "running" : "frozen at " + _frozenAt.ToString("0.000", CultureInfo.InvariantCulture))
                + ", " + _live + " flying";
        }

        internal static void Launch(ManagersScript mn, Component station, string icon, Transform to = null)
        {
            if (mn == null || station == null || string.IsNullOrEmpty(icon) || !Ready(mn))
            {
                return;
            }
            int slot = 0;
            for (int i = 0; i < Flights.Length; i++)
            {
                if (!Flights[i].Live) { slot = i; break; }
                if (Flights[i].Start < Flights[slot].Start) { slot = i; }
            }
            // the middle of what is drawn, not the pivot at the station's feet
            Renderer drawn = station.GetComponentInChildren<Renderer>();
            Vector3 from = drawn != null ? drawn.bounds.center : station.transform.position;
            int lane = 0;
            while (LaneTaken(slot, to, lane)) { lane++; }
            if (!Flights[slot].Live) { _live++; }
            Flights[slot] = new Flight { Live = true, From = from, To = to, Start = Now, Lane = lane, Offset = float.NaN };
            Icons[slot].sprite = AnimePieces.Get(icon);
            Icons[slot].enabled = false;   // placed by the next Tick
        }

        private static bool LaneTaken(int slot, Transform to, int lane)
        {
            for (int j = 0; j < Flights.Length; j++)
            {
                if (j != slot && Flights[j].Live && Flights[j].To == to && Flights[j].Lane == lane) { return true; }
            }
            return false;
        }

        // the lane's place across the head: the eater's lanes centred on it
        private static float LaneOffset(int i, float size)
        {
            int lanes = 1;
            for (int j = 0; j < Flights.Length; j++)
            {
                if (Flights[j].Live && Flights[j].To == Flights[i].To) { lanes = Mathf.Max(lanes, Flights[j].Lane + 1); }
            }
            return (Flights[i].Lane - (lanes - 1) * .5f) * size * 1.15f;
        }

        // Every frame, from Plugin.Update; nothing while nothing flies or waits.
        internal static void Tick(ManagersScript mn)
        {
            LaunchQueued(mn);
            if (_live == 0)
            {
                return;
            }
            Camera cam = Camera.main;
            PlayerMove p = mn != null && mn.gameMN != null ? mn.gameMN.pMove : null;
            for (int i = 0; i < Flights.Length; i++)
            {
                if (!Flights[i].Live)
                {
                    continue;
                }
                float s = Now - Flights[i].Start;
                float t = s / Seconds;
                Transform eater = Flights[i].To != null ? Flights[i].To : p != null ? p.transform : null;
                if (t >= 1f || cam == null || eater == null || Icons[i] == null || _canvas == null)
                {
                    Land(i);
                    continue;
                }
                Vector3 a = cam.WorldToScreenPoint(Flights[i].From);
                Vector3 b = cam.WorldToScreenPoint(eater.position + Vector3.up * Head);
                Vector3 c = cam.WorldToScreenPoint(eater.position + Vector3.up * Chest);
                if (a.z <= 0f || b.z <= 0f || c.z <= 0f
                    || !ItemFlight.ToLocal(_canvas, _uiCam, a, out Vector2 from)
                    || !ItemFlight.ToLocal(_canvas, _uiCam, b, out Vector2 to)
                    || !ItemFlight.ToLocal(_canvas, _uiCam, c, out Vector2 chest))
                {
                    Icons[i].enabled = false;
                    continue;
                }
                float size = Size * _canvas.rect.height;
                float lane = LaneOffset(i, size);
                Flights[i].Offset = float.IsNaN(Flights[i].Offset) ? lane
                    : Mathf.Lerp(Flights[i].Offset, lane, 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime));
                // only the hold is in lanes: they all sink into the one chest
                to.x += Flights[i].Offset;
                Vector2 at;
                float scale, alpha = 1f;
                float f = s / FlySeconds;
                if (f < Pop)
                {
                    // the pop: out of the station, a swell past full size and back
                    float k = f / Pop;
                    at = from + Vector2.up * size * .4f * k;
                    scale = Mathf.Sin(k * Mathf.PI * .5f) * 1.2f;
                }
                else if (f < 1f)
                {
                    // the flight: eased in and out along a lifted line to over their head
                    float k = (f - Pop) / (1f - Pop);
                    float e = k * k * (3f - 2f * k);
                    Vector2 start = from + Vector2.up * size * .4f;
                    at = Vector2.Lerp(start, to, e);
                    at.y += Mathf.Sin(e * Mathf.PI) * Mathf.Min(size * 3f, Vector2.Distance(start, to) * .3f);
                    scale = Mathf.Lerp(1.2f, 1f, e);
                }
                else if (s < FlySeconds + HoldSeconds)
                {
                    // the hold: at rest over the head, a small bob
                    float k = (s - FlySeconds) / HoldSeconds;
                    at = to + Vector2.up * Mathf.Sin(k * Mathf.PI) * size * .15f;
                    scale = 1f;
                }
                else
                {
                    // the consume: down into the chest, shrinking and fading
                    float k = (s - FlySeconds - HoldSeconds) / ConsumeSeconds;
                    float e = k * k;
                    at = Vector2.Lerp(to, chest, e);
                    scale = Mathf.Lerp(1f, .4f, e);
                    alpha = 1f - e;
                }
                var rect = Icons[i].rectTransform;
                rect.anchoredPosition = at;
                rect.sizeDelta = new Vector2(size * scale, size * scale);
                Icons[i].color = new Color(1f, 1f, 1f, alpha);
                Icons[i].enabled = true;
            }
        }

        private static void Land(int i)
        {
            Flights[i].Live = false;
            Flights[i].To = null;
            _live--;
            if (Icons[i] != null) { Icons[i].enabled = false; }
        }

        // The pool, on the canvas the pickup's toast flies across; made again when a
        // scene load took the canvas and the icons with it.
        private static bool Ready(ManagersScript mn)
        {
            if (_canvas != null && Icons[0] != null)
            {
                return true;
            }
            _canvas = ItemFlight.RootCanvas(mn.uiMN, out _uiCam);
            if (_canvas == null)
            {
                return false;
            }
            for (int i = 0; i < Icons.Length; i++)
            {
                Icons[i] = AnimePieces.Image(_canvas, "UseFlight", "st_water");
                Icons[i].preserveAspect = true;
                Icons[i].enabled = false;
                var rect = Icons[i].rectTransform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
                rect.SetAsFirstSibling();
                Flights[i].Live = false;
            }
            _live = 0;
            return true;
        }

        // ---------- your people, fed by the breed check ----------

        // GameManager.FoodStorageCheck and WaterStorageCheck walk the friend list and
        // yield right after each one who wanted feeding (food or water under 100),
        // fed or not: fed when they stood within the station's reach. So after a
        // step that yields, the one at the loop's index was fed exactly when
        // they are within maxDistance - the game's own test, in the same frame. The
        // iterator's hoisted fields are found by the compiler's names (<i>5__N);
        // a build that renames them turns this off, and nothing else.
        [HarmonyPatch]
        private static class NpcUse
        {
            private static readonly AccessTools.FieldRef<GameManager, ManagersScript> Managers = AccessTools.FieldRefAccess<GameManager, ManagersScript>("mn");
            private static System.Reflection.FieldInfo _food, _foodIndex, _foodReach, _foodGm, _water, _waterIndex, _waterReach, _waterGm;
            private static bool _foodRead, _waterRead;

            private static System.Collections.Generic.IEnumerable<System.Reflection.MethodBase> TargetMethods()
            {
                yield return AccessTools.EnumeratorMoveNext(AccessTools.Method(typeof(GameManager), nameof(GameManager.FoodStorageCheck)));
                yield return AccessTools.EnumeratorMoveNext(AccessTools.Method(typeof(GameManager), nameof(GameManager.WaterStorageCheck)));
            }

            private static void Postfix(object __instance, bool __result)
            {
                if (!__result || __instance == null || !Settings.NpcUseFlight.Value)
                {
                    return;
                }
                System.Type type = __instance.GetType();
                bool food = type.Name.Contains("FoodStorageCheck");
                if (!(food ? _foodRead : _waterRead))
                {
                    Resolve(type, food);
                }
                System.Reflection.FieldInfo station = food ? _food : _water, index = food ? _foodIndex : _waterIndex,
                    reach = food ? _foodReach : _waterReach, owner = food ? _foodGm : _waterGm;
                if (station == null || index == null || reach == null || owner == null)
                {
                    return;
                }
                var store = station.GetValue(__instance) as InventorySlot;
                var gm = owner.GetValue(__instance) as GameManager;
                ManagersScript mn = gm != null ? Managers(gm) : null;
                if (store == null || mn == null || mn.npcMN == null || mn.npcMN.friendList == null)
                {
                    return;
                }
                int i = (int)index.GetValue(__instance);
                if (i < 0 || i >= mn.npcMN.friendList.Count)
                {
                    return;
                }
                CommonStates who = mn.npcMN.friendList[i];
                if (who == null || Vector3.Distance(who.transform.position, store.transform.position) > (float)reach.GetValue(__instance))
                {
                    return;
                }
                Camera cam = Camera.main;
                Vector3 at = cam != null ? cam.WorldToViewportPoint(who.transform.position) : Vector3.back;
                if (at.z <= 0f || at.x < 0f || at.x > 1f || at.y < 0f || at.y > 1f)
                {
                    return;   // fed out of sight
                }
                string icon = AutoUse.IconFor(store.GetComponent<ItemInfo>(), food ? 4 : 3) ?? (food ? "st_food" : "st_water");
                Enqueue(store, icon, who.transform);
            }

            private static void Resolve(System.Type type, bool food)
            {
                System.Reflection.FieldInfo station = type.GetField("tmpInventory"), index = null, reach = null,
                    owner = type.GetField("<>4__this", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                foreach (System.Reflection.FieldInfo f in type.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic))
                {
                    if (f.FieldType == typeof(int) && f.Name.StartsWith("<i>5__")) { index = f; }
                    else if (f.FieldType == typeof(float) && f.Name.StartsWith("<maxDistance>5__")) { reach = f; }
                }
                if (station == null || index == null || reach == null || owner == null)
                {
                    Plugin.Log.LogWarning("UseFlight: " + type.Name + " has no tmpInventory, <i>, <maxDistance> or <>4__this; your people's meals do not fly");
                }
                if (food) { _food = station; _foodIndex = index; _foodReach = reach; _foodGm = owner; _foodRead = true; }
                else { _water = station; _waterIndex = index; _waterReach = reach; _waterGm = owner; _waterRead = true; }
            }
        }

        // ---------- a press: vanilla's panel, or the dock's key through it ----------

        private struct Before
        {
            public bool Armed;
            public bool Water;
            public float Bar;
            public float Stock;
            public InventorySlot Station;
            public string Icon;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(UIManager.PropActionButton))]
        private static void BeforePress(UIManager __instance, int id, out Before __state)
        {
            __state = default(Before);
            ManagersScript mn = __instance.mn;
            int[] states = __instance.propActionState;
            if (mn == null || mn.inventory == null || mn.gameMN == null || __instance.propActProgress != 0
                || states == null || id < 0 || id >= states.Length || (states[id] != 3 && states[id] != 4))
            {
                return;
            }
            InventorySlot store = mn.inventory.tmpSubInventory;
            CommonStates you = mn.gameMN.playerCommons != null && GameManager.selectPlayer < mn.gameMN.playerCommons.Length
                ? mn.gameMN.playerCommons[GameManager.selectPlayer] : null;
            string icon = store != null ? AutoUse.IconFor(store.GetComponent<ItemInfo>(), states[id]) : null;
            if (you == null || icon == null)
            {
                return;
            }
            __state.Armed = true;
            __state.Water = states[id] == 3;
            __state.Bar = __state.Water ? you.water : you.food;
            __state.Stock = store.slots[0].itemLife;
            __state.Station = store;
            __state.Icon = icon;
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(UIManager.PropActionButton))]
        private static void AfterPress(UIManager __instance, Before __state)
        {
            if (!__state.Armed || __state.Station == null)
            {
                return;
            }
            ManagersScript mn = __instance.mn;
            CommonStates you = mn.gameMN.playerCommons[GameManager.selectPlayer];
            float bar = __state.Water ? you.water : you.food;
            if (bar > __state.Bar + .01f || __state.Station.slots[0].itemLife < __state.Stock - .01f)
            {
                Launch(mn, __state.Station, __state.Icon);
            }
        }
    }
}
