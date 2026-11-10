using System.Collections.Generic;
using IslandUI.UI;
using UnityEngine;

namespace IslandUI
{
    // AN ITEM DROPPED ON A CHEST IN THE WORLD GOES INTO IT (Forgejo #207), as one
    // dropped on a person is used on them (InventoryWindow.Drop.cs). Before, a chest
    // took an item only through its window, and a release over one threw the item
    // on the ground at your feet.
    //
    // Nothing here is a new rule; each question already had its answer:
    //   which containers   plain storage of yours (ContainerPolicy.TakesDrop), the
    //                      stores All in fills. A planter, a cage, a rack or a bench
    //                      keeps its own rules, and a release there is what it was.
    //   how near           the container's own reach (ReachRule, D145). Out of reach
    //                      the badge says so and the item stays in hand: you do not
    //                      walk there, as you do to give something to a person.
    //   what does not fit  stays in hand: MoveStack tops up the stacks of the same
    //                      item, then takes one empty cell, and a stack that found
    //                      no room at all is refused with vanilla's "cannot store
    //                      any more". Never onto the ground: the release is consumed.
    //   a favourite        stays in your bags (FavouriteRules): refused, and home.
    //   the vicinity       a cell is asked first and a window's frame keeps the item,
    //                      as ever; this is only ever the world, under no UI.
    //   someone in front   a person under the pointer is asked first (the chain in
    //                      Tick): they are what is drawn over the chest.
    // A chest's shape is everything its prefab draws: the Harvest Box takes a drop on
    // the barrels and crates that are part of it.
    internal static partial class InventoryWindow
    {
        private static InventorySlot _dropStore;

        // True when this frame's release was a drop on a store (the chain stops).
        // Runs after TickDropOnPerson, which has just cleared the offer ring.
        private static bool TickDropOnStore(Camera cam)
        {
            _dropStore = null;
            if (_dropOver != null || HandEmpty() || _grabSource == null || _mn == null || _mn.gameMN == null
                || LiftedFromLoadout() || HitCell(cam) != null || OverUI())
            {
                return false;
            }
            InventorySlot store = StoreInWorld();
            if (store == null)
            {
                return false;
            }
            _dropStore = store;
            ItemSlot hand = _mn.inventory.hoverSlot;
            bool kept = Favourites.IsFavourite(hand);
            bool near = ReachRule.Live(_mn, store);
            bool room = HasRoom(store, hand);
            if (!kept && room)
            {
                PaintRing(ref _offerRing, ref _offerRingSprite, "IslandUI_OfferRing",
                    store.transform, OfferGold, ThingRadius(store));
            }
            string name = StoreName(store);
            CursorBadge.Show(kept ? FavouriteRules.KeptWords
                : !near ? name + "  ·  " + Shared.Loc.Get("drop.out_of_reach", "out of reach")
                : !room ? name + "  ·  " + Shared.Loc.Get("drop.no_room", "no room")
                : Shared.Loc.Get("drop.put_in", "put in  →  {0}", name));
            if (!Input.GetMouseButtonUp(0))
            {
                return false;
            }
            if (kept)
            {
                FavouriteRules.Refuse();
                HomeHeld();
            }
            else if (near)
            {
                PutHeldIn(store);
            }
            PaintOfferRing(null);
            SetPressed(_pressedCell, false);
            _pressedCell = null;
            _dragging = false;
            _cardPressed = CardPressNone;
            return true;
        }

        // As much of the hand as fits; the rest stays on the cursor.
        private static void PutHeldIn(InventorySlot store)
        {
            InventoryManager inv = _mn.inventory;
            ItemSlot hand = inv.hoverSlot;
            string key = hand.itemKey;
            int moved = MoveStack(inv, hand, store.slots, store.slots.Length, false, out ItemData data);
            if (moved == 0)
            {
                Refuse(18);   // vanilla's "cannot store any more"
                return;
            }
            if (string.IsNullOrEmpty(hand.itemKey))
            {
                _grabSource = null;
                NormalizeHover();
            }
            else
            {
                inv.StackTextChange(-1);
            }
            QuickStackFlight.Show(_mn, new List<QuickStack.Deposit>
            {
                new QuickStack.Deposit { Item = data, Storage = store, Count = moved, FromScreen = Input.mousePosition },
            });
            Plugin.Log.LogInfo($"Drop: {key} x{moved} into {store.name}" + (HandEmpty() ? "" : $", {hand.stack} left in hand"));
            AfterCtrlMove();
        }

        private static bool HasRoom(InventorySlot store, ItemSlot hand)
        {
            ItemData data = _mn.itemMN != null ? _mn.itemMN.FindItem(hand.itemKey) : null;
            for (int i = 0; i < store.slots.Length; i++)
            {
                ItemSlot slot = store.slots[i];
                if (slot != null && (string.IsNullOrEmpty(slot.itemKey)
                    || (data != null && slot.itemKey == hand.itemKey && slot.stack < data.maxStack)))
                {
                    return true;
                }
            }
            return false;
        }

