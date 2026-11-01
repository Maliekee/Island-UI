using IslandUI.UI;
using UnityEngine;

namespace IslandUI
{
    // FOOD DROPPED ON A FOOD BOX FILLS IT, as food dropped on a person feeds them
    // (InventoryWindow.Drop.cs): on the box in the world, on its marker or on its
    // card on the target dock. Shift gives up to ten, a box out of reach walks you
    // over, and the rest of the stack stays in hand.
    //
    // A right-click is yours (D141), and vanilla's own fill only runs while it has
    // the box open and you are held on it (ItemManager.UseFood). A box you target
    // from afar, or walked away from with the target kept, is neither, so a
    // right-click eats. The drop is the way that always works.
    //
    // Only what vanilla would put in: food with a food value, in a box of yours
    // that has room. Anything else over a box is not offered and does what it did.
    internal static partial class InventoryWindow
    {
        private static InventorySlot _dropBox;

        // True when this frame's release was a drop on a food box (the chain stops).
        // Runs after TickDropOnPerson, which has just cleared the offer ring.
        private static bool TickDropOnFoodBox(Camera cam)
        {
            _dropBox = null;
            if (_dropOver != null || HandEmpty() || _grabSource == null || _mn == null || _mn.gameMN == null
                || LiftedFromLoadout() || !HeldFills() || HitCell(cam) != null)
            {
                return false;
            }
            InventorySlot box = BoxCardUnderPointer();
            if (box == null && !OverUI())
            {
                box = OverBoxMarker() ? TargetBox() : BoxInWorld();
            }
            if (box == null)
            {
                return false;
            }
            _dropBox = box;
            bool full = BoxFull(box);
            bool near = ReachRule.Live(_mn, box);
            bool walk = !near && CanWalkToBox(box);
            int uses = ItemUse.UsesAsked(_mn, _mn.inventory.hoverSlot);
            if (!full && (near || walk))
            {
                GivePile.Want(_mn.itemMN.FindItem(_mn.inventory.hoverSlot.itemKey), uses);
            }
            // the target's own ring turns gold instead (PaintThingMarker)
            bool ring = !full && (near || walk) && !ReferenceEquals(box, TargetBox());
            PaintRing(ref _offerRing, ref _offerRingSprite, "IslandUI_OfferRing",
                ring ? box.transform : null, OfferGold, ThingRadius(box));
            string name = StoreName(box);
            CursorBadge.Show(full ? name + "  ·  " + Shared.Loc.Get("drop.box_full", "full")
                : near ? (uses > 1 ? Shared.Loc.Get("drop.give_many_to", "give {1} to  →  {0}", name, uses)
                    : Shared.Loc.Get("drop.give_to", "give to  →  {0}", name))
                : walk ? (uses > 1 ? Shared.Loc.Get("drop.walk_give_many_to", "walk over and give {1} to  →  {0}", name, uses)
                    : Shared.Loc.Get("drop.walk_give_to", "walk over and give to  →  {0}", name))
                : name + "  ·  " + Shared.Loc.Get("drop.out_of_reach", "out of reach"));
            if (!Input.GetMouseButtonUp(0))
            {
                return false;
            }
            // as with a person, a failed attempt keeps the item in hand
            if (full)
            {
                Refuse(18);   // vanilla's "cannot store any more"
            }
            else if (near)
            {
                FillBoxFromHand(box, uses);
            }
            else if (walk)
            {
                WalkThenFill(box, uses);
            }
            PaintOfferRing(null);
            SetPressed(_pressedCell, false);
            _pressedCell = null;
            _dragging = false;
            _cardPressed = CardPressNone;
            return true;
        }

        // Food with a food value: what UseFood's fill takes. A Bottle goes through
        // UseFood too.
        private static bool HeldFills()
        {
            return Fills(_mn.inventory != null ? _mn.inventory.hoverSlot : null);
        }

        private static bool Fills(ItemSlot slot)
        {
            ItemData data = slot != null && !string.IsNullOrEmpty(slot.itemKey) && _mn.itemMN != null
                ? _mn.itemMN.FindItem(slot.itemKey) : null;
            if (data == null || (data.itemType != ItemData.ItemType.Food && data.itemType != ItemData.ItemType.Bottle))
            {
                return false;
            }
            FoodInfo food = data.GetComponent<FoodInfo>();
            return food != null && food.food > 0f;
        }

        private static bool IsFoodBox(InventorySlot c)
        {
            return c != null && c.type == InventorySlot.Type.FoodStorage && c.slots != null && c.slots.Length > 0
                && ReachRule.Of(_mn, c) > 0f;
        }

        // Vanilla hardcodes it by item key (UseFood, FoodStorageUse): there is no field.
        internal static float FoodBoxMax(string key)
        {
            return key == "foodstorage_02" ? 2000f : 1000f;
        }

        private static bool BoxFull(InventorySlot box)
        {
            ItemInfo info = box.GetComponent<ItemInfo>();
            return box.slots[0].itemLife >= FoodBoxMax(info != null ? info.itemKey : null);
        }

