using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using IslandUI.UI;

namespace IslandUI
{
    // AN ITEM DROPPED ON SOMEONE IS USED ON THEM (window-system.md 6.31, D141).
    // A plain use is yours; to feed, dress or treat someone you drop the item on
    // them - on their bag window, their sheet, their roster strip, the indicator
    // over the target's head, or the person in the world. A cell under the
    // pointer is always asked first: a cell of their bag is a TRANSFER into it, as it
    // has always been, and only the rest of their window means "use it on them".
    //
    // Asked early in the Tick, above the sheets: a roster row and a sheet control
    // claim the pointer (TickSheets returns out of the input chain), so a drop there
    // used to be swallowed and the item stayed on the cursor.
    internal static partial class InventoryWindow
    {
        private static CommonStates _dropOver;
        // the ring under someone you hold an item over who is not the target; the
        // target's own ring turns gold instead (PaintWorldMarker)
        private static GameObject _offerRing;
        private static SpriteRenderer _offerRingSprite;
        private static readonly Color OfferGold = Theme.ParseColor("#F4C45C", Color.yellow);

        // For DragOwnsThePointer: a press over someone with an item in hand is a
        // drop on them, and vanilla must not read it as "drop it on the ground"
        // (InventoryManager.Hover drops on a mouse-down over the world).
        internal static bool OfferUnderPointer { get { return _dropOver != null; } }

        // True when this frame's release was a drop on someone (the chain stops).
        private static bool TickDropOnPerson(Camera cam)
        {
            _dropOver = null;
            if (HandEmpty() || _grabSource == null || _mn == null || _mn.gameMN == null
                // a worn item lifted off the loadout is not an offer to anyone: a
                // loadout grab is remembered now, and this chain stays as
                // it was before it was
                || LiftedFromLoadout()
                // a cell first: dropping on a cell of their bag moves it into the bag
                || HitCell(cam) != null)
            {
                PaintOfferRing(null);
                return false;
            }
            CommonStates who = PersonUnderPointer(cam);
            InventorySlot chest = who == null ? ChestUnderPointer(cam) : null;
            // Out over the world, the target's marker first - it is UI, above the
            // clutter of a camp - then a person, picked as Tab picks
            if (who == null && chest == null && !OverUI())
            {
                who = MarkerUnderPointer() ?? PersonInWorld();
            }
            bool takes = who == null || CanReceive(who);
            PaintOfferRing(takes && who != null && !ReferenceEquals(who, _unitFor) ? who : null);
            if (who == null && chest == null)
            {
                return false;
            }
            _dropOver = who;
            string name = who == null ? "this chest"
                : Hostile(who) || string.IsNullOrEmpty(who.charaName) ? "them" : who.charaName;
            // Using an item on someone is an act, and acts want reach (D145): the
            // roster lists people across the map, and a drop on a strip there used
            // to feed them from wherever you stood.
            bool near = who == null || InReach(who);
            // out of reach but in walking distance: you walk over and give it
            bool walk = !near && CanWalkTo(who);
            string verb = who != null && HeldIsGift() ? "give to  →  " : "use on  →  ";
            CursorBadge.Show(!takes ? name + "  ·  can't take this"
                : near ? verb + name : walk ? "walk over and " + verb + name
                : name + "  ·  out of reach");
            if (!Input.GetMouseButtonUp(0))
            {
                return false;
            }
            // A FAILED ATTEMPT KEEPS THE ITEM IN HAND - someone who cannot take it,
            // or one too far to walk to - so the next try is one move, not a lift
            // from the bag again. Never onto the ground: the release is consumed.
            if (takes && near)
            {
                UseHeldOn(who, chest);
            }
            else if (takes && walk)
            {
                WalkThenGive(who);
            }
            PaintOfferRing(null);
            SetPressed(_pressedCell, false);
            _pressedCell = null;
            _dragging = false;
            return true;
        }

        // For the harness, which cannot drag: lift the first stack of `key` in any
        // open window into the hand, as a click on its cell does; and put it back.
        internal static string HarnessLift(string key)
        {
            if (!HandEmpty())
            {
                return "hand full";
            }
            foreach (Plate plate in Plates)
            {
                foreach (CellView cell in plate.Cells)
                {
                    if (cell.Slot != null && cell.Slot.itemKey == key)
                    {
                        LeftClick(cell);
                        return HandEmpty() ? "refused" : "lifted";
                    }
                }
            }
            return "none";
        }

