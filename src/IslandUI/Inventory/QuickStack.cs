using System.Collections.Generic;
using UnityEngine;

namespace IslandUI
{
    // Feature: quick-stack — one key, or the cart glyph in the vicinity window's
    // heading, deposits stackable items into nearby containers.
    //
    // IT ACTS ON WHAT THE WINDOW SHOWS. It used to have its own
    // radius and its own container test, so a button sitting on a window listing
    // nine containers touched a subset of them, sourced from a bag that window
    // also drew separately - two rules wearing one control. Destinations are now
    // the families set to Stack, within the vicinity radius; the family modes are
    // where "which containers" is answered for both.
    //
    // Semantics (Terraria-style "quick stack"):
    //   pass 1 - top up existing matching stacks in range
    //   pass 2 - fill empty slots, but only in storages that already hold that item
    // Weapons/tools and non-stackables (maxStack <= 1) are never moved.
    //
    // Chest slots are manipulated directly (stack arithmetic + SlotClear/SlotLoadSlot),
    // the same idiom the game's CraftConsume uses on them; InventoryManager.SlotToSlot
    // is unsuitable here because it is coupled to the on-screen slot array via slotID.
    internal static class QuickStack
    {
        // What one sweep actually moved, and the reason the two placement passes
        // report more than a total: a flight has to name WHICH chest each
        // item went into, and the passes are the only code that knows. `FromScreen`
        // is captured before the move, because "which cell holds this key" can only
        // be asked while the key is still there.
        internal sealed class Deposit
        {
            internal ItemData Item;
            internal InventorySlot Storage;
            internal int Count;
            internal Vector2 FromScreen;
            // a WITHDRAWAL (a chest's All out): the flight runs the other
            // way, from the storage to FromScreen, which is then where it lands
            internal bool Reverse;
            // ...and where the chest's cell WAS, captured before a withdrawal moved
            // the stack out of it - afterwards no cell in the chest holds the item
            internal bool HasStorageScreen;
            internal Vector2 StorageScreen;
        }

        // What the next sweep would do, without doing it. The HUD button's hover
        // is a preview of this plan; click / G then run the real mutation. The
        // two must agree, so eligibility and the two placement passes are shared.
        internal sealed class Plan
        {
            internal readonly List<Deposit> Deposits = new List<Deposit>();
            internal readonly List<ItemSlot> Moving = new List<ItemSlot>();
            internal readonly List<ItemSlot> Staying = new List<ItemSlot>();
            internal readonly List<InventorySlot> Destinations = new List<InventorySlot>();
            internal bool Ready;
        }

        internal static bool CanRun(ManagersScript mn)
        {
            if (mn == null || !mn.allSet || GameManager.nowEvent || mn.inventory == null
                || mn.uiMN == null || mn.gameMN == null || mn.gameMN.pMove == null)
            {
                return false;
            }
            UIManager ui = mn.uiMN;
            // a VANILLA-owned container holds live item state in the 50+ mirror
            // slots; touching real chest slots while it is open risks desync.
            // An intercepted one (WI-4) never left the real slots, so it is safe
            // — and standing at a chest is exactly when quick-stack is wanted.
            return !VanillaContainerUI.MirrorsLive(mn.inventory)
                && !ui.settingMenu.activeSelf && !ui.nameInput.enabled
                && !ui.mainInputField.gameObject.activeSelf && !ui.talkEditPanel.activeSelf;
        }

        internal static Plan Preview(ManagersScript mn)
        {
            var plan = new Plan();
            if (!CanRun(mn))
            {
                return plan;
            }
            plan.Ready = true;
            InventoryManager inv = mn.inventory;
            List<InventorySlot> storages = FindNearbyStorages(mn);
            var work = Scratch.From(storages);
            for (int i = 0; i < inv.baseCount && i < inv.itemSlot.Length; i++)
            {
                ItemSlot slot = inv.itemSlot[i];
                if (slot == null || string.IsNullOrEmpty(slot.itemKey))
                {
                    continue;
                }
                if (!Eligible(mn, inv, slot, out ItemData data))
                {
                    plan.Staying.Add(slot);
                    continue;
                }
                int moved = Simulate(slot, data, work, plan.Deposits);
                if (moved > 0)
                {
                    plan.Moving.Add(slot);
                }
                else
                {
                    plan.Staying.Add(slot);
                }
            }
            for (int i = 0; i < plan.Deposits.Count; i++)
            {
                InventorySlot dest = plan.Deposits[i].Storage;
                if (dest != null && !plan.Destinations.Contains(dest))
                {
                    plan.Destinations.Add(dest);
                }
            }
            return plan;
        }