        private static bool CanWalkToBox(InventorySlot box)
        {
            PlayerMove p = _mn.gameMN.pMove;
            return p != null && (box.transform.position - p.transform.position).sqrMagnitude <= WalkGiveMax * WalkGiveMax;
        }

        // The target, when it is a food box of yours.
        private static InventorySlot TargetBox()
        {
            InventorySlot box = _unitThing != null ? _unitThing.GetComponent<InventorySlot>() : null;
            return IsFoodBox(box) ? box : null;
        }

        // The marker's whole silhouette, as for a person (MarkerUnderPointer).
        private static bool OverBoxMarker()
        {
            return TargetBox() != null && _markerHit != null && _markerHit.gameObject.activeInHierarchy
                && RectTransformUtility.RectangleContainsScreenPoint(_markerHit.rectTransform, Input.mousePosition, null);
        }

        // Its card on the target dock, as a person's portrait. The star keeps its click.
        private static InventorySlot BoxCardUnderPointer()
        {
            if (_animeIdentity == null || !_animeIdentity.gameObject.activeInHierarchy || OnCardStar)
            {
                return null;
            }
            InventorySlot box = TargetBox();
            return box != null && RectTransformUtility.RectangleContainsScreenPoint(_animeIdentity.rectTransform, Input.mousePosition, null)
                ? box : null;
        }

        // THE BOX IN THE WORLD, picked as PersonInWorld picks: whose drawn shape the
        // pointer is ON, and the smallest where shapes overlap.
        private static InventorySlot BoxInWorld()
        {
            Camera world = Camera.main;
            PlayerMove p = _mn.gameMN.pMove;
            Vector2 cursor = Input.mousePosition;
            if (world == null || p == null || !PointedGround(world, p, cursor, out Vector3 point))
            {
                return null;
            }
            Collider[] hits = Physics.OverlapSphere(point, TabRadius, LayerMask.GetMask("BG", "BGNPC"));
            InventorySlot best = null;
            float bestArea = float.MaxValue;
            for (int i = 0; i < hits.Length; i++)
            {
                InventorySlot box = hits[i].GetComponent<InventorySlot>();
                if (!IsFoodBox(box) || !ScreenShape(world, box.gameObject, out Rect shape) || !shape.Contains(cursor))
                {
                    continue;
                }
                float area = shape.width * shape.height;
                if (area < bestArea)
                {
                    best = box;
                    bestArea = area;
                }
            }
            return best;
        }

        // For the marker and the ring: food in hand, and this box can take it from
        // where you can get to. They light up as the place to drop it (GiftOffered).
        private static bool BoxOffered(ItemInfo prop)
        {
            InventorySlot box = prop != null ? prop.GetComponent<InventorySlot>() : null;
            return _mn != null && IsFoodBox(box) && !HandEmpty() && _grabSource != null && !LiftedFromLoadout()
                && HeldFills() && !BoxFull(box) && (ReachRule.Live(_mn, box) || CanWalkToBox(box));
        }

        // A give's own steps (UseHeldOn, GiveFrom): the stack back in its cell, used
        // from there through the one dispatcher, and the rest back on the cursor.
        // UseFood is synchronous, so what left has left when the call returns.
        private static void FillBoxFromHand(InventorySlot box, int uses)
        {
            CellView source = _grabSource;
            if (PutBack(source, out bool mine))
            {
                FillBox(box, source, mine, uses, true);
            }
        }

        // `relift` puts the rest back on the cursor, for a stack that came from the
        // hand; a Ctrl+click leaves it in its cell.
        private static void FillBox(InventorySlot box, CellView source, bool mine, int uses, bool relift)
        {
            ItemSlot slot = source.Slot;
            string key = slot.itemKey;
            ItemData item = _mn.itemMN != null ? _mn.itemMN.FindItem(key) : null;   // before the stack goes
            int before = CountHeld(key, slot);
            bool fired = ItemUse.UseMany(_mn, slot, source.VanillaSlotId, use => ItemUse.IntoBox(_mn, box, use), mine, uses);
            int spent = before - CountHeld(key, slot);
            Plugin.Log.LogInfo($"Drop: {key} x{spent} into {box.name}");
            if (relift)
            {
                Relift(source, key);
            }
            if (spent > 0 && item != null)
            {
                FlyTo(_mn.uiMN, item, spent, box.gameObject);
            }
            Done(fired);
        }

        // Vanilla walks you there and a prop's click leaves the prop in pMove.target;
        // the give lands at reach (3 m), before its contact would open the box.
        private static void WalkThenFill(InventorySlot box, int uses)
        {
            GameObject aim = box.gameObject;
            _mn.gameMN.pMove.target = aim;
            _walkGive = new WalkGive
            {
                ToBox = true, Box = box, Key = _mn.inventory.hoverSlot.itemKey, Aim = aim,
                Until = Time.unscaledTime + WalkGiveSeconds, Uses = uses,
            };
            Plugin.Log.LogInfo("Drop: walking to " + box.name + " to give " + _walkGive.Key);
        }
    }
}
