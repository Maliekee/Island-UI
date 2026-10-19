using System.Collections.Generic;
using IslandUI.UI;
using UnityEngine;

namespace IslandUI
{
    // THE ACT ROW'S SECOND HOME (D105/D112: one act row component, used in both
    // places for consistency). A thing's window - a chest, a well, a signboard - ends in the same band the
    // unit ends in: the ActRow, bound as the LOWEST plate of the thing's group so it
    // sits at the foot under the grid or the reading, holding what THIS thing lets
    // you do - Drink, Eat, Read - and nothing else: Move · Remove · Repair are every
    // building's and stay on the rim. Read off vanilla
    // (PropVerbs) and pressed through it (D65); absent when the thing has no act.
    internal static partial class InventoryWindow
    {
        private static readonly List<Plate> _actRowPlates = new List<Plate>();

        private static void BindActRow(int group, int column, ItemInfo prop, int maxCols)
        {
            // only what THIS thing lets you do: the
            // placement acts are every building's and live on the rim, so a thing
            // with no act of its own - a chest - has no row at all
            if (_mn == null || prop == null)
            {
                return;
            }
            PropVerbs.Read(_mn, prop);
            // D156: with the unit up the thing's own acts and Craft are the dock's,
            // and the window's foot keeps only what is about its grid
            bool docked = Settings.TheUnit.Value;
            if (docked ? !HasContainerActs(prop)
                : PropVerbs.Current.Count == 0 && !HasContainerActs(prop) && BenchPanel(prop) == 0)
            {
                return;
            }
            Plate plate = TakeActPlate(group, column, maxCols, out float width);
            plate.Prop = prop;
            plate.ActBag = null;
            FillPropRow(plate.Acts, prop);
            LayoutActPlate(plate, column, width);
        }

        // A PERSON'S BAG GETS THE CHEST'S THREE (the vicinity window's three
        // buttons, for looting a killed NPC). The same row, at the foot of the bag
        // window, moving against the bag the way ContainerAct moves against a
        // chest: All out empties a corpse into your two bags, Shared Bag first and
        // your own when that is full.
        private static void BindBagActRow(int group, int column, InventorySlot bag, int maxCols)
        {
            if (_mn == null || bag == null || bag.slots == null || bag.slots.Length == 0
                || !ContainerPolicy.BulkSafe(bag))
            {
                return;
            }
            Plate plate = TakeActPlate(group, column, maxCols, out float width);
            plate.Prop = null;
            plate.ActBag = bag;
            FillBagRow(plate.Acts, bag);
            LayoutActPlate(plate, column, width);
        }

        private static Plate TakeActPlate(int group, int column, int maxCols, out float width)
        {
            Plate plate = TakePlate();
            plate.Column = column;
            plate.Owner = null;
            plate.Prop = null;
            plate.ActBag = null;
            plate.Container = null;
            plate.IsLoadout = false;
            plate.IsSharedBag = false;
            plate.IsSheet = false;
            plate.IsHeader = false;
            plate.IsCard = false;
            bool grouped = group != GroupNone;
            plate.Group = grouped ? group : GroupNone;
            plate.IsGroupFrame = false;
            plate.IsTitle = false;
            _lastGroupBound = plate.Group;
            _prevMemberLoadout = false;
            plate.Shown = 0;
            plate.Cols = 0;
            plate.LaidOutCols = -1;
            HideCells(plate);
            for (int i = 0; i < plate.TypeIcons.Count; i++)
            {
                if (plate.TypeIcons[i].gameObject.activeSelf)
                {
                    plate.TypeIcons[i].gameObject.SetActive(false);
                }
            }
            if (plate.Header.gameObject.activeSelf)
            {
                plate.Header.gameObject.SetActive(false);
            }
            for (int i = 0; plate.Rows != null && i < plate.Rows.Count; i++)
            {
                if (plate.Rows[i].Go.activeSelf) { plate.Rows[i].Go.SetActive(false); }
            }
            plate.RowsShown = 0;
            plate.Back.enabled = false;
            plate.Edge.enabled = false;

            // the sheet's own width rule, so the row is cut to what the grid set
            width = _inventoryW[group] > 0f
                ? _inventoryW[group]
                : SheetFallbackWidth(null, maxCols, column == OwnColumn, full: true);
            if (plate.Acts == null)
            {
                plate.Acts = ActRow.Create(plate.Window.Content, "Acts");
                plate.Acts.Compact = true;
            }
            plate.Acts.Show(true);
            plate.Acts.Shed = IsCrate(group);
            return plate;
        }

