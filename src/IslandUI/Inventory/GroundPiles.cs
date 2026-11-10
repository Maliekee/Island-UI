using System.Collections.Generic;
using UnityEngine;

namespace IslandUI
{
    // LOOSE ITEMS ON THE GROUND, IN THE VICINITY (Forgejo #172). Only a body's
    // pockets used to get a row; an item lying on its own had none.
    //
    // WHAT ONE IS IN THE GAME: an instance of InventoryManager.dropItemObj - a world
    // object carrying its own live ItemSlot (key, stack, durability, colour, the
    // `common` of a carried person), made by MakeDropItem wherever something is put
    // down: a drag let go over the world, Ctrl+click's drop, a full bag's overflow
    // (GetItem's stockSlot), the NPCs' drops. The player takes one by walking up and
    // pressing E: PlayerMove's action finds the ItemSlot on its target and calls
    // ItemManager.ItemDropToSlot, which tops up stacks, refuses an oversized carry
    // item, cautions (5) and leaves it lying when the bag is full, shows the pickup
    // notice and destroys the object once all of it is taken. PickupCascade already
    // lends that routine your own bag.
    //
    // THE ROW IS A PILE. Loose items within the bodies' reach (ReachRule.Near, 3 m,
    // E's own range) are gathered into piles - an item joins every pile it lies
    // within PileGap of - and each pile is one row, ordered with the chests,
    // bags and bodies by distance, as they are. Its card is the picture of what the
    // pile holds most of; its cells are the items themselves, like with like side by
    // side. So a heap of drops is one row however many it holds, two drops apart
    // are two rows, and identical items close together share a row without being
    // merged into one cell (each cell is one thing on the ground, taken as one).
    // At most MaxItems are listed, nearest first.
    //
    // A pile is a holder the window already knows how to draw: an InventorySlot of
    // our own, whose slots ARE the world items' ItemSlots. It is never started - its
    // GameObject stays inactive, because InventorySlot.Start DESTROYS the slots it
    // finds and makes fresh ones - so no scene scan (FindObjectsOfType) ever meets
    // it, and the game never reads it. Its type is a prop's (Block), and its family
    // is Ground: never a bulk move's destination, never quick-stacked into.
    //
    // THE CELLS' VERBS ARE A BODY'S (InventoryWindow): a click lifts the item into
    // the hand, Ctrl+click takes it to your bags - here through the game's pickup,
    // ItemDropToSlot, so a full bag refuses as the game refuses - and All out takes
    // every one the same way. What empties a world item (a lift, a use) destroys its
    // object, as the game's pickup does (AfterWrite). A held item put on a ground
    // cell is put on the ground, as a held item let go over the world is.
    internal static class GroundPiles
    {
        internal const float PileGap = 1.5f;
        internal const int MaxItems = 48;

        private static readonly List<InventorySlot> Pool = new List<InventorySlot>();
        private static readonly HashSet<InventorySlot> Members = new HashSet<InventorySlot>();
        // the piles the vicinity is drawing while its list is held (Forgejo #237):
        // one that leaves range keeps its holder and its items, so its row can stand
        // frozen, and its items coming back claim it again
        private static readonly HashSet<InventorySlot> Held = new HashSet<InventorySlot>();
        private static Dictionary<ItemSlot, InventorySlot> _lastPile = new Dictionary<ItemSlot, InventorySlot>();
        private static GameObject _maskOf;
        private static int _mask;

        internal static bool Is(InventorySlot c)
        {
            return !ReferenceEquals(c, null) && Members.Contains(c);
        }

        // Set before each scan (InventoryWindow.ScanReach): empty unless the list holds.
        internal static void Hold(IEnumerable<InventorySlot> piles)
        {
            Held.Clear();
            foreach (InventorySlot p in piles)
            {
                if (Is(p))
                {
                    Held.Add(p);
                }
            }
        }

        // Does a pile still hold something lying on the ground: the frozen row's
        // "out of reach" rather than "gone"
        internal static bool HoldsLive(InventorySlot pile)
        {
            if (pile == null || pile.slots == null)
            {
                return false;
            }
            for (int i = 0; i < pile.slots.Length; i++)
            {
                ItemSlot s = pile.slots[i];
                if (s != null && !string.IsNullOrEmpty(s.itemKey) && s.stack > 0)
                {
                    return true;
                }
            }
            return false;
        }

