using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

namespace IslandUI
{
    // THE GAME'S OWN WORDS, side by side in its three languages, for whoever translates the
    // mods (Forgejo #19): the Japanese and Chinese translations use the game's names for
    // items, creatures, stats and states, so a player reads one vocabulary. A harness door:
    //
    //     game.ps1 invoke IslandUI.GameWords Dump -Args "C:\some\folder"
    //
    // writes four TSVs, each `id <TAB> jp <TAB> en <TAB> cn`: items.tsv (ItemData's name and
    // tip), creatures.tsv (StatsData by npcID), text.tsv (every string array of the three
    // TextManagers: the root is Japanese, its children EN and SC) and tables.tsv (the Unity
    // string tables the scene's labels read).
    internal static class GameWords
    {
        // DOES THE GAME SPEAK THE MODS' LANGUAGE (Forgejo #92)? A label read off one of the
        // game's buttons is in the game's language; when Island UI's is another (Russian, or
        // Japanese over an English game), a word of ours stands in for it.
        internal static bool SpeaksOurs => Shared.Loc.Language == Shared.Loc.GameLanguageCode;

        // THE GAME HAS NO WORDS IN THE MODS' LANGUAGE (Forgejo #110): it has text in Japanese,
        // English and Chinese only. In Russian, Portuguese, Spanish or German, a game word on
        // one of our controls gives way to a key of ours (ControlWords) - unless the game
        // speaks that language itself, through a locale a mod adds.
        internal static bool Lacking => !SpeaksOurs && !HasText(Shared.Loc.Language);

        private static bool HasText(string language)
        {
            return language == "jp" || language == "en" || language == "cn";
        }

        // the button still shows its string table's entry in the game's language, so its
        // words are the ones the entry names, not words the game wrote over them
        internal static bool ShowsEntry(GameObject go, string shown)
        {
            return !string.IsNullOrEmpty(shown) && Entry(go, null) == shown;
        }

        private static TextManager _jp, _en, _cn;

        // THE GAME'S TEXT IN THE MODS' LANGUAGE (Forgejo #92): of the three TextManagers (the
        // root Japanese, its children EN and SC, as UIManager.GetCurrentTextManager picks
        // them), the one Island UI speaks, so a pose or a personality reads in the language
        // of the words around it. A language the game has no text for reads the game's own.
        internal static TextManager Text(ManagersScript mn)
        {
            TextManager current = mn != null ? mn.textMN : null;
            string language = Shared.Loc.Language;
            if (!HasText(language))
            {
                return current;
            }
            if (_jp == null)
            {
                GameObject root = GameObject.Find("TextManager");
                _jp = root != null ? root.GetComponent<TextManager>() : null;
                _en = root != null ? root.transform.Find("EN")?.GetComponent<TextManager>() : null;
                _cn = root != null ? root.transform.Find("SC")?.GetComponent<TextManager>() : null;
            }
            TextManager ours = language == "jp" ? _jp : language == "en" ? _en : _cn;
            return ours != null ? ours : current;
        }

        // the game's locale in the mods' language, or null for a language the game lacks
        internal static Locale OurLocale()
        {
            string language = Shared.Loc.Language;
            string code = language == "jp" ? "ja" : language == "en" ? "en" : language == "cn" ? "zh" : null;
            if (code == null || !LocalizationSettings.HasSettings)
            {
                return null;
            }
            foreach (Locale locale in LocalizationSettings.AvailableLocales.Locales)
            {
                if (locale.Identifier.Code.StartsWith(code))
                {
                    return locale;
                }
            }
            return null;
        }

        // A LABEL OF THE GAME'S, asked of its LocalizeStringEvent rather than read off its
        // text, which an object never shown still has in the prefab's Japanese: in the mods'
        // language where the game has it, else in the game's own. Null when it has none.
        internal static string Label(GameObject go)
        {
            return Entry(go, OurLocale());
        }

        // THE WORDS ON ONE OF THE GAME'S BUTTONS, in the mods' language: the entry of its
        // string table, but only while the button still shows that entry - the game writes
        // other words over some (UIManager's prop acts), and those stay as shown.
        internal static string Translate(GameObject go, string shown)
        {
            if (SpeaksOurs || string.IsNullOrEmpty(shown))
            {
                return shown;
            }
            Locale ours = OurLocale();
            if (ours == null || !ShowsEntry(go, shown))
            {
                return shown;
            }
            return Entry(go, ours) ?? shown;
        }

        // a LocalizeStringEvent's string in that locale (null: the game's)
        private static string Entry(GameObject go, Locale locale)
        {
            var source = go != null ? go.GetComponent<UnityEngine.Localization.Components.LocalizeStringEvent>() : null;
            LocalizedString reference = source != null ? source.StringReference : null;
            if (reference == null || reference.IsEmpty)
            {
                return null;
            }
            string s = LocalizationSettings.StringDatabase.GetLocalizedString(
                reference.TableReference, reference.TableEntryReference, locale);
            return string.IsNullOrEmpty(s) ? null : s;
        }

        internal static string Dump(string folder)
        {
            ManagersScript mn = GameObject.Find("Managers")?.GetComponent<ManagersScript>();
            if (mn == null || mn.itemMN == null)
            {
                return "skipped: no managers";
            }
            Directory.CreateDirectory(folder);
            var report = new List<string>
            {
                Items(mn, Path.Combine(folder, "items.tsv")),
                Creatures(mn, Path.Combine(folder, "creatures.tsv")),
                Text(Path.Combine(folder, "text.tsv")),
                Tables(Path.Combine(folder, "tables.tsv")),
            };
            return string.Join("; ", report.ToArray());
        }