        private static void LayoutActPlate(Plate plate, int column, float width)
        {
            plate.Acts.Layout(0f, 0f, width);
            plate.Height = plate.Acts.Height;
            plate.Window.Rect.sizeDelta = new Vector2(width, plate.Height);
            Theme.Place(plate.Window.Content, 0f, 0f, width, plate.Height);
            plate.LaidOutW = width;
            plate.LaidOutH = plate.Height;
            _lastColumnWidth[column] = Mathf.Max(_lastColumnWidth[column], width);
            if (!plate.Window.Go.activeSelf)
            {
                plate.Window.Go.SetActive(true);
            }
            _actRowPlates.Add(plate);
        }

        private static void FillPropRow(ActRow row, ItemInfo prop)
        {
            row.Clear();
            if (_mn == null || prop == null)
            {
                return;
            }
            int next = 0;
            row.RightCount = HasContainerActs(prop) ? 3 : 0;
            // A CONTAINER'S OWN THREE (all in, stack and all out appear as actions).
            // Vanilla's buttons cannot be pressed through: an
            // intercepted container's mirror slots are never loaded, so
            // MoveAllItemsToMain walks an empty mirror and does nothing. The acts
            // are done on the REAL slots instead, with the same moves QuickStack
            // makes, and vanilla's own words are still read off its buttons.
            if (HasContainerActs(prop))
            {
                SetContainerActs(row, prop.GetComponent<InventorySlot>());
            }
            if (Settings.TheUnit.Value)
            {
                return;   // D156: the rest is on the dock
            }
            int panelID = BenchPanel(prop);
            if (panelID > 0)
            {
                row.Set(next++, new ActRow.Slot
                {
                    Name = "craft", Label = "Craft",
                    Press = () => OpenBenchCraft(prop),
                });
            }
            PropVerbs.Read(_mn, prop);
            IList<PropVerbs.Act> live = PropVerbs.Current;
            for (int i = 0; i < live.Count; i++)
            {
                next = row.NextFree(next);
                if (next < 0) { break; }
                int state = live[i].State;
                row.Set(next, new ActRow.Slot
                {
                    Name = live[i].Name ?? ("act" + state),
                    Label = live[i].Label,
                    Press = () => PressPropAct(prop, state),
                });
            }
        }

        private static readonly string[] ContainerActs = { "AllInButton", "StackButton", "AlloutButton" };

        // The three on the row's right, worded off vanilla's own buttons.
        private static void SetContainerActs(ActRow row, InventorySlot chest)
        {
            Transform panel = _mn.inventory.subInventory != null ? _mn.inventory.subInventory.transform : null;
            string[] fallback = { "All in", "Stack", "All out" };
            for (int i = 0; i < ContainerActs.Length; i++)
            {
                Transform t = panel != null ? panel.Find(ContainerActs[i]) : null;
                var text = t != null ? t.GetComponentInChildren<TMPro.TextMeshProUGUI>(true) : null;
                int which = i;
                row.Set(ActRow.Sockets - 3 + i, new ActRow.Slot
                {
                    Name = ContainerActs[i],
                    Label = text != null && !string.IsNullOrEmpty(text.text) ? text.text : fallback[i],
                    Press = () => ContainerAct(chest, which),
                });
            }
        }

        private static void FillBagRow(ActRow row, InventorySlot bag)
        {
            row.Clear();
            if (_mn == null || bag == null)
            {
                return;
            }
            row.RightCount = 3;
            SetContainerActs(row, bag);
        }