        // THE STORE IN THE WORLD, picked as PersonInWorld picks: whose drawn shape the
        // pointer is ON, one in reach over one out of it, else the smallest - so a
        // chest wins over the wider one behind it. One of yours only: a stranger's
        // has no reach (ReachRule.Of), and is not offered. Not one vanilla has open
        // with its mirror live, whose panel would write the old contents back on
        // closing.
        private static InventorySlot StoreInWorld()
        {
            Camera world = Camera.main;
            PlayerMove p = _mn.gameMN.pMove;
            Vector2 cursor = Input.mousePosition;
            if (world == null || p == null || !PointerGround(world, p, cursor, out Vector3 point))
            {
                return null;
            }
            InventoryManager inv = _mn.inventory;
            Collider[] hits = Physics.OverlapSphere(point, TabRadius, LayerMask.GetMask("BG"));
            InventorySlot best = null;
            float bestArea = float.MaxValue;
            bool bestNear = false;
            for (int i = 0; i < hits.Length; i++)
            {
                InventorySlot store = hits[i].GetComponent<InventorySlot>();
                if (store == null || !ContainerPolicy.TakesDrop(store) || ReachRule.Of(_mn, store) <= 0f
                    || (VanillaContainerUI.MirrorsLive(inv) && ReferenceEquals(inv.tmpSubInventory, store))
                    || !ScreenShape(world, store.gameObject, out Rect shape) || !shape.Contains(cursor))
                {
                    continue;
                }
                bool near = ReachRule.Live(_mn, store);
                float area = shape.width * shape.height;
                if ((near && !bestNear) || (near == bestNear && area < bestArea))
                {
                    best = store;
                    bestArea = area;
                    bestNear = near;
                }
            }
            return best;
        }

        // The ground the pointer's ray meets: the ray a click casts, else the plane
        // you stand on.
        private static bool PointerGround(Camera world, PlayerMove p, Vector2 cursor, out Vector3 point)
        {
            Ray ray = world.ScreenPointToRay(cursor);
            if (Physics.Raycast(ray, out RaycastHit hit, 200f, p.movableLayer))
            {
                point = hit.point;
                return true;
            }
            var ground = new Plane(Vector3.up, p.transform.position);
            if (ground.Raycast(ray, out float along))
            {
                point = ray.GetPoint(along);
                return true;
            }
            point = Vector3.zero;
            return false;
        }

        // Harness door: the nearest store a drop would fill, and what it holds. With
        // an item in hand and `room` of 0 or more, first fills its every empty cell
        // with that item - all to the full but the last, which keeps `room` free - so
        // the "does not fit" paths can be played. Nothing saves this; relaunch after.
        internal static string HarnessStore(int room)
        {
            PlayerMove p = _mn != null && _mn.gameMN != null ? _mn.gameMN.pMove : null;
            if (p == null)
            {
                return "no game";
            }
            InventorySlot best = null;
            float bestD = float.MaxValue;
            foreach (Collider hit in Physics.OverlapSphere(p.transform.position, ReachRule.Widest(_mn), LayerMask.GetMask("BG")))
            {
                InventorySlot c = hit.GetComponent<InventorySlot>();
                float d = c != null ? (c.transform.position - p.transform.position).sqrMagnitude : float.MaxValue;
                if (c != null && d < bestD && ContainerPolicy.TakesDrop(c) && ReachRule.Live(_mn, c))
                {
                    best = c;
                    bestD = d;
                }
            }
            if (best == null)
            {
                return "no store in reach";
            }
            InventoryManager inv = _mn.inventory;
            ItemData data = HandEmpty() ? null : _mn.itemMN.FindItem(inv.hoverSlot.itemKey);
            if (room >= 0 && data != null)
            {
                ItemSlot last = null;
                foreach (ItemSlot slot in best.slots)
                {
                    if (string.IsNullOrEmpty(slot.itemKey))
                    {
                        inv.SlotLoadSlot(inv.hoverSlot, slot);
                        slot.stack = data.maxStack;
                        last = slot;
                    }
                }
                if (last != null)
                {
                    last.stack = data.maxStack - room;
                }
            }
            var sb = new System.Text.StringBuilder(best.name + " at " + Mathf.Sqrt(bestD).ToString("0.0") + " m:");
            foreach (ItemSlot slot in best.slots)
            {
                sb.Append(' ').Append(string.IsNullOrEmpty(slot.itemKey) ? "-" : slot.itemKey + " x" + slot.stack);
            }
            return sb.ToString();
        }
    }
}