        // A thing lying on the ground: a live instance of the game's drop object,
        // holding something. Never a chest's slot, a bag's or a character's.
        internal static bool IsLoose(ManagersScript mn, ItemSlot s)
        {
            if (s == null || string.IsNullOrEmpty(s.itemKey) || s.stack <= 0 || !s.gameObject.activeInHierarchy)
            {
                return false;
            }
            GameObject prefab = mn != null && mn.inventory != null ? mn.inventory.dropItemObj : null;
            return prefab != null && s.gameObject.name.StartsWith(prefab.name, System.StringComparison.Ordinal)
                && s.GetComponent<InventorySlot>() == null && s.GetComponentInParent<CommonStates>() == null;
        }

        // The layers the drop object's colliders are on, read off the prefab itself.
        private static int Mask(GameObject prefab)
        {
            if (!ReferenceEquals(prefab, _maskOf))
            {
                _maskOf = prefab;
                _mask = 0;
                foreach (Collider c in prefab.GetComponentsInChildren<Collider>(true))
                {
                    _mask |= 1 << c.gameObject.layer;
                }
                if (_mask == 0)
                {
                    _mask = 1 << prefab.layer;
                }
            }
            return _mask;
        }

        // The loose items within `reach` of `center`, nearest first, at most MaxItems.
        internal static List<ItemSlot> Within(ManagersScript mn, Vector3 center, float reach)
        {
            var found = new List<ItemSlot>();
            GameObject prefab = mn != null && mn.inventory != null ? mn.inventory.dropItemObj : null;
            if (prefab == null)
            {
                return found;
            }
            Collider[] hits = Physics.OverlapSphere(center, reach + 1f, Mask(prefab), QueryTriggerInteraction.Collide);
            var seen = new HashSet<ItemSlot>();
            for (int i = 0; i < hits.Length; i++)
            {
                ItemSlot s = hits[i].GetComponentInParent<ItemSlot>();
                if (s == null || !seen.Add(s) || !IsLoose(mn, s) || !ReachRule.Live(s, center, reach))
                {
                    continue;
                }
                found.Add(s);
            }
            found.Sort((a, b) => (a.transform.position - center).sqrMagnitude
                .CompareTo((b.transform.position - center).sqrMagnitude));
            if (found.Count > MaxItems)
            {
                found.RemoveRange(MaxItems, found.Count - MaxItems);
            }
            return found;
        }

        // The vicinity's rows of loose items, added to `into` (InventoryWindow.Rescan).
        // A pile keeps the holder it had while any of its items is still in it, so
        // the held order (OrderReach) and the slides know it as the same row.
        internal static void Scan(ManagersScript mn, Vector3 center, List<InventorySlot> into)
        {
            Pool.RemoveAll(p => p == null);           // a scene change took them
            Members.RemoveWhere(p => p == null);
            List<ItemSlot> items = Within(mn, center, ReachRule.Near);
            var piles = new List<List<ItemSlot>>();
            float gap = PileGap * PileGap;
            for (int i = 0; i < items.Count; i++)
            {
                List<ItemSlot> home = null;
                for (int p = piles.Count - 1; p >= 0; p--)
                {
                    if (!Touches(piles[p], items[i], gap))
                    {
                        continue;
                    }
                    if (home == null)
                    {
                        home = piles[p];
                    }
                    else
                    {
                        home.AddRange(piles[p]);   // the item joins two piles into one
                        piles.RemoveAt(p);
                    }
                }
                if (home == null)
                {
                    home = new List<ItemSlot>();
                    piles.Add(home);
                }
                home.Add(items[i]);
            }
            // nearest pile first, as the items were
            piles.Sort((a, b) => Nearest(a, center).CompareTo(Nearest(b, center)));

            // each pile its old holder if it had one, then a free one, then a new one
            var holders = new InventorySlot[piles.Count];
            var used = new HashSet<InventorySlot>();
            for (int p = 0; p < piles.Count; p++)
            {
                for (int i = 0; i < piles[p].Count && holders[p] == null; i++)
                {
                    if (_lastPile.TryGetValue(piles[p][i], out InventorySlot was) && was != null && used.Add(was))
                    {
                        holders[p] = was;
                    }
                }
            }
            var next = new Dictionary<ItemSlot, InventorySlot>();
            for (int p = 0; p < piles.Count; p++)
            {
                InventorySlot holder = holders[p] ?? Free(used);
                used.Add(holder);
                List<ItemSlot> pile = piles[p];
                Vector3 at = pile[0].transform.position;
                float best = float.MaxValue;
                foreach (ItemSlot s in pile)
                {
                    float d = (s.transform.position - center).sqrMagnitude;
                    if (d < best)
                    {
                        best = d;
                        at = s.transform.position;
                    }
                    next[s] = holder;
                }
                // like with like, side by side, in an order that holds still
                pile.Sort((a, b) =>
                {
                    int k = string.CompareOrdinal(a.itemKey, b.itemKey);
                    return k != 0 ? k : a.GetInstanceID().CompareTo(b.GetInstanceID());
                });
                holder.slots = pile.ToArray();
                holder.size = holder.slots.Length;
                holder.transform.position = at;   // its distance is its nearest item's
                into.Add(holder);
            }
            foreach (InventorySlot spare in Pool)
            {
                if (!used.Contains(spare) && !Held.Contains(spare))
                {
                    spare.slots = new ItemSlot[0];   // no world item kept alive by a spare
                }
            }
            // a held pile out of range remembers its items, so they claim it back
            foreach (KeyValuePair<ItemSlot, InventorySlot> was in _lastPile)
            {
                if (was.Key != null && Held.Contains(was.Value) && !used.Contains(was.Value)
                    && !next.ContainsKey(was.Key))
                {
                    next[was.Key] = was.Value;
                }
            }
            _lastPile = next;
        }