        internal static void TryExecute(ManagersScript mn)
        {
            if (!CanRun(mn))
            {
                return;
            }
            InventoryManager inv = mn.inventory;

            List<InventorySlot> storages = FindNearbyStorages(mn);
            if (storages.Count == 0)
            {
                Plugin.Log.LogInfo("QuickStack: no owned storage in range");
                return;
            }

            int movedItems = 0;
            int touchedSlots = 0;
            // "moved 0 from 0" was true and said nothing about WHICH of the four
            // reasons it was: an empty source, everything filtered out, or two
            // passes that both found nowhere to put it. Four counters cost
            // nothing and turn one silent outcome into a diagnosis.
            int filled = 0;
            int skipped = 0;
            int homeless = 0;
            var deposits = new List<Deposit>();
            // BOTH OF YOUR BAGS. The sweep read the Shared Bag
            // alone, which is a fine rule and an invisible one: the window this
            // button sits on draws your bag and your Shared Bag as two bands of
            // one column, so "put this away" that silently means one of them
            // reads as a button that does nothing. The vanilla UI index comes
            // along because StackTextChange addresses the on-screen strip and
            // only the Shared Bag has one; -1 means "no cell to renumber".
            var sources = new List<KeyValuePair<ItemSlot, int>>();
            for (int i = 0; i < inv.baseCount && i < inv.itemSlot.Length; i++)
            {
                sources.Add(new KeyValuePair<ItemSlot, int>(inv.itemSlot[i], i));
            }
            CommonStates you = mn.gameMN.pMove.common;
            if (you != null && you.bag != null && you.bag.slots != null)
            {
                for (int i = 0; i < you.bag.slots.Length; i++)
                {
                    sources.Add(new KeyValuePair<ItemSlot, int>(you.bag.slots[i], -1));
                }
            }
            for (int n = 0; n < sources.Count; n++)
            {
                ItemSlot slot = sources[n].Key;
                int i = sources[n].Value;
                if (slot != null && !string.IsNullOrEmpty(slot.itemKey)) { filled++; }
                if (!Eligible(mn, inv, slot, out ItemData data))
                {
                    if (slot != null && !string.IsNullOrEmpty(slot.itemKey)) { skipped++; }
                    continue;
                }

                // Asked BEFORE the move, and this is the whole reason it is here
                // rather than in the flight code: the answer is "which on-screen
                // cell holds this key", and a moment later no cell does.
                List<Deposit> log = QuickStackFlight.SourceOf(mn, slot.itemKey,
                    out Vector2 from) ? deposits : null;

                int moved = TopUpExistingStacks(inv, slot, data, storages, log, from);
                if (!string.IsNullOrEmpty(slot.itemKey) && slot.stack > 0)
                {
                    moved += FillEmptySlots(inv, slot, data, storages, log, from);
                }
                if (moved > 0)
                {
                    movedItems += moved;
                    touchedSlots++;
                    if (i >= 0) { inv.StackTextChange(i); }
                }
                else
                {
                    homeless++;
                }
            }

            if (movedItems > 0)
            {
                mn.npcMN.CarryWeightCheck();
            }
            // The sweep says where it put things. A quick-stack is the one
            // move in the game you do not watch happen — you press a key and a
            // dozen stacks leave your bags at once — so without this the only
            // report is the bags being emptier than they were.
            QuickStackFlight.Show(mn, deposits);
            Plugin.Log.LogInfo($"QuickStack: moved {movedItems} item(s) from {touchedSlots} slot(s) "
                + $"into {storages.Count} storage(s) | your bags: {filled} filled slot(s), "
                + $"{skipped} skipped (weapon/tool/favourite/non-stacking), "
                + $"{homeless} eligible with nowhere to go");
        }

