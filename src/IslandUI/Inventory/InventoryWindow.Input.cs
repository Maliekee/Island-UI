using System.Collections.Generic;
using IslandUI.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI
{
    // InventoryWindow — Pointer input: clicks, drag, badges, recipient highlight.
    internal static partial class InventoryWindow
    {
        // Hovering an equippable previews WHERE it would land: the loadout slot
        // it fills, outlined in the plate of the character that would actually
        // receive it. That character is not always you — vanilla's right-click
        // path equips onto menuNPC when an NPC menu is open (UseEquipItem), so
        // while you are talking to someone the item goes on THEM. Red means that
        // character cannot wear it and vanilla would refuse with caution 20.
        private static string ApplyEquipPreview(ItemSlot hovered, CellView hoveredCell)
        {
            for (int p = 0; p < _platesUsed; p++)
            {
                List<CellView> cells = Plates[p].Cells;
                for (int i = 0; i < cells.Count; i++)
                {
                    if (cells[i].Highlight.enabled)
                    {
                        cells[i].Highlight.enabled = false;
                    }
                }
            }
            ClearRecipientHighlight();
            // An item already ON a character reads the other way round: the
            // question is not where it would land but how to take it off. Answer
            // that instead of previewing an equip that has already happened.
            if (hoveredCell != null && hovered != null && !string.IsNullOrEmpty(hovered.itemKey))
            {
                Plate wornOn = PlateOf(hoveredCell);
                if (wornOn != null && wornOn.IsLoadout)
                {
                    ItemData worn = _mn.itemMN.FindItem(hovered.itemKey);
                    string wornName = worn != null ? worn.GetItemName() : hovered.itemKey;
                    string wearer = wornOn.Owner == null || _mn.gameMN.IsPlayer(wornOn.Owner)
                        ? "you"
                        : (string.IsNullOrEmpty(wornOn.Owner.charaName) ? "them" : wornOn.Owner.charaName);
                    // the mark's line, as a bag cell gives it, on your own loadout
                    if (AltHeld() && HandEmpty() && WearsFavourites(wornOn))
                    {
                        return Favourites.IsFavourite(hovered)
                            ? $"{wornName}   ·   worn by {wearer}   ·   favourite   ·   alt-click  →  unmark"
                            : $"{wornName}   ·   worn by {wearer}   ·   alt-click  →  favourite";
                    }
                    return $"{wornName}   ·   worn by {wearer}   ·   "
                        + "left-click  →  into your hand    right-click  →  Shared Bag";
                }
            }
            if (hovered == null || string.IsNullOrEmpty(hovered.itemKey))
            {
                return "";
            }
            ItemData item = _mn.itemMN.FindItem(hovered.itemKey);
            string name = item != null ? item.GetItemName() : hovered.itemKey;
            string label = hovered.stack > 1 ? $"{name} ×{hovered.stack}" : name;
            // the mark's gesture announces itself only while Alt is down — a line
            // on every hover would be noise, and a gesture nothing mentions is
            // a gesture nobody finds
            if (AltHeld() && HandEmpty() && WearsFavourites(PlateOf(hoveredCell)))
            {
                // no star GLYPH here: the game's font renders these as tofu boxes,
                // which is why the mark on the cell is a generated sprite
                return Favourites.IsFavourite(hovered)
                    ? $"{label}   ·   favourite   ·   alt-click  →  unmark"
                    : $"{label}   ·   alt-click  →  favourite (never quick-stacked)";
            }
            if (item == null || _mn.randChar == null)
            {
                return label;
            }
            if (IsCreature(hovered.toolType))
            {
                // the butcher key announced where it works, as alt-click is above
                string butcher = Butchers(hoveredCell)
                    ? $"   ·   {KitKeyBind.Name(KeyRemap.Bound(KeyCode.T))}  →  butcher" : "";
                return hovered.itemLife >= 100f
                    ? $"{label}   ·   happiness MAX — can join the tribe{butcher}"
                    : $"{label}   ·   happiness {hovered.itemLife:0}/100{butcher}";
            }
            // The right-click recipient USED to be predicted here, as vanilla's
            // `menuNPC ?? player`. D25 replaced that rule with the container's
            // holder, so the line began saying "right-click → Nyama" over an item
            // in your own bag that D25 feeds to YOU — a hint that is wrong is worse
            // than no hint, because it is believed. Removed rather than repaired:
            // the cursor badge answers the same question against the live rule, and
            // says it at the cursor where the click is about to happen.
            if (item.name == "pocket_01")
            {
                return label + "   ·   " + PocketPreview();
            }
            int equipId = _mn.randChar.ItemTypeToEquipID(item.itemType);
            if (equipId < 0)
            {
                return label;
            }
            // Who RightClick will hand it to, so the loadout plate highlighted
            // below is the one that will actually change.
            CommonStates target = ActorNow();
            if (target == null)
            {
                return label;
            }
            bool wearable = _mn.randChar.EquipableNPCCache(item.name).Contains(target.npcID);
            for (int p = 0; p < _platesUsed; p++)
            {
                Plate plate = Plates[p];
                if (!plate.IsLoadout || !ReferenceEquals(plate.Owner, target))
                {
                    continue;
                }
                for (int i = 0; i < LoadoutOrder.Length && i < plate.Cells.Count; i++)
                {
                    if (LoadoutOrder[i] != equipId)
                    {
                        continue;
                    }
                    plate.Cells[i].Highlight.effectColor =
                        wearable ? Theme.AccentGold : Theme.Negative;
                    plate.Cells[i].Highlight.enabled = true;
                }
            }
            string who = string.IsNullOrEmpty(target.charaName) ? "you" : target.charaName;
            string slotName = equipId < LoadoutNames.Length ? LoadoutNames[equipId] : "";
            return wearable
                ? $"{label}   →   {who} · {slotName}"
                : $"{label}   →   {who} cannot wear this";
        }

        // GRAB / PLACE, through vanilla's own held-item slot.
        //
        // inv.hoverSlot is the item that follows the cursor. Driving it - rather
        // than inventing our own held item - is what lets a grab from the Shared
        // Bag (which IS a vanilla panel) be placed into one of our plates: one
        // continuous gesture across both UIs. SlotToSlot is used deliberately
        // here, unlike in bulk moves, because it already implements merge, swap
        // and overflow against the hover slot and calls CarryWeightCheck.
        //
        // Caveat, deliberate for now: SlotToSlot records originSlotID from the
        // source slot, and container slots all carry slotID 0 (recon §7.1). So an
        // item grabbed from a container and then dropped onto the WORLD takes the
        // "return to slot 0" branch rather than a clean drop. Nothing is lost -
        // SlotToSlot swaps rather than overwrites - but it is the known rough edge
        // of this pass.
        private static void LeftClick(CellView cell)
        {
            ItemSlot slot = cell.Slot;
            if (slot == null)
            {
                return;
            }
            Plate plate = PlateOf(cell);
            if (plate != null && plate.IsLoadout)
            {
                LoadoutClick(plate, cell);
                return;
            }
            InventoryManager inv = _mn.inventory;
            bool holding = inv.hoverSlot.gameObject.activeSelf;

            // A left click is grab-or-place and nothing else. Splitting is the
            // split dialog's, opened by Shift+click (ClickMap), which replaced
            // both Ctrl+click's half-stack and the wheel's one-per-notch.
            if (!holding)
            {
                if (string.IsNullOrEmpty(slot.itemKey))
                {
                    return;
                }
                inv.SlotToSlot(slot, inv.hoverSlot);
                _grabSource = cell;
            }
            else
            {
                // a held favourite goes only where it stays yours (FavouriteRules)
                if (FavouriteRules.Leaves(inv, inv.hoverSlot, slot))
                {
                    FavouriteRules.Refuse();
                    HomeHeld();
                    return;
                }
                if (plate != null && ContainerPolicy.IsPlanter(plate.Container))
                {
                    if (!PlantOne(inv, slot))
                    {
                        return;
                    }
                }
                else
                {
                    inv.SlotToSlot(inv.hoverSlot, slot);
                }
            }
            // BOTH branches, and the grab is the one that shows: taking a captive
            // out of a cage leaves them standing in it until something detaches
            // the object under pos_NN, and that something is PetCheck.
            ReportWrite(plate, slot);
            NormalizeHover();
            _nextRefresh = 0f;
            Refresh();
        }

        // The Loadout takes the same grab/place gesture as the bags, but the
        // verb underneath is different. Equipping is visuals, derived stats and
        // EquipEffectCheck (recon §7.1) — an item moved slot-to-slot would be worn
        // without counting for anything, which is worse than not moving at all.
        // So the gesture is ours and the mechanism is the game's, exactly as
        // ItemSlot.SlotClick open-codes it for the vanilla equip strip.
        private static void LoadoutClick(Plate plate, CellView cell)
        {
            CommonStates owner = plate.Owner;
            if (owner == null || owner.equip == null || _mn.randChar == null)
            {
                return;
            }
            int index = plate.Cells.IndexOf(cell);
            if (index < 0 || index >= LoadoutOrder.Length)
            {
                return;
            }
            int equipId = LoadoutOrder[index];
            InventoryManager inv = _mn.inventory;

            if (!inv.hoverSlot.gameObject.activeSelf)
            {
                ItemSlot worn = cell.Slot;
                if (worn == null || string.IsNullOrEmpty(worn.itemKey))
                {
                    return;
                }
                // Unequip has no vanilla function; this is ItemSlot.cs:104 with the
                // UI copy dropped — our cell IS common.equip[i], so SlotToSlot
                // moves it into the hand and clears the real entry in one step.
                _mn.randChar.UnEquip(owner, worn);
                inv.SlotToSlot(worn, inv.hoverSlot);
                _mn.randChar.EquipEffectCheck(owner, equipId);
                inv.EquipSlotLoad();
                _mn.uiMN.StatusChange(owner);
                // remembered like a bag's grab, so a refusal or the tool belt's
                // "send it home" can put it back on (a tool hung from the hand did
                // not go back to the hand - it stayed on the cursor, since nothing
                // knew where it came from)
                _grabSource = cell;
            }
            else
            {
                ItemData item = _mn.itemMN.FindItem(inv.hoverSlot.itemKey);
                if (item == null || _mn.randChar.ItemTypeToEquipID(item.itemType) < 0)
                {
                    // caution 34 is vanilla's own "that is not equipment" refusal
                    _mn.eventMN.StartCoroutine(_mn.eventMN.GoCaution(34));
                    return;
                }
                // a favourite is worn by you or by no one (FavouriteRules)
                if (Favourites.IsFavourite(inv.hoverSlot) && !_mn.gameMN.IsPlayer(owner))
                {
                    FavouriteRules.Refuse();
                    HomeHeld();
                    return;
                }
                // EquipItem SWAPS through inventory.equipSlot, so whatever was worn
                // lands in the hand — the grab/place grammar for free. It picks the
                // slot from the ITEM's type, so a hat dropped on the boots cell
                // still goes on the head, which is what vanilla's equip click does.
                // It also refuses with caution 20 when the character cannot wear it.
                _mn.randChar.EquipItem(owner, inv.hoverSlot);
            }
            NormalizeHover();
            _nextRefresh = 0f;
            Refresh();
        }

        // Right-click takes a worn item straight to the Shared Bag, no hand
        // involved. This is a VANILLA verb, not an invention: EquipItem's third
        // branch (RandomCharacter.cs:4543) is reached by right-clicking an equip
        // strip slot and does exactly this — free base slot or caution 6, UnEquip,
        // move, EquipEffectCheck. We cannot call it, because that branch is keyed
        // on `IsEquipSlot(slot.slotID)` and every real equip slot carries slotID 0
        // (recon §7.1), so the shape is reproduced against the real array instead.
        private static void UnequipToBag(Plate plate, CellView cell)
        {
            CommonStates owner = plate.Owner;
            ItemSlot worn = cell.Slot;
            if (owner == null || owner.equip == null || _mn.randChar == null
                || worn == null || string.IsNullOrEmpty(worn.itemKey))
            {
                return;
            }
            int index = plate.Cells.IndexOf(cell);
            if (index < 0 || index >= LoadoutOrder.Length)
            {
                return;
            }
            InventoryManager inv = _mn.inventory;
            int free = inv.EmptySlotCheck();
            if (free == -1)
            {
                // caution 6 is vanilla's own "no room for it" on this exact path
                _mn.eventMN.StartCoroutine(_mn.eventMN.GoCaution(6));
                return;
            }
            _mn.randChar.UnEquip(owner, worn);
            inv.SlotToSlot(worn, inv.itemSlot[free]);
            _mn.randChar.EquipEffectCheck(owner, LoadoutOrder[index]);
            inv.EquipSlotLoad();
            _mn.uiMN.StatusChange(owner);
            _nextRefresh = 0f;
            Refresh();
        }

        // Harness door: the dialog over the first stack of two or more on screen,
        // since /ui/click cannot hold Shift.
        internal static bool SplitProbe()
        {
            for (int p = 0; p < _platesUsed; p++)
            {
                Plate plate = Plates[p];
                for (int c = 0; plate.Window.Go.activeInHierarchy && c < plate.Shown && c < plate.Cells.Count; c++)
                {
                    CellView cell = plate.Cells[c];
                    if (cell.Slot != null && !string.IsNullOrEmpty(cell.Slot.itemKey) && cell.Slot.stack > 1)
                    {
                        OpenSplit(cell, cell.Slot);
                        return SplitDialog.IsOpen;
                    }
                }
            }
            return false;
        }

        // Shift+click on a stack (ClickMap.Verb.Split): the dialog chooses a count,
        // and the count comes off onto the cursor through vanilla's own split, as
        // the half-stack grab it replaced did. Re-checked on take, since the stack
        // can change under an open dialog.
        private static void OpenSplit(CellView cell, ItemSlot slot)
        {
            if (cell == null || slot == null || _root == null)
            {
                return;
            }
            string key = slot.itemKey;
            Plate plate = PlateOf(cell);
            // the dialog is modal, so the hover card stops being driven while it is
            // up - hidden here, or it froze over the count
            HoverCard.HideExternal();
            _hover.text = string.Empty;
            SplitDialog.Open((RectTransform)_root.transform, cell.Rect, slot.stack, n =>
            {
                InventoryManager inv = _mn != null ? _mn.inventory : null;
                if (inv == null || !HandEmpty() || !ReferenceEquals(cell.Slot, slot)
                    || slot.itemKey != key || slot.stack < 2)
                {
                    return;
                }
                inv.SlotToSlotSplit(slot, inv.hoverSlot, Mathf.Clamp(n, 1, slot.stack - 1));
                _grabSource = cell;
                ReportWrite(plate, slot);
                NormalizeHover();
                _nextRefresh = 0f;
                Refresh();
            });
        }

        // Vanilla gates right-click on "nothing in hand": ItemSlot.OnPointerClick
        // ends with `else if (!hoverSlot.gameObject.activeSelf) UseItem(...)`. And
        // SlotToSlotSplit clears the hover's DATA when it empties but leaves the
        // GameObject ACTIVE — so after a wheel notch you are
        // holding nothing, invisibly, and right-click stops working everywhere
        // until something else deactivates it. Heal that state.
        /// <summary>
        /// Put the held item back in the cell it was grabbed from, and empty the
        /// cursor. Returns false when the hand is empty or the grab did not come
        /// through one of our cells — a vanilla panel's own SlotClick leaves no
        /// source for us to return to, and inventing one would move somebody's
        /// item somewhere they did not ask for.
        /// </summary>
        internal static bool ReturnHeldToSource()
        {
            InventoryManager inv = _mn != null ? _mn.inventory : null;
            if (inv == null || inv.hoverSlot == null || !inv.hoverSlot.gameObject.activeSelf
                || string.IsNullOrEmpty(inv.hoverSlot.itemKey))
            {
                return false;
            }
            if (_grabSource == null || _grabSource.Slot == null)
            {
                return false;
            }
            // SlotToSlot rather than a raw assignment: it merges back into a stack
            // that has since been topped up, and swaps if something else took the
            // slot while the item was in hand.
            ReturnTo(_grabSource);
            _grabSource = null;
            NormalizeHover();
            _nextRefresh = 0f;
            Refresh();
            return true;
        }

        // The held stack back into the cell it was lifted from. A loadout cell is
        // worn again rather than written: a slot-to-slot move into equip[] would
        // wear the thing without the visuals, the stats or EquipEffectCheck,
        // so it goes through LoadoutClick's own place, EquipItem.
        private static void ReturnTo(CellView cell)
        {
            InventoryManager inv = _mn.inventory;
            Plate plate = PlateOf(cell);
            if (plate != null && plate.IsLoadout && plate.Owner != null && _mn.randChar != null)
            {
                _mn.randChar.EquipItem(plate.Owner, inv.hoverSlot);
                return;
            }
            inv.SlotToSlot(inv.hoverSlot, cell.Slot);
        }

        private static bool LiftedFromLoadout()
        {
            Plate plate = _grabSource != null ? PlateOf(_grabSource) : null;
            return plate != null && plate.IsLoadout;
        }

        // A favourite refused its way out (FavouriteRules) goes home: to the cell it
        // was lifted from while that is yours and still takes it (empty, or the same
        // kind to merge into - never a swap, which would lift something else), else
        // the first free cell of the Shared Bag, then of your bag. With no room
        // anywhere it stays in hand.
        internal static void HomeHeld()
        {
            InventoryManager inv = _mn != null ? _mn.inventory : null;
            if (inv == null || inv.hoverSlot == null || !inv.hoverSlot.gameObject.activeSelf
                || string.IsNullOrEmpty(inv.hoverSlot.itemKey))
            {
                return;
            }
            ItemSlot source = _grabSource != null ? _grabSource.Slot : null;
            ItemSlot home = source != null && FavouriteMoves.Yours(inv, source)
                && (string.IsNullOrEmpty(source.itemKey) || source.itemKey == inv.hoverSlot.itemKey)
                ? source : null;
            if (home == null)
            {
                int free = inv.EmptySlotCheck();
                home = free >= 0 && free < inv.itemSlot.Length ? inv.itemSlot[free] : null;
            }
            CommonStates you = _mn.gameMN != null && _mn.gameMN.pMove != null ? _mn.gameMN.pMove.common : null;
            ItemSlot[] bag = you != null && you.bag != null ? you.bag.slots : null;
            for (int i = 0; home == null && bag != null && i < bag.Length; i++)
            {
                if (bag[i] != null && string.IsNullOrEmpty(bag[i].itemKey))
                {
                    home = bag[i];
                }
            }
            if (home == null)
            {
                return;
            }
            if (ReferenceEquals(home, source) && LiftedFromLoadout())
            {
                ReturnTo(_grabSource);   // worn again, not written into equip[]
            }
            else
            {
                inv.SlotToSlot(inv.hoverSlot, home);
            }
            _grabSource = null;
            NormalizeHover();
            Invalidate();   // vanilla's drop can refuse with our windows shut: no Refresh then
        }

        // A PLANTER TAKES ONE SEED A CELL (vanilla plants one per cell, and our
        // cells used to accept a whole stack). Vanilla's click on a planter
        // cell, verbatim (ItemSlot.OnClick, ItemSlot.cs:143): only into an empty
        // cell, only a Plant, and one of it - the rest stays in hand. A stack in a
        // cell never grows: the growth tick (PlanterCheck, breed) counts a cell up
        // only while its stack is 1. Anything else is refused as vanilla refuses it,
        // silently.
        private static bool PlantOne(InventoryManager inv, ItemSlot slot)
        {
            ItemSlot hover = inv.hoverSlot;
            if (!string.IsNullOrEmpty(slot.itemKey) || hover.toolType != ItemData.ToolType.Plant)
            {
                return false;
            }
            inv.SlotLoadSlot(hover, slot);
            slot.stack = 1;
            hover.stack--;
            if (hover.stack <= 0)
            {
                inv.SlotClear(hover);
                hover.gameObject.SetActive(false);
            }
            return true;
        }

        // T BUTCHERS THE ANIMAL UNDER THE CURSOR. Vanilla's T (GameManager.Update -> ItemManager.DisassembleAnimal)
        // acts on the slot its own ItemSlot hover set, useSlotID - which our cells
        // never set - and only on the Shared Bag, because it finds the slot by index
        // into itemSlot. The act itself is two lines and is done here verbatim, on
        // any cell a hand may take from: the slot emptied, then the animal's DropItem
        // handed over by PickUpItem(destroy: false), as looting its body would. From
        // a cage trap, the write is reported (TrapLoad) so the creature leaves the
        // cage. Read through Input, not RawInput, so the keymap's "Disassemble
        // Animal" binding moves it with vanilla's.
        private static bool Butchers(CellView cell)
        {
            ItemSlot slot = cell != null ? cell.Slot : null;
            if (slot == null || slot.toolType != ItemData.ToolType.Animal || !HandEmpty())
            {
                return false;
            }
            Plate plate = PlateOf(cell);
            return plate != null && !plate.IsLoadout && LootOf(slot) != null;
        }

        private static DropItem LootOf(ItemSlot slot)
        {
            ItemData data = _mn.itemMN != null ? _mn.itemMN.FindItem(slot.itemKey) : null;
            return data != null && data.itemObj != null ? data.itemObj.GetComponent<DropItem>() : null;
        }

        private static void Butcher(CellView cell)
        {
            ItemSlot slot = cell.Slot;
            DropItem loot = LootOf(slot);
            string key = slot.itemKey;
            _mn.inventory.SlotClear(slot);
            ReportWrite(PlateOf(cell), slot);
            _mn.gameMN.PickUpItem(loot, destroy: false);
            Plugin.Log.LogInfo("Butchered " + key);
            _nextRefresh = 0f;
            Refresh();
        }

        private static void NormalizeHover()
        {
            ItemSlot hover = _mn.inventory != null ? _mn.inventory.hoverSlot : null;
            if (hover != null && hover.gameObject.activeSelf
                && string.IsNullOrEmpty(hover.itemKey))
            {
                hover.gameObject.SetActive(false);
            }
        }

        // BOTH pools. The reach members live in ReachPlates so the viewport can
        // clip them, and this searched only the main pool - so every cell in the
        // vicinity window resolved to a NULL plate. It cost nothing while the
        // answer was only used for `IsLoadout` and `Owner`, since a pooled chest
        // is neither; it costs correctness now that the plate carries the
        // container a write has to be reported against. ScanPlates has searched
        // both since the pool was split; this had not caught up.
        private static Plate PlateOf(CellView cell)
        {
            for (int p = 0; p < _platesUsed; p++)
            {
                if (Plates[p].Cells.Contains(cell))
                {
                    return Plates[p];
                }
            }
            for (int p = 0; p < _reachPlatesUsed; p++)
            {
                if (ReachPlates[p].Cells.Contains(cell))
                {
                    return ReachPlates[p];
                }
            }
            return null;
        }

        // Where this slot sits in its container, by IDENTITY rather than by cell
        // position. Cell order and slot order agree today, and relying on that
        // would be a rule nothing enforces.
        //
        // A -1 here is not a failure: it is what you get when the cell is backed
        // by a MIRROR slot, which means the vanilla panel has this container open
        // and its own poll is already watching. Reporting the write ourselves as
        // well would run the callback twice.
        private static int SlotIndexIn(InventorySlot container, ItemSlot slot)
        {
            if (container == null || container.slots == null || slot == null)
            {
                return -1;
            }
            for (int i = 0; i < container.slots.Length; i++)
            {
                if (ReferenceEquals(container.slots[i], slot))
                {
                    return i;
                }
            }
            return -1;
        }

        // D39. Every path that changes a container cell ends here. Silent for the
        // overwhelming majority of containers - a chest has no machinery to tell -
        // and load-bearing for the handful whose props are driven by the panel we
        // took away.
        private static void ReportWrite(Plate plate, ItemSlot slot)
        {
            if (plate == null || plate.Container == null)
            {
                return;
            }
            int index = SlotIndexIn(plate.Container, slot);
            if (index >= 0)
            {
                ContainerPolicy.AfterWrite(_mn, plate.Container, index);
            }
        }

        // Vanilla's right-click convention: equipment swaps onto the character,
        // food and consumables are used. Our cells are not vanilla ItemSlots, so
        // ItemManager.UseItem cannot be called on them — it resolves a slot by
        // index into inventory.itemSlot, and container slots are not in that
        // array. Equipment has a direct entry point that takes a real slot
        // (RandomCharacter.EquipItem, recon §7.1), so that path is exact.
        // Consumables have no such entry point: UseFood/UseConsumables index the
        // UI array too, which is why eating still happens from the Shared Bag.
        private static void RightClick(CellView cell)
        {
            ItemSlot slot = cell.Slot;
            if (slot == null || string.IsNullOrEmpty(slot.itemKey) || _mn.randChar == null)
            {
                return;
            }
            Plate plate = PlateOf(cell);
            if (plate != null && plate.IsLoadout)
            {
                UnequipToBag(plate, cell);
                return;
            }
            // ONE dispatcher, shared with the action bar (ItemUse).
            //
            // This branch used to be "mirrored cells get vanilla's whole use path,
            // everything else gets the EQUIP SUBSET" — and a plate only carries
            // real vanilla indices while its container is the LIVE MIRROR. Your own
            // pack and a target's bag almost never are, so right-clicking food in
            // either fell past the equip gate and returned in silence. Reported as
            // "nothing happens when I click", and it was true.
            // Two different questions, and conflating them is what D25 did. WHO
            // acts is the focus, and vanilla already resolves that itself - so the
            // aim delegate is now a pass-through. Whether the item may be STAGED
            // through your Shared Bag is about the SOURCE: staging an NPC's item
            // through your own container would move their property into yours, and
            // that stays forbidden however the actor is chosen.
            CommonStates sourceOwner = plate != null ? plate.Owner : null;
            bool mine = sourceOwner == null
                || ReferenceEquals(sourceOwner, _mn.gameMN.pMove.common);
            // D141: on YOU, whatever is targeted - to use it on
            // someone, drop it on them
            if (!ItemUse.Use(_mn, slot, cell.VanillaSlotId, use => ItemUse.OnYou(_mn, use), mine))
            {
                // Refused rather than done, and it must SAY so — a silent no-op is
                // the bug this branch was. Caution 34 is the game's own "not like
                // that", already used by the loadout paths above.
                if (_mn.eventMN != null)
                {
                    _mn.eventMN.StartCoroutine(_mn.eventMN.GoCaution(34));
                }
                return;
            }
            // Using an item CONSUMES it, which empties a slot exactly as a grab
            // does. Refusals return above, so reaching here means something moved.
            ReportWrite(plate, slot);
            _nextRefresh = 0f;
            Refresh();
        }

        // What the modifier you are holding will do to the thing under the cursor.
        // Three rules, and each is silent unless it would actually fire — a badge
        // that promises an inert action is worse than no badge.
        private static void OfferBadge(CellView cell, ItemSlot slot)
        {
            if (cell == null || slot == null)
            {
                return;
            }
            // the same question the click asks (ClickMap), so the badge cannot
            // promise what the click will not do
            Plate plate = PlateOf(cell);
            ClickMap.Verb verb = ClickMap.ForCell(slot, HandEmpty(), WearsFavourites(plate),
                plate != null && plate.IsLoadout, MoveKind(plate));
            if (verb != ClickMap.Verb.None)
            {
                MoveRoute route = verb == ClickMap.Verb.Move ? RouteFrom(plate) : null;
                ClickMap.Offer(verb, route != null ? route.Name : null);
                return;
            }
            if (ClickMap.Alt || ClickMap.Shift || ClickMap.Ctrl)
            {
                return;   // a held modifier with nothing to offer here stays silent
            }
            // The "right-click -> Merry" line went with D141: a plain use is always
            // yours now, and "this feeds you" over every item is noise. A drop on
            // someone names them instead (the drag's own badge).
        }

        // Committed every frame from Plugin, not from Tick: Tick returns early when
        // the window has nothing on screen, and a badge that stops being updated
        // would be a badge that stays up forever.
        // Harness door: raise the badge once and say where it landed.
        internal static string BadgeProbe()
        {
            CursorBadge.Show("alt-click  →  favourite", HudGlyphs.Get("star"));
            CommitBadge();
            return CursorBadge.Where();
        }

        internal static void CommitBadge()
        {
            // a favourite's refusal lingers a moment, wherever the pointer went
            if (Time.unscaledTime < FavouriteRules.KeptUntil)
            {
                CursorBadge.Show(FavouriteRules.KeptWords, HudGlyphs.Get("star"));
            }
            Canvas canvas = _root != null ? _root.GetComponentInParent<Canvas>() : null;
            Transform root = canvas != null ? canvas.rootCanvas.transform : null;
            Camera cam = canvas != null
                && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.rootCanvas.worldCamera : null;
            CursorBadge.Commit(root, cam);
        }

        // Who a use acts on: THE FOCUS (D25, rewritten). Vanilla's own
        // answer, and we no longer override it.
        //
        // It used to be the holder of the container the item came from. That was
        // written to fix a real complaint - targeting an NPC opens the inventory,
        // which reads as an invitation, while saying nothing about your next click
        // being performed BY them - but it bought the representation with a
        // coupling, and the coupling had two costs.
        //
        // It made TWO rules where the game has one. pocket_01 expands a container,
        // so a holder is meaningless to it and it had to resolve the focus instead;
        // the two paths then disagreed, and the disagreement is what surfaced as
        // the pocket refusing to be used on a chest.
        //
        // And it put us in the position of having to be right about every vanilla
        // use path, including the ones we never read. Overriding menuNPC around a
        // call means owning the consequences for whatever that call reaches. Not
        // overriding it means we cannot be wrong about them.
        //
        // What paid for the coupling is kept: the cursor badge still names the
        // actor when it is not you. That was the conveyance D25 was after, and it
        // never needed the coupling - it needs a NAME, which the focus has. The
        // focused character now also has a panel of its own with that name on it,
        // which is the representation the original objection said was missing.
        // D141 retires the focus as the actor: a plain use is yours, so this is you.
        // Someone else acts only where you drop the item on them.
        private static CommonStates ActorNow()
        {
            return _mn.gameMN.pMove.common;
        }

        // pocket_01 expands a container by one slot. Vanilla gated both its
        // branches on the container UI being open, which made the item a silent
        // no-op the rest of the time; we re-target it at the Focus, or your own
        // Bag when nothing is in focus. Name that target before the click is spent.
        private static string PocketPreview()
        {
            VanillaContainerUI.PocketTarget target =
                VanillaContainerUI.ResolvePocketTarget(_mn);
            HighlightRecipient(target.Owner);
            if (!target.Expandable)
            {
                return target.Container != null
                    ? $"{target.Name} cannot be expanded"
                    : "nothing to expand";
            }
            return target.Capped
                ? $"{target.Name} is at the 50-slot cap"
                : $"right-click  →  {target.Name}  +1 slot";
        }

        private static readonly List<Plate> Highlighted = new List<Plate>();

        private static void ClearRecipientHighlight()
        {
            for (int i = 0; i < Highlighted.Count; i++)
            {
                Plate plate = Highlighted[i];
                if (plate.Edge != null)
                {
                    plate.Edge.enabled = false;
                }
            }
            Highlighted.Clear();
        }

        // Outline every plate the receiver owns, so "who gets this" is a place on
        // screen rather than a name to read.
        private static void HighlightRecipient(CommonStates receiver)
        {
            if (receiver == null)
            {
                return;
            }
            for (int p = 0; p < _platesUsed; p++)
            {
                Plate plate = Plates[p];
                if (!ReferenceEquals(plate.Owner, receiver) || plate.Edge == null)
                {
                    continue;
                }
                plate.Edge.enabled = true;
                plate.Edge.effectColor = Theme.AccentGold;
                Highlighted.Add(plate);
            }
        }
    }
}
