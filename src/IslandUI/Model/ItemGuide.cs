using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace IslandUI
{
    // HOW TO GET AN ITEM, as a player would tell you: diamonds, say, are also found
    // in the dungeon, which is probably their main source - knowledge that has to be
    // extracted from guides.
    //
    // Reading the world out of the scene said what exists,
    // not what is useful: the Ruins are generated on each visit, and chests, trades
    // and workers are no DropItem. So the answer is written down - Data/item-guide.tsv,
    // one line per item id, our own English from the facts the community wiki
    // collects (madisland.wikiru.jp, "アイテム一覧"), and shipped inside the DLL.
    //
    // The file: `item_id <TAB> how to get it`, `#` starts a comment. Places and items
    // are named as the game names them in English, so a line can be checked against
    // what the player sees.
    internal static class ItemGuide
    {
        private const string Resource = "IslandUI.item-guide.tsv";
        private static Dictionary<string, string> _lines;

        internal static string Of(string itemKey)
        {
            if (string.IsNullOrEmpty(itemKey))
            {
                return null;
            }
            if (_lines == null)
            {
                _lines = Load();
            }
            return _lines.TryGetValue(itemKey, out string line) ? line : null;
        }

        private static Dictionary<string, string> Load()
        {
            var lines = new Dictionary<string, string>();
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(Resource))
            {
                if (stream == null)
                {
                    Plugin.Log.LogWarning("Item guide: " + Resource + " is not in the assembly");
                    return lines;
                }
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    string row;
                    while ((row = reader.ReadLine()) != null)
                    {
                        if (row.Length == 0 || row[0] == '#')
                        {
                            continue;
                        }
                        int tab = row.IndexOf('\t');
                        if (tab <= 0 || tab == row.Length - 1)
                        {
                            continue;
                        }
                        // "; " separates the ways to get it; each gets its own line
                        lines[row.Substring(0, tab).Trim()] = row.Substring(tab + 1).Trim().Replace("; ", "\n");
                    }
                }
            }
            Plugin.Log.LogInfo("Item guide: " + lines.Count + " items");
            return lines;
        }

        // Harness door for writing the guide: the game's own names for a list of item
        // ids, one per line in `idsPath`, written to `outPath` as
        // `id <TAB> name (lang 0) <TAB> name in each localized slot`.
        internal static string DumpNames(string idsPath, string outPath)
        {
            ManagersScript mn = UnityEngine.GameObject.Find("Managers")?.GetComponent<ManagersScript>();
            if (mn == null || mn.itemMN == null)
            {
                return "skipped: no managers";
            }
            var sb = new StringBuilder();
            int found = 0, missing = 0;
            foreach (string raw in File.ReadAllLines(idsPath, Encoding.UTF8))
            {
                string id = raw.Trim();
                if (id.Length == 0) { continue; }
                ItemData data = mn.itemMN.FindItem(id);
                sb.Append(id);
                if (data == null)
                {
                    missing++;
                    sb.Append("\t?\n");
                    continue;
                }
                found++;
                sb.Append('\t').Append(data.itemName);
                if (data.localizeName != null)
                {
                    for (int i = 0; i < data.localizeName.Length; i++)
                    {
                        sb.Append('\t').Append(data.localizeName[i] ?? "");
                    }
                }
                sb.Append('\n');
            }
            File.WriteAllText(outPath, sb.ToString(), new UTF8Encoding(false));
            return found + " named, " + missing + " unknown -> " + outPath;
        }

        // ...and every creature's, by npcID: `id <TAB> name (lang 0) <TAB> localized...`
        internal static string DumpCreatures(string outPath)
        {
            ManagersScript mn = UnityEngine.GameObject.Find("Managers")?.GetComponent<ManagersScript>();
            if (mn == null || mn.npcMN == null || mn.npcMN.npcPrefab == null)
            {
                return "skipped: no managers";
            }
            var sb = new StringBuilder();
            int named = 0;
            for (int id = 0; id < mn.npcMN.npcPrefab.Length; id++)
            {
                StatsData stats = mn.npcMN.npcPrefab[id] != null ? mn.npcMN.GetStatsData(id) : null;
                if (stats == null) { continue; }
                named++;
                sb.Append(id).Append('\t').Append(stats.charaName);
                if (stats.localizedName != null)
                {
                    foreach (string name in stats.localizedName) { sb.Append('\t').Append(name ?? ""); }
                }
                sb.Append('\n');
            }
            File.WriteAllText(outPath, sb.ToString(), new UTF8Encoding(false));
            return named + " creatures -> " + outPath;
        }
    }
}
