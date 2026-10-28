using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace IslandUI
{
    // Why a feeling moved. The love tick's four are told apart by their value
    // (GameManager.LoveCheck); the rest by their caller (LoveLedger.Classify).
    internal enum LoveCause
    {
        Other,
        Liked,       // the tick, their areas overlap and the other IS what they LIKE: +10
        Disliked,    // the tick, overlap, the other IS what they DISLIKE: -5
        Plain,       // the tick, overlap, anyone else: +5
        Apart,       // the tick, a lover whose area no longer overlaps: -5, dropped at 0
        Following,   // you, while they follow you (NPCManager.FollowerLoveChange)
        Emote,       // your dance, lewd dance or threat (NPCManager.RecieveEmotion)
        Sex,         // with each other (SexManager.CommonSexNPC, CommonSexPlayer)
        Forced,      // forced (SexManager.ManRapes, ManRapesSleep, CommonRapesNPC)
        Frustrated,  // wanting and no place for it: -2 toward you (NPCMove.Live)
        Item,        // a consumable used on them (ItemManager.UseConsumables)
        Story,       // a story event (StoryManager)
    }

    internal struct LoveEvent
    {
        public float Day;      // as MoodEvent.Day
        public int Partner;    // friendID of the one the feeling is toward
        public float Delta;    // what the love actually moved
        public float After;    // the love after it; NaN when the bond was dropped
        public LoveCause Cause;
        public int Place;      // 0 outdoors, else the house they were inside
    }

    // THE LOVE LEDGER (the Social tab says what happened and why, not a forecast -
    // jobs, night shifts, fights and paths decide where people really
    // are). Every change of one person's feeling toward another goes through
    // CommonStates.LoveChange, so a prefix and a postfix there see them all: toward
    // whom, how much, why, and whether they were indoors at the time - indoors their
    // area is the interior, 25 m under the house, so they meet only others indoors
    // (docs/social-links.md section 5).
    //
    // Kept like the mood ledger: in the save block (<Love>), by friendID, and across
    // a hot reload as the same text. Three days of a camp is a few thousand ticks.
    [HarmonyPatch]
    internal static class LoveLedger
    {
        private const float KeepDays = 3f;   // the Social page's three days, the mood ledger's span
        private const int KeepMax = 600;

        private static readonly Dictionary<CommonStates, List<LoveEvent>> Log =
            new Dictionary<CommonStates, List<LoveEvent>>();
        private static readonly List<LoveEvent> None = new List<LoveEvent>();

        internal static IList<LoveEvent> Events(CommonStates who)
        {
            return who != null && Log.TryGetValue(who, out List<LoveEvent> list) ? list : None;
        }

        internal static void Clear()
        {
            Log.Clear();
            TicksSeen.Clear();
        }

        // ---------- the last day, tick by tick ----------

        // The love tick runs at these minutes of the day (GameManager.FixedUpdate);
        // 21:00 and 00:00 are the night's two
        internal static readonly int[] TickMinutes = { 0, 360, 540, 720, 1080, 1260 };
        internal const int TicksADay = 6;

        // every tick the ledger saw run, as day * 6 + slot: a tick that ran with no
        // event between two people means they did not meet; one that is not here
        // ran before the record began
        private static readonly HashSet<int> TicksSeen = new HashSet<int>();

        private static bool IsTick(LoveCause c)
        {
            return c == LoveCause.Liked || c == LoveCause.Disliked || c == LoveCause.Plain || c == LoveCause.Apart;
        }

        // the tick an event of the love tick belongs to: the last tick at or before
        // it (the tick walks the camp a person a frame, so its events trail it)
        internal static int TickKey(float day)
        {
            int d = Mathf.FloorToInt(day);
            float minute = (day - d) * 1440f + 0.5f;
            int slot = 0;
            for (int i = 1; i < TicksADay; i++)
            {
                if (TickMinutes[i] <= minute) { slot = i; }
            }
            return d * TicksADay + slot;
        }

        internal static float TickDay(int key)
        {
            int d = key >= 0 ? key / TicksADay : (key - TicksADay + 1) / TicksADay;
            return d + TickMinutes[key - d * TicksADay] / 1440f;
        }

        internal static int TickHour(int key)
        {
            return TickMinutes[((key % TicksADay) + TicksADay) % TicksADay] / 60;
        }

        internal static bool TickAtNight(int key)
        {
            int hour = TickHour(key);
            return hour >= 21 || hour < 6;
        }

        internal static bool TickOnRecord(int key)
        {
            return TicksSeen.Contains(key);
        }

        // what one person's tick made of their feeling toward another
        internal enum TickMark
        {
            Unknown,    // the tick ran before the record began
            NotMet,     // it ran, and their areas did not overlap
            Liked,      // +10
            Plain,      // +5
            Disliked,   // -5
            Apart,      // -5, a bond fading because they no longer overlap
        }

        internal sealed class Meeting
        {
            public int Partner;
            public readonly TickMark[] Mine;     // the person's feeling toward them, a tick a slot
            public readonly TickMark[] Theirs;   // theirs back
            public readonly int[] MyPlace;       // 0 outdoors, else the house
            public readonly int[] TheirPlace;

            public Meeting(int ticks)
            {
                Mine = new TickMark[ticks];
                Theirs = new TickMark[ticks];
                MyPlace = new int[ticks];
                TheirPlace = new int[ticks];
            }

            public float MineSum, TheirSum;
            public int DroppedAt = -1;   // the tick at which one of the two let the bond go, if any
            public bool DroppedByMe;
        }

        private static TickMark MarkOf(LoveCause c)
        {
            switch (c)
            {
                case LoveCause.Liked: return TickMark.Liked;
                case LoveCause.Disliked: return TickMark.Disliked;
                case LoveCause.Apart: return TickMark.Apart;
                default: return TickMark.Plain;
            }
        }

        // The last `count` ticks up to `now` (a day's six by default), oldest
        // first, as tick keys
        internal static int[] LastTicks(float now, int count = TicksADay)
        {
            int latest = TickKey(now);
            var keys = new int[count];
            for (int i = 0; i < count; i++)
            {
                keys[i] = latest - (count - 1 - i);
            }
            return keys;
        }

        // EVERYONE A PERSON MET IN THE GIVEN TICKS, both ways: what each tick
        // made of the person's feeling toward them and of theirs back, where each
        // was at the time, and the sums. A pair with a bond fading apart counts,
        // as they are still in each other's ledger.
        internal static List<Meeting> MetIn(CommonStates who, int[] ticks)
        {
            var byPartner = new Dictionary<int, Meeting>();
            var result = new List<Meeting>();
            if (who == null || who.friendID < 0 || ticks == null || ticks.Length == 0)
            {
                return result;
            }
            int first = ticks[0], last = ticks[ticks.Length - 1];
            Meeting Get(int partner)
            {
                if (!byPartner.TryGetValue(partner, out Meeting m))
                {
                    m = new Meeting(ticks.Length) { Partner = partner };
                    byPartner[partner] = m;
                    result.Add(m);
                }
                return m;
            }
            foreach (KeyValuePair<CommonStates, List<LoveEvent>> pair in Log)
            {
                bool mine = ReferenceEquals(pair.Key, who);
                if (pair.Key == null || (!mine && pair.Key.friendID < 0))
                {
                    continue;
                }
                List<LoveEvent> list = pair.Value;
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    LoveEvent e = list[i];
                    int key = TickKey(e.Day);
                    if (key < first)
                    {
                        break;
                    }
                    if (key > last || !IsTick(e.Cause) || (!mine && e.Partner != who.friendID))
                    {
                        continue;
                    }
                    Meeting m = Get(mine ? e.Partner : pair.Key.friendID);
                    int slot = key - first;
                    if (mine)
                    {
                        m.Mine[slot] = MarkOf(e.Cause);
                        m.MyPlace[slot] = e.Place;
                        m.MineSum += e.Delta;
                    }
                    else
                    {
                        m.Theirs[slot] = MarkOf(e.Cause);
                        m.TheirPlace[slot] = e.Place;
                        m.TheirSum += e.Delta;
                    }
                    if (float.IsNaN(e.After) && key > m.DroppedAt)
                    {
                        m.DroppedAt = key;
                        m.DroppedByMe = mine;
                    }
                }
            }
            foreach (Meeting m in result)
            {
                for (int s = 0; s < ticks.Length; s++)
                {
                    TickMark none = TicksSeen.Contains(first + s) ? TickMark.NotMet : TickMark.Unknown;
                    if (m.Mine[s] == TickMark.Unknown) { m.Mine[s] = none; }
                    if (m.Theirs[s] == TickMark.Unknown) { m.Theirs[s] = none; }
                }
            }
            return result;
        }

        // ---------- recording ----------

        private static float LoveOf(CommonStates who, CommonStates partner)
        {
            if (who.lovers != null && partner != null)
            {
                for (int i = 0; i < who.lovers.Count; i++)
                {
                    if (who.lovers[i].friendID == partner.friendID) { return who.lovers[i].love; }
                }
            }
            return float.NaN;
        }

        [HarmonyPatch(typeof(CommonStates), nameof(CommonStates.LoveChange))]
        [HarmonyPrefix]
        private static void LoveChange_Prefix(CommonStates __instance, CommonStates partner, out float __state)
        {
            __state = __instance != null ? LoveOf(__instance, partner) : float.NaN;
        }

        [HarmonyPatch(typeof(CommonStates), nameof(CommonStates.LoveChange))]
        [HarmonyPostfix]
        private static void LoveChange_Postfix(CommonStates __instance, float __state, CommonStates partner,
            float value, bool remove)
        {
            try
            {
                if (__instance == null || partner == null || __instance.friendID < 0 || partner.friendID < 0
                    || ReferenceEquals(partner, __instance))
                {
                    return;   // not between two of yours
                }
                float after = LoveOf(__instance, partner);
                float before = float.IsNaN(__state) ? 0f : __state;
                float delta = float.IsNaN(after) ? (float.IsNaN(__state) ? 0f : value) : after - before;
                if (Mathf.Abs(delta) < 0.001f)
                {
                    return;   // a count kept, or the 200-lover cap
                }
                NPCMove move = __instance.nMove;
                Add(__instance, new LoveEvent
                {
                    Day = MoodLedger.Now(MoodLedger.Managers()),
                    Partner = partner.friendID,
                    Delta = delta,
                    After = after,
                    Cause = Classify(value, remove),
                    Place = move != null && move.house != null ? Mathf.Max(1, __instance.houseID) : 0,
                });
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Love ledger: could not record a change - " + e.Message);
            }
        }

        private static void Add(CommonStates who, LoveEvent e)
        {
            if (!Log.TryGetValue(who, out List<LoveEvent> list))
            {
                list = new List<LoveEvent>();
                Log[who] = list;
            }
            list.Add(e);
            if (IsTick(e.Cause)) { TicksSeen.Add(TickKey(e.Day)); }
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

        private static LoveCause Classify(float value, bool remove)
        {
            string caller = MoodLedger.Caller();
            if (caller == "GameManager.LoveCheck")
            {
                if (remove) { return LoveCause.Apart; }
                if (value >= 10f) { return LoveCause.Liked; }
                return value < 0f ? LoveCause.Disliked : LoveCause.Plain;
            }
            switch (caller)
            {
                case "NPCManager.FollowerLoveChange": return LoveCause.Following;
                case "NPCManager.RecieveEmotion": return LoveCause.Emote;
                case "SexManager.CommonSexNPC":
                case "SexManager.CommonSexPlayer": return LoveCause.Sex;
                case "SexManager.ManRapes":
                case "SexManager.ManRapesSleep":
                case "SexManager.CommonRapesNPC": return LoveCause.Forced;
                case "NPCMove.Live": return LoveCause.Frustrated;
                case "ItemManager.UseConsumables": return LoveCause.Item;
            }
            return caller.StartsWith("StoryManager.", StringComparison.Ordinal) ? LoveCause.Story : LoveCause.Other;
        }

        // ---------- the save block, and across a hot reload ----------

        // One element per person: <Love f="friendID" e="day,partner,delta,after,cause,place;..." />
        internal static void WriteTo(StringBuilder body)
        {
            foreach (KeyValuePair<CommonStates, List<LoveEvent>> pair in Log)
            {
                if (pair.Key == null || pair.Key.friendID < 0 || pair.Value.Count == 0)
                {
                    continue;
                }
                body.Append("    <Love f=\"").Append(pair.Key.friendID.ToString(CultureInfo.InvariantCulture))
                    .Append("\" e=\"").Append(Encode(pair.Value)).Append("\" />\n");
            }
        }

        internal static bool Restore(ManagersScript mn, int friendID, string encoded, bool dryRun)
        {
            CommonStates who = mn != null && mn.npcMN != null && friendID >= 0 ? mn.npcMN.GetFriend(friendID) : null;
            List<LoveEvent> list = Decode(encoded);
            if (who == null || list.Count == 0)
            {
                return false;
            }
            if (!dryRun)
            {
                Log[who] = list;
                foreach (LoveEvent e in list)
                {
                    if (IsTick(e.Cause)) { TicksSeen.Add(TickKey(e.Day)); }
                }
            }
            return true;
        }

        private static string Encode(List<LoveEvent> list)
        {
            var sb = new StringBuilder();
            CultureInfo inv = CultureInfo.InvariantCulture;
            for (int i = 0; i < list.Count; i++)
            {
                LoveEvent e = list[i];
                if (i > 0) { sb.Append(';'); }
                sb.Append(e.Day.ToString("0.####", inv)).Append(',')
                  .Append(e.Partner).Append(',')
                  .Append(e.Delta.ToString("0.##", inv)).Append(',')
                  .Append(float.IsNaN(e.After) ? "x" : e.After.ToString("0.##", inv)).Append(',')
                  .Append((int)e.Cause).Append(',')
                  .Append(e.Place);
            }
            return sb.ToString();
        }

        private static List<LoveEvent> Decode(string s)
        {
            var list = new List<LoveEvent>();
            if (string.IsNullOrEmpty(s))
            {
                return list;
            }
            CultureInfo inv = CultureInfo.InvariantCulture;
            foreach (string item in s.Split(';'))
            {
                string[] f = item.Split(',');
                float after = float.NaN;
                if (f.Length != 6
                    || !float.TryParse(f[0], NumberStyles.Float, inv, out float day)
                    || !int.TryParse(f[1], NumberStyles.Integer, inv, out int partner)
                    || !float.TryParse(f[2], NumberStyles.Float, inv, out float delta)
                    || (f[3] != "x" && !float.TryParse(f[3], NumberStyles.Float, inv, out after))
                    || !int.TryParse(f[4], NumberStyles.Integer, inv, out int cause)
                    || !int.TryParse(f[5], NumberStyles.Integer, inv, out int place)
                    || !Enum.IsDefined(typeof(LoveCause), cause))
                {
                    continue;
                }
                list.Add(new LoveEvent { Day = day, Partner = partner, Delta = delta, After = after, Cause = (LoveCause)cause, Place = place });
            }
            return list;
        }

        private const string HandoverKey = "IslandUI.LoveLedger.handover";

        internal static void HandOver()
        {
            var ids = new List<int>();
            var texts = new List<string>();
            foreach (KeyValuePair<CommonStates, List<LoveEvent>> pair in Log)
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
            ManagersScript mn = MoodLedger.Managers();
            int kept = 0;
            for (int i = 0; i < ids.Length && i < texts.Length; i++)
            {
                if (Restore(mn, ids[i], texts[i], dryRun: false)) { kept++; }
            }
            Plugin.Log.LogInfo($"Love ledger: reload kept {kept} person(s)");
        }
    }
}