        private static bool Touches(List<ItemSlot> pile, ItemSlot s, float gapSqr)
        {
            Vector3 at = s.transform.position;
            for (int i = 0; i < pile.Count; i++)
            {
                if ((pile[i].transform.position - at).sqrMagnitude <= gapSqr)
                {
                    return true;
                }
            }
            return false;
        }

        private static float Nearest(List<ItemSlot> pile, Vector3 center)
        {
            float best = float.MaxValue;
            for (int i = 0; i < pile.Count; i++)
            {
                best = Mathf.Min(best, (pile[i].transform.position - center).sqrMagnitude);
            }
            return best;
        }

        private static InventorySlot Free(HashSet<InventorySlot> used)
        {
            foreach (InventorySlot p in Pool)
            {
                if (!used.Contains(p) && !Held.Contains(p))
                {
                    return p;
                }
            }
            // inactive BEFORE the component goes on, and never activated: Start
            // would destroy the world items' slots (see the head of this file)
            var go = new GameObject("IslandUI.GroundPile");
            go.SetActive(false);
            InventorySlot holder = go.AddComponent<InventorySlot>();
            holder.enabled = false;
            holder.type = InventorySlot.Type.Block;
            holder.size = 0;
            holder.slots = new ItemSlot[0];
            Pool.Add(holder);
            Members.Add(holder);
            return holder;
        }

