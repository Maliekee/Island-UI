using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace IslandUI
{
    // Phase 0 of the vicinity policy work (docs/window-system.md §6.15): find out
    // what is actually out there before deciding what the pool may touch.
    //
    // The question this exists to answer is not "what types are there" - the enum
    // has 31 entries and is a five-second read. It is WHICH OF THEM THE PLAYER
    // ACTUALLY BUILDS, and what engine drives each one. Those are different
    // questions and only the second can be answered from a running save: the
    // engine is decided by a BreedInfo component on a prefab, and there is no
    // table anywhere that lists which prop carries which.
    //
    // AGGREGATED, not a dump of instances. Forty wooden chests are one fact. The
    // key is (itemKey, type, breed, trap) because that is what a policy could be
    // written against - a rule per instance is not a rule.
    //
    // SCENE-WIDE rather than within reach. A census bounded by where the player
    // happens to be standing measures the walk, not the base.
    internal static class ContainerCensus
    {
        // Was a private copy of ScanReach's old type test, kept deliberately so
        // the census could show it disagreeing with IsPlainStorage. It did, in
        // eight owned kinds; both copies are gone and this asks the real gate now.
        // The LEAK column stays: it is what proves the repair, and what would
        // catch the next route that reaches for a container without asking.
        private static bool ReachEligible(InventorySlot c)
        {
            return ContainerPolicy.PoolEligible(c);
        }

        private sealed class Row
        {
            public string ItemKey;
            public string Name;
            public InventorySlot.Type Type;
            public string Breed = "-";
            public string Trap = "-";
            public ContainerPolicy.Engine Engine;
            public int Slots;
            public int Count;
            public int Owned;
            public bool Intercepted;   // IsPlainStorage: we take the panel
            public bool InReach;       // ScanReach would pool it
        }

        internal static void Run(ManagersScript mn)
        {
            if (mn == null || !mn.allSet)
            {
                Plugin.Log.LogWarning("[census] no game loaded");
                return;
            }
            InventorySlot[] all = UnityEngine.Object.FindObjectsOfType<InventorySlot>();
            var rows = new Dictionary<string, Row>();
            int bags = 0;

            for (int i = 0; i < all.Length; i++)
            {
                InventorySlot c = all[i];
                if (c == null || c.slots == null || c.slots.Length == 0)
                {
                    continue;
                }
                // A character's bag is a container by every test here and a prop by
                // none of them; counting one row per NPC would drown the table it
                // is meant to inform. Counted, not listed.
                if (c.transform.parent != null
                    && c.transform.parent.GetComponent<CommonStates>() != null)
                {
                    bags++;
                    continue;
                }
                var info = c.GetComponent<ItemInfo>();
                var breed = c.GetComponent<BreedInfo>();
                string key = info != null ? info.itemKey : "(no ItemInfo)";
                ContainerPolicy.Engine engine = ContainerPolicy.EngineOf(c);
                string id = key + "|" + c.type + "|"
                    + (breed != null ? breed.breedType.ToString() : "-") + "|"
                    + c.slots.Length;

                if (!rows.TryGetValue(id, out Row row))
                {
                    ItemData data = info != null && mn.itemMN != null
                        ? mn.itemMN.FindItem(info.itemKey) : null;
                    row = new Row
                    {
                        ItemKey = key,
                        Name = data != null ? data.GetItemName() : "?",
                        Type = c.type,
                        Engine = engine,
                        Slots = c.slots.Length,
                        Intercepted = VanillaContainerUI.IsPlainStorage(c),
                        InReach = ReachEligible(c),
                    };
                    if (breed != null)
                    {
                        row.Breed = breed.breedType.ToString();
                        row.Trap = breed.trapType.ToString();
                    }
                    rows[id] = row;
                }
                row.Count++;
                if (info != null && mn.buildMN != null && mn.buildMN.OwnCheck(info))
                {
                    row.Owned++;
                }
            }

            var list = new List<Row>(rows.Values);
            // Engine first, because the engine is the finding; owned count second,
            // because a prop the player has never built is a row you can skip.
            list.Sort((a, b) =>
            {
                int e = a.Engine.CompareTo(b.Engine);
                if (e != 0) { return e; }
                int o = b.Owned.CompareTo(a.Owned);
                return o != 0 ? o : string.CompareOrdinal(a.ItemKey, b.ItemKey);
            });

            Emit(list, bags, all.Length);
        }

        private static void Emit(List<Row> list, int bags, int total)
        {
            var tsv = new StringBuilder();
            tsv.Append("engine\titemKey\tname\ttype\tbreed\ttrap\tslots\tcount\towned")
               .Append("\tintercepted\tinPool\tLEAK\n");

            int leaks = 0;
            var log = new StringBuilder();
            log.Append("[census] ").Append(total).Append(" InventorySlot in scene, ")
               .Append(bags).Append(" of them character bags, ")
               .Append(list.Count).Append(" distinct prop kinds\n");

            for (int i = 0; i < list.Count; i++)
            {
                Row r = list[i];
                // THE LEAK: in the pool, not ours to intercept, and no callback
                // to make a write whole. Breed props were exactly this until D39
                // gave them one, which is why the test asks the ENGINE rather
                // than repeating `InPool && !Intercepted` - that form flagged
                // every cage in the base and would go on flagging them now that
                // they are correct.
                bool leak = r.InReach && !r.Intercepted
                    && r.Engine != ContainerPolicy.Engine.Breed;
                if (leak && r.Owned > 0)
                {
                    leaks++;
                }
                string line = string.Format(CultureInfo.InvariantCulture,
                    "{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}\t{7}\t{8}\t{9}\t{10}\t{11}",
                    r.Engine, r.ItemKey, r.Name, r.Type, r.Breed, r.Trap, r.Slots,
                    r.Count, r.Owned, r.Intercepted ? "yes" : "no",
                    r.InReach ? "yes" : "no", leak ? "LEAK" : "");
                tsv.Append(line).Append('\n');
                // The console gets the OWNED rows only. Everything the player has
                // never built is noise there and stays in the file.
                if (r.Owned > 0)
                {
                    log.Append("  ").Append(line).Append('\n');
                }
            }
            log.Append("[census] ").Append(leaks)
               .Append(" owned kind(s) the pool touches without owning the panel");

            string path = Path.Combine(BepInEx.Paths.PluginPath, "container-census.tsv");
            try
            {
                File.WriteAllText(path, tsv.ToString());
                log.Append("  ->  ").Append(path);
            }
            catch (Exception e)
            {
                log.Append("  (file write failed: ").Append(e.Message).Append(')');
            }
            Plugin.Log.LogInfo(log.ToString());
        }
    }
}