        // Whose window is this the foot of: a chest's, or a bag's whose owner is
        // the target. The row moves only against what is in focus.
        private static bool ActsOnFocus(InventorySlot chest)
        {
            object focus = Focus();
            if (ReferenceEquals(focus, chest))
            {
                return true;
            }
            return focus is CommonStates who && ReferenceEquals(who.bag, chest);
        }

        private static int BenchPanel(ItemInfo prop)
        {
            InventorySlot slot = prop != null ? prop.GetComponent<InventorySlot>() : null;
            return slot != null && slot.type == InventorySlot.Type.Bench && !ContainerPolicy.IsWrecked(slot)
                ? AggregatedCrafting.PanelOf(prop.itemKey) : 0;
        }

        private static void OpenBenchCraft(ItemInfo prop)
        {
            int panel = BenchPanel(prop);
            if (_mn == null || panel == 0 || !ReferenceEquals(FocusProp(Focus()), prop)) { return; }
            if (!_mn.uiMN.craftPanel.activeSelf) { _mn.uiMN.LeftPanelState(1); }
            _mn.inventory.CraftOpen(panel);
        }

        // The three, on the real slots. Vanilla's "main" is the Shared Bag -
        // itemSlot[0..baseCount) - and its buttons move against that alone; ours
        // move against BOTH of your bags, as the quick-stack sweep does. All in empties the Shared Bag and
        // then your bag into the chest, Stack sends only what the chest already
        // holds, All out empties the chest into them - like with like first, then
        // the Shared Bag's empty cells before your bag's. Nothing moves while an
        // item is held on the cursor, as vanilla refuses too.
        private static void ContainerAct(InventorySlot chest, int which)
        {
            InventoryManager inv = _mn != null ? _mn.inventory : null;
            if (inv == null || chest == null || chest.slots == null || !ActsOnFocus(chest)
                || (inv.hoverSlot != null && inv.hoverSlot.gameObject.activeSelf))
            {
                return;
            }
            CommonStates you = _mn.gameMN != null && _mn.gameMN.pMove != null ? _mn.gameMN.pMove.common : null;
            ItemSlot[] yours = you != null && you.bag != null ? you.bag.slots : null;
            int moved = 0;
            // the same flight the quick-stack sweep flies: one notice per item
            // per direction, from the Shared Bag's cell to the chest, or back
            var log = new List<QuickStack.Deposit>();
            if (which == 2)
            {
                for (int i = 0; i < chest.slots.Length; i++)
                {
                    string key = chest.slots[i] != null ? chest.slots[i].itemKey : null;
                    // the chest's cell, before the stack leaves it: afterwards no
                    // cell in the chest holds the item and the flight would start
                    // from the chest in the world instead
                    ItemData before = !string.IsNullOrEmpty(key) && _mn.itemMN != null ? _mn.itemMN.FindItem(key) : null;
                    RectTransform cell = CellFor(chest, before);
                    bool hasCell = cell != null;
                    Vector2 cellScreen = hasCell ? ItemFlight.ScreenPointOf(cell) : Vector2.zero;
                    int n = MoveStackOut(inv, chest.slots[i], yours, out ItemData data);
                    if (n > 0 && QuickStackFlight.SourceOf(_mn, key, out Vector2 landing))
                    {
                        NoteFlight(log, data, chest, n, landing, reverse: true, hasCell, cellScreen);
                    }
                    moved += n;
                }
            }
            else
            {
                var sources = new List<ItemSlot>();
                for (int i = 0; i < inv.baseCount && i < inv.itemSlot.Length; i++)
                {
                    sources.Add(inv.itemSlot[i]);
                }
                if (yours != null)
                {
                    sources.AddRange(yours);
                }
                bool intoBag = ContainerPolicy.IsCharacterBag(chest);
                for (int i = 0; i < sources.Count; i++)
                {
                    string key = sources[i] != null ? sources[i].itemKey : null;
                    // a favourite stays in the bag, whichever of the two moves,
                    // and so does a tool on the tool belt
                    if (Favourites.IsFavourite(sources[i]) || ToolBelt.IsHung(sources[i]))
                    {
                        continue;
                    }
                    // vanilla's gate on a person's bag: no oversized carry items
                    // (NPCToNPCCheck, caution 30) - skipped here, not refused
                    if (intoBag && !string.IsNullOrEmpty(key) && _mn.npcMN != null && _mn.npcMN.CheckCarrySize(key))
                    {
                        continue;
                    }
                    Vector2 from = Vector2.zero;
                    bool seen = !string.IsNullOrEmpty(key) && QuickStackFlight.SourceOf(_mn, key, out from);
                    ItemData data;
                    int n = MoveStack(inv, sources[i], chest.slots, chest.slots.Length, which == 1, out data);
                    if (n > 0 && seen)
                    {
                        NoteFlight(log, data, chest, n, from, reverse: false);
                    }
                    moved += n;
                }
            }
            if (moved > 0)
            {
                if (_mn.sound != null) { _mn.sound.GoSound(195, _mn.inventory.transform.position); }
                _forceRefresh = true;
                _nextRefresh = 0f;
                QuickStackFlight.Show(_mn, log);
            }
        }

