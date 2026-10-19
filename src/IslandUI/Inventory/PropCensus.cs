using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace IslandUI
{
    // WHAT SHAPE IS A PROP? A one-off census, run from the harness against a real
    // base (window-system.md §6.24, D82).
    //
    // The question it settles: our window's contents come from what a target HAS
    // (D76), and that worked until the well - which carries an InventorySlot whose
    // single slot is a WATER LEVEL, not a bag. Presence gave the right tab for the
    // wrong reason. So before writing display rules we count what actually exists,
    // rather than what ItemData.SubType and BreedInfo.BreedType between them permit.
    //
    // It reads and reports. It calls nothing on vanilla - in particular not
    // PropPanelTypeChange, which would touch uiTarget and propActProgress on live
    // objects, and which is the panel D71 rings off.
    internal static class PropCensus
    {
        private struct Shape
        {
            public string SubType;
            public string Breed;
            public int Slots;
            public bool Life;
            public bool Scaler;
            public bool Position;
        }

        private static string Key(Shape s)
        {
            return s.SubType + " | breed " + s.Breed + " | slots " + s.Slots
                + (s.Life ? " | life" : "") + (s.Scaler ? " | scaler" : "")
                + (s.Position ? " | position" : "");
        }

        internal static string Run(ManagersScript mn)
        {
            if (mn == null || mn.itemMN == null)
            {
                return "no managers";
            }
            var shapes = new Dictionary<string, List<string>>();
            var counts = new Dictionary<string, int>();
            int seen = 0;

            // every placed prop in the world, not every item that could exist -
            // the point is what a real base contains
            ItemInfo[] all = Object.FindObjectsOfType<ItemInfo>();
            for (int i = 0; i < all.Length; i++)
            {
                ItemInfo info = all[i];
                if (info == null || string.IsNullOrEmpty(info.itemKey))
                {
                    continue;
                }
                seen++;
                var slot = info.GetComponent<InventorySlot>();
                var breed = info.GetComponent<BreedInfo>();
                ItemData data = null;
                try { data = mn.itemMN.FindItem(info.itemKey); }
                catch (System.Exception) { }

                var shape = new Shape
                {
                    SubType = data != null ? data.subType.ToString() : "?",
                    Breed = breed != null ? breed.breedType.ToString() : "-",
                    Slots = slot != null && slot.slots != null ? slot.slots.Length : 0,
                    Life = info.lifeMax > 0f,
                    Scaler = info.GetComponent<BuildScaler>() != null,
                    Position = info.GetComponent<PositionEdit>() != null,
                };
                string key = Key(shape);
                List<string> keys;
                if (!shapes.TryGetValue(key, out keys))
                {
                    keys = new List<string>();
                    shapes[key] = keys;
                    counts[key] = 0;
                }
                counts[key]++;
                if (!keys.Contains(info.itemKey) && keys.Count < 12)
                {
                    keys.Add(info.itemKey);
                }
            }

            var order = new List<string>(shapes.Keys);
            order.Sort((a, b) => counts[b].CompareTo(counts[a]));
            var sb = new StringBuilder();
            sb.AppendLine("PROP CENSUS - " + seen + " placed props, " + order.Count + " distinct shapes");
            sb.AppendLine("(shape = subType | breed | slot count | life | scaler | position)");
            sb.AppendLine();
            for (int i = 0; i < order.Count; i++)
            {
                string k = order[i];
                sb.AppendLine(string.Format("{0,4}x  {1}", counts[k], k));
                sb.AppendLine("        " + string.Join(", ", shapes[k].ToArray()));
            }
            string text = sb.ToString();
            try
            {
                System.IO.File.WriteAllText(
                    System.IO.Path.Combine(BepInEx.Paths.BepInExRootPath, "prop-census.txt"), text);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning("prop census: " + e.Message);
            }
            return seen + " props, " + order.Count + " shapes (see BepInEx/prop-census.txt)";
        }
    }
}
