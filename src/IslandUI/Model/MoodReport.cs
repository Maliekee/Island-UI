using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace IslandUI
{
    // THE MOOD IN THE LOG (the mood census): two lines the game's own
    // log never says.
    //  - A WARNING when someone reaches the last step before rebelling (-90), and
    //    when they rebel with no cause of the game's to name - the game logs a
    //    rebellion only when one of its three drains tips it.
    //  - A MORNING REPORT. Mood comes back at dawn and only at dawn (NPCMove.Sleep
    //    pays out as the night ends), so dawn is when "who did not sleep, and why"
    //    is worth one line: how many won mood back, and the ones near you who
    //    could not, by name and reason. Silent after a night spent elsewhere.
    internal static class MoodReport
    {
        // ---------- the warning ----------

        // From MoodLedger's postfix, when a change moved the level up.
        internal static void Crossed(ManagersScript mn, CommonStates who, int before, int after, NPCManager.MoralCause cause)
        {
            if (mn == null || mn.uiMN == null || after <= before || after < 3)
            {
                return;
            }
            if (after == 4 && cause != NPCManager.MoralCause.None)
            {
                return;   // the game says this one itself (CommonStates.MoralChange)
            }
            string word = MoodCheck.Word(mn, who, MoodCheck.Check(mn, who));
            string text = Name(mn, who) + (after == 4 ? " has rebelled" : " is one step from rebelling")
                + (string.IsNullOrEmpty(word) ? "." : ": " + word + ".");
            mn.uiMN.GoLogText(text);
        }

        // the game's level for a mood (CommonStates.DiscontentChanged)
        internal static int Level(float moral, CommonStates who)
        {
            if (moral > 0f) { return 0; }
            if (moral > -50f) { return 1; }
            if (moral > -90f) { return 2; }
            if (moral > -100f) { return 3; }
            return who.nMove != null && who.nMove.orderType == NPCMove.OrderType.Undying ? 3 : 4;
        }

        private static string Name(ManagersScript mn, CommonStates who)
        {
            string name = who.charaName ?? "?";
            try
            {
                return "<color=#" + mn.uiMN.GetColorCode(mn.uiMN.GetNPCColor(who.npcID)) + ">" + name + "</color>";
            }
            catch (System.Exception)
            {
                return name;
            }
        }

        // ---------- the morning ----------

        private static GameManager.Ambient _last = GameManager.Ambient.Noon;
        private static float _dueAt = -1f;

        internal static void Tick(ManagersScript mn)
        {
            if (mn == null || !mn.allSet || mn.gameMN == null || mn.npcMN == null || mn.uiMN == null)
            {
                return;
            }
            GameManager.Ambient now = mn.gameMN.ambient;
            if (_last == GameManager.Ambient.Night && now == GameManager.Ambient.Morning)
            {
                // the sleepers pay out as their coroutines notice the night is over
                _dueAt = Time.time + 6f;
            }
            _last = now;
            if (_dueAt > 0f && Time.time >= _dueAt)
            {
                _dueAt = -1f;
                try { Morning(mn); }
                catch (System.Exception e) { Plugin.Log.LogWarning("Mood report: " + e.Message); }
            }
        }

        private static void Morning(ManagersScript mn)
        {
            float since = MoodLedger.Now(mn) - 0.15f;
            int rested = 0;
            var sleepless = new List<string>();
            foreach (CommonStates who in mn.npcMN.friendList)
            {
                if (who == null || !mn.npcMN.IsActiveFriend(who))
                {
                    continue;
                }
                IList<MoodEvent> events = MoodLedger.Events(who);
                bool slept = false;
                for (int i = events.Count - 1; i >= 0 && events[i].Day >= since; i--)
                {
                    if (events[i].Cause == MoodCause.Sleep) { slept = true; break; }
                }
                if (slept)
                {
                    rested++;
                    continue;
                }
                if (who.moral >= 100f)
                {
                    continue;   // nothing to win back
                }
                foreach (MoodFinding f in MoodCheck.Check(mn, who))
                {
                    if (f.Key == "far") { break; }
                    if (f.Key == "sleep" && f.Severity == MoodSeverity.Bad)
                    {
                        sleepless.Add(Name(mn, who) + " (" + f.Short + ")");
                        break;
                    }
                }
            }
            // nobody slept near you: you spent the night elsewhere, and the log
            // has nothing to say about a base you were not at
            if (rested == 0)
            {
                return;
            }
            var sb = new StringBuilder("Dawn: ").Append(rested).Append(" slept indoors");
            if (sleepless.Count > 0)
            {
                // one line in the log panel: two names, then a count
                int shown = Mathf.Min(2, sleepless.Count);
                sb.Append("; no sleep: ").Append(string.Join(", ", sleepless.GetRange(0, shown).ToArray()));
                if (sleepless.Count > shown) { sb.Append(" +").Append(sleepless.Count - shown); }
            }
            mn.uiMN.GoLogText(sb.Append('.').ToString());
        }
    }
}