        internal static string HarnessHome()
        {
            if (HandEmpty() || _grabSource == null)
            {
                return "hand empty";
            }
            LeftClick(_grabSource);
            return HandEmpty() ? "home" : "still held";
        }

        // The real "is the pointer over UI" answer - gameMN.OnUI is forced true for
        // the length of a drag (DragOwnsThePointer). Our marker draws no raycasts,
        // so the pointer over it is still "over the world" here.
        private static bool OverUI()
        {
            EventSystem events = EventSystem.current;
            return events != null && events.IsPointerOverGameObject();
        }

        // Who a held item would go to, by name, for the hints that teach the drop:
        // the target when it is a person the window may name (D89), else null.
        internal static string GiveName()
        {
            CommonStates who = Focus() as CommonStates;
            return who == null || Hostile(who) || string.IsNullOrEmpty(who.charaName)
                ? null : who.charaName;
        }

        // THE OFFER: food, a drink or a consumable in hand and a person in walking
        // distance targeted. Their indicator lights up as the place it can be
        // dropped - UI, above the clutter of a camp - and the words come only at
        // the cursor, over it. Giving is a drop (D141), and a drop nobody knows
        // about is a feature nobody has: the first report after 0.3.0 was a potion
        // spent on yourself by a player who never found it. (A text slip over the
        // target dock came first and read poorly; the dock was a drop place too,
        // until a release beside the bags gave an item away.)
        private static bool GiftOffered(CommonStates who)
        {
            return who != null && _mn != null && !HandEmpty() && _grabSource != null
                && !LiftedFromLoadout() && CanReceive(who)
                && (InReach(who) || CanWalkTo(who)) && HeldIsGift();
        }

        // WHO AN ITEM CAN BE USED ON: whom vanilla would have let you, which is
        // whoever its click opens a menu for (PlayerMove.InteractNPC) - a living
        // person (charaType 1) who has not turned on you, or a shopkeeper (2).
        // An animal, an enemy or a boss (10, 11, 100) is attacked or searched
        // instead, and vanilla's use refuses the knocked out and the dead (caution
        // 40). Every drop place asks this, the world and the windows alike.
        private static bool CanReceive(CommonStates who)
        {
            if (who == null || who.dead != 0 || who.life <= 0.0
                || (_mn != null && _mn.gameMN != null && _mn.gameMN.pMove != null
                    && ReferenceEquals(who, _mn.gameMN.pMove.common)))
            {
                return false;
            }
            return (who.charaType == 1 && (who.debuff == null || who.debuff.discontent != 4))
                || who.charaType == 2;
        }

        private static void PaintOfferRing(CommonStates who)
        {
            PaintRing(ref _offerRing, ref _offerRingSprite, "IslandUI_OfferRing",
                who != null ? who.transform : null, OfferGold, PersonRadius);
        }

        // The target, when the pointer is on its marker (the medallion, the plate,
        // its footer) - null otherwise.
        private static CommonStates MarkerUnderPointer()
        {
            if (_unitFor == null || _markerHit == null || !_markerHit.gameObject.activeInHierarchy)
            {
                return null;
            }
            return RectTransformUtility.RectangleContainsScreenPoint(_markerHit.rectTransform, Input.mousePosition, null)
                ? _unitFor : null;
        }

        // THE PERSON IN THE WORLD, picked as Tab picks (D159) so a drop and a click
        // agree on who is meant: whose drawn shape the pointer is ON - near is not
        // enough, a drop in a crowd must not guess - and the target first where
        // shapes overlap, else the smallest. One who can take the item wins over
        // one who cannot; one who cannot is still returned, so the badge can say
        // so and the release does not throw the item on the ground.
        private static CommonStates PersonInWorld()
        {
            Camera world = Camera.main;
            PlayerMove p = _mn.gameMN.pMove;
            if (world == null || p == null)
            {
                return null;
            }
            Vector2 cursor = Input.mousePosition;
            Ray ray = world.ScreenPointToRay(cursor);
            Vector3 point;
            if (Physics.Raycast(ray, out RaycastHit hit, 200f, p.movableLayer))
            {
                point = hit.point;
            }
            else
            {
                var ground = new Plane(Vector3.up, p.transform.position);
                if (!ground.Raycast(ray, out float along))
                {
                    return null;
                }
                point = ray.GetPoint(along);
            }
            Collider[] hits = Physics.OverlapSphere(point, TabRadius, LayerMask.GetMask("NPC"));
            CommonStates best = null;
            float bestArea = float.MaxValue;
            bool bestTakes = false;
            for (int i = 0; i < hits.Length; i++)
            {
                if (!(TargetableAt(hits[i].gameObject, p) is CommonStates who)
                    || !ScreenShape(world, who, out Rect shape) || !shape.Contains(cursor))
                {
                    continue;
                }
                bool takes = CanReceive(who);
                if (takes && ReferenceEquals(who, _unitFor))
                {
                    return who;
                }
                float area = shape.width * shape.height;
                if ((takes && !bestTakes) || (takes == bestTakes && area < bestArea))
                {
                    best = who;
                    bestArea = area;
                    bestTakes = takes;
                }
            }
            return best;
        }

