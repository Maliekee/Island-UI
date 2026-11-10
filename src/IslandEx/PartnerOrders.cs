using System;
using System.Collections;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace IslandEx
{
    // MAN AND YONA TAKE ORDERS (Forgejo #246, GitHub #24). The game hides its
    // Orders tab for the two player characters (UIManager.StatusChange, npcID 0
    // and 1), yet nothing in the paths those orders drive excludes them: the one
    // you switch away from becomes an ordinary friendly NPC (NPCManager.NPCToPlayer)
    // that runs NPCMove.Live(), which starts a job wherever workType is set and
    // reads the combat stance and the permission flags (sexInfo 7-10) like anyone
    // else's; Call is NPCManager.WarpToPlayer, which has no player check. The
    // job's animation plays only if the skeleton has it (NPCManager.NPCWork), so
    // the partner mostly works in the idle pose.
    //
    // Island UI's Orders sheet draws those rows greyed for them; with these
    // switches on it makes them live. One master switch, off by default, and one
    // switch per kind of order under it, on by default, so one click on the master
    // turns the whole of it on and the four take parts away. Island UI reads them
    // through Plugin.PartnerTakes, never through this assembly.
    //
    // THE SAVE. The game writes Man and Yona field by field (SaveManager.TempToSave)
    // and reads them back in Loading(), skipping orderType and workType, so a
    // stance or a job would be gone after every load. While the stance or the job
    // switch is on, the two fields the save already has for them (CharaSave in
    // playerSave) are filled once the game's own gathering is done and put back
    // once Loading() has finished. Off, neither patch does anything, so the save is
    // written exactly as the game writes it. The permission flags are saved by the
    // game already; Island UI's PlayerPermissions puts them back to the game's
    // defaults after a load while the Permissions switch is off.
    //
    // A SWITCH TURNED OFF puts what it allowed back to the game's defaults, on both
    // player characters, at once: no job, Aggressor, the flags as CommonStates.Awake
    // sets them for the players (7, 8, 9 allowed, 10 off). Only a value that
    // differs is written.
    [HarmonyPatch]
    internal static class PartnerOrders
    {
        internal const string Job = "job";
        internal const string Stance = "stance";
        internal const string Permissions = "permissions";
        internal const string Call = "call";

        private const string Section = "Man and Yona";

        private static readonly int[] Flags = { 7, 8, 9, 10 };
        private static readonly int[] FlagDefaults = { 0, 0, 0, 1 };

        private static ConfigEntry<bool> _master, _job, _stance, _permissions, _call;

        // what each kind was last read as, so a switch going off is seen once
        private static bool _wasJob, _wasStance, _wasPermissions;

        internal static void Bind(ConfigFile cfg)
        {
            _master = cfg.Bind(Section, "Take Orders", false,
                "The player character you are not playing takes orders from Island UI's Orders sheet, like anyone you employ: the kinds switched on below. Off = as in the unmodded game, which gives Man and Yona none of them. Applies live.");
            _job = cfg.Bind(Section, "Job", true,
                "A job: the partner works at a work place within 10 m, mostly in the idle pose, and keeps the job after a load.");
            _stance = cfg.Bind(Section, "Combat Stance", true,
                "A combat stance, kept after a load.");
            _permissions = cfg.Bind(Section, "Permissions", true,
                "The four permissions (houses, love, sex, force). Off = they go back to the game's defaults after every load.");
            _call = cfg.Bind(Section, "Call", true,
                "Call: the partner comes to you.");
            _wasJob = Allows(Job);
            _wasStance = Allows(Stance);
            _wasPermissions = Allows(Permissions);
            EventHandler changed = (s, e) => SwitchesChanged();
            _master.SettingChanged += changed;
            _job.SettingChanged += changed;
            _stance.SettingChanged += changed;
            _permissions.SettingChanged += changed;
            _call.SettingChanged += changed;
            Plugin.Enabled.SettingChanged += changed;
        }

        // the switch for one kind of order, as Island UI asks for it: the mod on,
        // the master on, and the kind's own switch on
        internal static bool Allows(string order)
        {
            if (_master == null || !_master.Value || Plugin.Enabled == null || !Plugin.Enabled.Value)
            {
                return false;
            }
            switch (order)
            {
                case Job: return _job.Value;
                case Stance: return _stance.Value;
                case Permissions: return _permissions.Value;
                case Call: return _call.Value;
                default: return false;
            }
        }

        private static void SwitchesChanged()
        {
            bool job = Allows(Job), stance = Allows(Stance), permissions = Allows(Permissions);
            bool resetJob = _wasJob && !job, resetStance = _wasStance && !stance, resetFlags = _wasPermissions && !permissions;
            _wasJob = job;
            _wasStance = stance;
            _wasPermissions = permissions;
            if (resetJob || resetStance || resetFlags)
            {
                try
                {
                    Reset(resetJob, resetStance, resetFlags);
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning("Partner orders: not reset - " + e);
                }
            }
        }

        private static ManagersScript Managers()
        {
            GameObject managers = GameObject.Find("Managers");
            return managers != null ? managers.GetComponent<ManagersScript>() : null;
        }

        // the game's defaults back, the way its own setters write them
        // (UIManager.OrderChange and WorkChange)
        private static void Reset(bool job, bool stance, bool flags)
        {
            ManagersScript mn = Managers();
            CommonStates[] players = mn != null && mn.gameMN != null ? mn.gameMN.playerCommons : null;
            if (players == null)
            {
                return;
            }
            var changed = new System.Collections.Generic.List<string>();
            foreach (CommonStates pc in players)
            {
                if (pc == null)
                {
                    continue;
                }
                NPCMove move = pc.GetComponent<NPCMove>();
                if (stance && move != null && move.orderType != NPCMove.OrderType.Aggress)
                {
                    changed.Add(pc.charaName + " stance " + move.orderType + "->Aggress");
                    move.tmpEnemy = null;
                    move.orderType = NPCMove.OrderType.Aggress;
                }
                if (job && move != null && move.workType != NPCMove.WorkType.None)
                {
                    changed.Add(pc.charaName + " job " + move.workType + "->None");
                    move.workType = NPCMove.WorkType.None;
                    if (mn.uiMN != null)
                    {
                        mn.uiMN.FriendWorkCheck(pc);
                    }
                }
                if (flags && pc.sexInfo != null)
                {
                    for (int i = 0; i < Flags.Length; i++)
                    {
                        int id = Flags[i];
                        if (id < pc.sexInfo.Length && pc.sexInfo[id] != FlagDefaults[i])
                        {
                            changed.Add(pc.charaName + " [" + id + "] " + pc.sexInfo[id] + "->" + FlagDefaults[i]);
                            pc.sexInfo[id] = FlagDefaults[i];
                        }
                    }
                }
            }
            if (changed.Count > 0)
            {
                Plugin.Log.LogInfo("Partner orders: switched off, back to the game's defaults - " + string.Join(", ", changed.ToArray()));
            }
        }

        // ---------- the save ----------

        [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.TempToSave))]
        [HarmonyPostfix]
        private static void TempToSave_Postfix(SaveManager __instance, ref IEnumerator __result)
        {
            if (__result != null && (Allows(Stance) || Allows(Job)))
            {
                __result = WriteAfter(__instance, __result);
            }
        }

        private static IEnumerator WriteAfter(SaveManager save, IEnumerator inner)
        {
            while (inner.MoveNext())
            {
                yield return inner.Current;
            }
            bool stance = Allows(Stance), job = Allows(Job);
            if (!stance && !job)
            {
                yield break;
            }
            try
            {
                ManagersScript mn = Managers();
                CommonStates[] players = mn != null && mn.gameMN != null ? mn.gameMN.playerCommons : null;
                SaveManager.CharaSave[] pl = save.saveEntry != null ? save.saveEntry.playerSave : null;
                if (players == null || pl == null)
                {
                    yield break;
                }
                for (int u = 0; u < pl.Length && u < players.Length; u++)
                {
                    NPCMove move = players[u] != null ? players[u].GetComponent<NPCMove>() : null;
                    if (pl[u] == null || move == null)
                    {
                        continue;
                    }
                    if (stance)
                    {
                        pl[u].orderType = (int)move.orderType;
                    }
                    if (job)
                    {
                        pl[u].workType = (int)move.workType;
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Partner orders: not saved - " + e);
            }
        }

        [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.Loading))]
        [HarmonyPostfix]
        private static void Loading_Postfix(SaveManager __instance, ref IEnumerator __result)
        {
            if (__result != null && (Allows(Stance) || Allows(Job)))
            {
                __result = ApplyAfter(__instance, __result);
            }
        }

        private static IEnumerator ApplyAfter(SaveManager save, IEnumerator inner)
        {
            while (inner.MoveNext())
            {
                yield return inner.Current;
            }
            bool stance = Allows(Stance), job = Allows(Job);
            if (!stance && !job)
            {
                yield break;
            }
            try
            {
                ManagersScript mn = Managers();
                CommonStates[] players = mn != null && mn.gameMN != null ? mn.gameMN.playerCommons : null;
                SaveManager.CharaSave[] pl = save.saveDB != null && save.saveDB.save != null && save.saveDB.save.Count > 0
                    ? save.saveDB.save[0].playerSave
                    : null;
                if (players == null || pl == null)
                {
                    yield break;
                }
                for (int u = 0; u < pl.Length && u < players.Length; u++)
                {
                    NPCMove move = players[u] != null ? players[u].GetComponent<NPCMove>() : null;
                    if (pl[u] == null || move == null)
                    {
                        continue;
                    }
                    if (stance && Enum.IsDefined(typeof(NPCMove.OrderType), pl[u].orderType))
                    {
                        move.orderType = (NPCMove.OrderType)pl[u].orderType;
                    }
                    if (job && Enum.IsDefined(typeof(NPCMove.WorkType), pl[u].workType))
                    {
                        move.workType = (NPCMove.WorkType)pl[u].workType;
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Partner orders: not restored - " + e);
            }
        }
    }
}