        // One notice per item per chest. A stack that fills three slots of the same
        // box went to ONE place, and saying it three times would read as three
        // different destinations.
        private static void Note(List<Deposit> log, ItemData item, Vector2 from,
            InventorySlot storage, int count)
        {
            if (log == null || count <= 0)
            {
                return;
            }
            for (int i = 0; i < log.Count; i++)
            {
                if (log[i].Storage == storage && log[i].Item == item)
                {
                    log[i].Count += count;
                    return;
                }
            }
            log.Add(new Deposit
            {
                Item = item,
                Storage = storage,
                Count = count,
                FromScreen = from,
            });
        }

        // Owned containers LIVE by the reach rule whose family is set to Stack.
        // One reach and one container question for the window and the sweep both
        // (ReachRule, D145): the sweep is a verb ON that window now, and a verb
        // whose reach differs from the surface it sits on is a rule nobody can
        // read off the screen.
        //
        // Materials first, so crafting-visible stock is topped up before anything
        // else; distance breaks the tie, which makes the nearest of a kind the one
        // that fills.
        private static List<InventorySlot> FindNearbyStorages(ManagersScript mn)
        {
            var storages = new List<InventorySlot>();
            Vector3 center = mn.gameMN.pMove.transform.position;
            Collider[] hits = Physics.OverlapSphere(center, ReachRule.Widest(mn), LayerMask.GetMask("BG"));
            for (int i = 0; i < hits.Length; i++)
            {
                InventorySlot storage = hits[i].GetComponent<InventorySlot>();
                if (storage == null || storages.Contains(storage))
                {
                    continue;
                }
                // The safety gate first (D38), then the family's own mode. Never
                // one call: PoolEligible says a write here can be made whole, and
                // Stack says you want a sweep landing here unattended. The second
                // may narrow the first and may never widen it.
                bool eligible = ContainerPolicy.PoolEligible(storage)
                    && ContainerPolicy.BulkSafe(storage)   // a sweep is a bulk write
                    && ContainerPolicy.FamilyStacks(ContainerPolicy.FamilyOf(storage));
                if (eligible && ReachRule.Live(mn, storage))   // owned, and live for its kind
                {
                    storages.Add(storage);
                }
            }
            storages.Sort((a, b) =>
            {
                int fa = (int)ContainerPolicy.FamilyOf(a);
                int fb = (int)ContainerPolicy.FamilyOf(b);
                if (fa != fb) { return fa - fb; }
                return (a.transform.position - center).sqrMagnitude
                    .CompareTo((b.transform.position - center).sqrMagnitude);
            });
            return storages;
        }

        private static int TopUpExistingStacks(InventoryManager inv, ItemSlot slot, ItemData data,
            List<InventorySlot> storages, List<Deposit> log, Vector2 from)
        {
            int moved = 0;
            for (int c = 0; c < storages.Count; c++)
            {
                ItemSlot[] slots = storages[c].slots;
                for (int j = 0; j < slots.Length; j++)
                {
                    if (slots[j].itemKey != slot.itemKey || slots[j].stack >= data.maxStack)
                    {
                        continue;
                    }
                    int amount = Mathf.Min(data.maxStack - slots[j].stack, slot.stack);
                    slots[j].stack += amount;
                    slot.stack -= amount;
                    moved += amount;
                    Note(log, data, from, storages[c], amount);
                    if (slot.stack <= 0)
                    {
                        inv.SlotClear(slot);
                        return moved;
                    }
                }
            }
            return moved;
        }