        // True when a listed pile has lost an item since the scan - taken by hand
        // in the world, emptied, destroyed - so the vicinity rescans now rather than
        // draw a cell for something no longer there.
        internal static bool Stale()
        {
            foreach (InventorySlot pile in Members)
            {
                // a held pile is drawn frozen, as it was: its losses are not news
                if (pile == null || pile.slots == null || Held.Contains(pile))
                {
                    continue;
                }
                for (int i = 0; i < pile.slots.Length; i++)
                {
                    ItemSlot s = pile.slots[i];
                    if (s == null || string.IsNullOrEmpty(s.itemKey) || s.stack <= 0)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        // The card's picture: the item the pile holds most of.
        internal static Sprite Picture(InventorySlot pile)
        {
            if (pile == null || pile.slots == null)
            {
                return null;
            }
            Sprite best = null;
            int most = 0;
            for (int i = 0; i < pile.slots.Length; i++)
            {
                ItemSlot s = pile.slots[i];
                if (s == null || string.IsNullOrEmpty(s.itemKey))
                {
                    continue;
                }
                int n = 0;
                for (int j = 0; j < pile.slots.Length; j++)
                {
                    if (pile.slots[j] != null && pile.slots[j].itemKey == s.itemKey) { n++; }
                }
                if (n > most)
                {
                    most = n;
                    best = s.itemImage;
                }
            }
            return best;
        }

        // TAKEN AS THE GAME TAKES IT: ItemDropToSlot, what E on the item calls -
        // stacks topped up, the carry gate, a full bag's caution with the item left
        // lying, the notice, the sound, the object destroyed when all of it went.
        // Returns how many reached your bags.
        internal static int PickUp(ManagersScript mn, ItemSlot s)
        {
            if (mn == null || mn.itemMN == null || !IsLoose(mn, s))
            {
                return 0;
            }
            string key = s.itemKey;
            int before = Carried(mn, key);
            mn.itemMN.ItemDropToSlot(s);
            InventoryWindow.Invalidate();
            return Carried(mn, key) - before;
        }

        // How many of an item are in the Shared Bag and your own bag together.
        private static int Carried(ManagersScript mn, string key)
        {
            int n = 0;
            InventoryManager inv = mn.inventory;
            for (int i = 0; inv != null && i < inv.baseCount && i < inv.itemSlot.Length; i++)
            {
                if (inv.itemSlot[i] != null && inv.itemSlot[i].itemKey == key) { n += inv.itemSlot[i].stack; }
            }
            ItemSlot[] bag = inv != null ? PickupCascade.CharacterBag(mn, inv) : null;
            for (int i = 0; bag != null && i < bag.Length; i++)
            {
                if (bag[i] != null && bag[i].itemKey == key) { n += bag[i].stack; }
            }
            return n;
        }

        // After a write by hand to a ground cell (InventoryWindow.ReportWrite): an
        // item emptied - lifted into the hand, used up - is gone from the ground, and
        // its object goes as the game's own pickup ends, destroyed.
        internal static void AfterWrite(ItemSlot s)
        {
            if (s != null && (string.IsNullOrEmpty(s.itemKey) || s.stack <= 0))
            {
                Object.Destroy(s.gameObject);
            }
        }

        // ---------- harness doors ----------

        // What the ground holds near you: every loose item within 6 m, its layer and
        // distance, and the piles the vicinity would list.
        internal static string Probe()
        {
            ManagersScript mn = GameObject.Find("Managers")?.GetComponent<ManagersScript>();
            if (mn == null || mn.gameMN == null || mn.gameMN.pMove == null || mn.inventory == null)
            {
                return "no player";
            }
            Vector3 here = mn.gameMN.pMove.transform.position;
            var sb = new System.Text.StringBuilder();
            GameObject prefab = mn.inventory.dropItemObj;
            sb.Append("prefab ").Append(prefab != null ? prefab.name + " layer " + prefab.layer + " mask " + Mask(prefab) : "none").Append('\n');
            int all = 0;
            foreach (ItemSlot s in Object.FindObjectsOfType<ItemSlot>())
            {
                if (!IsLoose(mn, s))
                {
                    continue;
                }
                all++;
                float d = Vector3.Distance(here, s.transform.position);
                if (d <= 6f)
                {
                    sb.Append("  ").Append(s.itemKey).Append(" x").Append(s.stack).Append(" @ ").Append(d.ToString("0.0"))
                        .Append(" m, layer ").Append(s.gameObject.layer).Append('\n');
                }
            }
            sb.Append("loose in the scene ").Append(all).Append(", by physics within reach ")
                .Append(Within(mn, here, ReachRule.Near).Count).Append('\n');
            // the bags' free cells, and an item carried that does not stack - what a
            // test fills the bags with
            int free = 0;
            string single = null;
            ItemSlot[] bag = PickupCascade.CharacterBag(mn, mn.inventory);
            var cells = new List<ItemSlot>();
            for (int i = 0; i < mn.inventory.baseCount; i++) { cells.Add(mn.inventory.itemSlot[i]); }
            if (bag != null) { cells.AddRange(bag); }
            foreach (ItemSlot c in cells)
            {
                if (c == null) { continue; }
                if (string.IsNullOrEmpty(c.itemKey) || c.stack <= 0) { free++; continue; }
                ItemData d = mn.itemMN.FindItem(c.itemKey);
                if (single == null && d != null && d.maxStack == 1 && c.common == null) { single = c.itemKey; }
            }
            sb.Append("bags free ").Append(free).Append(", one-stack item ").Append(single ?? "none").Append('\n');
            var piles = new List<InventorySlot>();
            Scan(mn, here, piles);
            foreach (InventorySlot p in piles)
            {
                sb.Append("pile @ ").Append(Vector3.Distance(here, p.transform.position).ToString("0.0")).Append(" m:");
                foreach (ItemSlot s in p.slots) { sb.Append(' ').Append(s.itemKey).Append('x').Append(s.stack); }
                sb.Append('\n');
            }
            return sb.ToString();
        }

        // Puts `copies` of `count` x `key` on the ground around you, through the
        // game's own drop object, scattered `spread` metres - a pile, or apart.
        internal static string Spawn(string key, int count, int copies, float spread)
        {
            ManagersScript mn = GameObject.Find("Managers")?.GetComponent<ManagersScript>();
            ItemData data = mn != null && mn.itemMN != null ? mn.itemMN.FindItem(key) : null;
            if (data == null || mn.inventory == null || mn.inventory.stockSlot == null
                || !string.IsNullOrEmpty(mn.inventory.stockSlot.itemKey))
            {
                return "no such item, or the scratch cell is busy";
            }
            for (int i = 0; i < copies; i++)
            {
                mn.itemMN.ItemToSlot(data, mn.inventory.stockSlot, count);
                ItemSlot w = mn.inventory.MakeDropItem();
                mn.inventory.SlotToSlot(mn.inventory.stockSlot, w);
                Vector2 off = Random.insideUnitCircle * spread;
                w.transform.position += new Vector3(off.x, 0f, off.y);
            }
            InventoryWindow.Invalidate();
            return "dropped " + copies + " x " + key + " x" + count;
        }
    }
}
