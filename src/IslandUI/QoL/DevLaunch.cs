using System;
using System.IO;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IslandUI
{
    // Parameterized launch for fast dev iteration.
    //
    // A dev launch script writes BepInEx/devlaunch.txt (a save slot or "latest")
    // before launching; this reads it and DELETES it (one-shot — normal launches
    // stay vanilla), then auto-loads the requested save as soon as the title scene
    // has settled. SaveManager.Load jumps straight to Scene_01, so the whole
    // intro/title sequence disappears — the same shortcut the game's own FastLoad
    // dev component uses.
    internal static class DevLaunch
    {
        private static string _loadSave; // "latest" or a numeric slot
        private static bool _done;
        private static int _titleEnterFrame = -1;

        internal static void Init(string bepinexRoot)
        {
            try
            {
                string path = Path.Combine(bepinexRoot, "devlaunch.txt");
                if (!File.Exists(path))
                {
                    return;
                }
                foreach (string line in File.ReadAllLines(path))
                {
                    string[] kv = line.Split(new[] { '=' }, 2);
                    if (kv.Length == 2 && kv[0].Trim().ToLowerInvariant() == "loadsave")
                    {
                        _loadSave = kv[1].Trim();
                    }
                }
                File.Delete(path); // one-shot: only the parameterized launch auto-loads
                if (_loadSave != null)
                {
                    Plugin.Log.LogInfo($"DevLaunch: will auto-load save '{_loadSave}'");
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"DevLaunch init failed: {e.Message}");
            }
        }

        // Boot-scene redirect: the boot scene's only job is SceneChange("title_01").
        // With a pending dev-launch we set the save statics ourselves (what
        // SaveManager.Load does: loadedID, loaded, timeScale) and go straight to
        // Scene_01 — the title scene never loads at all. The Tick() path below
        // remains as fallback if this misses.
        internal static bool TryBootSkip()
        {
            if (_done || _loadSave == null)
            {
                return false;
            }
            int slot = ResolveSlot();
            if (slot < 0 || !File.Exists(Application.dataPath
                + "/StreamingAssets/XML/SaveView" + slot.ToString("00") + ".xml"))
            {
                Plugin.Log.LogWarning($"DevLaunch: save slot '{_loadSave}' not found — normal boot");
                _done = true;
                return false;
            }
            _done = true;
            SaveManager.SaveStatic.loadedID = slot;
            SaveManager.SaveStatic.loaded = true;
            Time.timeScale = 1f; // SaveManager.Load's GameSpeedReset equivalent
            Plugin.Log.LogInfo($"DevLaunch: boot redirect straight into save slot {slot}");
            return true;
        }

        // called every frame from Plugin.Update
        internal static void Tick()
        {
            if (_done || _loadSave == null)
            {
                return;
            }
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.name.StartsWith("title"))
            {
                _titleEnterFrame = -1;
                return;
            }
            if (_titleEnterFrame < 0)
            {
                _titleEnterFrame = Time.frameCount;
                return;
            }
            if (Time.frameCount - _titleEnterFrame < 5)
            {
                return; // let the title scene's own Start coroutines settle
            }
            _done = true;
            try
            {
                AutoLoad();
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"DevLaunch auto-load failed: {e}");
            }
        }

        private static void AutoLoad()
        {
            // NOTE: FastLoad's `storyable` guard deliberately NOT copied — it is true
            // for ordinary story-mode saves (it protected the devs' sandbox testing,
            // not save loading; the Continue button loads regardless of it)
            SaveManager save = GameObject.Find("SaveManager") != null
                ? GameObject.Find("SaveManager").GetComponent<SaveManager>()
                : null;
            if (save == null)
            {
                Plugin.Log.LogWarning("DevLaunch: SaveManager not found on title scene");
                return;
            }
            int slot = ResolveSlot();
            if (slot < 0)
            {
                Plugin.Log.LogWarning("DevLaunch: no save slot found to load");
                return;
            }
            Plugin.Log.LogInfo($"DevLaunch: auto-loading save slot {slot}");
            save.Load(slot);
        }

        private static int ResolveSlot()
        {
            if (int.TryParse(_loadSave, out int explicitSlot))
            {
                return explicitSlot;
            }
            // "latest": the most recently written SaveViewNN.xml
            string dir = Path.Combine(Application.dataPath, "StreamingAssets/XML");
            FileInfo newest = new DirectoryInfo(dir).GetFiles("SaveView*.xml")
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .FirstOrDefault();
            if (newest == null)
            {
                return -1;
            }
            string num = Path.GetFileNameWithoutExtension(newest.Name).Substring("SaveView".Length);
            return int.TryParse(num, out int id) ? id : -1;
        }
    }

    [HarmonyPatch(typeof(TitleLoad), "Start")]
    internal static class TitleLoadBootSkip
    {
        private static bool Prefix(TitleLoad __instance)
        {
            if (!DevLaunch.TryBootSkip())
            {
                return true; // normal boot -> title screen
            }
            __instance.GetComponent<SceneScript>().SceneChange("Scene_01");
            return false;
        }
    }
}
