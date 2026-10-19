using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using HarmonyLib;
using UnityEngine;

namespace IslandUI
{
    // THE UI'S STATE INSIDE THE GAME'S OWN SAVE: our own block inside the save
    // file, chosen over YotanModCore's DataStore and a file beside the save. The
    // action bar's bindings, the favourite marks and the tool
    // board's hooks are written as one <IslandUI> element into SaveDataNN.xml, and
    // read back from the same file on load.
    //
    // Why inside, not beside: a store beside the save is a second save file, and it
    // disagreed with the first in every direction once (inventory-window-ux.md, the
    // favourites persistence entry). In the file it is written only when the game saves,
    // read only when the game loads, and copied with the save.
    //
    // WRITE. The game serializes SaveDataBase and closes the file, then calls
    // SaveTextChange(saveID); our element goes in there, just before the root's
    // closing tag, through a temporary file swapped over the save so a failure can
    // never leave a half-written one. The snapshot is taken when the game's own
    // gathering (TempToSave) has finished - that step closes a vanilla container
    // panel first, which puts mirrored items back in their real cells.
    //
    // READ. The game's loader is a plain XmlSerializer, which skips elements it has
    // no member for, so the element costs vanilla nothing - with or without this
    // mod installed. LoadStart / LoadDirectoryStart name the file; our element is
    // read from it then and applied when Loading() has finished refilling the cells.
    //
    // A MARK IS AN ADDRESS PLUS A KEY. Favourites and hooks are marks on ItemSlot
    // objects, which do not survive a load. The game saves your cells by index -
    // the Shared Bag's, and each player character's bag and equipment - so the
    // address round-trips, and the item key is the check: a mark comes back only
    // where that cell still holds that item.
    [HarmonyPatch]
    internal static class SaveBlock
    {
        private const string Tag = "IslandUI";
        private const string RootClose = "</SaveDataBase>";
        private const string SaveListClose = "</save>";

        private static int _savingId = -1;
        private static string _pendingWrite;
        private static string _pendingLoad;
        private static string _pendingLoadFrom;

        private static ManagersScript Mn()
        {
            GameObject managers = GameObject.Find("Managers");
            return managers != null ? managers.GetComponent<ManagersScript>() : null;
        }

        private static string SlotPath(int saveID)
        {
            return Application.dataPath + "/StreamingAssets/XML/SaveData" + saveID.ToString("00") + ".xml";
        }

        // ---------- saving ----------

