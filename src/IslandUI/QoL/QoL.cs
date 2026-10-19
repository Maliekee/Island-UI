using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace IslandUI
{
    // The non-inventory quality-of-life pieces: the keymap, the crafting and quick-save hotkeys and
    // the dev launch. They were the QoLTweaks plugin.
    //
    // Plugin calls in: Bind from Awake before the census (KeyRemap's patches read the
    // map), Apply after it, Tick from Update.
    internal static class QoL
    {
        internal static ConfigEntry<bool> KeymapEnabled;
        internal static ConfigEntry<KeyCode> OpenCraftingMenuKey;
        internal static ConfigEntry<KeyCode> QuickSaveKey;

        // Every key the game's gameplay code reads (see docs/keymap.md), labeled by what
        // it does. Remapping is key-level: all context meanings of a key move together.
        private static readonly (string label, KeyCode key, string note)[] RemapTargets =
        {
            ("Move Forward", KeyCode.W, ""),
            ("Move Left", KeyCode.A, ""),
            ("Move Back", KeyCode.S, ""),
            ("Move Right", KeyCode.D, ""),
            ("Jump / Confirm / Skip Dialog", KeyCode.Space, ""),
            ("Menu", KeyCode.Tab, ""),
            ("Action (hold)", KeyCode.E, ""),
            ("Carry / Pickup", KeyCode.Q, ""),
            ("Interact", KeyCode.F, ""),
            ("Context Action / Mount", KeyCode.R, ""),
            ("Disassemble Animal", KeyCode.T, ""),
            ("Drop Item / Close Menu", KeyCode.H, ""),
            ("Backpack", KeyCode.I, ""),
            ("Map", KeyCode.M, ""),
            ("Toggle Run", KeyCode.X, ""),
            ("Right-Click Substitute", KeyCode.Z, ""),
            ("Drone: Hide Model", KeyCode.V, "Only used while flying the drone."),
            ("Prev Character / Slow Time", KeyCode.Comma, ""),
            ("Next Character / Speed Time", KeyCode.Period, ""),
            ("Mute Audio", KeyCode.At, ""),
            ("Modifier: Bulk", KeyCode.LeftShift, ""),
            ("Modifier: Split/Bulk+", KeyCode.LeftControl, ""),
            ("Modifier: Alt Behavior", KeyCode.LeftAlt, ""),
            ("Pause", KeyCode.Escape, ""),
            ("Confirm / Submit", KeyCode.Return, "Changing it can break dialogs."),
        };

        private static readonly Dictionary<KeyCode, ConfigEntry<KeyCode>> RemapEntries =
            new Dictionary<KeyCode, ConfigEntry<KeyCode>>();

        internal static void Bind(ConfigFile cfg)
        {
            KeymapEnabled = cfg.Bind("Keymap", "Enabled", true,
                "Turns key remapping on or off. Off = the game's own keys, whatever is set below.");

            foreach (var (label, key, note) in RemapTargets)
            {
                // Pause once moved off Escape by default so the close-all hotkey could
                // have it. Reverted: Escape is the game's key and we
                // are giving it back. Taking a key the player already knows, to drive a
                // window system we are still redesigning, spends their muscle memory on
                // a decision we have not made yet - and every remap here is one more
                // thing that has to be re-derived once the input pass happens.
                //
                // Nothing is special-cased any more: every entry defaults to vanilla.
                string desc = $"The game's own key is {key}. None = no key. " + note;
                ConfigEntry<KeyCode> entry = cfg.Bind("Keymap", $"{label} ({key})", key, desc.TrimEnd());
                RemapEntries[key] = entry;
                entry.SettingChanged += (_, __) => RebuildMap();
                Bindings.Register(label, entry);
            }
            RebuildMap();

            OpenCraftingMenuKey = cfg.Bind("Hotkeys", "Open Crafting Menu", KeyCode.C,
                "Opens and closes the crafting window (your own recipes when no bench is targeted). The keymap does not apply to it. None = no key.");
            Bindings.Register("Open Crafting Menu", OpenCraftingMenuKey);

            QuickSaveKey = cfg.Bind("Hotkeys", "Quick Save", KeyCode.F6,
                "Saves to the slot this game was loaded from. The keymap does not apply to it. None = no key.");
            Bindings.Register("Quick Save", QuickSaveKey);
        }

        // After the census, which has already applied every attributed patch class
        // here (KeyRemap, the dev launch's boot skip).
        internal static void Apply(Harmony harmony)
        {
            DevLaunch.Init(BepInEx.Paths.BepInExRootPath);
        }

        internal static void Tick(ManagersScript mn)
        {
            DevLaunch.Tick();
            KeyCode craftKey = OpenCraftingMenuKey.Value;
            if (craftKey != KeyCode.None && RawInput.GetKeyDown(craftKey))
            {
                ToggleCraftingMenu(mn);
            }
            KeyCode saveKey = QuickSaveKey.Value;
            if (saveKey != KeyCode.None && RawInput.GetKeyDown(saveKey))
            {
                QuickSave.Run(mn);
            }
            QuickSave.Tick();
        }

        private static void RebuildMap()
        {
            var mappings = new List<KeyValuePair<KeyCode, KeyCode>>();
            foreach (KeyValuePair<KeyCode, ConfigEntry<KeyCode>> pair in RemapEntries)
            {
                mappings.Add(new KeyValuePair<KeyCode, KeyCode>(pair.Key, pair.Value.Value));
            }
            KeyRemap.Rebuild(mappings);
        }

        private static void ToggleCraftingMenu(ManagersScript mn)
        {
            if (mn == null || !mn.allSet || GameManager.nowEvent || mn.gameMN.pMove == null)
            {
                Plugin.Log.LogInfo($"CraftMenu hotkey: blocked by game state (mn={(mn == null ? "null" : "ok")}, allSet={mn?.allSet}, nowEvent={GameManager.nowEvent}, pMove={(mn?.gameMN?.pMove == null ? "null" : "ok")})");
                return;
            }
            UIManager ui = mn.uiMN;
            // same states in which the game itself ignores menu hotkeys
            if (ui.settingMenu.activeSelf || ui.nameInput.enabled
                || ui.mainInputField.gameObject.activeSelf || ui.talkEditPanel.activeSelf)
            {
                Plugin.Log.LogInfo($"CraftMenu hotkey: blocked by UI state (settingMenu={ui.settingMenu.activeSelf}, nameInput={ui.nameInput.enabled}, mainInput={ui.mainInputField.gameObject.activeSelf}, talkEdit={ui.talkEditPanel.activeSelf})");
                return;
            }
            Plugin.Log.LogDebug("CraftMenu hotkey: invoking LeftPanelState(1)");
            // the exact call the craft tab button (and the surround bench) makes;
            // built-in toggle: same state twice closes the panel
            ui.LeftPanelState(1);
        }
    }
}
