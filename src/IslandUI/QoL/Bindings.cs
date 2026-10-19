using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace IslandUI
{
    /// <summary>
    /// Unified registry of all key bindings — both remaps of vanilla keys and hotkeys
    /// for actions. A binding is presentation-agnostic: a label plus a rebindable key.
    /// Other mods may register their own hotkey entries so every binding shows up in
    /// one place for any future rebind GUI.
    ///
    /// CONTRACT: public surface, additive evolution only.
    /// </summary>
    public static class Bindings
    {
        public sealed class Binding
        {
            public string Label { get; }
            public ConfigEntry<KeyCode> Entry { get; }

            internal Binding(string label, ConfigEntry<KeyCode> entry)
            {
                Label = label;
                Entry = entry;
            }
        }

        private static readonly List<Binding> Registered = new List<Binding>();

        public static IReadOnlyList<Binding> All => Registered;

        public static void Register(string label, ConfigEntry<KeyCode> entry)
        {
            Registered.Add(new Binding(label, entry));
        }
    }
}
