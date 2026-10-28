using UnityEngine;

namespace IslandUI
{
    // THE LAST BOSS'S BENCH, a one-off (StoryManager's Entking3 fight). Mid-fight the
    // game puts a "Nearby items" bench (bench_surround, recipe panel 28) beside you, its
    // four slots holding the parts, and the fight turns once ev_king3_hammer_01 is in
    // your bag. The bench is nobody's, so every rule that asks "is it yours" passed it
    // over: its recipes left the menu at the first rescan, its parts were not counted,
    // and the vicinity never listed it. While the scene has it out it counts as yours -
    // this bench alone, found by its place in the scene, never a rule for strangers'.
    internal static class BossBench
    {
        private const string Path = "StaticBG/Events/Main_Entking3/Bench";
        internal const int Panel = 28;

        private static InventorySlot _bench;

        // the bench while the fight has it out, else null
        internal static InventorySlot Active()
        {
            if (_bench == null)
            {
                GameObject go = GameObject.Find(Path);   // finds it only while it is out
                _bench = go != null ? go.GetComponent<InventorySlot>() : null;
            }
            return _bench != null && _bench.gameObject.activeInHierarchy ? _bench : null;
        }

        internal static bool Is(InventorySlot container)
        {
            return container != null && ReferenceEquals(container, Active());
        }

        internal static bool Is(ItemInfo prop)
        {
            return prop != null && Is(prop.GetComponent<InventorySlot>());
        }

        // Harness door: the bench's recipes, what each needs, and what its slots hold
        internal static string Probe()
        {
            ManagersScript mn = GameObject.Find("Managers")?.GetComponent<ManagersScript>();
            if (mn == null) { return "no managers"; }
            var sb = new System.Text.StringBuilder();
            foreach (CraftInfo recipe in mn.craftMN.craftData[Panel].craftInfo)
            {
                if (recipe == null) { continue; }
                sb.Append(recipe.name).Append(" <-");
                foreach (CraftInfo.Required r in recipe.required)
                {
                    ItemData d = r != null ? r.itemData : null;
                    sb.Append(' ').Append(d != null ? d.name : "null").Append(" x").Append(r != null ? r.count : 0)
                        .Append(d != null && d.itemSprite == null ? " (no sprite)" : "")
                        .Append(" [").Append(d != null ? d.GetItemName() : "").Append(']');
                }
                sb.Append('\n');
            }
            InventorySlot bench = Active();
            sb.Append("bench: ").Append(bench == null ? "not out" : "");
            if (bench != null)
            {
                foreach (ItemSlot s in bench.slots)
                {
                    sb.Append(s != null ? s.itemKey + " x" + s.stack : "null").Append("; ");
                }
            }
            return sb.ToString();
        }

        // out, and within the bench's reach of `from`
        internal static InventorySlot Live(Vector3 from)
        {
            InventorySlot bench = Active();
            return bench != null && ReachRule.Live(bench, from, ReachRule.Workshop) ? bench : null;
        }
    }
}
