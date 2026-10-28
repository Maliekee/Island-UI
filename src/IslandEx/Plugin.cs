using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace IslandEx
{
    // Island Extensions: the small things that are neither the UI nor how NPCs move.
    // The FX filter, hold-to-struggle, keep-fullscreen, no rain indoors
    // (DryIndoors), a further zoom (ZoomOut) and broken tools that stay
    // (KeepBroken). Each is its own section
    // in the .cfg and its own page in the Island Ex menu (Island UI draws that menu,
    // reaching these settings through BepInEx, never through this assembly).
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "madisland.islandex";
        public const string PluginName = "Island Ex";
        public const string PluginVersion = "0.2.0";

        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> Enabled;

        private void Awake()
        {
            Log = Logger;
            // its words follow Island UI's language setting, or the game's (Forgejo #19)
            Shared.Loc.Init("IslandEx", null);
            Enabled = Config.Bind("General", "Enabled", true,
                "Turns Island Extensions on or off. Off = everything below is as in the unmodded game. Applies live; clicking the mod's icon on the title screen also switches it.");
            FXFilter.Bind(Config);
            GrappleHold.Bind(Config);
            KeepFullscreen.Bind(Config);
            DryIndoors.Bind(Config);
            ZoomOut.Bind(Config);
            KeepBroken.Bind(Config);

            var harmony = new Harmony(PluginGuid);
            Shared.PatchCensus.ApplyAndReport(harmony, Assembly.GetExecutingAssembly(), Log);
            // still guarded by hand: its target is a compiler-generated iterator that
            // no attribute can name
            GrappleHold.Apply(harmony);
            // not a Harmony patch: redirects one native import in UnityPlayer.dll
            KeepFullscreen.Install();
            // live: every feature asks Enabled every time, so running and wanted are the same answer
            Shared.TitleBadge.Install(Assembly.GetExecutingAssembly(), "IslandEx.icon.png", PluginName, PluginVersion,
                () => Enabled.Value, () => Enabled.Value, on =>
                {
                    Enabled.Value = on;
                    Log.LogInfo(PluginName + " switched " + (on ? "on" : "off") + " from the title screen.");
                });
        }

        private void Update()
        {
            KeepFullscreen.Tick();
            DryIndoors.Tick();
            ZoomOut.Tick();
        }

        // What the Island Ex menu lists on its Effects page: every effect spawned this
        // session, newest first. Island UI finds it by name through the plugin
        // instance; a build without it just shows the blocked list.
        public static string[] SeenEffects()
        {
            return FXFilter.SeenNewestFirst();
        }
    }
}
