using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace IslandUI
{
    // Keymap layer: remap the keys the game already reads. All of the game's hardcoded
    // Input.GetKey/GetKeyDown/GetKeyUp(KeyCode) calls funnel through Unity's managed
    // wrappers; three prefixes rewrite the requested KeyCode through a translation table
    // before the engine is asked. Key-level semantics: every context meaning of a vanilla
    // key moves together. Not covered by design: the 1-9 quick slots (read via
    // Input.inputString), mouse buttons/axes, and text field typing (event-based, which
    // is why remapping letters can never corrupt typing).
    [HarmonyPatch(typeof(Input))]
    internal static class KeyRemap
    {
        // KeyCode's highest value (Joystick8Button19) is 509
        private static readonly KeyCode[] Map = BuildIdentityMap(512);

        // Raw reads (our own hotkeys, other mods via RawInput) bypass translation
        [System.ThreadStatic]
        internal static bool Bypass;

        // A key-binding row in the settings window is listening. While it is, the
        // keyboard belongs to that row alone: every other read - the game's, ours,
        // another mod's through RawInput - is told no key is down, so the key being
        // bound does not also open a bag or swing an axe. Not covered, as with the
        // keymap: the 1-9 quick slots (Input.inputString) and the mouse.
        internal static bool Capturing;

        private static KeyCode[] BuildIdentityMap(int size)
        {
            var map = new KeyCode[size];
            for (int i = 0; i < size; i++)
            {
                map[i] = (KeyCode)i;
            }
            return map;
        }

        // The physical key a vanilla key is read from now, for a label that names it
        internal static KeyCode Bound(KeyCode vanilla)
        {
            int i = (int)vanilla;
            return i >= 0 && i < Map.Length ? Map[i] : vanilla;
        }

        internal static void Rebuild(IEnumerable<KeyValuePair<KeyCode, KeyCode>> mappings)
        {
            for (int i = 0; i < Map.Length; i++)
            {
                Map[i] = (KeyCode)i;
            }
            var targets = new Dictionary<KeyCode, KeyCode>();
            foreach (var pair in mappings)
            {
                int index = (int)pair.Key;
                if (index < 0 || index >= Map.Length)
                {
                    continue;
                }
                Map[index] = pair.Value;
                // conflict: two vanilla keys redirected onto the same physical key
                if (pair.Value != KeyCode.None && pair.Value != pair.Key)
                {
                    if (targets.TryGetValue(pair.Value, out KeyCode other))
                    {
                        Plugin.Log.LogWarning(
                            $"Keymap conflict: both {other} and {pair.Key} are mapped to {pair.Value} — one press will trigger both.");
                    }
                    else
                    {
                        targets[pair.Value] = pair.Key;
                    }
                }
            }
        }

        private static void Translate(ref KeyCode key)
        {
            if (Capturing && !Bypass)
            {
                key = KeyCode.None;
                return;
            }
            if (Bypass || !QoL.KeymapEnabled.Value)
            {
                return;
            }
            int index = (int)key;
            if (index >= 0 && index < Map.Length)
            {
                key = Map[index];
            }
        }

        [HarmonyPatch(nameof(Input.GetKey), typeof(KeyCode))]
        [HarmonyPrefix]
        private static void GetKey_Prefix(ref KeyCode key) => Translate(ref key);

        [HarmonyPatch(nameof(Input.GetKeyDown), typeof(KeyCode))]
        [HarmonyPrefix]
        private static void GetKeyDown_Prefix(ref KeyCode key) => Translate(ref key);

        [HarmonyPatch(nameof(Input.GetKeyUp), typeof(KeyCode))]
        [HarmonyPrefix]
        private static void GetKeyUp_Prefix(ref KeyCode key) => Translate(ref key);
    }

    /// <summary>
    /// Key reads that bypass the keymap translation. Use for mod-defined hotkeys:
    /// user bindings mean the physical key, and must not be re-routed by remap entries.
    /// CONTRACT: public surface, additive evolution only.
    /// </summary>
    public static class RawInput
    {
        public static bool GetKeyDown(KeyCode key)
        {
            return !KeyRemap.Capturing && Physical(key, down: true);
        }

        public static bool GetKey(KeyCode key)
        {
            return !KeyRemap.Capturing && Physical(key, down: false);
        }

        // The listening row's own read: the first keyboard key that went down this
        // frame, whatever is being swallowed from everyone else. None = nothing.
        internal static KeyCode Captured()
        {
            if (!Input.anyKeyDown)
            {
                return KeyCode.None;
            }
            for (int k = (int)KeyCode.Backspace; k < (int)KeyCode.Mouse0; k++)
            {
                if (Physical((KeyCode)k, down: true))
                {
                    return (KeyCode)k;
                }
            }
            return KeyCode.None;
        }

        internal static bool Physical(KeyCode key, bool down)
        {
            KeyRemap.Bypass = true;
            try
            {
                return down ? Input.GetKeyDown(key) : Input.GetKey(key);
            }
            finally
            {
                KeyRemap.Bypass = false;
            }
        }
    }
}
