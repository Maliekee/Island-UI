using UnityEngine;
using IslandUI.UI;

namespace IslandUI
{
    // AN ITEM DROPPED ON SOMEONE IS USED ON THEM (window-system.md 6.31, D141).
    // A plain use is yours; to feed, dress or treat someone you drop the item on
    // them - on their unit, their bag window, their sheet, or their roster strip
    // (the person in the world comes second). A cell under the
    // pointer is always asked first: a cell of their bag is a TRANSFER into it, as it
    // has always been, and only the rest of their window means "use it on them".
    //
    // Asked early in the Tick, above the sheets: a roster row and a sheet control
    // claim the pointer (TickSheets returns out of the input chain), so a drop there
    // used to be swallowed and the item stayed on the cursor.
    internal static partial class InventoryWindow
    {
        private static CommonStates _dropOver;

        // True when this frame's release was a drop on someone (the chain stops).
        private static bool TickDropOnPerson(Camera cam)
        {
            _dropOver = null;
            if (HandEmpty() || _grabSource == null || _mn == null || _mn.gameMN == null)
            {
                return false;
            }
            // a worn item lifted off the loadout is not an offer to anyone: a
            // loadout grab is remembered now, and this chain stays as
            // it was before it was
            if (LiftedFromLoadout())
            {
                return false;
            }
            // a cell first: dropping on a cell of their bag moves it into the bag
            if (HitCell(cam) != null)
            {
                return false;
            }
            CommonStates who = PersonUnderPointer(cam);
            InventorySlot chest = who == null ? ChestUnderPointer(cam) : null;
            if (who == null && chest == null)
            {
                return false;
            }
            _dropOver = who;
            string name = who != null
                ? (string.IsNullOrEmpty(who.charaName) ? "them" : who.charaName)
                : "this chest";
            // Using an item on someone is an act, and acts want reach (D145): the
            // roster lists people across the map, and a drop on a strip there used
            // to feed them from wherever you stood.
            bool near = who == null || InReach(who);
            CursorBadge.Show(near ? "use on  →  " + name : name + "  ·  out of reach");
            if (!Input.GetMouseButtonUp(0))
            {
                return false;
            }
            if (near)
            {
                UseHeldOn(who, chest);
            }
            else
            {
                LeftClick(_grabSource);   // back where it came from
            }
            SetPressed(_pressedCell, false);
            _pressedCell = null;
            _dragging = false;
            return true;
        }

        // Whose window is under the pointer: the unit, the bag window, a sheet, a
        // roster strip. Null over anything else.
        private static CommonStates PersonUnderPointer(Camera cam)
        {
            Vector2 mouse = Input.mousePosition;
            if (_unitRect != null && _unitFor != null && _unitRect.gameObject.activeInHierarchy
                && RectTransformUtility.RectangleContainsScreenPoint(_unitRect, mouse, null))
            {
                return _unitFor;
            }
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
            LeftClick(source);   // put the stack back in its cell
            if (!HandEmpty() || source.Slot == null || string.IsNullOrEmpty(source.Slot.itemKey))
            {
                return;   // it could not go back (the cell filled meanwhile): keep it in hand
            }
            Plate plate = PlateOf(source);
            CommonStates sourceOwner = plate != null ? plate.Owner : null;
            bool mine = sourceOwner == null || ReferenceEquals(sourceOwner, _mn.gameMN.pMove.common);
            bool done;
            if (who != null)
            {
                done = ItemUse.Use(_mn, source.Slot, source.VanillaSlotId, use => ItemUse.OnThem(_mn, who, use), mine);
                Plugin.Log.LogInfo("Drop: " + source.Slot.itemKey + " used on " + who.charaName + (done ? "" : " (refused)"));
                // ...and they are the target now: you are
                // dealing with them, and their unit says who you gave to
                if (!ReferenceEquals(Focus(), who))
                {
                    PressRosterRow(who);
                }
            }
            else
            {
                VanillaContainerUI.PocketDrop = chest;
                try
                {
                    done = ItemUse.Use(_mn, source.Slot, source.VanillaSlotId, use => ItemUse.OnYou(_mn, use), mine);
                }
                finally
                {
                    VanillaContainerUI.PocketDrop = null;
                }
            }
            if (!done && _mn.eventMN != null)
            {
                _mn.eventMN.StartCoroutine(_mn.eventMN.GoCaution(34));
            }
            _nextRefresh = 0f;
            Refresh();
        }
    }
}
