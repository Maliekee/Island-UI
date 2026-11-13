using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace IslandEx
{
    // The music starts at your volume, not loud for a few seconds after a load.
    //
    // Vanilla applies the music volume late. Every scene's SaveManager reads the
    // settings into SaveManager.SaveSettingStatic at once (LoadSettingsBuild, before
    // its first yield), but only hands them to the audio after waiting for the
    // localization system to start (SaveManager.cs:5573 -> UIManager.LoadSetting,
    // which sets bgmPrefab's volume and the slider; the slider's BGMVolume then
    // turns the playing music down, UIManager.cs:5130). Meanwhile the music has
    // already started: SoundManager.GoBGM (and GoBGMDontDestroy) instantiate
    // bgmPrefab and play it at whatever volume the prefab carries, and Start adopts
    // a "BGM" left over from the scene before. So for a few seconds it plays at the
    // prefab's volume, then drops to yours.
    //
    // This sets the playing music to the saved volume the moment it starts, from
    // the same static vanilla applies later, so the later step changes nothing.
    // GoBGMFade needs nothing: it fades the new music up to the old one's volume.
    // Off = vanilla's loud start.
    [HarmonyPatch]
    internal static class MusicVolume
    {
        private static ConfigEntry<bool> _enabled;

        internal static void Bind(ConfigFile cfg)
        {
            _enabled = cfg.Bind("Fixes", "Music Starts At Your Volume", true,
                "After a load the music plays at your music volume from the start. Off = it starts loud for a few seconds, as in the unmodded game. Applies from the next music start.");
        }

        private static void Apply(SoundManager sound)
        {
            if (_enabled == null || !_enabled.Value || !Plugin.Active || sound == null || sound.tmpBgm == null)
            {
                return;
            }
            AudioSource audio = sound.tmpBgm.GetComponent<AudioSource>();
            if (audio != null)
            {
                audio.volume = SaveManager.SaveSettingStatic.bgmVolumeStatic;
            }
        }

        [HarmonyPatch(typeof(SoundManager), nameof(SoundManager.GoBGM))]
        [HarmonyPostfix]
        private static void AfterGoBGM(SoundManager __instance)
        {
            Apply(__instance);
        }

        [HarmonyPatch(typeof(SoundManager), nameof(SoundManager.GoBGMDontDestroy))]
        [HarmonyPostfix]
        private static void AfterGoBGMDontDestroy(SoundManager __instance)
        {
            Apply(__instance);
        }

        [HarmonyPatch(typeof(SoundManager), "Start")]
        [HarmonyPostfix]
        private static void AfterStart(SoundManager __instance)
        {
            Apply(__instance);
        }
    }
}
