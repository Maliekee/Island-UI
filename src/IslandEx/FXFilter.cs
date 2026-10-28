using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace IslandEx
{
    // Suppress named visual effects wherever the game spawns them.
    //
    // The question this answers is "do I have to open every scene and edit it?"
    // — no, because effects do not live in the scenes. Every animation-driven
    // effect in the game arrives through ONE funnel: a Spine animation carries
    // events named FX_00..FX_03, `AnimationFX.FXEvent` (AnimationFX.cs:41) turns
    // each into `FXManager.GoFXR(fxID[i], fxPos[i])`, and GoFXR pools a prefab
    // out of `FXManager.fx[]`. Roughly a hundred scene prefabs, one call site.
    // Code elsewhere also spawns effects directly through GoFX/GoFXObj, so all
    // three are filtered and share one decision.
    //
    // WHICH id is which cannot be read from the decompiled source: `fxID` is
    // inspector data on each prefab, not a literal in a method. So the filter is
    // also the identifier — do the thing whose effect you want gone, and it is
    // listed on the menu's Effects page (and in the debug log, with its id and
    // prefab name). Once per distinct id, so it names the whole effect
    // vocabulary over a session instead of spamming.
    //
    // Blocking is by NAME as well as by id because a name survives a game update
    // that renumbers the array, and reads as something in the config file.
    [HarmonyPatch]
    internal static class FXFilter
    {
        private static ConfigEntry<bool> _enabled;
        private static ConfigEntry<string> _blocked;

        internal static void Bind(ConfigFile cfg)
        {
            _enabled = cfg.Bind("FX", "Enabled", true,
                "Hides the effects listed in Blocked. Off = every effect shows, and the list is kept. Applies live.");
            _blocked = cfg.Bind("FX", "Blocked", "",
                "Effects never to show, comma-separated. A number blocks that effect id; a word blocks every effect whose name contains it. Names survive game updates. Example: splash, 81");
        }

        // Log once per id: the point is to enumerate the vocabulary, and an effect
        // that fires every footstep would otherwise bury the one you are hunting.
        // The same list, in order, is what the menu offers to hide: newest first,
        // since the one you are after is the one you just saw.
        private static readonly HashSet<int> Seen = new HashSet<int>();
        private static readonly List<string> SeenNames = new List<string>();
        // Ids whose prefab is gameplay, not decoration: the pool also carries the
        // melee hitbox (fx[0], "attackColl", spawned by FXManager.GoAttack),
        // projectiles and damage triggers. Their callers use the spawned object
        // without a null check, so a skip throws mid-attack and leaves attacker
        // and target frozen in their attack/hit states. Never blocked, never listed.
        private static readonly HashSet<int> Gameplay = new HashSet<int>();
        private static readonly HashSet<int> BlockedIds = new HashSet<int>();
        private static readonly List<string> BlockedNames = new List<string>();
        // what BlockedIds/BlockedNames were parsed from, so an edit in the config
        // file takes effect without a restart and without re-parsing every spawn
        private static string _parsedFrom;

        // The one decision, shared by all three spawn methods. Never throws: an
        // effect is decoration, and a filter on it must not be able to break a
        // frame of the game.
        private static bool Blocked(FXManager fxMN, int fxID)
        {
            try
            {
                if (fxID < 0 || fxMN == null || fxMN.fx == null || fxID >= fxMN.fx.Length)
                {
                    return false;
                }
                GameObject prefab = fxMN.fx[fxID] != null ? fxMN.fx[fxID].fxPrefab : null;
                string name = prefab != null ? prefab.name : "?";
                if (Seen.Add(fxID))
                {
                    if (IsGameplay(prefab))
                    {
                        Gameplay.Add(fxID);
                        Plugin.Log.LogDebug($"FX {fxID} = {name} (gameplay, never filtered)");
                        return false;
                    }
                    lock (SeenNames)
                    {
                        SeenNames.Add(name);
                    }
                    Plugin.Log.LogDebug($"FX {fxID} = {name}");
                }
                if (Gameplay.Contains(fxID)
                    || _enabled == null || !_enabled.Value || !Plugin.Enabled.Value)
                {
                    return false;
                }
                Parse();
                if (BlockedIds.Contains(fxID))
                {
                    return true;
                }
                for (int i = 0; i < BlockedNames.Count; i++)
                {
                    if (name.IndexOf(BlockedNames[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                }
                return false;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"FXFilter: {e.Message}");
                return false;
            }
        }

        // A collider or rigidbody anywhere in the prefab means the object does
        // something in the world: hits, is thrown, triggers. Pure visuals have none.
        private static bool IsGameplay(GameObject prefab)
        {
            return prefab != null
                && (prefab.GetComponentInChildren<Collider>(true) != null
                    || prefab.GetComponentInChildren<Rigidbody>(true) != null);
        }

        internal static string[] SeenNewestFirst()
        {
            lock (SeenNames)
            {
                string[] names = SeenNames.ToArray();
                Array.Reverse(names);
                return names;
            }
        }

        private static void Parse()
        {
            string raw = _blocked != null ? _blocked.Value : null;
            if (raw == _parsedFrom)
            {
                return;
            }
            _parsedFrom = raw;
            BlockedIds.Clear();
            BlockedNames.Clear();
            if (string.IsNullOrEmpty(raw))
            {
                return;
            }
            foreach (string part in raw.Split(','))
            {
                string entry = part.Trim();
                if (entry.Length == 0)
                {
                    continue;
                }
                if (int.TryParse(entry, out int id))
                {
                    BlockedIds.Add(id);
                }
                else
                {
                    BlockedNames.Add(entry);
                }
            }
            Plugin.Log.LogInfo($"FXFilter: blocking {BlockedIds.Count} id(s), "
                + $"{BlockedNames.Count} name(s)");
        }

        // Three entry points, one rule. Parameters bind BY NAME to the original's
        // (the Harmony trap this project has been bitten by), so `fxID` is spelled
        // exactly as FXManager spells it.
        [HarmonyPatch(typeof(FXManager), nameof(FXManager.GoFXR))]
        [HarmonyPrefix]
        private static bool GoFXR_Prefix(FXManager __instance, int fxID)
        {
            return !Blocked(__instance, fxID);
        }

        [HarmonyPatch(typeof(FXManager), nameof(FXManager.GoFX))]
        [HarmonyPrefix]
        private static bool GoFX_Prefix(FXManager __instance, int fxID)
        {
            return !Blocked(__instance, fxID);
        }

        // Returns a GameObject rather than void, so a skip must also supply the
        // answer: null. Many callers do NOT survive one (GoAttack, GoRanged,
        // GoArrow dereference it at once); that is safe only because Blocked never
        // skips a gameplay prefab, and those are the ones such callers spawn.
        [HarmonyPatch(typeof(FXManager), nameof(FXManager.GoFXObj))]
        [HarmonyPrefix]
        private static bool GoFXObj_Prefix(FXManager __instance, int fxID,
            ref GameObject __result)
        {
            if (!Blocked(__instance, fxID))
            {
                return true;
            }
            __result = null;
            return false;
        }
    }
}