        // What a person can be given by a drop: what is eaten, drunk or used up.
        // Equipment dropped on someone dresses them, which their loadout says.
        private static bool HeldIsGift()
        {
            ItemSlot hand = _mn.inventory != null ? _mn.inventory.hoverSlot : null;
            ItemData data = hand != null && !string.IsNullOrEmpty(hand.itemKey) && _mn.itemMN != null
                ? _mn.itemMN.FindItem(hand.itemKey) : null;
            return data != null && (data.itemType == ItemData.ItemType.Food
                || data.itemType == ItemData.ItemType.Bottle
                || data.itemType == ItemData.ItemType.Consumables);
        }

        // Whose window is under the pointer: the bag window, a sheet, a roster
        // strip. Null over anything else. NOT THE TARGET DOCK: it sits on the action
        // bar and under the bags, where an item is moved about all the time, and a
        // release a little off its mark there gave the item away. Over the dock a
        // release now does what it does over any panel - nothing.
        private static CommonStates PersonUnderPointer(Camera cam)
        {
            Vector2 mouse = Input.mousePosition;
            if (_target is CommonStates held)
            {
                int bag = _groupFrame[GroupBag];
                if (bag >= 0 && bag < Plates.Count && Plates[bag].Window.Go.activeInHierarchy
                    && RectTransformUtility.RectangleContainsScreenPoint(Plates[bag].Window.Rect, mouse, cam))
                {
                    return held;
                }
            }
            for (int i = 0; i < SheetPlates.Count; i++)
            {
                Plate plate = SheetPlates[i];
                if (plate == null || !(plate.Owner is CommonStates owner) || !plate.Window.Go.activeInHierarchy)
                {
                    continue;
                }
                bool roster = plate.IsRoster;
                if (!roster && !IsDossier(plate.Group))
                {
                    continue;
                }
                if (roster && ClippedAway(plate, cam))
                {
                    continue;
                }
                if (RectTransformUtility.RectangleContainsScreenPoint(plate.Window.Rect, mouse, cam))
                {
                    return owner;
                }
            }
            return null;
        }

        // A chest's own window, for the one item that acts on a chest (pocket_01).
        private static InventorySlot ChestUnderPointer(Camera cam)
        {
            if (!(_target is InventorySlot chest) || !HeldIs("pocket_01"))
            {
                return null;
            }
            int frame = _groupFrame[GroupCharacter];
            return frame >= 0 && frame < Plates.Count && Plates[frame].Window.Go.activeInHierarchy
                && RectTransformUtility.RectangleContainsScreenPoint(Plates[frame].Window.Rect, Input.mousePosition, cam)
                ? chest : null;
        }

        private static bool HeldIs(string key)
        {
            ItemSlot hand = _mn.inventory != null ? _mn.inventory.hoverSlot : null;
            return hand != null && hand.itemKey == key;
        }

        // The stack goes back where it came from, and the item is used from there
        // through the one dispatcher (ItemUse), aimed at them - so staging, equipment
        // and vanilla's own refusals behave exactly as a right-click does on you.
        private static void UseHeldOn(CommonStates who, InventorySlot chest)
        {
            CellView source = _grabSource;
            if (!PutBack(source, out bool mine))
            {
                return;
            }
            if (who != null)
            {
                GiveFrom(who, source, mine);
                return;
            }
            bool done;
            VanillaContainerUI.PocketDrop = chest;
            try
            {
                done = ItemUse.Use(_mn, source.Slot, source.VanillaSlotId, use => ItemUse.OnYou(_mn, use), mine);
            }
            finally
            {
                VanillaContainerUI.PocketDrop = null;
            }
            Done(done);
        }

