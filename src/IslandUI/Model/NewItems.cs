using System.Collections.Generic;
using UnityEngine;

namespace IslandUI
{
    // NEW SINCE THE INVENTORY WAS LAST OPEN (Forgejo #192, docs/inventory-window-ux.md §9.5).
    //
    // What came into your bags - the Shared Backpack and your Backpack - while the
    // inventory was closed wears a border the first time you open it, so a run
    // through the jungle ends with "what did I pick up" answered at a glance. It is
    // Terraria's rule ("Highlight New Items", on by default) and Baganator's: the
    // mark goes when you point at the cell, and every mark goes when you close the
    // inventory - it means "since you last looked", and after a close you have.
    //
    // ONE SNAPSHOT, TAKEN AT THE CLOSE, DIFFED AT THE OPEN. No per-frame scan and no
    // patch on the game's many ways of adding an item: the inventory's own open and
    // close are the only two moments that matter, and at each the two bags are read
    // once (a few dozen slots). Between them nothing here runs.
    //
    // NEW IS BY KIND FIRST, THEN BY SLOT. An item counts only if your bags hold more
    // of its kind than at the close, so an item that only moved between your own
    // bags - the game's cascade, a swap - is not new. Of a kind that grew, the cells
    // marked are the ones that changed: a slot that held something else (or nothing),
    // or a stack that grew. A stack of wood that went from 12 to 20 is marked; the
    // other stack of wood you did not touch is not.
    //
    // THE SAVE'S OWN CONTENTS ARE NEVER NEW. The baseline is taken when a load has
    // finished (SaveBlock calls Rebase), so the first open after a load marks only
    // what you picked up since. A hot reload takes it at once.
    internal static class NewItems
    {
        private struct Seen
        {
            public string Key;
            public int Stack;
        }

        private static readonly Dictionary<ItemSlot, Seen> Before = new Dictionary<ItemSlot, Seen>();
        private static readonly Dictionary<string, int> TotalsBefore = new Dictionary<string, int>();
        private static readonly Dictionary<string, int> TotalsNow = new Dictionary<string, int>();
        private static readonly List<ItemSlot> Bags = new List<ItemSlot>();
        // each marked slot with the key it was marked on: a slot that no longer holds
        // that key is not new (the same contract as Favourites)
        private static readonly Dictionary<ItemSlot, string> Marked = new Dictionary<ItemSlot, string>();

        private static bool _primed;
        private static bool _wasOpen;
        private static bool _loading;
        private static float _loadingSince;

        internal static bool On => Settings.NewItemMark == null || Settings.NewItemMark.Value;

        internal static int Count => Marked.Count;

        internal static bool IsNew(ItemSlot slot)
        {
            if (Marked.Count == 0 || slot == null || !On)
            {
                return false;
            }
            return Marked.TryGetValue(slot, out string key) && key == slot.itemKey
                && !string.IsNullOrEmpty(key);
        }

        // The pointer reached the cell: it has been seen. True when a mark went, so
        // the caller repaints that one cell.
        internal static bool See(ItemSlot slot)
        {
            return slot != null && Marked.Count > 0 && Marked.Remove(slot);
        }

        // Once a frame, from the window's tick, with whether your bags are up.
        internal static void Tick(ManagersScript mn, bool open)
        {
            bool was = _wasOpen;
            _wasOpen = open;
            if (_loading)
            {
                // a load that never reported its end must not switch the mark off
                // for the rest of the session
                if (Time.realtimeSinceStartup - _loadingSince < 120f)
                {
                    return;
                }
                _loading = false;
            }
            if (!_primed)
            {
                if (!open && Read(mn))
                {
                    Keep();
                    _primed = true;
                }
                return;
            }
            if (open && !was)
            {
                Diff(mn);
            }
            else if (!open && was)
            {
                Marked.Clear();
                if (Read(mn))
                {
                    Keep();
                }
            }
        }

        // a load is under way: what lands in the bags now is the save, not new
        internal static void LoadStarted()
        {
            _loading = true;
            _loadingSince = Time.realtimeSinceStartup;
            _primed = false;
            Marked.Clear();
        }

        // the load has finished: the bags as they stand are the baseline
        internal static void Rebase()
        {
            _loading = false;
            _primed = false;
            Marked.Clear();
        }

