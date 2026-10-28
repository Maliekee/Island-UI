using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace IslandUI
{
    internal enum MoodSeverity
    {
        Good,   // working for them: tonight's sleep
        Info,
        Warn,   // not costing mood yet, or only sometimes
        Bad,    // costing mood now, or keeping it from coming back
    }

    internal sealed class MoodFinding
    {
        public string Key;          // sleep · far · company · crush · bed · you · rebel
        public MoodSeverity Severity;
        public string Short;        // the few words a roster row has room for
        public string Effect;       // what it does to the mood: "-6/day", "no recovery"
        public string Text;         // what is going on, one line
        public string Fix;          // what to do about it, or null
        public CommonStates Other;  // the other person, when there is one
    }

    // WHAT IS DOING IT, BEFORE IT HAS (the mood ledger's other half). The game's
    // mood is one number, `moral`, and very little moves it:
    //  - UP, mostly a night indoors: +10 at dawn, or +20 while negative but never
    //    past 10 (NPCMove.Sleep). Nothing else a villager does on their own comes
    //    close.
    //  - DOWN, three drains (NPCManager.MoralCause):
    //    bad company - two friends whose areas overlap, one disliking the other's
    //      type, lose 5 love a check (6 a day, GameManager.LoveCheck); from -10
    //      love on, every change costs 1 mood, forever. It runs for everyone,
    //      near or far.
    //    crush - a lover (love 50+) in an overlapping area who says no
    //      (NPCManager.ShallWeSex: another house, no love back, no libido): -5 a
    //      time once libido is 90, which a night's sleep sets it to. Any occupied
    //      toilet within 10 m spends the libido instead.
    //    no bed - the lover says yes and there is no free bed within 10 m: -5,
    //      unless they like you (love for you above 2).
    //  - At -100 they rebel (debuff.discontent 4); the Survivor order holds them
    //    one step short.
    // Every check below is that code read forward: the same conditions, asked of
    // the state as it is now, so a cause can be named before it has cost anything.
    // Read-only - the game's own helpers are called only where they are pure.
    internal static class MoodCheck
    {
        internal static List<MoodFinding> Check(ManagersScript mn, CommonStates who)
        {
            var found = new List<MoodFinding>();
            if (mn == null || mn.npcMN == null || who == null || who.nMove == null
                || who.sexInfo == null || who.sexInfo.Length < 10)
            {
                return found;
            }
            Rebellion(who, found);
            Sleep(mn, who, found);
            Company(mn, who, found);
            Lover(mn, who, found);
            TowardYou(who, found);
            found.Sort((a, b) => b.Severity.CompareTo(a.Severity));
            return found;
        }

        // ---------- the levels ----------

        private static void Rebellion(CommonStates who, List<MoodFinding> found)
        {
            int level = who.debuff != null ? who.debuff.discontent : 0;
            if (level == 4)
            {
                found.Add(new MoodFinding
                {
                    Key = "rebel", Severity = MoodSeverity.Bad,
                    Short = Shared.Loc.Get("mood.check.rebelling", "rebelling"),
                    Effect = Shared.Loc.Get("mood.check.rebelling.effect", "fights"),
                    Text = Shared.Loc.Get("mood.check.rebelling.text", "Rebelling: fights you and the villagers"),
                    Fix = Shared.Loc.Get("mood.check.rebelling.fix", "Knock them down (+20 mood), then fix what drains them"),
                });
            }
            else if (level == 3)
            {
                found.Add(new MoodFinding
                {
                    Key = "rebel", Severity = MoodSeverity.Bad,
                    Short = who.moral <= -100f ? Shared.Loc.Get("mood.check.survivor", "held by Survivor")
                        : Shared.Loc.Get("mood.check.near_rebellion", "near rebellion"),
                    Effect = who.moral <= -100f ? Shared.Loc.Get("mood.check.survivor.effect", "at -100")
                        : Shared.Loc.Get("mood.check.near_rebellion.effect", "rebels at -100"),
                    Text = who.moral <= -100f ? Shared.Loc.Get("mood.check.survivor.text", "Held back from rebelling only by the Survivor order")
                        : Shared.Loc.Get("mood.check.near_rebellion.text", "One step from rebelling (they rebel at -100)"),
                });
            }
        }

        // ---------- recovery: a night indoors ----------

        private static void Sleep(ManagersScript mn, CommonStates who, List<MoodFinding> found)
        {
            NPCMove move = who.nMove;
            string blocker = null, fix = null, brief = null;
            bool never = false;
            if (move.npcType == NPCMove.NPCType.Follow)
            {
                blocker = Shared.Loc.Get("mood.check.following.text", "Following you, so never sleeps");
                brief = Shared.Loc.Get("mood.check.following", "no sleep: following");
                fix = Shared.Loc.Get("mood.check.following.fix", "Leave them at home for the night");
            }
            else if (move.orderType == NPCMove.OrderType.Guards)
            {
                blocker = Shared.Loc.Get("mood.check.guard.text", "On guard duty, so never sleeps");
                brief = Shared.Loc.Get("mood.check.guard", "no sleep: guard duty");
                fix = Shared.Loc.Get("mood.check.guard.fix", "Give them another combat order");
            }
            else if (move.territoryRad <= 0f)
            {
                blocker = Shared.Loc.Get("mood.check.area_zero.text", "Their area is 0, so never sleeps");
                brief = Shared.Loc.Get("mood.check.area_zero", "no sleep: area 0");
                fix = Shared.Loc.Get("mood.check.area_zero.fix", "Give them an area with a house in it");
            }
            else if (who.npcID == 88 || !HasSleep(who))
            {
                blocker = Shared.Loc.Get("mood.check.never_sleeps.text", "Their kind never sleeps");
                brief = Shared.Loc.Get("mood.check.never_sleeps", "never sleeps");
                fix = Shared.Loc.Get("mood.check.never_sleeps.fix", "Only foods, sex and relief raise their mood: keep the drains away");
                never = true;
            }
            else if (move.house == null && who.houseID == 0)
            {
                if (who.sexInfo[7] != 0)
                {
                    blocker = Shared.Loc.Get("mood.check.outside.text", "Not allowed into houses, so sleeps outside");
                    brief = Shared.Loc.Get("mood.check.outside", "sleeps outside");
                    fix = Shared.Loc.Get("mood.fix.allow_houses", "Allow them to enter houses (Orders, Permissions)");
                }
                else if (mn.npcMN.FindFriendHouse(who) == null)
                {
                    blocker = Shared.Loc.Get("mood.check.no_house.text", "No house door inside their area");
                    brief = Shared.Loc.Get("mood.check.no_house", "no house in area");
                    fix = Shared.Loc.Get("mood.check.no_house.fix", "Widen their area to take in a house, or build one in it");
                }
            }
            if (blocker != null)
            {
                found.Add(new MoodFinding
                {
                    Key = "sleep",
                    // costing them now only once there is something to win back
                    Severity = never ? MoodSeverity.Info : who.moral < 0f ? MoodSeverity.Bad : MoodSeverity.Warn,
                    Short = brief, Effect = Shared.Loc.Get("mood.check.no_recovery", "no recovery"),
                    Text = Shared.Loc.Get("mood.check.no_recovery.text", "{0} - no mood back at dawn", blocker),
                    Fix = fix,
                });
                return;
            }
            // The game runs a villager only while their part of the map is live
            // around you. Out of it they never walk home, but the love checks that
            // drain them still run.
            if (!who.gameObject.activeInHierarchy)
            {
                found.Add(new MoodFinding
                {
                    // being away from you is the normal state of a base you are not
                    // at; it is news only for someone who needs the night
                    Key = "far", Severity = who.moral < 0f ? MoodSeverity.Warn : MoodSeverity.Info,
                    Short = Shared.Loc.Get("mood.check.far", "out of range"),
                    Effect = Shared.Loc.Get("mood.check.far.effect", "no sleep here"),
                    Text = Shared.Loc.Get("mood.check.far.text", "Out of your range: villagers sleep only while you are near"),
                    Fix = Shared.Loc.Get("mood.check.far.fix", "Spend the night near them"),
                });
                return;
            }
            found.Add(new MoodFinding
            {
                Key = "sleep", Severity = MoodSeverity.Good,
                Short = Shared.Loc.Get("mood.check.sleeps", "sleeps indoors"),
                Effect = who.moral < 0f ? Shared.Loc.Get("mood.check.sleeps.effect_low", "+20 at dawn")
                    : Shared.Loc.Get("mood.check.sleeps.effect", "+10 at dawn"),
                Text = who.moral < 0f ? Shared.Loc.Get("mood.check.sleeps.text_low", "Sleeps indoors: +20 at dawn, up to 10")
                    : Shared.Loc.Get("mood.check.sleeps.text", "Sleeps indoors: +10 at dawn"),
            });
        }

        private static bool HasSleep(CommonStates who)
        {
            try
            {
                return who.anim == null || who.anim.skeleton == null
                    || who.anim.skeleton.Data.FindAnimation("A_sleep") != null;
            }
            catch (System.Exception)
            {
                return true;   // unknown: do not claim they never sleep
            }
        }

        // ---------- the first drain: bad company ----------

        private static void Company(ManagersScript mn, CommonStates who, List<MoodFinding> found)
        {
            NPCManager npcs = mn.npcMN;
            // LoveCheck skips anyone with love off, from both ends
            if (npcs.friendList == null || !npcs.IsActiveFriend(who) || who.sexInfo[8] == 1)
            {
                return;
            }
            for (int i = 0; i < npcs.friendList.Count; i++)
            {
                CommonStates other = npcs.friendList[i];
                if (other == null || ReferenceEquals(other, who) || other.sexInfo == null || other.sexInfo.Length < 9
                    || other.sexInfo[8] == 1 || !npcs.IsActiveFriend(other) || !npcs.NPCDistanceCheck(who, other))
                {
                    continue;
                }
                bool disliked = who.person[1] != other.person[0] && who.person[2] == other.person[0];
                float love = LoveFor(npcs, who, other.friendID);
                if (love <= -10f)
                {
                    found.Add(new MoodFinding
                    {
                        Key = "company", Severity = MoodSeverity.Bad, Other = other,
                        Short = Shared.Loc.Get("mood.check.cant_stand", "can't stand {0}", other.charaName),
                        Effect = disliked ? Shared.Loc.Get("mood.check.cant_stand.effect", "-6/day")
                            : Shared.Loc.Get("mood.check.sore.effect", "-1 a check"),
                        Text = disliked
                            ? Shared.Loc.Get("mood.check.cant_stand.text", "Can't stand {0}: -1 mood six times a day", other.charaName)
                            : Shared.Loc.Get("mood.check.sore.text", "Still sore at {0} (love {1}): -1 mood a check until it passes -10",
                                other.charaName, love.ToString("0")),
                        Fix = disliked ? Shared.Loc.Get("mood.check.apart.fix", "Move their areas apart, or turn off Love for one of them") : null,
                    });
                }
                else if (disliked)
                {
                    int checks = Mathf.Max(1, Mathf.CeilToInt((love + 10f) / 5f));
                    found.Add(new MoodFinding
                    {
                        Key = "company", Severity = MoodSeverity.Warn, Other = other,
                        Short = Shared.Loc.Get("mood.check.dislikes", "dislikes {0}", other.charaName),
                        Effect = checks <= 6 ? Shared.Loc.Get("mood.check.dislikes.hours", "sours in {0}h", checks * 4)
                            : Shared.Loc.Get("mood.check.dislikes.days", "sours in {0}d", Mathf.CeilToInt(checks / 6f)),
                        Text = checks <= 6
                            ? Shared.Loc.Plural("mood.check.dislikes.text_hours",
                                "Dislikes {1}'s type, areas overlap: drains from about {0} hour on",
                                "Dislikes {1}'s type, areas overlap: drains from about {0} hours on", checks * 4, other.charaName)
                            : Shared.Loc.Plural("mood.check.dislikes.text_days",
                                "Dislikes {1}'s type, areas overlap: drains from about {0} day on",
                                "Dislikes {1}'s type, areas overlap: drains from about {0} days on", Mathf.CeilToInt(checks / 6f),
                                other.charaName),
                        Fix = Shared.Loc.Get("mood.check.apart.fix", "Move their areas apart, or turn off Love for one of them"),
                    });
                }
            }
        }

        // ---------- the other two drains: a lover ----------

        private static void Lover(ManagersScript mn, CommonStates who, List<MoodFinding> found)
        {
            NPCManager npcs = mn.npcMN;
            if (who.sexInfo[9] != 0 || who.lovers == null || who.lovers.Count == 0
                || who.nMove.npcType != NPCMove.NPCType.Friend || mn.sexMN == null)
            {
                return;
            }
            CommonStates partner = SexableLover(mn, who);
            if (partner == null || partner.npcID <= 1)
            {
                return;
            }
            bool urgent = who.libido >= 90f;
            string when = urgent ? Shared.Loc.Get("mood.check.when.urgent", "-5 mood, several times a day")
                : Shared.Loc.Get("mood.check.when.morning", "-5 mood once libido reaches 90 (every morning)");
            SexManager.SexInviteState answer = npcs.ShallWeSex(who, partner);
            if (answer == SexManager.SexInviteState.No)
            {
                // an occupied toilet in reach spends the libido instead
                if (npcs.FindSexualGratification(who) != null)
                {
                    return;
                }
                string why = who.houseID != partner.houseID ? Shared.Loc.Get("mood.check.why.house", "{0} lives in another house", partner.charaName)
                    : !npcs.LoveLoveCheck(who, partner) ? Shared.Loc.Get("mood.check.why.unloved", "{0} doesn't love them back", partner.charaName)
                    : partner.libido < 20f ? Shared.Loc.Get("mood.check.why.not_in_mood", "{0} isn't in the mood", partner.charaName)
                    : Shared.Loc.Get("mood.check.why.no", "{0} says no", partner.charaName);
                found.Add(new MoodFinding
                {
                    Key = "crush", Severity = urgent ? MoodSeverity.Bad : MoodSeverity.Warn, Other = partner,
                    Short = Shared.Loc.Get("mood.check.pining", "pining for {0}", partner.charaName),
                    Effect = Shared.Loc.Get("mood.check.a_time", "-5 a time"),
                    Text = Shared.Loc.Get("mood.check.pining.text", "Pining for {0} - {1}: {2}", partner.charaName, why, when),
                    Fix = Shared.Loc.Get("mood.check.pining.fix", "Put both in one house, an occupied toilet within 10 m, or turn off Sex"),
                });
            }
            else if (answer == SexManager.SexInviteState.Yes)
            {
                SexPlace bed = npcs.FindSexPlaceListed(who, 10f, SexPlace.SexPlaceType.Normal, findZeroHeight: true);
                // NPCMove.Live takes 2 love for you first, and costs mood when that
                // leaves it at 0 or below
                if ((bed == null || bed.user != null) && LoveFor(npcs, who, GameManager.selectPlayer) - 2f <= 0f)
                {
                    found.Add(new MoodFinding
                    {
                        Key = "bed", Severity = urgent ? MoodSeverity.Bad : MoodSeverity.Warn, Other = partner,
                        Short = Shared.Loc.Get("mood.check.no_bed", "no bed with {0}", partner.charaName),
                        Effect = Shared.Loc.Get("mood.check.a_time", "-5 a time"),
                        Text = Shared.Loc.Get("mood.check.no_bed.text", "No free bed for them and {0}: {1}", partner.charaName, when),
                        Fix = Shared.Loc.Get("mood.check.no_bed.fix", "Place a bed within 10 m of them, in their house"),
                    });
                }
            }
        }

        // NPCManager.GetSexableLover, without its clean-up (it drops lovers whose
        // record is gone, which a read must not do)
        internal static CommonStates SexableLover(ManagersScript mn, CommonStates who)
        {
            NPCManager npcs = mn.npcMN;
            int best = -1;
            CommonStates bestFriend = null;
            for (int i = 0; i < who.lovers.Count; i++)
            {
                var l = who.lovers[i];
                if (l == null || l.friendID < 0)
                {
                    continue;
                }
                CommonStates friend = npcs.GetFriend(l.friendID);
                if (friend == null)
                {
                    continue;
                }
                if ((l.love >= 50f || !npcs.EnableLove(friend)) && npcs.NPCDistanceCheck(who, friend)
                    && mn.sexMN.SexCheck(who, friend) && (best == -1 || l.love >= who.lovers[best].love))
                {
                    best = i;
                    bestFriend = friend;
                }
            }
            return bestFriend;
        }

        // ---------- you ----------

        private static void TowardYou(CommonStates who, List<MoodFinding> found)
        {
            float love = LoveFor(null, who, GameManager.selectPlayer);
            if (love <= -10f)
            {
                found.Add(new MoodFinding
                {
                    Key = "you", Severity = MoodSeverity.Info,
                    Short = Shared.Loc.Get("mood.check.you", "dislikes you"),
                    Effect = Shared.Loc.Get("mood.check.you.effect", "-1 a change"),
                    Text = Shared.Loc.Get("mood.check.you.text", "Dislikes you (love {0}): each change to it costs 1 mood", love.ToString("0")),
                    Fix = Shared.Loc.Get("mood.check.you.fix", "Take them along: following raises their love for you"),
                });
            }
        }

        // their love for friendID, 0 when they have no record of them
        private static float LoveFor(NPCManager npcs, CommonStates who, int friendID)
        {
            if (who.lovers == null)
            {
                return 0f;
            }
            for (int i = 0; i < who.lovers.Count; i++)
            {
                if (who.lovers[i] != null && who.lovers[i].friendID == friendID)
                {
                    return who.lovers[i].love;
                }
            }
            return 0f;
        }

        // ---------- the headline ----------

        internal static bool Unhappy(CommonStates who)
        {
            return who != null && (who.moral < 0f || (who.debuff != null && who.debuff.discontent != 0));
        }

        // The finding a row names, and how many more stand behind it. Recovery
        // first - someone who never sleeps never climbs back, whatever else is
        // wrong - then the drains that cost mood now, then the ones that will.
        internal static MoodFinding Headline(List<MoodFinding> found, out int more)
        {
            MoodFinding best = null;
            int bestRank = int.MaxValue;
            int counted = 0;
            foreach (MoodFinding f in found)
            {
                int rank = Rank(f);
                if (rank == int.MaxValue)
                {
                    continue;
                }
                counted++;
                if (rank < bestRank)
                {
                    bestRank = rank;
                    best = f;
                }
            }
            more = best != null ? counted - 1 : 0;
            return best;
        }

        private static int Rank(MoodFinding f)
        {
            bool bad = f.Severity == MoodSeverity.Bad;
            switch (f.Key)
            {
                // a kind that never sleeps (Info) is a fact, not a risk: Word names it
                // only for the unhappy, after the ledger
                case "sleep": return bad ? 0 : f.Severity == MoodSeverity.Warn ? 5 : int.MaxValue;
                case "company": return bad ? 1 : 5;
                case "crush": return bad ? 2 : 5;
                case "bed": return bad ? 3 : 5;
                case "far": return f.Severity == MoodSeverity.Warn ? 4 : int.MaxValue;
            }
            return int.MaxValue;   // the level itself, and you, are not causes to name
        }

        // What a row says about them: the headline and a count of the rest, or
        // the ledger's last drain once nothing predicts one, or null.
        internal static string Word(ManagersScript mn, CommonStates who, List<MoodFinding> found)
        {
            MoodFinding head = Headline(found, out int more);
            if (head != null)
            {
                return head.Short + (more > 0 ? " +" + more : string.Empty);
            }
            if (!Unhappy(who))
            {
                return null;
            }
            if (MoodLedger.LastDrain(who, out MoodEvent drain))
            {
                CommonStates other = drain.Partner >= 0 && mn != null && mn.npcMN != null ? mn.npcMN.GetFriend(drain.Partner) : null;
                string name = other != null ? other.charaName : null;
                switch (drain.Cause)
                {
                    case MoodCause.BadCompany: return name != null ? Shared.Loc.Get("mood.check.cant_stand", "can't stand {0}", name)
                        : Shared.Loc.Get("mood.word.company", "bad company");
                    case MoodCause.Crush: return name != null ? Shared.Loc.Get("mood.check.pining", "pining for {0}", name)
                        : Shared.Loc.Get("mood.word.heartbroken", "heartbroken");
                    case MoodCause.NoBed: return name != null ? Shared.Loc.Get("mood.check.no_bed", "no bed with {0}", name)
                        : Shared.Loc.Get("mood.word.no_bed", "no bed");
                    case MoodCause.Execution: return Shared.Loc.Get("mood.word.execution", "saw an execution");
                    case MoodCause.Food: return Shared.Loc.Get("mood.word.food", "bad food");
                }
            }
            foreach (MoodFinding f in found)
            {
                if (f.Key == "sleep" && f.Severity == MoodSeverity.Info) { return f.Short; }
            }
            switch (who.lastMoralCause)
            {
                case NPCManager.MoralCause.Crush: return Shared.Loc.Get("mood.word.heartbroken", "heartbroken");
                case NPCManager.MoralCause.Relation: return Shared.Loc.Get("mood.word.company", "bad company");
                case NPCManager.MoralCause.NoBed: return Shared.Loc.Get("mood.word.no_bed", "no bed");
            }
            return Shared.Loc.Get("mood.word.unhappy", "unhappy");
        }

        // ---------- the census ----------

        // Everyone of yours, worst mood first: mood, level, the last day's net and
        // what moved it, and every finding. Logged, and returned for the harness
        // (invoke IslandUI.MoodCheck Census).
        internal static string Census()
        {
            GameObject go = GameObject.Find("Managers");
            ManagersScript mn = go != null ? go.GetComponent<ManagersScript>() : null;
            if (mn == null || mn.npcMN == null || mn.npcMN.friendList == null)
            {
                return "Mood census: no game loaded";
            }
            var people = new List<CommonStates>();
            foreach (CommonStates who in mn.npcMN.friendList)
            {
                if (who != null && mn.npcMN.IsActiveFriend(who))
                {
                    people.Add(who);
                }
            }
            CommonStates partner = InventoryWindow.Partner(mn);
            if (partner != null)
            {
                people.Add(partner);
            }
            people.Sort((a, b) => a.moral.CompareTo(b.moral));
            int unhappy = 0;
            foreach (CommonStates who in people)
            {
                if (who.moral < 0f) { unhappy++; }
            }
            var sb = new StringBuilder();
            sb.Append("Mood census, day ").Append(MoodLedger.Now(mn).ToString("0.00", CultureInfo.InvariantCulture))
              .Append(": ").Append(unhappy).Append(" unhappy of ").Append(people.Count).Append('\n');
            foreach (CommonStates who in people)
            {
                int level = who.debuff != null ? who.debuff.discontent : 0;
                sb.Append(who.charaName).Append("  mood ").Append(who.moral.ToString("0"))
                  .Append("  level ").Append(level)
                  .Append("  libido ").Append(who.libido.ToString("0"))
                  .Append("  last day ").Append(MoodLedger.Net(mn, who, 1f).ToString("+0;-0;0"));
                if (!who.gameObject.activeInHierarchy) { sb.Append("  (out of range)"); }
                sb.Append('\n');
                AppendCauses(mn, who, sb);
                foreach (MoodFinding f in Check(mn, who))
                {
                    sb.Append("    ").Append(Mark(f.Severity)).Append(' ').Append(f.Text);
                    if (f.Fix != null) { sb.Append("  -> ").Append(f.Fix); }
                    sb.Append('\n');
                }
            }
            string report = sb.ToString();
            Plugin.Log.LogInfo(report);
            return report;
        }

        // the ledger's last day, summed by cause and person
        private static void AppendCauses(ManagersScript mn, CommonStates who, StringBuilder sb)
        {
            IList<MoodEvent> events = MoodLedger.Events(who);
            float since = MoodLedger.Now(mn) - 1f;
            var sums = new Dictionary<string, float>();
            var order = new List<string>();
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Day < since) { continue; }
                string key = events[i].Cause.ToString();
                if (events[i].Partner >= 0)
                {
                    CommonStates other = mn.npcMN.GetFriend(events[i].Partner);
                    key += " " + (other != null ? other.charaName : "#" + events[i].Partner);
                }
                if (!sums.ContainsKey(key)) { sums[key] = 0f; order.Add(key); }
                sums[key] += events[i].Delta;
            }
            if (order.Count == 0)
            {
                return;
            }
            sb.Append("    moved by:");
            foreach (string key in order)
            {
                sb.Append(' ').Append(key).Append(' ').Append(sums[key].ToString("+0;-0;0")).Append(';');
            }
            sb.Append('\n');
        }

        private static string Mark(MoodSeverity s)
        {
            switch (s)
            {
                case MoodSeverity.Bad: return "[!!]";
                case MoodSeverity.Warn: return "[! ]";
                case MoodSeverity.Good: return "[ok]";
                default: return "[  ]";
            }
        }
    }
}