        private static string Cell(string text)
        {
            return (text ?? "").Replace("\t", " ").Replace("\r", "").Replace("\n", "\\n");
        }

        private static void Row(StringBuilder sb, string id, string jp, string en, string cn)
        {
            sb.Append(Cell(id)).Append('\t').Append(Cell(jp)).Append('\t')
                .Append(Cell(en)).Append('\t').Append(Cell(cn)).Append('\n');
        }

        private static string Slot(string[] localized, int i)
        {
            return localized != null && i < localized.Length ? localized[i] : null;
        }

        private static string Items(ManagersScript mn, string path)
        {
            var sb = new StringBuilder("# item id\tjp\ten\tcn (name, then id.tip for the tip)\n");
            int n = 0;
            var seen = new HashSet<string>();
            // every item prefab, as ItemManager.FindItem loads them one by one
            foreach (GameObject go in Resources.LoadAll<GameObject>("Items"))
            {
                ItemData data = go != null ? go.GetComponent<ItemData>() : null;
                if (data == null || !seen.Add(go.name))
                {
                    continue;
                }
                n++;
                Row(sb, go.name, data.itemName, Slot(data.localizeName, 1), Slot(data.localizeName, 2));
                if (!string.IsNullOrEmpty(data.TipsText))
                {
                    Row(sb, go.name + ".tip", data.TipsText, Slot(data.localizedTipsText, 1), Slot(data.localizedTipsText, 2));
                }
            }
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
            return n + " items";
        }

        private static string Creatures(ManagersScript mn, string path)
        {
            var sb = new StringBuilder("# npcID\tjp\ten\tcn\n");
            int n = 0;
            for (int id = 0; mn.npcMN != null && mn.npcMN.npcPrefab != null && id < mn.npcMN.npcPrefab.Length; id++)
            {
                StatsData stats = mn.npcMN.npcPrefab[id] != null ? mn.npcMN.GetStatsData(id) : null;
                if (stats == null)
                {
                    continue;
                }
                n++;
                Row(sb, id.ToString(), stats.charaName, Slot(stats.localizedName, 1), Slot(stats.localizedName, 2));
            }
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
            return n + " creatures";
        }

        private static string Text(string path)
        {
            GameObject root = GameObject.Find("TextManager");
            TextManager jp = root != null ? root.GetComponent<TextManager>() : null;
            TextManager en = root != null ? root.transform.Find("EN")?.GetComponent<TextManager>() : null;
            TextManager cn = root != null ? root.transform.Find("SC")?.GetComponent<TextManager>() : null;
            if (jp == null || en == null || cn == null)
            {
                return "no TextManagers";
            }
            var sb = new StringBuilder("# field[index]\tjp\ten\tcn\n");
            int n = 0;
            foreach (FieldInfo field in typeof(TextManager).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (field.FieldType == typeof(string))
                {
                    Row(sb, field.Name, (string)field.GetValue(jp), (string)field.GetValue(en), (string)field.GetValue(cn));
                    n++;
                    continue;
                }
                if (field.FieldType != typeof(string[]))
                {
                    continue;
                }
                var a = (string[])field.GetValue(jp) ?? new string[0];
                var b = (string[])field.GetValue(en) ?? new string[0];
                var c = (string[])field.GetValue(cn) ?? new string[0];
                int length = Mathf.Max(a.Length, Mathf.Max(b.Length, c.Length));
                for (int i = 0; i < length; i++)
                {
                    Row(sb, field.Name + "[" + i + "]", i < a.Length ? a[i] : null, i < b.Length ? b[i] : null, i < c.Length ? c[i] : null);
                    n++;
                }
            }
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
            return n + " texts";
        }

        // the locales by language, each table's entries by key id
        private static string Tables(string path)
        {
            if (!LocalizationSettings.HasSettings)
            {
                return "no localization settings";
            }
            var codes = new[] { "ja", "en", "zh" };
            var byLocale = new Dictionary<string, Dictionary<string, string>>[codes.Length];
            var order = new List<string>();
            var known = new HashSet<string>();
            for (int l = 0; l < codes.Length; l++)
            {
                byLocale[l] = new Dictionary<string, Dictionary<string, string>>();
                Locale locale = null;
                foreach (Locale candidate in LocalizationSettings.AvailableLocales.Locales)
                {
                    if (candidate.Identifier.Code.StartsWith(codes[l]))
                    {
                        locale = candidate;
                    }
                }
                if (locale == null)
                {
                    continue;
                }
                var handle = LocalizationSettings.StringDatabase.GetAllTables(locale);
                IList<StringTable> tables = handle.WaitForCompletion();
                if (tables == null)
                {
                    continue;
                }
                foreach (StringTable table in tables)
                {
                    foreach (StringTableEntry entry in table.Values)
                    {
                        string key = table.TableCollectionName + "/" + (entry.Key ?? entry.KeyId.ToString());
                        if (!byLocale[l].TryGetValue(table.TableCollectionName, out var entries))
                        {
                            byLocale[l][table.TableCollectionName] = entries = new Dictionary<string, string>();
                        }
                        entries[key] = entry.LocalizedValue;
                        if (known.Add(key))
                        {
                            order.Add(key);
                        }
                    }
                }
            }
            var sb = new StringBuilder("# table/key\tjp\ten\tcn\n");
            foreach (string key in order)
            {
                string table = key.Substring(0, key.IndexOf('/'));
                var cells = new string[codes.Length];
                for (int l = 0; l < codes.Length; l++)
                {
                    cells[l] = byLocale[l].TryGetValue(table, out var entries) && entries.TryGetValue(key, out string v) ? v : null;
                }
                Row(sb, key, cells[0], cells[1], cells[2]);
            }
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
            return order.Count + " table entries";
        }
    }
}
