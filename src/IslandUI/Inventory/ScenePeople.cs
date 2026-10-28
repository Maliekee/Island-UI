using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace IslandUI
{
    // EVERYONE IN THE SCENE, without asking the engine for them, as
    // SceneContainers does for chests. The always-on markers (Forgejo #154)
    // asked FindObjectsOfType<CommonStates> twice a second, and in a busy base
    // that find is 24 ms: a hitch every half second. So the living are counted
    // in as they wake, the one find is spent once to seed the list (a hot
    // reload, or one that woke before the patch), and the destroyed fall out on
    // the next walk.
    [HarmonyPatch]
    internal static class ScenePeople
    {
        private static readonly List<CommonStates> All = new List<CommonStates>();
        private static readonly HashSet<CommonStates> Known = new HashSet<CommonStates>();
        private static bool _seeded;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CommonStates), "Awake")]
        private static void Woke(CommonStates __instance)
        {
            if (__instance != null && Known.Add(__instance))
            {
                All.Add(__instance);
            }
        }

        // the live ones, as FindObjectsOfType would return them: active in the
        // scene, the destroyed dropped from the list as they are met
        internal static List<CommonStates> Live(List<CommonStates> into)
        {
            into.Clear();
            // the seed takes the INACTIVE too: one that woke before this patch (a hot
            // reload) and is switched off - a village out of range - would otherwise
            // be found by neither the seed nor Woke, and never be listed. Prefabs
            // and other assets have no scene and stay out
            if (!_seeded)
            {
                _seeded = true;
                foreach (CommonStates c in Resources.FindObjectsOfTypeAll<CommonStates>())
                {
                    if (c != null && c.gameObject.scene.IsValid()) { Woke(c); }
                }
            }
            for (int i = All.Count - 1; i >= 0; i--)
            {
                CommonStates c = All[i];
                if (c == null)
                {
                    Known.Remove(c);
                    All.RemoveAt(i);
                    continue;
                }
                if (c.gameObject.activeInHierarchy)
                {
                    into.Add(c);
                }
            }
            return into;
        }
    }
}
