using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace IslandUI
{
    // MAN'S AND YONA'S PERMISSIONS BACK TO THE GAME'S OWN (GitHub #24). Island UI's
    // Orders sheet once offered the four permission flags (sexInfo 7-10: houses,
    // love, sex, force) for the two player characters, which vanilla never does
    // (InventoryWindow.IsPlayerCharacter). Those flags are in the players' save, so
    // a save made back then still carries them. In vanilla only CommonStates.Awake
    // sets them for the players - 7, 8 and 9 to 0 (allowed), 10 to 1 (force off) -
    // so once Loading() has applied the player saves, any other value is ours, and
    // it goes back. A value already at the default is not written. While Island Ex
    // lets Man and Yona take permissions (PartnerSwitches, Forgejo #246) their
    // flags are theirs to keep, and nothing is reset.
    [HarmonyPatch]
    internal static class PlayerPermissions
    {
        private static readonly int[] Flags = { 7, 8, 9, 10 };
        private static readonly int[] Defaults = { 0, 0, 0, 1 };

        [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.Loading))]
        [HarmonyPostfix]
        private static void Loading_Postfix(ref IEnumerator __result)
        {
            __result = ResetAfter(__result);
        }

        private static IEnumerator ResetAfter(IEnumerator inner)
        {
            while (inner.MoveNext())
            {
                yield return inner.Current;
            }
            if (PartnerSwitches.IslandExAllows(PartnerSwitches.Permissions))
            {
                yield break;
            }
            GameObject managers = GameObject.Find("Managers");
            ManagersScript mn = managers != null ? managers.GetComponent<ManagersScript>() : null;
            CommonStates[] players = mn != null && mn.gameMN != null ? mn.gameMN.playerCommons : null;
            if (players == null)
            {
                yield break;
            }
            var changed = new List<string>();
            foreach (CommonStates pc in players)
            {
                if (pc == null || pc.sexInfo == null)
                {
                    continue;
                }
                for (int i = 0; i < Flags.Length; i++)
                {
                    int id = Flags[i];
                    if (id < pc.sexInfo.Length && pc.sexInfo[id] != Defaults[i])
                    {
                        changed.Add(pc.charaName + " [" + id + "] " + pc.sexInfo[id] + "->" + Defaults[i]);
                        pc.sexInfo[id] = Defaults[i];
                    }
                }
            }
            if (changed.Count > 0)
            {
                Plugin.Log.LogInfo("Player permissions: back to the game's defaults - " + string.Join(", ", changed.ToArray()));
            }
        }
    }
}