        // A second press while a save runs is turned away by the coroutine itself
        // (nowSaving), and must not re-aim the running one at its slot.
        [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.SaveCoroutine))]
        [HarmonyPrefix]
        private static void SaveCoroutine_Prefix(int saveID)
        {
            if (!SaveManager.nowSaving)
            {
                _savingId = saveID;
                _pendingWrite = null;
            }
        }

        [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.TempToSave))]
        [HarmonyPostfix]
        private static void TempToSave_Postfix(ref IEnumerator __result)
        {
            __result = SnapshotAfter(__result);
        }

        private static IEnumerator SnapshotAfter(IEnumerator inner)
        {
            while (inner.MoveNext())
            {
                yield return inner.Current;
            }
            try
            {
                _pendingWrite = Build(Mn());
            }
            catch (Exception e)
            {
                _pendingWrite = null;
                Plugin.Log.LogWarning("Save block: snapshot failed, this save keeps no UI state - " + e);
            }
        }

        // Also called for slot 00 while a save to another slot backs the old file up
        // there; that copy already carries the old file's element, so only the slot
        // being saved is written.
        [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.SaveTextChange))]
        [HarmonyPostfix]
        private static void SaveTextChange_Postfix(int saveID)
        {
            if (!SaveManager.nowSaving || saveID != _savingId || _pendingWrite == null)
            {
                return;
            }
            string block = _pendingWrite;
            _pendingWrite = null;
            _savingId = -1;
            try
            {
                var watch = Stopwatch.StartNew();
                Inject(SlotPath(saveID), block);
                Plugin.Log.LogInfo($"Save block: written into slot {saveID:00} in {watch.ElapsedMilliseconds} ms");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Save block: could not write into slot {saveID:00}; the game's save is untouched - " + e);
            }
        }

        // ---------- loading ----------

        [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.LoadStart))]
        [HarmonyPrefix]
        private static void LoadStart_Prefix(int loadID)
        {
            ReadPending(SlotPath(loadID));
        }

        // the same path LoadDirectoryStart builds for a downloaded save
        [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.LoadDirectoryStart))]
        [HarmonyPrefix]
        private static void LoadDirectoryStart_Prefix(string dataName)
        {
            string path = dataName != null && (dataName.Contains("2739590") || dataName.Contains("steamapps"))
                ? dataName
                : Path.Combine(Application.dataPath + "/StreamingAssets/DLSave", dataName ?? "");
            ReadPending(path);
        }

        private static void ReadPending(string path)
        {
            MoodLedger.Clear();   // a load starts from the save's record, not this session's
            _pendingLoadFrom = path;
            try
            {
                _pendingLoad = File.Exists(path) ? Extract(ReadText(path, out _)) : null;
            }
            catch (Exception e)
            {
                _pendingLoad = null;
                Plugin.Log.LogWarning("Save block: could not read " + path + " - " + e);
            }
        }

        [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.Loading))]
        [HarmonyPostfix]
        private static void Loading_Postfix(ref IEnumerator __result)
        {
            __result = ApplyAfter(__result);
        }

        private static IEnumerator ApplyAfter(IEnumerator inner)
        {
            while (inner.MoveNext())
            {
                yield return inner.Current;
            }
            string block = _pendingLoad;
            string from = _pendingLoadFrom;
            _pendingLoad = null;
            _pendingLoadFrom = null;
            if (block == null)
            {
                Plugin.Log.LogInfo("Save block: " + (from != null ? Path.GetFileName(from) : "this save") + " keeps no UI state");
                yield break;
            }
            try
            {
                Plugin.Log.LogInfo("Save block: " + Apply(Mn(), block, dryRun: false));
                InventoryWindow.Invalidate();
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Save block: could not apply the saved UI state - " + e);
            }
        }

        // ---------- across a hot reload ----------

        // THE SAME STATE, HANDED TO THE NEXT GENERATION: a reload would otherwise
        // wipe the state of the UI components, like favourites and the action bar.
        // A reload is a new assembly with its own empty statics, and no
        // load follows it to put the save's marks back. The AppDomain is the one
        // place both generations reach without knowing the other exists, and the
        // harness stays out of it.
        //
        // Live references, not the save's addresses: the ItemSlots are the game's
        // and outlive the swap, so a mark on a chest cell - which the save cannot
        // address - comes back too. Only Assembly-CSharp and mscorlib types cross.
        private const string HandoverKey = "IslandUI.SaveBlock.handover";

        // Teardown, after close-all has put mirrored items back in their cells.
        internal static void HandOver()
        {
            var hooks = new ItemSlot[ToolBelt.Hooks];
            for (int h = 0; h < hooks.Length; h++)
            {
                List<ItemSlot> hung = ToolBelt.HungSlots(h);
                hooks[h] = hung.Count > 0 ? hung[0] : null;
            }
            AppDomain.CurrentDomain.SetData(HandoverKey, new object[]
            {
                ActionBar.SnapshotBindings(), Favourites.Slots().ToArray(), hooks,
            });
        }

        // Awake. Nothing waiting (a boot, not a reload) is the ordinary case.
        internal static void TakeOver()
        {
            object[] state = AppDomain.CurrentDomain.GetData(HandoverKey) as object[];
            AppDomain.CurrentDomain.SetData(HandoverKey, null);
            if (state == null || state.Length != 3
                || !(state[0] is string[] bar) || !(state[1] is ItemSlot[] favs)
                || !(state[2] is ItemSlot[] hooks))
            {
                return;
            }
            int bound = 0, marked = 0, hung = 0;
            foreach (string k in bar)
            {
                if (!string.IsNullOrEmpty(k)) { bound++; }
            }
            ActionBar.RestoreBindings(bar);
            foreach (ItemSlot slot in favs)
            {
                // a destroyed slot compares equal to null; the scene is the same one
                if (slot != null && !string.IsNullOrEmpty(slot.itemKey))
                {
                    Favourites.Set(slot, true);
                    marked++;
                }
            }
            for (int h = 0; h < hooks.Length && h < ToolBelt.Hooks; h++)
            {
                if (hooks[h] != null && ToolBelt.Fits(h, hooks[h].toolType))
                {
                    ToolBelt.Hang(h, hooks[h]);
                    hung++;
                }
            }
            InventoryWindow.Invalidate();
            Plugin.Log.LogInfo($"Save block: reload kept {bound} binding(s), {marked} favourite(s), {hung} hook(s)");
        }

        // ---------- the element ----------

        private static string Build(ManagersScript mn)
        {
            if (mn == null || mn.inventory == null || mn.gameMN == null)
            {
                return null;
            }
            var body = new StringBuilder();
            string[] bar = ActionBar.SnapshotBindings();
            for (int i = 0; i < bar.Length; i++)
            {
                if (!string.IsNullOrEmpty(bar[i]))
                {
                    body.Append("    <Bar i=\"").Append(i).Append("\" key=\"").Append(Esc(bar[i])).Append("\" />\n");
                }
            }
            var seen = new HashSet<string>();
            foreach (ItemSlot slot in Favourites.Slots())
            {
                string at = AddressOf(mn, slot);
                if (at != null && seen.Add(at))
                {
                    body.Append("    <Fav ").Append(at).Append(" key=\"").Append(Esc(slot.itemKey)).Append("\" />\n");
                }
            }
            for (int h = 0; h < ToolBelt.Hooks; h++)
            {
                foreach (ItemSlot slot in ToolBelt.HungSlots(h))
                {
                    string at = AddressOf(mn, slot);
                    if (at != null)
                    {
                        body.Append("    <Hook h=\"").Append(h).Append("\" ").Append(at)
                            .Append(" key=\"").Append(Esc(slot.itemKey)).Append("\" />\n");
                        break;
                    }
                }
            }
            MoodLedger.WriteTo(body);
            if (body.Length == 0)
            {
                return null;   // nothing to keep: the file is left as the game wrote it
            }
            return "  <" + Tag + " v=\"1\">\n" + body + "  </" + Tag + ">\n";
        }

        // Puts the element back into live state, or only counts what would come back.
        private static string Apply(ManagersScript mn, string block, bool dryRun)
        {
            if (mn == null || mn.inventory == null || mn.gameMN == null)
            {
                return "skipped: no managers";
            }
            var doc = new XmlDocument();
            doc.LoadXml(block);
            var keys = new List<string>();
            int favs = 0, hooks = 0, moods = 0, dropped = 0;
            foreach (XmlNode node in doc.DocumentElement.ChildNodes)
            {
                var e = node as XmlElement;
                if (e == null)
                {
                    continue;
                }
                string key = e.GetAttribute("key");
                switch (e.Name)
                {
                    case "Bar":
                        int i = Int(e, "i");
                        if (i < 0 || i > 64 || string.IsNullOrEmpty(key))
                        {
                            dropped++;
                            break;
                        }
                        while (keys.Count <= i)
                        {
                            keys.Add(null);
                        }
                        keys[i] = key;
                        break;
                    case "Fav":
                    {
                        ItemSlot slot = Resolve(mn, e);
                        if (slot == null || string.IsNullOrEmpty(key) || slot.itemKey != key)
                        {
                            dropped++;
                            break;
                        }
                        if (!dryRun)
                        {
                            Favourites.Set(slot, true);
                        }
                        favs++;
                        break;
                    }
                    case "Hook":
                    {
                        int h = Int(e, "h");
                        ItemSlot slot = Resolve(mn, e);
                        if (h < 0 || h >= ToolBelt.Hooks || slot == null || string.IsNullOrEmpty(key)
                            || slot.itemKey != key || !ToolBelt.Fits(h, slot.toolType))
                        {
                            dropped++;
                            break;
                        }
                        if (!dryRun)
                        {
                            ToolBelt.Hang(h, slot);
                        }
                        hooks++;
                        break;
                    }
                    case "Mood":
                        if (MoodLedger.Restore(mn, Int(e, "f"), e.GetAttribute("e"), dryRun)) { moods++; }
                        else { dropped++; }
                        break;
                }
            }
            int bound = 0;
            foreach (string k in keys)
            {
                if (!string.IsNullOrEmpty(k)) { bound++; }
            }
            if (!dryRun && keys.Count > 0)
            {
                ActionBar.RestoreBindings(keys);
            }
            return $"{bound} binding(s), {favs} favourite(s), {hooks} hook(s), {moods} mood record(s)"
                + (dropped > 0 ? $", {dropped} dropped (their cell or person is gone)" : "");
        }

        // Your cells as the save stores them: the Shared Bag by index, and each
        // player character's bag and equipment by character and index.
        private static string AddressOf(ManagersScript mn, ItemSlot slot)
        {
            InventoryManager inv = mn.inventory;
            for (int i = 0; i < inv.baseCount && i < inv.itemSlot.Length; i++)
            {
                if (ReferenceEquals(inv.itemSlot[i], slot))
                {
                    return "in=\"shared\" i=\"" + i + "\"";
                }
            }
            CommonStates[] players = mn.gameMN.playerCommons;
            for (int p = 0; players != null && p < players.Length; p++)
            {
                CommonStates who = players[p];
                if (who == null)
                {
                    continue;
                }
                ItemSlot[] bag = who.bag != null ? who.bag.slots : null;
                for (int i = 0; bag != null && i < bag.Length; i++)
                {
                    if (ReferenceEquals(bag[i], slot))
                    {
                        return "in=\"bag\" who=\"" + p + "\" i=\"" + i + "\"";
                    }
                }
                for (int i = 0; who.equip != null && i < who.equip.Length; i++)
                {
                    if (ReferenceEquals(who.equip[i], slot))
                    {
                        return "in=\"equip\" who=\"" + p + "\" i=\"" + i + "\"";
                    }
                }
            }
            return null;
        }

        private static ItemSlot Resolve(ManagersScript mn, XmlElement e)
        {
            int i = Int(e, "i");
            if (i < 0)
            {
                return null;
            }
            string where = e.GetAttribute("in");
            if (where == "shared")
            {
                InventoryManager inv = mn.inventory;
                return i < inv.baseCount && i < inv.itemSlot.Length ? inv.itemSlot[i] : null;
            }
            int p = Int(e, "who");
            CommonStates[] players = mn.gameMN.playerCommons;
            CommonStates who = players != null && p >= 0 && p < players.Length ? players[p] : null;
            if (who == null)
            {
                return null;
            }
            ItemSlot[] cells = where == "bag" ? (who.bag != null ? who.bag.slots : null)
                : where == "equip" ? who.equip : null;
            return cells != null && i < cells.Length ? cells[i] : null;
        }

        private static int Int(XmlElement e, string name)
        {
            return int.TryParse(e.GetAttribute(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : -1;
        }

        private static string Esc(string s)
        {
            return SecurityElement.Escape(s);
        }

        // ---------- the file ----------

        private static string ReadText(string path, out bool bom)
        {
            byte[] bytes = File.ReadAllBytes(path);
            bom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
            int skip = bom ? 3 : 0;
            return new UTF8Encoding(false).GetString(bytes, skip, bytes.Length - skip);
        }

        // Our element, if the file has one. It is only ever looked for after the
        // game's own <save> list, so nothing inside the game's data can be taken for it.
        private static string Extract(string text)
        {
            int from = text.LastIndexOf(SaveListClose, StringComparison.Ordinal);
            int start = from >= 0 ? text.IndexOf("<" + Tag, from, StringComparison.Ordinal) : -1;
            if (start < 0)
            {
                return null;
            }
            string close = "</" + Tag + ">";
            int end = text.IndexOf(close, start, StringComparison.Ordinal);
            return end < 0 ? null : text.Substring(start, end + close.Length - start);
        }

        private static void Inject(string path, string block)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("the save the game just wrote is not there", path);
            }
            string text = ReadText(path, out bool bom);
            // one element only: a block already there (a file written by a test, or
            // saved twice) is replaced, not repeated
            string old = Extract(text);
            if (old != null)
            {
                int at = text.LastIndexOf(old, StringComparison.Ordinal);
                int lineStart = text.LastIndexOf('\n', at) + 1;
                int lineEnd = at + old.Length;
                if (lineEnd < text.Length && text[lineEnd] == '\r') { lineEnd++; }
                if (lineEnd < text.Length && text[lineEnd] == '\n') { lineEnd++; }
                text = text.Remove(lineStart, lineEnd - lineStart);
            }
            int close = text.LastIndexOf(RootClose, StringComparison.Ordinal);
            if (close < 0)
            {
                throw new InvalidDataException("no </SaveDataBase> in " + path);
            }
            string newline = text.Contains("\r\n") ? "\r\n" : "\n";
            string output = text.Substring(0, close) + block.Replace("\n", newline) + text.Substring(close);

            string tmp = path + ".islandui-tmp";
            using (var stream = new FileStream(tmp, FileMode.Create, FileAccess.Write))
            {
                if (bom)
                {
                    stream.Write(new byte[] { 0xEF, 0xBB, 0xBF }, 0, 3);
                }
                byte[] data = new UTF8Encoding(false).GetBytes(output);
                stream.Write(data, 0, data.Length);
                stream.Flush();
            }
            try
            {
                File.Replace(tmp, path, null);
            }
            catch (Exception)
            {
                File.Copy(tmp, path, true);
                File.Delete(tmp);
            }
        }

        // ---------- harness door ----------

        // Against a COPY of a save slot, never the slot itself: the live UI state
        // (or a stand-in binding when there is none) is injected into the copy, the
        // copy is deserialized by the game's own SaveDataBase serializer, the element
        // is read back and must match, and the live state's addresses must resolve
        // back to cells holding the same items. The copy is deleted afterwards.
        internal static string SelfTest(int slot)
        {
            string path = SlotPath(slot);
            if (!File.Exists(path))
            {
                return "skipped: no save in slot " + slot.ToString("00");
            }
            ManagersScript mn = Mn();
            string live = Build(mn);
            string block = live ?? "  <" + Tag + " v=\"1\">\n    <Bar i=\"0\" key=\"selftest_key\" />\n  </" + Tag + ">\n";
            string copy = Path.Combine(Application.temporaryCachePath, "islandui-saveblock-selftest.xml");
            var log = new StringBuilder("slot " + slot.ToString("00") + (live != null ? " with the live UI state" : " with a stand-in binding") + ":");
            try
            {
                File.Copy(path, copy, true);
                var watch = Stopwatch.StartNew();
                Inject(copy, block);
                log.Append(" inject ").Append(watch.ElapsedMilliseconds).Append(" ms;");
                Inject(copy, block);   // a second write must replace, not repeat
                string text = ReadText(copy, out _);
                int count = 0;
                for (int at = text.IndexOf("<" + Tag + " ", StringComparison.Ordinal); at >= 0;
                     at = text.IndexOf("<" + Tag + " ", at + 1, StringComparison.Ordinal))
                {
                    count++;
                }
                log.Append(" one element ").Append(count == 1 ? "ok" : "FAIL (" + count + ")").Append(';');
                string back = Extract(text);
                string expected = block.Trim().Replace("\n", text.Contains("\r\n") ? "\r\n" : "\n");
                log.Append(" read back ").Append(back == expected ? "ok" : "FAIL").Append(';');

                watch = Stopwatch.StartNew();
                SaveManager.SaveDataBase db;
                using (var stream = new FileStream(copy, FileMode.Open, FileAccess.Read))
                {
                    db = new XmlSerializer(typeof(SaveManager.SaveDataBase)).Deserialize(stream) as SaveManager.SaveDataBase;
                }
                bool loads = db != null && db.save != null && db.save.Count == 1 && db.save[0].invItem != null;
                log.Append(" game's loader ").Append(loads ? "ok" : "FAIL").Append(" (").Append(watch.ElapsedMilliseconds).Append(" ms);");
                if (back != null)
                {
                    log.Append(" live addresses: ").Append(live != null ? Apply(mn, back, dryRun: true) : "none to check");
                }
            }
            catch (Exception e)
            {
                log.Append(" FAIL ").Append(e.GetType().Name).Append(": ").Append(e.Message);
            }
            finally
            {
                try { File.Delete(copy); } catch (Exception) { }
            }
            return log.ToString();
        }
    }
}
