using System.Collections.Generic;
using UnityEngine;

namespace IslandUI
{
    // CTRL+RIGHT-CLICK USES AN ITEM ON YOUR TARGET (D180): left moves, right uses,
    // and Ctrl sends it to the target. A plain right-click stays yours (D141), so a
    // use on someone else is only ever asked for. With no target Ctrl changes
    // nothing: a right-click is on you.
    //
    // From your two bags only. What it does is what a drop on the target would do
    // with what can be used: they eat it or wear it, a food box fills, a planter
    // plants, a pocket expands a chest. Anything else does nothing.
    //
    // CONSUMED OR TRANSFERRED is the line that matters. What is consumed is gone:
    // the icon flies, to show where, and a favourite may go. What is transferred is
    // still an item, somewhere you can see: worn by them or in a planter's cell. It
    // does not fly, and a favourite stays in your bags.
    internal static partial class InventoryWindow
    {
        private struct UseAim
        {
            internal CommonStates Who;
            internal InventorySlot Box;
            internal Plate Planter;
            internal InventorySlot Chest;
            internal string Name;
            internal int Uses;
        }

        private static bool HasUseTarget()
        {
            return Focus() is CommonStates || _unitThing != null;
        }

        // What this item would be used on, if the target can use it at all.
        private static bool AimUse(ItemSlot slot, out UseAim aim)
        {
            aim = default(UseAim);
            CommonStates who = Focus() as CommonStates;
            if (who != null)
            {
                if (!CanReceive(who) || !Usable(slot))
                {
                    return false;
                }
                aim.Who = who;
                aim.Name = Hostile(who) || string.IsNullOrEmpty(who.charaName) ? Shared.Loc.Get("drop.them", "them") : who.charaName;
                aim.Uses = IsGift(slot) ? ItemUse.UsesAsked(_mn, slot) : 1;
                return true;
            }
            InventorySlot box = TargetBox();
            if (box != null)
            {
                aim.Box = box;
                aim.Name = StoreName(box);
                aim.Uses = ItemUse.UsesAsked(_mn, slot);
                return Fills(slot);
            }
            Plate planter = TargetPlanter();
            if (planter != null)
            {
                aim.Planter = planter;
                aim.Name = StoreName(planter.Container);
                aim.Uses = ClickMap.Shift ? Mathf.Min(ItemUse.ManyUses, slot.stack) : 1;
                return slot.toolType == ItemData.ToolType.Plant;
            }
            if (_target is InventorySlot chest && slot.itemKey == "pocket_01")
            {
                aim.Chest = chest;
                aim.Name = StoreName(chest);
                aim.Uses = 1;
                return true;
            }
            return false;
        }

        // Is this item gone once used: eaten, drunk, in a food box's level, or the
        // pocket that expanded a chest. Worn or planted, it is still an item.
        private static bool Consumes(ItemSlot slot, UseAim aim)
        {
            return aim.Box != null || aim.Chest != null || (aim.Who != null && IsGift(slot));
        }

        // True when this right-click was Ctrl's: the target's, done or refused.
        private static bool TargetUse(Plate plate, CellView cell)
        {
            if (!ClickMap.Ctrl || !WearsFavourites(plate) || !HandEmpty() || !HasUseTarget())
            {
                return false;
            }
            ItemSlot slot = cell.Slot;
            if (!AimUse(slot, out UseAim aim))
            {
                return true;   // something the target has no use for does nothing
            }
            if (Favourites.IsFavourite(slot) && !Consumes(slot, aim))
            {
                FavouriteRules.Refuse();   // a favourite stays in your bags
            }
            else if (aim.Who != null)
            {
                GiveToPerson(aim.Who, cell, aim.Uses);
            }
            else if (aim.Box != null)
            {
                GiveToBox(aim.Box, cell, aim.Uses);
            }
            else if (aim.Planter != null)
            {
                CtrlMove(cell, aim.Uses);
            }
            else
            {
                Done(UsePocket(aim.Chest, slot, cell.VanillaSlotId, true));
            }
            return true;
        }

        // As a drop: in reach it goes from the cell, out of reach it is lifted and
        // you walk over with it on the cursor, a walk's only way to carry it.
        private static void GiveToPerson(CommonStates who, CellView cell, int uses)
        {
            if (InReach(who))
            {
                GiveFrom(who, cell, true, uses, false);
            }
            else if (CanWalkTo(who))
            {
                LeftClick(cell);
                WalkThenGive(who, uses);
            }
            else
            {
                Say(Shared.Loc.Get("drop.out_of_reach", "out of reach"));
            }
        }

        private static void GiveToBox(InventorySlot box, CellView cell, int uses)
        {
            if (BoxFull(box))
            {
                Refuse(18);   // vanilla's "cannot store any more"
            }
            else if (ReachRule.Live(_mn, box))
            {
                FillBox(box, cell, true, uses, false);
            }
            else if (CanWalkToBox(box))
            {
                LeftClick(cell);
                WalkThenFill(box, uses);
            }
            else
            {
                Say(Shared.Loc.Get("drop.out_of_reach", "out of reach"));
            }
        }

        // For the badge: what Ctrl+right-click would do here, or null.
        private static string TargetUseWords(Plate plate, ItemSlot slot)
        {
            if (!WearsFavourites(plate) || !HandEmpty() || !HasUseTarget() || !AimUse(slot, out UseAim aim)
                || (Favourites.IsFavourite(slot) && !Consumes(slot, aim)))
            {
                return null;
            }
            return aim.Uses > 1 ? Shared.Loc.Get("inv.hint.use_on_many", "ctrl-shift-right-click  →  give {1} to {0}", aim.Name, aim.Uses)
                : Shared.Loc.Get("inv.hint.use_on", "ctrl-right-click  →  give to {0}", aim.Name);
        }

        // What something dropped on a person is put in: their bag, when they are
        // yours and vanilla is not holding it open with its mirror live, whose panel
        // would write the old contents back on closing.
        private static ItemSlot[] BagOf(CommonStates who)
        {
            InventoryManager inv = _mn.inventory;
            if (who == null || who.bag == null || who.bag.slots == null || !Yours(who, _mn.gameMN.pMove)
                || (VanillaContainerUI.MirrorsLive(inv) && ReferenceEquals(inv.tmpSubInventory, who.bag)))
            {
                return null;
            }
            return who.bag.slots;
        }

        // The last step of a drop's order (D180): what they can neither eat nor wear
        // goes in their bag, and the rest stays in hand.
        private static void PutHeldInBag(CommonStates who, int count)
        {
            InventoryManager inv = _mn.inventory;
            ItemSlot hand = inv.hoverSlot;
            ItemSlot[] bag = BagOf(who);
            string key = hand.itemKey;
            if (bag == null)
            {
                return;
            }
            // vanilla's gate on a person's bag: no oversized carry items (caution 30)
            if (_mn.npcMN != null && _mn.npcMN.CheckCarrySize(key))
            {
                Refuse(30);
                return;
            }
            int moved = MoveSome(inv, hand, count, part => MoveStack(inv, part, bag, bag.Length, false, out _));
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
            Plugin.Log.LogInfo($"Drop: {key} x{moved} into {who.charaName}'s bag");
            AfterCtrlMove();
        }
    }
}