        private static void NoteFlight(List<QuickStack.Deposit> log, ItemData item, InventorySlot storage,
            int count, Vector2 screen, bool reverse, bool hasStorageScreen = false, Vector2 storageScreen = default(Vector2))
        {
            if (item == null || count <= 0)
            {
                return;
            }
            for (int i = 0; i < log.Count; i++)
            {
                if (log[i].Item == item && log[i].Reverse == reverse)
                {
                    log[i].Count += count;
                    return;
                }
            }
            log.Add(new QuickStack.Deposit
            {
                Item = item, Storage = storage, Count = count, FromScreen = screen, Reverse = reverse,
                HasStorageScreen = hasStorageScreen, StorageScreen = storageScreen,
            });
        }

        // One of the chest's stacks out into your two bags: first into whichever
        // already holds the item (MoveStack's onlyExisting), Shared Bag first,
        // then into an empty cell, again the Shared Bag's before your bag's.
        private static int MoveStackOut(InventoryManager inv, ItemSlot from, ItemSlot[] yours, out ItemData data)
        {
            int n = MoveStack(inv, from, inv.itemSlot, inv.baseCount, true, out data);
            ItemData d;
            if (yours != null) { n += MoveStack(inv, from, yours, yours.Length, true, out d); data = data ?? d; }
            n += MoveStack(inv, from, inv.itemSlot, inv.baseCount, false, out d); data = data ?? d;
            if (yours != null) { n += MoveStack(inv, from, yours, yours.Length, false, out d); data = data ?? d; }
            return n;
        }

        // One stack into a run of slots: top up the stacks of the same item first,
        // then an empty slot - or only the former when `onlyExisting`. The same
        // two moves QuickStack makes, on the same real slots.
        private static int MoveStack(InventoryManager inv, ItemSlot from, ItemSlot[] into, int count,
            bool onlyExisting, out ItemData data)
        {
            data = null;
            if (from == null || string.IsNullOrEmpty(from.itemKey) || from.stack <= 0 || into == null)
            {
                return 0;
            }
            data = _mn.itemMN != null ? _mn.itemMN.FindItem(from.itemKey) : null;
            if (data == null)
            {
                return 0;
            }
            int moved = 0;
            bool present = false;
            for (int j = 0; j < count && j < into.Length; j++)
            {
                ItemSlot to = into[j];
                if (to == null || to.itemKey != from.itemKey)
                {
                    continue;
                }
                present = true;
                if (to.stack >= data.maxStack)
                {
                    continue;
                }
                int amount = Mathf.Min(data.maxStack - to.stack, from.stack);
                to.stack += amount;
                from.stack -= amount;
                moved += amount;
                if (from.stack <= 0)
                {
                    inv.SlotClear(from);
                    return moved;
                }
            }
            if (onlyExisting && !present)
            {
                return moved;
            }
            for (int j = 0; j < count && j < into.Length; j++)
            {
                ItemSlot to = into[j];
                if (to == null || !string.IsNullOrEmpty(to.itemKey))
                {
                    continue;
                }
                int amount = from.stack;
                inv.SlotLoadSlot(from, to);
                inv.SlotClear(from);
                return moved + amount;
            }
            return moved;
        }