        // the stack back in its cell, and whether the cell is one of yours
        private static bool PutBack(CellView source, out bool mine)
        {
            mine = false;
            LeftClick(source);
            if (!HandEmpty() || source.Slot == null || string.IsNullOrEmpty(source.Slot.itemKey))
            {
                return false;   // it could not go back (the cell filled meanwhile): keep it in hand
            }
            Plate plate = PlateOf(source);
            CommonStates sourceOwner = plate != null ? plate.Owner : null;
            mine = sourceOwner == null || ReferenceEquals(sourceOwner, _mn.gameMN.pMove.common);
            return true;
        }

        // THE STACK STAYS IN YOUR HAND. One of it is given; the rest comes back to
        // the cursor - at once on a refusal, and once the one has left on a give
        // (Relift) - so a stack of ten can be handed round a camp one by one, and
        // the flight always starts where the hand is. `cell` is the cell the stack
        // was put back into for the use.
        private static void GiveFrom(CommonStates who, CellView cell, bool mine)
        {
            ItemSlot slot = cell.Slot;
            int vanillaSlotId = cell.VanillaSlotId;
            string key = slot.itemKey;
            ItemData item = _mn.itemMN != null ? _mn.itemMN.FindItem(key) : null;   // before the stack goes
            int before = CountHeld(key, slot);
            // A REFUSAL IS SAID INSIDE THE CALL: vanilla raises its caution before
            // the first yield of the use (the collapsed, a perfume that does
            // nothing), so a caution during the call is the refusal, at once
            int cautions = CautionsRaised;
            bool fired = ItemUse.Use(_mn, slot, vanillaSlotId, use => ItemUse.OnThem(_mn, who, use), mine);
            bool refused = !fired || CautionsRaised != cautions;
            Plugin.Log.LogInfo("Drop: " + key + " used on " + who.charaName + (refused ? " (refused)" : ""));
            if (refused)
            {
                if (!fired && _mn.eventMN != null)
                {
                    _mn.eventMN.StartCoroutine(_mn.eventMN.GoCaution(34));
                }
                // back in the hand, for another try - as a failed drop leaves it
                Relift(cell, key);
                _nextRefresh = 0f;
                Refresh();
                return;
            }
            // THE TARGET STAYS WHO IT WAS. A give used to make the recipient the
            // target; but acting on someone is not choosing them - in a fight you
            // act on what is not your target all the time - and handing a stack
            // round a camp moved the target with every one. The flight says who
            // got it.
            FlyTo(who, item, cell, before);
            Done(true);
        }

        // Every caution vanilla raises, counted (TargetPatches): GiveFrom reads a
        // change across its call as the use's refusal.
        internal static int CautionsRaised;

        // THE ITEM FLIES TO THEM: the quick-stack's flight (ItemFlight), from the
        // cursor - where the hand is, the rest of the stack back on it - to the
        // middle of their drawn body. "-1" in vanilla's own format: it left your bags.
        //
        // ONLY WHEN IT LEFT. A use that was fired is not a use that took: vanilla
        // refuses inside the call (a perfume on someone it does nothing for, the
        // collapsed - "Cannot be used for collapsed NPC") and keeps the item, and a
        // flight then said the opposite of the caution under it. Most uses spend
        // the item at once; a consumable's coroutine can spend it a few frames
        // later, so the count is watched for a moment before the flight is let go -
        // and before the rest is lifted back: lifted sooner, the coroutine would
        // spend from an emptied slot.
        private static void FlyTo(CommonStates who, ItemData item, CellView cell, int before)
        {
            UIManager ui = _mn.uiMN;
            // StartCoroutine throws on an inactive behaviour
            if (ui == null || !ui.isActiveAndEnabled || item == null || who == null)
            {
                Relift(cell, item != null ? item.name : null);
                return;
            }
            ui.StartCoroutine(FlyWhenSpent(ui, who, item, cell, cell.Slot, before));
        }