        internal static void ResetForSceneLoad()
        {
            _primed = false;
            Marked.Clear();
            Before.Clear();
            TotalsBefore.Clear();
        }

        // the marks as text, for the harness and the log
        internal static string Describe()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("newItems on=").Append(On).Append(" primed=").Append(_primed)
                .Append(" loading=").Append(_loading).Append(" marked=").Append(Marked.Count);
            foreach (KeyValuePair<ItemSlot, string> m in Marked)
            {
                sb.Append(' ').Append(m.Value);
                if (m.Key != null)
                {
                    sb.Append('#').Append(m.Key.slotID).Append('x').Append(m.Key.stack);
                }
            }
            return sb.ToString();
        }

        // Harness door: `count` of item `key` through the game's own pickup
        // (ItemManager.GetItem), as walking over it would give it.
        internal static string HarnessGive(string key, int count)
        {
            ManagersScript mn = GameObject.Find("Managers")?.GetComponent<ManagersScript>();
            ItemData data = mn != null && mn.itemMN != null ? mn.itemMN.FindItem(key) : null;
            if (data == null)
            {
                return "no item " + key;
            }
            mn.itemMN.GetItem(data, Mathf.Max(1, count));
            return "gave " + key + " x" + count;
        }

        // Harness door: what the two bags hold, slot by slot.
        internal static string HarnessBags()
        {
            ManagersScript mn = GameObject.Find("Managers")?.GetComponent<ManagersScript>();
            if (!Read(mn))
            {
                return "no bags";
            }
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < Bags.Count; i++)
            {
                ItemSlot s = Bags[i];
                if (s != null && !string.IsNullOrEmpty(s.itemKey))
                {
                    sb.Append(i).Append(':').Append(s.itemKey).Append('x').Append(s.stack).Append(' ');
                }
            }
            return sb.ToString();
        }

        // ---------- the two bags ----------

        private static bool Read(ManagersScript mn)
        {
            Bags.Clear();
            if (mn == null || mn.inventory == null || mn.inventory.itemSlot == null
                || mn.gameMN == null || mn.gameMN.pMove == null || mn.gameMN.pMove.common == null)
            {
                return false;
            }
            InventoryManager inv = mn.inventory;
            for (int i = 0; i < inv.baseCount && i < inv.itemSlot.Length; i++)
            {
                Bags.Add(inv.itemSlot[i]);
            }
            CommonStates player = mn.gameMN.pMove.common;
            if (player.bag != null && player.bag.slots != null)
            {
                Bags.AddRange(player.bag.slots);
            }
            return true;
        }

        private static void Total(Dictionary<string, int> into)
        {
            into.Clear();
            for (int i = 0; i < Bags.Count; i++)
            {
                ItemSlot s = Bags[i];
                if (s == null || string.IsNullOrEmpty(s.itemKey))
                {
                    continue;
                }
                into.TryGetValue(s.itemKey, out int n);
                into[s.itemKey] = n + Mathf.Max(1, s.stack);
            }
        }

        private static void Keep()
        {
            Before.Clear();
            for (int i = 0; i < Bags.Count; i++)
            {
                ItemSlot s = Bags[i];
                if (s != null)
                {
                    Before[s] = new Seen { Key = s.itemKey, Stack = s.stack };
                }
            }
            Total(TotalsBefore);
        }

        private static void Diff(ManagersScript mn)
        {
            Marked.Clear();
            if (!Read(mn))
            {
                return;
            }
            Total(TotalsNow);
            for (int i = 0; i < Bags.Count; i++)
            {
                ItemSlot s = Bags[i];
                if (s == null || string.IsNullOrEmpty(s.itemKey))
                {
                    continue;
                }
                TotalsBefore.TryGetValue(s.itemKey, out int then);
                if (TotalsNow[s.itemKey] <= then)
                {
                    continue;   // no more of it than before: moved, or nothing happened
                }
                bool changed = !Before.TryGetValue(s, out Seen was) || was.Key != s.itemKey
                    || s.stack > was.Stack;
                if (changed)
                {
                    Marked[s] = s.itemKey;
                }
            }
            if (Marked.Count > 0)
            {
                Plugin.Log.LogInfo(Describe());
            }
        }
    }
}
