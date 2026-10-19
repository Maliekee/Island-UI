using System.Collections.Generic;

namespace IslandUI
{
    // InventoryWindow — Ctrl+click: the item skips the hand.
    internal static partial class InventoryWindow
    {
        // CTRL+CLICK MOVES: as shift+click is the convention for splitting and
        // alt+click for favouriting, ctrl+click is the convention for moving
        // stuff. The item goes to the
        // one other place on screen that could take it:
        //
        //   from your two bags   into the target's bag or chest while its window
        //                        is up; else into the vicinity's storage while that
        //                        window is up (a chest already holding it first,
        //                        then the nearest with room - All in's rule, for one
        //                        stack); else, with no target at all, onto the
        //                        ground at your feet, as a drag out onto the world
        //   from the target's bag or chest, or a vicinity container
        //                        into your two bags (All out's rule: like with like
        //                        first, the Shared Bag's empty cells before your
        //                        bag's)
        //
        // Chests, material storage and people's bags only (ContainerPolicy's
        // Materials, Chests and Bags). A cage, a planter, a hive or a bench strip
        // keeps its own rules - vanilla's planter takes one seed a cell - and a
        // loadout is worn, not stored, so Ctrl does nothing there. A target up with
        // no bag on screen (a person on their Profile) takes nothing and drops
        // nothing: the ground is only for when nothing else is there. The exact
        // rules are still open; this is the first cut, to be played with.
        private sealed class MoveRoute
        {
            internal string Name;          // for the badge
            internal InventorySlot Into;   // the target's bag or chest
            internal bool ToYours;
            internal bool ToReach;
            internal bool Drop;
        }

        private static readonly MoveRoute ToYoursRoute = new MoveRoute { Name = "your bags", ToYours = true };
        private static readonly MoveRoute ToReachRoute = new MoveRoute { Name = "nearby storage", ToReach = true };
        private static readonly MoveRoute DropRoute = new MoveRoute { Name = "drop", Drop = true };
        // asked every hovered frame while Ctrl is down, so one instance is reused
        private static readonly MoveRoute IntoRoute = new MoveRoute();

        private static bool CtrlMoves(InventorySlot c)
        {
            if (c == null || c.slots == null)
            {
                return false;
            }
            ContainerPolicy.Family f = ContainerPolicy.FamilyOf(c);
            return f == ContainerPolicy.Family.Materials || f == ContainerPolicy.Family.Chests
                || f == ContainerPolicy.Family.Bags || f == ContainerPolicy.Family.Bodies;
        }

        private static MoveRoute RouteFrom(Plate plate)
        {
            if (plate == null || plate.IsLoadout || _mn == null || _mn.inventory == null)
            {
                return null;
            }
            if (!WearsFavourites(plate))
            {
                return (plate.Column == CharacterColumn || plate.Column == ReachColumn)
                    && CtrlMoves(plate.Container) ? ToYoursRoute : null;
            }
            Plate target = TargetStore();
            if (target != null)
            {
                IntoRoute.Into = target.Container;
                IntoRoute.Name = StoreName(target.Container);
                return IntoRoute;
            }
            if (_reachOpen && _slotView == ViewReach)
            {
                // a list of racks, benches and bags has nothing Ctrl may fill, and
                // the badge may not promise what the click will not do
                return HasReachStore() ? ToReachRoute : null;
            }
            return Focus() == null ? DropRoute : null;
        }

        private static readonly List<InventorySlot> ReachStores = new List<InventorySlot>();

        // The vicinity's plain storage, nearest first (All in's destinations).
        private static List<InventorySlot> ReachStoresNow()
        {
            ReachStores.Clear();
            ScanReach(ReachStores);
            ReachStores.RemoveAll(c => c == null || c.slots == null || !ContainerPolicy.BulkTarget(c));
            return ReachStores;
        }

        private static bool HasReachStore()
        {
            return ReachStoresNow().Count > 0;
        }

        // ClickMap's word for the route: 0 nowhere, 1 somewhere, 2 the ground.
        private static int MoveKind(Plate plate)
        {
            if (!ClickMap.Ctrl)
            {
                return 0;   // the route is only worth asking while Ctrl is down
            }
            MoveRoute route = RouteFrom(plate);
            return route == null ? 0 : route.Drop ? 2 : 1;
        }

        // The target's bag or chest, while its window shows one Ctrl may fill.
        private static Plate TargetStore()
        {
            for (int p = 0; p < _platesUsed; p++)
            {
                Plate plate = Plates[p];
                if (plate.Column == CharacterColumn && !plate.IsLoadout && CtrlMoves(plate.Container)
                    && plate.Window.Go.activeInHierarchy)
                {
                    return plate;
                }
            }
            return null;
        }

        private static string StoreName(InventorySlot c)
        {
            CommonStates owner = BagOwner(c);
            if (owner != null && ContainerPolicy.IsBodyBag(c))
            {
                return string.IsNullOrEmpty(owner.charaName) ? "the body's pockets" : owner.charaName + "'s pockets";
            }
            if (owner != null)
            {
                return string.IsNullOrEmpty(owner.charaName) ? "their bag" : owner.charaName + "'s bag";
            }
            return ChestTitle(c);
        }