        // Is vanilla's sub-inventory loaded for this prop's container? Only then do
        // its three buttons act on it - MoveAllItemsToMain checks the same thing.
        // ...or, with the unit up (D156), is it plain storage: the three are ours
        // on the real slots (ContainerAct), so a chest kept past vanilla's end
        // (D153) keeps them after vanilla has unloaded it.
        private static bool HasContainerActs(ItemInfo prop)
        {
            InventoryManager inv = _mn != null ? _mn.inventory : null;
            if (inv == null || inv.subInventory == null || prop == null)
            {
                return false;
            }
            // bulk moves, so storage only: the poultry farm's grid looks like a
            // chest's and is not one
            InventorySlot chest = prop.GetComponent<InventorySlot>();
            if (chest == null || !ContainerPolicy.BulkSafe(chest))
            {
                return false;
            }
            if (Settings.TheUnit.Value && VanillaContainerUI.IsPlainStorage(chest))
            {
                return true;
            }
            return inv.tmpSubInventory != null && inv.subInventory.activeSelf
                && ReferenceEquals(inv.tmpSubInventory.GetComponent<ItemInfo>(), prop);
        }

        // Pressed through vanilla's own button (D65), re-read first: the panel's
        // contents belong to whatever vanilla last opened, and that can change
        // under us.
        private static void PressPropAct(ItemInfo prop, int state)
        {
            if (_mn == null || prop == null) { return; }
            PropVerbs.Read(_mn, prop);
            IList<PropVerbs.Act> live = PropVerbs.Current;
            for (int i = 0; i < live.Count; i++)
            {
                if (live[i].State != state) { continue; }
                // FactoryBreed's vanilla action searches mirror slots, which an
                // intercepted container never loads. Use the real slots and the
                // same single-pig drop route; never manufacture a Feed/Collect act.
                InventorySlot pen = prop.GetComponent<InventorySlot>();
                if (state == 7 && pen != null && pen.type == InventorySlot.Type.FactoryBreed
                    && VanillaContainerUI.IsPlainStorage(pen))
                {
                    if (!ReferenceEquals(Focus(), pen) || pen.slots == null) { return; }
                    for (int j = 0; j < pen.slots.Length; j++)
                    {
                        ItemSlot slot = pen.slots[j];
                        int npc = slot != null && slot.itemKey == "animal_pig_01" ? 55
                            : slot != null && slot.itemKey == "animal_pig_02" ? 64 : -1;
                        if (npc < 0) { continue; }
                        _mn.inventory.SlotClear(slot);
                        _mn.itemMN.GetDrop(_mn.npcMN.npcPrefab[npc].GetComponent<DropItem>());
                        _forceRefresh = true;
                        _nextRefresh = 0f;
                        break;
                    }
                }
                else { PropVerbs.Press(live[i]); }
                return;
            }
        }

        // Every frame the window is up: the rows answer the pointer and repaint.
        // Re-read on a cadence, since vanilla swaps Move · Remove for Repair on its
        // own clock and a press may have changed what is lit.
        private static float _actRowsReadAt;

        private static void PaintActRows()
        {
            bool reread = Time.unscaledTime - _actRowsReadAt > 0.25f;
            if (reread) { _actRowsReadAt = Time.unscaledTime; }
            for (int i = 0; i < _actRowPlates.Count; i++)
            {
                Plate plate = _actRowPlates[i];
                if (plate.Acts == null || !plate.Window.Go.activeSelf)
                {
                    continue;
                }
                if (reread)
                {
                    if (plate.ActBag != null) { FillBagRow(plate.Acts, plate.ActBag); }
                    else { FillPropRow(plate.Acts, plate.Prop); }
                    plate.Acts.Layout(0f, 0f, plate.Window.Rect.sizeDelta.x);
                    if (!Mathf.Approximately(plate.Height, plate.Acts.Height)) { _forceRefresh = true; }
                }
                plate.Acts.ReadInput();
                plate.Acts.Paint();
            }
        }
    }
}
