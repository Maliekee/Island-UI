using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace IslandUI
{
    // Why a mood moved, as far as we can tell. The game's own answer is
    // NPCManager.MoralCause, which names three drains; everything else is read
    // off the caller (MoodLedger.Classify).
    internal enum MoodCause
    {
        Other,
        Sleep,       // a night indoors (NPCMove.Sleep)
        BadCompany,  // a relation at or below -10 love (CommonStates.LoveChange)
        Crush,       // a lover who says no (NPCMove.Live)
        NoBed,       // a lover who says yes, and no free bed (NPCMove.Live)
        Sex,         // villager with villager (SexManager.CommonSexNPC)
        Relief,      // a toilet or a bed alone, with love off (NPCMove.Live, SexManager.OnaniNPC)
        Forced,      // forcing a villager (NPCMove.Live)
        Food,        // a food with a mood value (ItemManager.UseFood)
        Execution,   // someone executed nearby (NPCManager.ExecutionEffects)
        Torture,     // a torture device's regain (GameManager.LoveCheck)
        Training,    // a training device (NPCManager.WorkResult)
        Rebel,       // a rebel knocked down, sated, or taken along
    }

    internal struct MoodEvent
    {
        public float Day;      // GameManager.dayCount + the fraction of the day
        public float Delta;    // what the mood actually moved, after the clamp
        public float After;
        public MoodCause Cause;
        public int Partner;    // friendID of the other person, or -1
    }

    // THE MOOD LEDGER (it is hard to keep track of how to make NPCs happy, and
    // hard to track a cause down and fix it). The game keeps ONE
    // cause per person, lastMoralCause, and every mood change overwrites it - a
    // gain writes None, so a night's sleep erases the only record of why they
    // were unhappy, and a load resets it. Every change goes through
    // CommonStates.MoralChange, so a postfix there sees them all: how much, why,
    // and against whom, kept for the last few in-game days.
    //
    // The three drains name themselves (the cause argument, with the partner).
    // The rest pass None and are told apart by their caller, read off the stack:
    // there are a dozen call sites, each in a method that says what it is, and a
    // stack walk on a change that happens a few times a minute costs nothing
    // anyone can measure.
    //
    // Kept in the save block (<Mood>), addressed by friendID, and handed across a
    // hot reload as the same text.
    [HarmonyPatch]
    internal static class MoodLedger
    {
        private const float KeepDays = 3f;
        private const int KeepMax = 160;

        private static readonly Dictionary<CommonStates, List<MoodEvent>> Log =
            new Dictionary<CommonStates, List<MoodEvent>>();
        private static readonly List<MoodEvent> None = new List<MoodEvent>();

        private static ManagersScript _mn;

        // the scene's managers; a scene load destroys them, and Unity's null says so
        private static ManagersScript Mn()
        {
            if (_mn == null)
            {
                GameObject go = GameObject.Find("Managers");
                _mn = go != null ? go.GetComponent<ManagersScript>() : null;
            }
            return _mn;
        }

        internal static float Now(ManagersScript mn)
        {
            GameManager g = mn != null ? mn.gameMN : null;
            if (g == null)
            {
                return 0f;
            }
            return g.dayCount + (g.dayTime > 0f ? g.tmpDayTime / g.dayTime : 0f);
        }

        internal static IList<MoodEvent> Events(CommonStates who)
        {
            return who != null && Log.TryGetValue(who, out List<MoodEvent> list) ? list : None;
        }

        // The mood moved this much in the last `days`.
        internal static float Net(ManagersScript mn, CommonStates who, float days)
        {
            float since = Now(mn) - days, sum = 0f;
            IList<MoodEvent> list = Events(who);
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Day >= since) { sum += list[i].Delta; }
            }
            return sum;
        }

        // The latest drain on record: why they are unhappy, which the game forgets
        // the moment anything goes well.
        internal static bool LastDrain(CommonStates who, out MoodEvent drain)
        {
            IList<MoodEvent> list = Events(who);
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i].Delta < 0f)
                {
                    drain = list[i];
                    return true;
                }
            }
            drain = default(MoodEvent);
            return false;
        }

        internal static void Clear()
        {
            Log.Clear();
        }

        // ---------- recording ----------

        [HarmonyPatch(typeof(CommonStates), nameof(CommonStates.MoralChange))]
        [HarmonyPrefix]
        private static void MoralChange_Prefix(CommonStates __instance, out float __state)
        {
            __state = __instance.moral;
        }

        [HarmonyPatch(typeof(CommonStates), nameof(CommonStates.MoralChange))]
        [HarmonyPostfix]
        private static void MoralChange_Postfix(CommonStates __instance, float __state, float value,
            CommonStates partner, NPCManager.MoralCause cause)
        {
            try
            {
                if (__instance == null || __instance.friendID < 0)
                {
                    return;   // not one of yours
                }
                MoodCause why = Classify(__instance, partner, cause);
                float after = __instance.moral;
                // Sleep adds 20 to a negative mood and then sets it back to 10 if
                // that overshot - after this postfix, so the cap is applied here
                if (why == MoodCause.Sleep && value >= 20f)
                {
                    after = Mathf.Min(after, 10f);
                }
                float delta = after - __state;
                if (Mathf.Abs(delta) < 0.001f)
                {
                    return;   // at the limit already
                }
                ManagersScript mn = Mn();
                MoodReport.Crossed(mn, __instance, MoodReport.Level(__state, __instance),
                    __instance.debuff != null ? __instance.debuff.discontent : 0, cause);
                Add(__instance, new MoodEvent
                {
                    Day = Now(mn),
                    Delta = delta,
                    After = after,
                    Cause = why,
                    Partner = partner != null && !ReferenceEquals(partner, __instance) ? partner.friendID : -1,
                });
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Mood ledger: could not record a change - " + e.Message);
            }
        }

        private static void Add(CommonStates who, MoodEvent e)
        {
            if (!Log.TryGetValue(who, out List<MoodEvent> list))
            {
                list = new List<MoodEvent>();
                Log[who] = list;
            }
            list.Add(e);
            float cutoff = e.Day - KeepDays;
            int drop = 0;
            while (drop < list.Count && (list[drop].Day < cutoff || list.Count - drop > KeepMax))
            {
                drop++;
            }
            if (drop > 0)
            {
                list.RemoveRange(0, drop);
            }
        }

        private static MoodCause Classify(CommonStates who, CommonStates partner, NPCManager.MoralCause cause)
        {
            switch (cause)
            {
                case NPCManager.MoralCause.Crush: return MoodCause.Crush;
                case NPCManager.MoralCause.Relation: return MoodCause.BadCompany;
                case NPCManager.MoralCause.NoBed: return MoodCause.NoBed;
            }
            // NPCMove.Live passes the person themself when they force a villager
            if (partner != null && ReferenceEquals(partner, who))
            {
                return MoodCause.Forced;
            }
            switch (Caller())
            {
                case "NPCMove.Sleep": return MoodCause.Sleep;
                case "NPCMove.Live": return MoodCause.Relief;          // the toilet's +1
                case "SexManager.OnaniNPC": return MoodCause.Relief;
                case "SexManager.CommonSexNPC": return MoodCause.Sex;
                case "ItemManager.UseFood": return MoodCause.Food;
                case "NPCManager.ExecutionEffects": return MoodCause.Execution;
                case "GameManager.LoveCheck": return MoodCause.Torture;
                case "NPCManager.WorkResult": return MoodCause.Training;
                case "NPCMove.Dead":
                case "SexManager.PlayerRaped":
                case "NPCManager.NPCFollow": return MoodCause.Rebel;
            }
            return MoodCause.Other;
        }

        // The game method that asked for the change, as Type.Method. A coroutine's
        // body runs as MoveNext on a nested <Name>d__N, which names the coroutine.
        private static string Caller()
        {
            var trace = new System.Diagnostics.StackTrace(2, false);
            Assembly ours = typeof(MoodLedger).Assembly;
            for (int i = 0; i < trace.FrameCount; i++)
            {
                MethodBase m = trace.GetFrame(i)?.GetMethod();
                Type t = m != null ? m.DeclaringType : null;
                if (t == null || t == typeof(CommonStates) || t.Assembly == ours)
                {
                    continue;   // Harmony's replacement, the method itself, or us
                }
                string name = m.Name;
                if (t.Name.StartsWith("<", StringComparison.Ordinal) && t.DeclaringType != null)
                {
                    int close = t.Name.IndexOf('>');
                    if (close > 1) { name = t.Name.Substring(1, close - 1); }
                    t = t.DeclaringType;
                }
                if (t.Namespace != null && t.Namespace.StartsWith("HarmonyLib", StringComparison.Ordinal))
                {
                    continue;
                }
                return t.Name + "." + name;
            }
            return "?";
        }

        // ---------- the save block, and across a hot reload ----------

        // One element per person: <Mood f="friendID" e="day,delta,after,cause,partner;..." />
        internal static void WriteTo(StringBuilder body)
        {
            foreach (KeyValuePair<CommonStates, List<MoodEvent>> pair in Log)
            {
                if (pair.Key == null || pair.Key.friendID < 0 || pair.Value.Count == 0)
                {
                    continue;
                }
                body.Append("    <Mood f=\"").Append(pair.Key.friendID.ToString(CultureInfo.InvariantCulture))
                    .Append("\" e=\"").Append(Encode(pair.Value)).Append("\" />\n");
            }
        }

        internal static bool Restore(ManagersScript mn, int friendID, string encoded, bool dryRun)
        {
            CommonStates who = mn != null && mn.npcMN != null && friendID >= 0 ? mn.npcMN.GetFriend(friendID) : null;
            List<MoodEvent> list = Decode(encoded);
            if (who == null || list.Count == 0)
            {
                return false;
            }
            if (!dryRun)
            {
                Log[who] = list;
            }
            return true;
        }

        private static string Encode(List<MoodEvent> list)
        {
            var sb = new StringBuilder();
            CultureInfo inv = CultureInfo.InvariantCulture;
            for (int i = 0; i < list.Count; i++)
            {
                MoodEvent e = list[i];
                if (i > 0) { sb.Append(';'); }
                sb.Append(e.Day.ToString("0.####", inv)).Append(',')
                  .Append(e.Delta.ToString("0.##", inv)).Append(',')
                  .Append(e.After.ToString("0.##", inv)).Append(',')
                  .Append((int)e.Cause).Append(',')
                  .Append(e.Partner);
            }
            return sb.ToString();
        }

        private static List<MoodEvent> Decode(string s)
        {
            var list = new List<MoodEvent>();
            if (string.IsNullOrEmpty(s))
            {
                return list;
            }
            CultureInfo inv = CultureInfo.InvariantCulture;
            foreach (string item in s.Split(';'))
            {
                string[] f = item.Split(',');
                if (f.Length != 5
                    || !float.TryParse(f[0], NumberStyles.Float, inv, out float day)
                    || !float.TryParse(f[1], NumberStyles.Float, inv, out float delta)
                    || !float.TryParse(f[2], NumberStyles.Float, inv, out float after)
                    || !int.TryParse(f[3], NumberStyles.Integer, inv, out int cause)
                    || !int.TryParse(f[4], NumberStyles.Integer, inv, out int partner)
                    || !Enum.IsDefined(typeof(MoodCause), cause))
                {
                    continue;
                }
                list.Add(new MoodEvent { Day = day, Delta = delta, After = after, Cause = (MoodCause)cause, Partner = partner });
            }
            return list;
        }

        private const string HandoverKey = "IslandUI.MoodLedger.handover";

        internal static void HandOver()
        {
            var ids = new List<int>();
            var texts = new List<string>();
            foreach (KeyValuePair<CommonStates, List<MoodEvent>> pair in Log)
            {
                if (pair.Key != null && pair.Key.friendID >= 0 && pair.Value.Count > 0)
                {
                    ids.Add(pair.Key.friendID);
                    texts.Add(Encode(pair.Value));
                }
            }
            AppDomain.CurrentDomain.SetData(HandoverKey, new object[] { ids.ToArray(), texts.ToArray() });
        }

        internal static void TakeOver()
        {
            object[] state = AppDomain.CurrentDomain.GetData(HandoverKey) as object[];
            AppDomain.CurrentDomain.SetData(HandoverKey, null);
            if (state == null || state.Length != 2 || !(state[0] is int[] ids) || !(state[1] is string[] texts))
            {
                return;
            }
            ManagersScript mn = Mn();
            int kept = 0;
            for (int i = 0; i < ids.Length && i < texts.Length; i++)
            {
                if (Restore(mn, ids[i], texts[i], dryRun: false)) { kept++; }
            }
            Plugin.Log.LogInfo($"Mood ledger: reload kept {kept} person(s)");
        }
    }
}