        private static IEnumerator FlyWhenSpent(UIManager ui, CommonStates who, ItemData item, CellView cell,
            ItemSlot source, int before)
        {
            float until = Time.unscaledTime + SpentWithin;
            while (CountHeld(item.name, source) >= before)
            {
                if (Time.unscaledTime > until)
                {
                    Relift(cell, item.name);
                    yield break;   // it never left: back in the hand, no flight
                }
                yield return null;
            }
            Relift(cell, item.name);
            Camera world = Camera.main;
            RectTransform canvasRect = ItemFlight.RootCanvas(ui, out Camera uiCam);
            if (world == null || who == null || canvasRect == null || !ScreenShape(world, who, out Rect body))
            {
                yield break;
            }
            IEnumerator flight = Fly(ui, canvasRect, uiCam, item, Input.mousePosition, body.center);
            while (flight.MoveNext())
            {
                yield return flight.Current;
            }
        }

        private const float SpentWithin = 0.5f;

        // The rest of the stack back onto the cursor: from the cell it was used
        // from, or - when the use staged it through the Shared Bag and left the
        // remainder there - from a cell of yours holding the same item. Never over
        // something the hand took up meanwhile.
        private static void Relift(CellView cell, string key)
        {
            if (string.IsNullOrEmpty(key) || _mn == null || !HandEmpty())
            {
                return;
            }
            if (cell != null && cell.Slot != null && cell.Slot.itemKey == key)
            {
                LeftClick(cell);
                return;
            }
            CommonStates you = _mn.gameMN != null && _mn.gameMN.pMove != null ? _mn.gameMN.pMove.common : null;
            foreach (Plate plate in Plates)
            {
                if (plate.Owner != null && !ReferenceEquals(plate.Owner, you))
                {
                    continue;
                }
                foreach (CellView c in plate.Cells)
                {
                    if (c.Slot != null && c.Slot.itemKey == key && !plate.IsLoadout)
                    {
                        LeftClick(c);
                        return;
                    }
                }
            }
        }

        // How many of `key` you hold where a give takes it from: the Shared Bag,
        // your own bag (staging may have moved it into either), and the cell it
        // came from when that is someone else's.
        private static int CountHeld(string key, ItemSlot source)
        {
            int n = 0;
            InventoryManager inv = _mn != null ? _mn.inventory : null;
            for (int i = 0; inv != null && i < inv.baseCount && i < inv.itemSlot.Length; i++)
            {
                n += Count(inv.itemSlot[i], key);
            }
            CommonStates you = _mn != null && _mn.gameMN != null && _mn.gameMN.pMove != null ? _mn.gameMN.pMove.common : null;
            ItemSlot[] bag = you != null && you.bag != null ? you.bag.slots : null;
            bool seen = false;
            for (int i = 0; bag != null && i < bag.Length; i++)
            {
                n += Count(bag[i], key);
                seen |= ReferenceEquals(bag[i], source);
            }
            for (int i = 0; inv != null && i < inv.baseCount && i < inv.itemSlot.Length; i++)
            {
                seen |= ReferenceEquals(inv.itemSlot[i], source);
            }
            return seen ? n : n + Count(source, key);
        }

        private static int Count(ItemSlot slot, string key)
        {
            return slot != null && slot.itemKey == key ? slot.stack : 0;
        }

        private static IEnumerator Fly(UIManager ui, RectTransform canvasRect, Camera uiCam, ItemData item,
            Vector2 fromScreen, Vector2 toScreen)
        {
            GameObject toast = ItemFlight.Spawn(ui, item, -1);
            if (toast == null)
            {
                yield break;
            }
            // try/finally: the toast is ours, nothing in vanilla will destroy it
            try
            {
                ItemFlight.Style(toast);
                var rect = (RectTransform)toast.transform;
                CanvasGroup group = ItemFlight.Detach(toast, canvasRect, out Vector2 _);
                if (!ItemFlight.ToLocal(canvasRect, uiCam, fromScreen, out Vector2 from)
                    || !ItemFlight.ToLocal(canvasRect, uiCam, toScreen, out Vector2 to))
                {
                    yield break;
                }
                rect.anchoredPosition = from;   // before the first frame is drawn
                IEnumerator leg = ItemFlight.Travel(rect, group, from, to, GiveFlightSeconds);
                while (leg.MoveNext())
                {
                    yield return leg.Current;
                }
            }
            finally
            {
                if (toast != null)
                {
                    Object.Destroy(toast);
                }
            }
        }

        private const float GiveFlightSeconds = 0.45f;