        // The click, on release. The route is asked again rather than carried from
        // the badge: what is on screen can change while the button is down.
        private static void CtrlMove(CellView cell)
        {
            ItemSlot slot = cell != null ? cell.Slot : null;
            InventoryManager inv = _mn != null ? _mn.inventory : null;
            Plate plate = PlateOf(cell);
            MoveRoute route = RouteFrom(plate);
            if (inv == null || route == null || slot == null || string.IsNullOrEmpty(slot.itemKey)
                || !HandEmpty() || _mn.gameMN == null || _mn.gameMN.pMove == null)
            {
                return;
            }
            // a favourite stays in your bags until unmarked (FavouriteRules)
            if (Favourites.IsFavourite(slot) && !route.ToYours)
            {
                FavouriteRules.Refuse();
                return;
            }
            string key = slot.itemKey;
            int count = slot.stack;
            if (route.Drop)
            {
                // exactly a drag let go over the world: into the hand, then out
                inv.SlotToSlot(slot, inv.hoverSlot);
                ReportWrite(plate, slot);
                inv.ItemDrop(inv.hoverSlot);
                NormalizeHover();
                Plugin.Log.LogInfo($"Ctrl+click: dropped {key} x{count}");
                AfterCtrlMove();
                return;
            }
            NPCManager npcs = _mn.npcMN;
            int moved = 0;
            if (route.ToYours)
            {
                // vanilla's gate on taking an oversized carry item (QuickMove);
                // caution 16 is its "cannot carry any more"
                if (npcs != null && npcs.CheckCarrySize(key) && !npcs.CarriableCheck())
                {
                    Refuse(16);
                    return;
                }
                CommonStates you = _mn.gameMN.pMove.common;
                ItemSlot[] yours = you != null && you.bag != null ? you.bag.slots : null;
                moved = MoveStackOut(inv, slot, yours, out _);
            }
            else if (route.Into != null)
            {
                // vanilla's gate on a person's bag: no oversized carry items
                // (NPCToNPCCheck, caution 30)
                if (ContainerPolicy.IsCharacterBag(route.Into) && npcs != null && npcs.CheckCarrySize(key))
                {
                    Refuse(30);
                    return;
                }
                moved = MoveStack(inv, slot, route.Into.slots, route.Into.slots.Length, false, out _);
            }
            else if (route.ToReach)
            {
                List<InventorySlot> targets = ReachStoresNow();
                for (int c = 0; c < targets.Count && !string.IsNullOrEmpty(slot.itemKey); c++)
                {
                    moved += MoveStack(inv, slot, targets[c].slots, targets[c].slots.Length, true, out _);
                }
                for (int c = 0; c < targets.Count && !string.IsNullOrEmpty(slot.itemKey); c++)
                {
                    moved += MoveStack(inv, slot, targets[c].slots, targets[c].slots.Length, false, out _);
                }
            }
            if (moved == 0)
            {
                // vanilla's two "no room"s: 2 "the inventory is full" for yours,
                // 18 "cannot store any more" for a bag or chest
                Refuse(route.ToYours ? 2 : 18);
                return;
            }
            ReportWrite(plate, slot);
            Plugin.Log.LogInfo($"Ctrl+click: {key} x{moved} of {count} to {route.Name}");
            AfterCtrlMove();
        }

        // Refused rather than done, and it says so: a silent no-op reads as a bug.
        private static void Refuse(int caution)
        {
            if (_mn.eventMN != null)
            {
                _mn.eventMN.StartCoroutine(_mn.eventMN.GoCaution(caution));
            }
        }

        // Harness doors, since /ui/click cannot hold Ctrl. CtrlRoutes: where the
        // first item of every plate on screen would go. CtrlMoveProbe: the click
        // itself, on the first item in a plate of `column` (0 yours, 1 the target,
        // 2 the vicinity) whose key contains `key`.
        internal static string CtrlRoutes()
        {
            var sb = new System.Text.StringBuilder();
            foreach (Plate plate in ShownPlates())
            {
                for (int c = 0; c < plate.Shown && c < plate.Cells.Count; c++)
                {
                    ItemSlot slot = plate.Cells[c].Slot;
                    if (slot != null && !string.IsNullOrEmpty(slot.itemKey))
                    {
                        MoveRoute route = RouteFrom(plate);
                        sb.Append(plate.Column).Append(':').Append(slot.itemKey).Append(" -> ")
                            .Append(route != null ? route.Name : "nothing").Append("; ");
                        break;
                    }
                }
            }
            return sb.ToString();
        }

        internal static string CtrlMoveProbe(int column, string key)
        {
            foreach (Plate plate in ShownPlates())
            {
                if (plate.Column != column)
                {
                    continue;
                }
                for (int c = 0; c < plate.Shown && c < plate.Cells.Count; c++)
                {
                    ItemSlot slot = plate.Cells[c].Slot;
                    if (slot != null && !string.IsNullOrEmpty(slot.itemKey) && slot.itemKey.Contains(key))
                    {
                        string was = slot.itemKey + " x" + slot.stack;
                        MoveRoute route = RouteFrom(plate);
                        CtrlMove(plate.Cells[c]);
                        return was + " -> " + (route != null ? route.Name : "nothing")
                            + "; left in the cell: " + (string.IsNullOrEmpty(slot.itemKey) ? "empty" : slot.itemKey + " x" + slot.stack);
                    }
                }
            }
            return "no " + key + " in column " + column;
        }

        private static IEnumerable<Plate> ShownPlates()
        {
            for (int p = 0; p < _platesUsed; p++)
            {
                if (Plates[p].Window.Go.activeInHierarchy) { yield return Plates[p]; }
            }
            for (int p = 0; p < _reachPlatesUsed; p++)
            {
                if (ReachPlates[p].Window.Go.activeInHierarchy) { yield return ReachPlates[p]; }
            }
        }

        private static void AfterCtrlMove()
        {
            InventoryManager inv = _mn.inventory;
            for (int i = 0; i < inv.baseCount && i < inv.itemSlot.Length; i++)
            {
                inv.StackTextChange(i);
            }
            if (_mn.npcMN != null)
            {
                _mn.npcMN.CarryWeightCheck();
            }
            _forceRefresh = true;
            _nextRefresh = 0f;
            Refresh();
        }
    }
}