        private static int FillEmptySlots(InventoryManager inv, ItemSlot slot, ItemData data,
            List<InventorySlot> storages, List<Deposit> log, Vector2 from)
        {
            for (int c = 0; c < storages.Count; c++)
            {
                if (!ContainsItem(storages[c], slot.itemKey))
                {
                    continue;
                }
                ItemSlot[] slots = storages[c].slots;
                for (int j = 0; j < slots.Length; j++)
                {
                    if (!string.IsNullOrEmpty(slots[j].itemKey))
                    {
                        continue;
                    }
                    int amount = slot.stack;
                    inv.SlotLoadSlot(slot, slots[j]);
                    inv.SlotClear(slot);
                    Note(log, data, from, storages[c], amount);
                    return amount;
                }
            }
            return 0;
        }

        private static bool ContainsItem(InventorySlot storage, string itemKey)
        {
            for (int j = 0; j < storage.slots.Length; j++)
            {
                if (storage.slots[j].itemKey == itemKey)
                {
                    return true;
                }
            }
            return false;
        }

        // A favourite is never swept. This is half of what the mark MEANS —
        // "this one is mine" — and it is the half you notice, since the alternative
        // is hunting a chest for the rope you kept on you.
        private static bool Eligible(ManagersScript mn, InventoryManager inv, ItemSlot slot,
            out ItemData data)
        {
            data = null;
            if (slot == null || string.IsNullOrEmpty(slot.itemKey)
                || inv.IsWeaponOrTool(slot.toolType)
                || Favourites.IsFavourite(slot))
            {
                return false;
            }
            data = mn.itemMN.FindItem(slot.itemKey);
            return data != null && data.maxStack > 1;
        }

        // Same two passes as TopUpExistingStacks / FillEmptySlots, against a
        // working copy of each chest so a hover can ask "what would happen"
        // without moving anything.
        private static int Simulate(ItemSlot slot, ItemData data, Scratch[] work,
            List<Deposit> log)
        {
            int remaining = slot.stack;
            int moved = 0;
            for (int c = 0; c < work.Length && remaining > 0; c++)
            {
                Scratch chest = work[c];
                for (int j = 0; j < chest.Keys.Length && remaining > 0; j++)
                {
                    if (chest.Keys[j] != slot.itemKey || chest.Stacks[j] >= data.maxStack)
                    {
                        continue;
                    }
                    int amount = Mathf.Min(data.maxStack - chest.Stacks[j], remaining);
                    chest.Stacks[j] += amount;
                    remaining -= amount;
                    moved += amount;
                    Note(log, data, Vector2.zero, chest.Storage, amount);
                }
            }
            if (remaining <= 0)
            {
                return moved;
            }
            for (int c = 0; c < work.Length; c++)
            {
                Scratch chest = work[c];
                if (!chest.Contains(slot.itemKey))
                {
                    continue;
                }
                for (int j = 0; j < chest.Keys.Length; j++)
                {
                    if (!string.IsNullOrEmpty(chest.Keys[j]))
                    {
                        continue;
                    }
                    chest.Keys[j] = slot.itemKey;
                    chest.Stacks[j] = remaining;
                    Note(log, data, Vector2.zero, chest.Storage, remaining);
                    return moved + remaining;
                }
            }
            return moved;
        }

        private sealed class Scratch
        {
            internal InventorySlot Storage;
            internal string[] Keys;
            internal int[] Stacks;

            internal static Scratch[] From(List<InventorySlot> storages)
            {
                var copies = new Scratch[storages.Count];
                for (int c = 0; c < storages.Count; c++)
                {
                    ItemSlot[] slots = storages[c].slots;
                    var copy = new Scratch
                    {
                        Storage = storages[c],
                        Keys = new string[slots.Length],
                        Stacks = new int[slots.Length],
                    };
                    for (int j = 0; j < slots.Length; j++)
                    {
                        copy.Keys[j] = slots[j].itemKey;
                        copy.Stacks[j] = slots[j].stack;
                    }
                    copies[c] = copy;
                }
                return copies;
            }

            internal bool Contains(string itemKey)
            {
                for (int j = 0; j < Keys.Length; j++)
                {
                    if (Keys[j] == itemKey)
                    {
                        return true;
                    }
                }
                return false;
            }
        }
    }
}