        private static void Done(bool done)
        {
            if (!done && _mn.eventMN != null)
            {
                _mn.eventMN.StartCoroutine(_mn.eventMN.GoCaution(34));
            }
            _nextRefresh = 0f;
            Refresh();
        }

        // GIVING FROM AFAR. A drop on someone out of reach walks you over and
        // gives it the frame you are in reach. The stack stays on the cursor for the
        // walk, and the give at the end is a drop's own (UseHeldOn), so it flies from
        // the cursor and the rest stays in hand. Vanilla does the walking:
        // pMove.target is their interaction collider, as a click leaves it, and the
        // give lands at reach (3 m), before vanilla's own arrival at 1 m
        // (InteractNPC, which would open their menu). Anything that takes the walk
        // away - WASD, a click elsewhere, a new target - ends it with the item still
        // in hand, and so does the item leaving the hand. Across the map is not "out
        // of reach" but away: a roster strip of someone far off still says so.
        private sealed class WalkGive
        {
            public CommonStates Who;
            public string Key;
            public GameObject Aim;
            public float Until;
        }

        private static WalkGive _walkGive;
        private const float WalkGiveMax = 20f;
        private const float WalkGiveSeconds = 20f;

        private static bool CanWalkTo(CommonStates who)
        {
            PlayerMove p = _mn.gameMN.pMove;
            return p != null && who != null && who.nMove != null && CanReceive(who)
                && (who.transform.position - p.transform.position).sqrMagnitude <= WalkGiveMax * WalkGiveMax;
        }

        private static void WalkThenGive(CommonStates who)
        {
            ItemSlot hand = _mn.inventory.hoverSlot;
            GameObject aim = InteractCollider(who);
            _mn.gameMN.pMove.target = aim;
            _walkGive = new WalkGive
            {
                Who = who, Key = hand.itemKey, Aim = aim, Until = Time.unscaledTime + WalkGiveSeconds,
            };
            Plugin.Log.LogInfo("Drop: walking to " + who.charaName + " to give " + _walkGive.Key);
        }

        // what a click on them leaves in pMove.target: the layer-14 child collider
        // PlayerMove.InteractNPC reads its NPCMove through
        private static GameObject InteractCollider(CommonStates who)
        {
            foreach (Collider c in who.GetComponentsInChildren<Collider>())
            {
                if (c.gameObject.layer == 14 && c.transform.parent == who.transform)
                {
                    return c.gameObject;
                }
            }
            return who.gameObject;
        }

        internal static void TickWalkGive(ManagersScript mn)
        {
            WalkGive w = _walkGive;
            if (w == null)
            {
                return;
            }
            _mn = mn;
            PlayerMove p = mn != null && mn.gameMN != null ? mn.gameMN.pMove : null;
            string why = p == null ? "no player"
                : !ReferenceEquals(p.target, w.Aim) ? "the walk was taken over"
                : w.Who == null || !w.Who.gameObject.activeInHierarchy || !CanReceive(w.Who) ? "they can no longer take it"
                : !StillHeld(w.Key) ? "the item left the hand"
                : Time.unscaledTime > w.Until ? "too long on the way"
                : null;
            if (why != null)
            {
                _walkGive = null;
                Plugin.Log.LogInfo("Drop: the walk to give " + w.Key + " ended - " + why);
                return;
            }
            if (!ReachRule.Live(mn, w.Who))
            {
                return;
            }
            Arrive(p, w);
        }

        private static bool StillHeld(string key)
        {
            return !HandEmpty() && _grabSource != null && _mn.inventory.hoverSlot.itemKey == key;
        }

        // arrived: vanilla's walk ends here, not at its 1 m menu, and the give is a
        // drop's own - the stack held, one of it given
        private static void Arrive(PlayerMove p, WalkGive w)
        {
            _walkGive = null;
            p.target = null;
            if (StillHeld(w.Key))
            {
                UseHeldOn(w.Who, null);
            }
        }

        // For the patch on PlayerMove.InteractNPC: vanilla arrived before reach
        // did (it should not - reach is wider) - the give, not their menu.
        internal static bool ArrivedToGive(PlayerMove p)
        {
            WalkGive w = _walkGive;
            if (w == null || p == null || !ReferenceEquals(p.target, w.Aim))
            {
                return false;
            }
            Arrive(p, w);
            return true;
        }
    }
}
