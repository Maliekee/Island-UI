using System.Collections.Generic;
using IslandUI.UI;
using TMPro;
using UnityEngine;

namespace IslandUI
{
    // MOOD AND RELATIONS, the Social page's two sections, kept apart as their
    // units are. MOOD is theirs, one number: the chart, and under it what moves
    // it - each cause a lane on the chart's clock, its changes at their minutes,
    // the nights' dawns among them, its sum, what comes next and its fix in the
    // column under the MORAL box. RELATIONS is love, per person, each way: a lane
    // for every relation, met or not - a column a love tick, the last eighteen
    // and the next, in the colours PERSONALITY prints the types in (love moves at a
    // tick and nowhere between, so the columns are even and step, where mood's
    // clock is the hours); love both ways in the column. Where love turns into
    // mood it says so: a red notch under each tick that cost mood. The names
    // stand in a gutter left of the axis. Together they take the place of WHO THEY MEET, BONDS, FAMILY and
    // ALL RELATIONS: a bond or a parent is a tag under the name, a relation not
    // met in the three days says where the two feelings stand, and only whoever
    // has no one in the world to draw a lane for is a line of names.
    internal static partial class InventoryWindow
    {
        private const int LaneTicks = LoveLedger.TicksADay * 3;   // the chart's three days of ticks
        private const int LaneColumns = LaneTicks + 1;            // ...and the next, as a ghost
        // the ticks that ran fill the chart's width, so the now line falls on the
        // MOOD chart's right edge above; the ghost stands past it, in the gap
        // before the box column
        private const float LaneGutter = 46f;                     // the names, left of the shared axis
        private const float LaneCell = 3.8f;
        private const int LaneFirstPage = 8;
        private const int LineMoodCaps = 37;     // a line of captions: the lanes' love, or the causes' columns
        private const float FormLaneH = 15f;     // a lane: the name at the form's text size over its tag
        private const float FormLoveHeadH = 13f; // RELATIONS' head: the key, the days, the captions
        private const float CompanyBoxW = 50f;   // the MORAL box, and the three columns under it
        private const float LaneLoveRight = 24f; // LOVE's right edge in the box column...
        private const float LaneBackRight = 49f; // ...and BACK's
        private const float FormWhatMovesH = 8f; // WHAT MOVES THEM over the cause lanes, and the column's captions
        private static readonly string SleepKey = (int)MoodCause.Sleep + ":-1";   // the nights' ledger group (MoodKey)

        private static readonly Color LaneDesire = Theme.ParseColor("#B86E0E", Color.yellow);

        // the chart's and the lanes' shared geometry: the axis, and the box column at the right
        private static void CompanyAxis(float w, out float x0, out float x1, out float boxX)
        {
            float right = w - SheetPadX;
            boxX = right - CompanyBoxW;
            x0 = SheetPadX + LaneGutter;
            x1 = boxX - 6f;
        }

        private sealed class Lane
        {
            public CommonStates Other;
            public LoveLedger.Meeting Days;   // the eighteen ticks
            public LoveLedger.Meeting Day;    // the last six, for the note and the why
            public string Tag;
            public Color TagTint;
            public int Number;
            public int Rank = MetPlainRank;
            public string Note = string.Empty;
            public float Sort;
        }

        private static void WriteLanes(List<SheetLine> lines, CommonStates who, List<MoodCauseRow> causes, float now)
        {
            NPCManager npcs = _mn != null ? _mn.npcMN : null;
            if (npcs == null)
            {
                return;
            }
            string me = string.IsNullOrEmpty(who.charaName) ? "?" : who.charaName;
            int[] ticks = LoveLedger.LastTicks(now, LaneTicks);
            MetRead read = MetOf(who);
            var lanes = new Dictionary<int, Lane>();
            Lane LaneOf(CommonStates other)
            {
                if (!lanes.TryGetValue(other.friendID, out Lane l))
                {
                    l = new Lane { Other = other };
                    lanes[other.friendID] = l;
                }
                return l;
            }
            foreach (LoveLedger.Meeting m in LoveLedger.MetIn(who, ticks))
            {
                CommonStates other = Friend(npcs, m.Partner);
                if (other != null) { LaneOf(other).Days = m; }
            }
            foreach (LoveLedger.Meeting m in read.Met)
            {
                CommonStates other = Friend(npcs, m.Partner);
                if (other == null) { continue; }
                Lane l = LaneOf(other);
                l.Day = m;
                l.Note = MetNote(npcs, who, other, m, me, out l.Rank);
            }
            // THE BONDS ride on their lanes: the game's pick of partner, and every
            // grudge either way - each has a lane, met or not (BondTag names them)
            CommonStates pick = DesiredOf(who);
            if (pick != null)
            {
                LaneOf(pick);
            }
            foreach (CommonStates other in People(npcs))
            {
                if (other != null && !ReferenceEquals(other, who) && GrudgeTag(npcs, who, other) != null)
                {
                    LaneOf(other);
                }
            }
            // EVERY RELATION has a lane, met or not: each of their feelings and
            // each feeling toward them, as ALL RELATIONS counted them - all but the
            // one you play, whose feelings are the Character page's love. One with
            // no one in the world to draw is a name in the line at the foot
            var elsewhere = new List<int>();
            for (int i = 0; who.lovers != null && i < who.lovers.Count; i++)
            {
                CommonStates.Lovers rel = who.lovers[i];
                if (rel == null) { continue; }
                CommonStates other = Friend(npcs, rel.friendID);
                if (other == null) { elsewhere.Add(rel.friendID); }
                else if (!ReferenceEquals(other, who) && !Played(other)) { LaneOf(other); }
            }
            foreach (CommonStates other in People(npcs))
            {
                if (other != null && !ReferenceEquals(other, who) && !Played(other) && LoveBetween(npcs, other, who, out float _))
                {
                    LaneOf(other);
                }
            }
            // FAMILY is a tag on the lane
            // the word in the ELSEWHERE line, and the lane's tag in capitals: two
            // keys, as the code never upper-cases a translation. `kinOrder` keeps
            // FAMILY's order: the parents, then the children
            var family = new Dictionary<int, string>();
            var kinOrder = new List<int>();
            void Kin(int id, string word)
            {
                if (!family.ContainsKey(id)) { kinOrder.Add(id); }
                family[id] = word;
            }
            if (who.status != null && who.status.Length > 9 && who.npcID > 1)
            {
                if (who.status[8] != -1)
                {
                    Kin(who.status[8], Shared.Loc.Get("bonds.family.father", "father"));
                }
                if (who.status[9] != -1)
                {
                    Kin(who.status[9], Shared.Loc.Get("bonds.family.mother", "mother"));
                }
            }
            foreach (CommonStates c in ChildrenOf(npcs, who))
            {
                Kin(c.friendID, Shared.Loc.Get("bonds.family.child", "child"));
            }
            foreach (KeyValuePair<int, string> f in family)
            {
                CommonStates kin = Friend(npcs, f.Key);
                if (kin == null) { elsewhere.Add(f.Key); }
                else if (!Played(kin)) { LaneOf(kin); }
            }
            foreach (Lane l in lanes.Values)
            {
                l.Tag = BondTag(npcs, who, l.Other, pick, out l.TagTint);
                // not met at the last ticks: where the two feelings stand
                if (string.IsNullOrEmpty(l.Note))
                {
                    l.Note = FeelingNote(npcs, who, l.Other, ref l.Rank);
                }
            }
            // the cause rows' numbers on the people they are about
            foreach (MoodCauseRow r in causes)
            {
                if (r.Number > 0 && r.Other != null && lanes.TryGetValue(r.Other.friendID, out Lane l)
                    && (l.Number == 0 || r.Number < l.Number))
                {
                    l.Number = r.Number;
                }
            }
            // ORDER: whoever moves the mood first, as MOOD numbers them; then the trouble, the liked,
            // the plain, the fading; then whoever moved the feeling most
            var order = new List<Lane>(lanes.Values);
            foreach (Lane l in order)
            {
                float sum = 0f;
                if (l.Days != null) { sum = Mathf.Abs(l.Days.MineSum) + Mathf.Abs(l.Days.TheirSum); }
                l.Sort = (l.Number > 0 ? l.Number : 100 + (l.Tag != null && l.Rank >= MetPlainRank ? MetWarn : l.Rank) * 10)
                    * 100000f - sum;
            }
            order.Sort((a, b) => a.Sort != b.Sort ? a.Sort.CompareTo(b.Sort)
                : string.Compare(a.Other.charaName, b.Other.charaName, System.StringComparison.OrdinalIgnoreCase));

            // a column a tick, its middle
            var xs = new float[ticks.Length];
            for (int i = 0; i < ticks.Length; i++)
            {
                xs[i] = (i + 0.5f) / LaneTicks;
            }
            // the key's two types are theirs: who is +10 near them, who -5
            var head = new SheetLine
            {
                Kind = LineMoodCaps, Enabled = true, Style = 0, Label = me, SeriesNow = now,
                Index = who.person != null && who.person.Length > 1 ? who.person[1] : -1,
                Arg = who.person != null && who.person.Length > 2 ? who.person[2] : -1,
                Tip = "<b>" + Shared.Loc.Get("company.ticks.help", "Every love tick of the three days, a column each") + "</b>\n"
                    + TickWord(LoveLedger.TickMark.Liked) + "\n" + TickWord(LoveLedger.TickMark.Plain) + "\n"
                    + TickWord(LoveLedger.TickMark.Disliked) + "\n" + TickWord(LoveLedger.TickMark.NotMet) + "\n"
                    + TickWord(LoveLedger.TickMark.Apart) + "\n" + TipIcon("mood_cost")
                    + Shared.Loc.Get("company.ticks.cost", "-1 moral: a love change while {0}'s love for them is -10 or below", me) + "\n\n"
                    + Shared.Loc.Get("company.head.love", "LOVE") + "  " + Shared.Loc.Get("company.head.love.help", "{0}'s toward each", me) + "\n"
                    + Shared.Loc.Get("company.head.back", "BACK") + "  " + Shared.Loc.Get("company.head.back.help", "each one's toward {0}", me),
            };
            // FAMILY LEADS: the few who are theirs for life, then everyone
            MetActs.Clear();
            WriteFamilyLanes(lines, who, lanes, kinOrder, family, pick, head, ticks, xs, now, read);
            lines.Add(Section(Shared.Loc.Get("company.section", "RELATIONS"), order.Count > 0 ? order.Count.ToString() : string.Empty));
            lines.Add(order.Count > 0 ? head : NoneLine());   // no key over no lanes
            int first = Mathf.Min(order.Count, Mathf.Max(LaneFirstPage, CountFirst(order)));
            int shown = Mathf.Clamp(MetShown(who, first), first, order.Count);
            for (int i = 0; i < shown; i++)
            {
                Lane l = order[i];
                SheetLine lane = LaneLine(who, l, ticks, xs, now);
                // WHY, ON HOVER: where they meet and what would end it, as the lane's
                // tip - the lane itself shows when and what each meeting was worth,
                // and Love off is the fix on their cause's lane in MOOD
                if (l.Day != null)
                {
                    lane.Tip = WhyTip(MetWhyLine(who, l.Other, l.Day, read.Ticks, me, l.Other.charaName ?? "?", l.Rank),
                        me, l.Other.charaName ?? "?");
                }
                lines.Add(lane);
            }
            int rest = order.Count - shown;
            if (rest > 0 || shown > first)
            {
                lines.Add(new SheetLine
                {
                    Kind = LineMetFold, Enabled = true, Arg = first,
                    Options = new[]
                    {
                        rest > 0 ? Shared.Loc.Get("social.show_more", "Show {0} more", Mathf.Min(MetPage, rest)) : null,
                        shown > first ? Shared.Loc.Get("social.show_less", "Show less") : null,
                    },
                    Label = rest > 0 ? Shared.Loc.Get("social.not_shown", "{0} not shown", rest) : null,
                });
            }
            // ELSEWHERE: a relation or a parent the game keeps a name for, and no one
            // in the world to draw a lane for
            var names = new List<string>();
            var listed = new HashSet<int>();
            foreach (int id in elsewhere)
            {
                if (!listed.Add(id)) { continue; }
                string name = FriendNameOf(npcs, id);
                if (!string.IsNullOrEmpty(name))
                {
                    names.Add(family.TryGetValue(id, out string kin) ? Shared.Loc.Get("company.elsewhere.kin", "{0} ({1})", name, kin) : name);
                }
            }
            if (names.Count > 0)
            {
                lines.Add(new SheetLine
                {
                    Kind = LineMoodCaps, Enabled = true, Style = 2,
                    Label = Shared.Loc.Get("company.elsewhere", "ELSEWHERE"), Value = string.Join(Shared.Loc.Separator, names.ToArray()),
                });
            }
        }

        // FAMILY over RELATIONS (Forgejo #132): the same lanes again, theirs
        // only - the parents, then the children - each tagged with what they are
        // to them, and a stronger tag after it. Each also stands in RELATIONS
        // where its order puts it. The one you play has no lane there (their
        // feelings are the Character page's), so here theirs is drawn plain;
        // anyone in no place to draw is a name in the line under it.
        private static void WriteFamilyLanes(List<SheetLine> lines, CommonStates who, Dictionary<int, Lane> lanes,
            List<int> kin, Dictionary<int, string> family, CommonStates pick, SheetLine head, int[] ticks, float[] xs,
            float now, MetRead read)
        {
            if (kin.Count == 0)
            {
                return;
            }
            NPCManager npcs = _mn.npcMN;
            string me = string.IsNullOrEmpty(who.charaName) ? "?" : who.charaName;
            lines.Add(Section(Shared.Loc.Get("bonds.family.section", "FAMILY"), kin.Count.ToString()));
            lines.Add(head);
            var away = new List<string>();
            foreach (int id in kin)
            {
                CommonStates other = Friend(npcs, id);
                if (other == null)
                {
                    string name = FriendNameOf(npcs, id);
                    if (!string.IsNullOrEmpty(name)) { away.Add(Shared.Loc.Get("company.elsewhere.kin", "{0} ({1})", name, family[id])); }
                    continue;
                }
                if (!lanes.TryGetValue(id, out Lane l))
                {
                    l = new Lane { Other = other };
                    l.Tag = BondTag(npcs, who, other, pick, out l.TagTint);
                    l.Note = FeelingNote(npcs, who, other, ref l.Rank);
                }
                SheetLine lane = LaneLine(who, l, ticks, xs, now);
                string word = KinTag(who, other);
                if (l.Tag != null && l.Tag != word)
                {
                    lane.Label2 = word + Shared.Loc.Separator + l.Tag;
                }
                else
                {
                    lane.Label2 = word;
                    lane.Tint = Theme.DossierSoft;
                }
                if (l.Day != null)
                {
                    lane.Tip = WhyTip(MetWhyLine(who, other, l.Day, read.Ticks, me, other.charaName ?? "?", l.Rank),
                        me, other.charaName ?? "?");
                }
                if (Played(other))
                {
                    lane.Link = null;
                    lane.Label = Shared.Loc.Get("bonds.family.you", "{0} (you)", lane.Label);
                }
                lines.Add(lane);
            }
            if (away.Count > 0)
            {
                lines.Add(new SheetLine
                {
                    Kind = LineMoodCaps, Enabled = true, Style = 2,
                    Label = Shared.Loc.Get("company.elsewhere", "ELSEWHERE"), Value = string.Join(Shared.Loc.Separator, away.ToArray()),
                });
            }
        }

        // what `other` is to `who`, as a lane's tag
        private static string KinTag(CommonStates who, CommonStates other)
        {
            if (who.status != null && who.status.Length > 9 && who.npcID > 1)
            {
                if (who.status[8] == other.friendID) { return Shared.Loc.Get("company.tag.father", "FATHER"); }
                if (who.status[9] == other.friendID) { return Shared.Loc.Get("company.tag.mother", "MOTHER"); }
            }
            return Shared.Loc.Get("company.tag.child", "CHILD");
        }

        private static int CountFirst(List<Lane> order)
        {
            int n = 0;
            foreach (Lane l in order) { if (l.Number > 0 || l.Tag != null || l.Rank < MetPlainRank) { n++; } }
            return n;
        }

        private static SheetLine LaneLine(CommonStates who, Lane l, int[] ticks, float[] xs, float now)
        {
            NPCManager npcs = _mn != null ? _mn.npcMN : null;
            CommonStates other = l.Other;
            var marks = new int[ticks.Length + 1];
            for (int i = 0; i < ticks.Length; i++)
            {
                marks[i] = l.Days != null ? (int)l.Days.Mine[i]
                    : (int)(LoveLedger.TickOnRecord(ticks[i]) ? LoveLedger.TickMark.NotMet : LoveLedger.TickMark.Unknown);
            }
            string me = string.IsNullOrEmpty(who.charaName) ? "?" : who.charaName;
            string name = string.IsNullOrEmpty(other.charaName) ? "?" : other.charaName;
            marks[ticks.Length] = NextMark(npcs, who, other, me, name, NextHour(MetOf(who).Ticks), out string nextTip);
            bool mineKnown = LoveBetween(npcs, who, other, out float mine);
            bool theirsKnown = LoveBetween(npcs, other, who, out float theirs);
            // where love turns into mood: each tick's cost - a love change while
            // their love for the person is -10 or below costs 1, recorded at the
            // tick's minute
            var costs = new float[ticks.Length];
            foreach (MoodEvent e in MoodLedger.Events(who))
            {
                if (e.Partner != other.friendID || e.Day < now - 3f || e.Cause != MoodCause.BadCompany)
                {
                    continue;
                }
                int at = -1;
                float gap = 0.06f;
                for (int i = 0; i < ticks.Length; i++)
                {
                    float g = Mathf.Abs(LoveLedger.TickDay(ticks[i]) - e.Day);
                    if (g <= gap) { gap = g; at = i; }
                }
                if (at >= 0) { costs[at] += e.Delta; }
            }
            string costWhy = Shared.Loc.Get("company.cost.why", "{0}'s love for {1} is {2}: at -10 or below, every love change costs 1",
                me, name, mineKnown ? TipMoral(mine) : "-");
            return new SheetLine
            {
                Kind = LineMetPerson, Enabled = true,
                Label = name, Arg = other.friendID, Link = other, Away = AwayTip(other),
                Style = other.person != null && other.person.Length > 0 ? other.person[0] : -1,
                LabelTint = Theme.DossierInk,
                Marks = marks, SeriesX = xs, SeriesNow = now, Number = l.Number,
                Tips = new[] { nextTip, costWhy }, Costs = costs,
                Value = mineKnown ? mine.ToString("0") : "-", Value2 = theirsKnown ? theirs.ToString("0") : "-",
                Label2 = l.Tag ?? (l.Note ?? string.Empty).ToUpperInvariant(),
                Tint = l.Tag != null ? l.TagTint : NoteInk(l.Rank), Index = l.Rank,
            };
        }

        // ---------- a pair, in RELATIONS' words (also the placement panel's neighbours) ----------

        // the game's pick of partner for them, when sex is allowed them
        private static CommonStates DesiredOf(CommonStates who)
        {
            bool sexOff = who.sexInfo != null && who.sexInfo.Length > 9 && who.sexInfo[9] != 0;
            return !sexOff && who.lovers != null && _mn != null && _mn.sexMN != null ? MoodCheck.SexableLover(_mn, who) : null;
        }

        // a grudge either way - love at -10 or below - as the tag under the name, or null
        private static string GrudgeTag(NPCManager npcs, CommonStates who, CommonStates other)
        {
            bool iCant = LoveBetween(npcs, who, other, out float mine) && mine <= -10f;
            bool theyCant = LoveBetween(npcs, other, who, out float theirs) && theirs <= -10f;
            if (!iCant && !theyCant)
            {
                return null;
            }
            string me = string.IsNullOrEmpty(who.charaName) ? "?" : who.charaName;
            return iCant && theyCant ? Shared.Loc.Get("company.tag.cant_stand_each", "CAN'T STAND EACH OTHER")
                : iCant ? Shared.Loc.Get("company.tag.cant_stand", "CAN'T STAND")
                : Shared.Loc.Get("company.tag.cant_stand_who", "CAN'T STAND {0}", me.ToUpperInvariant());
        }

        // THE TAG UNDER A NAME: a grudge, else the game's pick of partner (`pick`,
        // DesiredOf), else family - or null, and a note stands there instead
        private static string BondTag(NPCManager npcs, CommonStates who, CommonStates other, CommonStates pick, out Color tint)
        {
            string tag = GrudgeTag(npcs, who, other);
            if (tag != null)
            {
                tint = MoralBad;
                return tag;
            }
            if (pick != null && ReferenceEquals(pick, other))
            {
                tint = LaneDesire;
                return Shared.Loc.Get("company.tag.desires", "DESIRES");
            }
            tint = Theme.DossierSoft;
            if (other.status != null && other.status.Length > 9
                && (other.status[8] == who.friendID || other.status[9] == who.friendID))
            {
                return Shared.Loc.Get("company.tag.child", "CHILD");
            }
            if (who.status != null && who.status.Length > 9)
            {
                if (who.status[9] == other.friendID) { return Shared.Loc.Get("company.tag.mother", "MOTHER"); }
                if (who.status[8] == other.friendID) { return Shared.Loc.Get("company.tag.father", "FATHER"); }
            }
            return null;
        }

        // where the two feelings stand, in ALL RELATIONS' words; lovers rank good
        private static string FeelingNote(NPCManager npcs, CommonStates who, CommonStates other, ref int rank)
        {
            bool known = LoveBetween(npcs, who, other, out float mine);
            bool backKnown = LoveBetween(npcs, other, who, out float back);
            string me = string.IsNullOrEmpty(who.charaName) ? "?" : who.charaName;
            string name = string.IsNullOrEmpty(other.charaName) ? "?" : other.charaName;
            switch (RelationGroup(known ? mine : 0f, back, backKnown))
            {
                case RelBoth: rank = MetGood; return Shared.Loc.Get("social.note.lovers", "lovers");
                case RelOneWay: return known && mine >= 50f ? Shared.Loc.Get("social.note.loves_not_back", "loves {0}, not back", name)
                    : Shared.Loc.Get("social.note.loved_not_back", "{0} loves {1}, not back", name, me);
                case RelNeither: return Shared.Loc.Get("social.note.acquainted", "acquainted");
            }
            return null;
        }

        // ONE PAIR'S WORDS under the name, as a RELATIONS lane prints them: a
        // bond's tag, else what their meetings at the last ticks said, else where
        // the two feelings stand - for a name with no lane of its own
        private static string PairWords(NPCManager npcs, CommonStates who, CommonStates other, CommonStates pick, out Color tint)
        {
            string tag = BondTag(npcs, who, other, pick, out tint);
            if (tag != null)
            {
                return tag;
            }
            int rank = MetPlainRank;
            string note = null;
            foreach (LoveLedger.Meeting m in MetOf(who).Met)
            {
                if (m.Partner == other.friendID)
                {
                    note = MetNote(npcs, who, other, m, string.IsNullOrEmpty(who.charaName) ? "?" : who.charaName, out rank);
                    break;
                }
            }
            if (string.IsNullOrEmpty(note))
            {
                note = FeelingNote(npcs, who, other, ref rank);
            }
            tint = NoteInk(rank);
            return (note ?? string.Empty).ToUpperInvariant();
        }

        // a note in the colour of how bad it is, as WHO THEY MEET printed it: red
        // can't stand, amber dislike, green like
        private static Color NoteInk(int rank)
        {
            return rank == MetBad ? MoralBad : rank == MetWarn ? PaperWarn : rank == MetGood ? PaperGood : Theme.DossierSoft;
        }

        // THE NIGHTS among the causes: a mark at each dawn of the three days -
        // filled green where a night indoors paid, a red cross where none did,
        // grey where they were at the top already and it had nothing to add;
        // nothing before the record. `line` is the sleep cause's line, its number,
        // sums and fix, or a bare one when the ledger has none
        private static SheetLine NightsLane(CommonStates who, float now, SheetLine line)
        {
            float from = now - 3f;
            float since = MoodLedger.RecordSince(_mn);
            IList<MoodEvent> events = MoodLedger.Events(who);
            var at = new List<float>();
            var state = new List<int>();
            var tips = new List<string>();
            int indoors = 0, known = 0;
            for (int d = Mathf.FloorToInt(from); d <= Mathf.FloorToInt(now); d++)
            {
                float dawn = d + 0.25f;
                if (dawn < from || dawn > now) { continue; }
                at.Add((dawn - from) / 3f);
                string when = "<b>" + DayClock(dawn) + "</b>\n";
                if (dawn < since)
                {
                    state.Add(0);
                    tips.Add(when + Shared.Loc.Get("social.before_record", "Before the record"));
                    continue;
                }
                bool slept = false;
                float before = float.NaN;
                for (int i = 0; i < events.Count; i++)
                {
                    if (events[i].Cause == MoodCause.Sleep && Mathf.Abs(events[i].Day - dawn) < 0.2f) { slept = true; }
                    if (events[i].Day <= dawn) { before = events[i].After; }
                }
                known++;
                if (slept)
                {
                    indoors++;
                    state.Add(1);
                    tips.Add(when + TipIcon("dawn_in") + Shared.Loc.Get("company.dawn.in", "A night indoors: moral up"));
                }
                else if (!float.IsNaN(before) ? before >= 99.5f : who.moral >= 99.5f)
                {
                    state.Add(3);
                    tips.Add(when + TipIcon("dawn_full") + Shared.Loc.Get("company.dawn.full", "At the top: nothing to add"));
                }
                else
                {
                    state.Add(2);
                    tips.Add(when + TipIcon("dawn_out") + Shared.Loc.Get("company.dawn.out", "No night indoors: nothing back"));
                }
            }
            line.Band = 1;
            line.Style = 1;
            line.SeriesNow = now;
            line.MarkX = at.ToArray();
            line.Marks = state.ToArray();
            line.Foots = tips.ToArray();
            line.Link = null;
            line.Label = Shared.Loc.Get("company.nights", "Nights");
            line.Label2 = known > 0 ? Shared.Loc.Get("company.nights.count", "{0} OF {1} INDOORS", indoors, known) : Shared.Loc.Get("mood.chart.no_record", "NO RECORD");
            if (string.IsNullOrEmpty(line.Tip))
            {
                line.Tip = "<b>" + Shared.Loc.Get("company.nights.help", "Each dawn of the three days") + "</b>\n" + TipIcon("dawn_in") + Shared.Loc.Get("company.dawn.in", "A night indoors: moral up") + "\n"
                    + TipIcon("dawn_out") + Shared.Loc.Get("company.dawn.out", "No night indoors: nothing back") + "\n" + TipIcon("dawn_full") + Shared.Loc.Get("company.dawn.full", "At the top: nothing to add");
            }
            return line;
        }

        // A CAUSE ON THE CLOCK: each change its group made in the three days, at
        // its minute (changes within twenty minutes are one mark); a cause about
        // a person is that person's name, the rest of it the tag under
        private static SheetLine CauseLane(CommonStates who, MoodCauseRow r, float now, SheetLine line)
        {
            float from = now - 3f;
            var xs = new List<float>();
            var sums = new List<float>();
            var days = new List<float>();
            if (r.Key != null)
            {
                foreach (MoodEvent e in MoodLedger.Events(who))
                {
                    if (e.Day < from || MoodKey(e) != r.Key) { continue; }
                    float x = Mathf.Clamp01((e.Day - from) / 3f);
                    if (xs.Count > 0 && x - xs[xs.Count - 1] < 1f / 216f)
                    {
                        sums[sums.Count - 1] += e.Delta;
                        continue;
                    }
                    xs.Add(x);
                    sums.Add(e.Delta);
                    days.Add(e.Day);
                }
            }
            var tips = new string[xs.Count];
            for (int i = 0; i < xs.Count; i++)
            {
                tips[i] = "<b>" + DayClock(days[i]) + "</b>\n" + r.Label + "  " + TipSigned(sums[i]);
            }
            line.Band = 1;
            line.SeriesNow = now;
            line.MarkX = xs.ToArray();
            line.Fracs = sums.ToArray();
            line.Foots = tips;
            string name = r.Other != null ? r.Other.charaName : null;
            int at = !string.IsNullOrEmpty(name) && r.Label != null ? r.Label.LastIndexOf(name, System.StringComparison.Ordinal) : -1;
            if (at >= 0)
            {
                line.Label = name;
                line.Label2 = (r.Label.Substring(0, at) + r.Label.Substring(at + name.Length)).Trim().ToUpperInvariant();
            }
            return line;
        }

        private static float CauseLaneH(SheetLine line)
        {
            return line.Style == 3 ? FixRowH(line) : FormLaneH;
        }

        // RELATIONS' ground: a column a tick, the eighteen and the next - the
        // night's two (21:00 and 00:00) shaded, a dash where a day begins. The
        // window is the ticks that ran, so it steps when one runs, never slides
        private static int[] _groundTicks;
        private static float _groundNow = float.NaN;

        private static int[] GroundTicks(float now)
        {
            if (_groundTicks == null || now != _groundNow)
            {
                _groundTicks = LoveLedger.LastTicks(now, LaneTicks);
                _groundNow = now;
            }
            return _groundTicks;
        }

        private static int TickSlot(int key)
        {
            return ((key % LoveLedger.TicksADay) + LoveLedger.TicksADay) % LoveLedger.TicksADay;
        }

        // ...and the now line where the ticks that ran end and the next begins, down
        // from `from`: NOW is a point between two columns, not a column. The shading
        // and the dashes start at `top`
        private static void TickGround(SheetRow row, float now, float x0, float x1, float h, float from = 0f, float top = 0f)
        {
            int[] ticks = GroundTicks(now);
            if (ticks.Length == 0) { return; }
            float cw = (x1 - x0) / LaneTicks;
            for (int i = 0; i < LaneColumns; i++)
            {
                int slot = TickSlot(ticks[0] + i);
                if (slot == 0 || slot == LoveLedger.TicksADay - 1)
                {
                    FormImage(row, null, 0, MetNight, x0 + i * cw, top, cw, h - top);
                }
                if (slot == 0 && i > 0)
                {
                    FormDash(row, Alpha(Theme.DossierSoft, 0.8f), x0 + i * cw, top, h - top, true, 0.3f, 0.8f, 0.8f);
                }
            }
            FormImage(row, null, 0, Theme.DossierInk, x0 + ticks.Length * cw - Hair * 0.5f, from, Hair, h - from);
        }

        // a cause lane's background: the nights shaded and the midnights dashed, as on the chart
        private static void LaneGround(SheetRow row, float now, float x0, float x1, float h)
        {
            float from = now - 3f;
            for (int d = Mathf.FloorToInt(from) - 1; d <= Mathf.FloorToInt(now); d++)
            {
                float a = Mathf.Max(0f, (d + 21f / 24f - from) / 3f);
                float b = Mathf.Min(1f, (d + 1f + 6f / 24f - from) / 3f);
                if (b > a)
                {
                    FormImage(row, null, 0, MetNight, x0 + a * (x1 - x0), 0f, (b - a) * (x1 - x0), h);
                }
            }
            for (int d = Mathf.FloorToInt(from) + 1; d <= Mathf.FloorToInt(now); d++)
            {
                FormDash(row, Alpha(Theme.DossierSoft, 0.8f), x0 + (d - from) / 3f * (x1 - x0), 0f, h, true, 0.3f, 0.8f, 0.8f);
            }
        }

        private static int DrawMoodLane(SheetRow row, SheetLine line, float w, float h)
        {
            float left = SheetPadX;
            float right = w - SheetPadX;
            CompanyAxis(w, out float x0, out float x1, out float boxX);
            TickGround(row, line.SeriesNow, x0, x1, h);
            // the gutter: the token and the name (planks 0, 1), the tag under - no
            // cause's number: MOOD is a section of its own, its lanes have theirs.
            // FAMILY's lane for the one you play leads nowhere, and is drawn plain
            bool linked = line.Link != null;
            if (!linked)
            {
                FormNoHit(row, 0);
                FormNoHit(row, 1);
            }
            DrawLaneName(row, line.Style, line.Label, line.LabelTint, line.Away, line.Label2, line.Tint, left, x0 - 2f, linked);
            // the eighteen ticks at their hours, the refusals over them, the next as a ghost
            float cy = h * 0.5f - LaneCell * 0.5f;
            int n = line.SeriesX != null && line.Marks != null ? Mathf.Min(line.SeriesX.Length, line.Marks.Length) : 0;
            for (int i = 0; i < n; i++)
            {
                var mark = (LoveLedger.TickMark)line.Marks[i];
                float cx = x0 + line.SeriesX[i] * (x1 - x0) - LaneCell * 0.5f;
                MetCellDraw(row, mark, cx, cy, 1f, false, LaneCell);
                string word = TickWord(mark);
                float cost = line.Costs != null && i < line.Costs.Length ? line.Costs[i] : 0f;
                if (cost <= -0.5f)
                {
                    // the bridge: a love change that cost mood, a red notch under it
                    FormImage(row, null, 0, MoralBad, cx, cy + LaneCell + 0.7f, LaneCell, 0.8f);
                    word += "\n" + TipIcon("mood_cost") + Shared.Loc.Get("company.cost", "{0} moral: {1}", TipSigned(cost),
                        line.Tips != null && line.Tips.Length > 1 ? line.Tips[1] : string.Empty);
                }
                row.TipZones.Add(new Rect(cx - 0.6f, 0f, LaneCell + 1.2f, h));
                row.TipTexts.Add(word);
            }
            if (line.Marks != null && line.Marks.Length > n && line.Marks[n] >= 0)
            {
                float gx = x0 + (n + 0.5f) / LaneTicks * (x1 - x0) - LaneCell * 0.5f;
                MetCellDraw(row, (LoveLedger.TickMark)line.Marks[n], gx, cy, 1f, true, LaneCell);
                if (line.Tips != null && line.Tips.Length > 0 && line.Tips[0] != null)
                {
                    row.TipZones.Add(new Rect(gx - 0.6f, 0f, LaneCell + 1.2f, h));
                    row.TipTexts.Add(line.Tips[0]);
                }
            }
            // love, theirs toward them and back, under the MORAL box
            LaneLove(row, line.Value, boxX + LaneLoveRight, h);
            LaneLove(row, line.Value2, boxX + LaneBackRight, h);
            FormRule(row, Theme.DossierRule, left, h - 0.5f, right - left);
            if (!string.IsNullOrEmpty(line.Tip))
            {
                row.TipZones.Add(new Rect(left, 0f, right - left, h));
                row.TipTexts.Add(line.Tip);
            }
            return 2;
        }

        // A LANE'S NAME, from `nx` to `right`: the type's token, the name, the tag
        // under it in its colour. Linked (RELATIONS), the token and the name are
        // planks 0 and 1, and a name away says where; the placement panel's
        // neighbours draw it plain, a click there being the pick's.
        private const float LaneNameH = 9f;

        private static void DrawLaneName(SheetRow row, int type, string name, Color nameTint, string away,
            string tag, Color tagTint, float nx, float right, bool linked)
        {
            const float ts = 6f;
            if (type >= 0)
            {
                DrawTraitToken(row, type, nx, 1.6f, ts);
            }
            if (linked && type >= 0)
            {
                if (Hot(row, 0)) { HoverFrame(row, nx - 1f, 0.6f, ts + 2f, ts + 2f); }
                FormHit(row, 0, nx - 1f, 0f, ts + 2f, LaneNameH);
            }
            else if (linked)
            {
                FormNoHit(row, 0);
            }
            float tx = nx + ts + 1.8f;
            float nameW = Mathf.Min(MeasureForm(row, name ?? string.Empty, FormTextSize) + 1f, right - tx);
            FormText(row, name ?? string.Empty, FormTextSize, nameTint, TextAlignmentOptions.MidlineLeft, false,
                tx, 0.2f, nameW, LaneNameH, 0f, fit: true);
            if (linked)
            {
                if (Hot(row, 1)) { FormRule(row, Theme.DossierInk, tx, LaneNameH - 0.6f, nameW, 0.4f); }
                FormHit(row, 1, tx, 0f, nameW, LaneNameH);
            }
            if (!string.IsNullOrEmpty(away))
            {
                row.TipZones.Add(new Rect(tx, 0f, nameW, LaneNameH));
                row.TipTexts.Add(away);
            }
            FormText(row, tag ?? string.Empty, CaptionSize * 0.9f, tagTint, TextAlignmentOptions.MidlineLeft, true,
                nx, LaneNameH, right - nx, 5f, CaptionSpacing * 0.3f, fit: true);
        }

        // the why a lane once opened to, as its tip: the two, then each line of it,
        // then what keeps or ends it
        private static string WhyTip(SheetLine why, string me, string name)
        {
            var sb = new System.Text.StringBuilder("<b>").Append(Shared.Loc.Get("company.why.pair", "{0} and {1}", me, name)).Append("</b>");
            foreach (string say in why.Values ?? new string[0])
            {
                sb.Append('\n').Append(say);
            }
            if (!string.IsNullOrEmpty(why.Label))
            {
                sb.Append("\n\n").Append(why.Label);
            }
            return sb.ToString();
        }

        private static void LaneLove(SheetRow row, string text, float rightX, float h)
        {
            text = text ?? string.Empty;
            float.TryParse(text, out float v);
            Color ink = text == "-" ? Theme.DossierSoft : v <= -10f ? MoralBad : v >= 50f ? PaperGood : Theme.DossierInk;
            FormText(row, text, FormTextSize * 0.85f, ink, TextAlignmentOptions.MidlineRight, false, rightX - 16f, 0f, 16f, h);
        }

        // one entry of RELATIONS' key: a tick's square and what it is worth, and
        // the type it is worth that near, when it is one of theirs
        private static void LoveKey(SheetRow row, LoveLedger.TickMark mark, string word, ref float x, float y, float s, float size,
            int type = -1)
        {
            MetCellDraw(row, mark, x, y, 1f, false, s);
            float w = MeasureForm(row, word, size) + 1.5f;
            FormText(row, word, size, Theme.DossierSoft, TextAlignmentOptions.MidlineLeft, false, x + s + 0.8f, y - 0.7f, w, s + 1.4f);
            x += s + 0.8f + w;
            if (type >= 0)
            {
                const float ts = 4.4f;
                DrawTraitToken(row, type, x, y + s * 0.5f - ts * 0.5f, ts);
                row.TipZones.Add(new Rect(x, 0f, ts, y + s + 1f));
                row.TipTexts.Add(TypeWithIcon(type));
                x += ts + 0.6f;
            }
            x += 2f;
        }

        // a cause's number on a lane, in its row's colour
        private static readonly Dictionary<int, Color> _moodNumberTints = new Dictionary<int, Color>();

        private static Color MoodNumberTint(int number)
        {
            return _moodNumberTints.TryGetValue(number, out Color c) ? c : Theme.DossierSoft;
        }

        // A CAUSE LANE: the gutter as a person's lane - its number, a person's
        // token or the nights' moon, the name and the tag - its changes on the
        // clock, and in the box column what it moved them by, bold, what it will
        // do next, and its fix under them
        private static int DrawCauseLane(SheetRow row, SheetLine line, float w, float h)
        {
            if (line.Style == 3)
            {
                return DrawFixRow(row, line, w, h);
            }
            float left = SheetPadX;
            float right = w - SheetPadX;
            CompanyAxis(w, out float x0, out float x1, out float boxX);
            bool fold = line.Args != null && line.Args.Length > 1 && line.Args[0] == 0;
            if (fold && (Hot(row, 1) || line.On))
            {
                FormImage(row, null, 0, MetOpenWash, left - 1.5f, 0f, right - left + 3f, h);
            }
            LaneGround(row, line.SeriesNow, x0, x1, h);
            Color tint = line.ValueTint;
            float nx = left;
            if (line.Number > 0)
            {
                const float ns = 5.2f;
                FormImage(row, null, 0, tint, nx, 1.2f, ns, ns);
                FormText(row, line.Number.ToString(), ns * 0.78f, MoodMarkInk, TextAlignmentOptions.Center, true, nx, 1.3f, ns, ns);
            }
            nx += 6.5f;
            const float ts = 6f, nameH = 9f;
            float tx = nx;
            if (line.Style == 1)
            {
                FormImage(row, "kit_moon", 0, Theme.DossierSoft, nx + 0.4f, 1.4f, 5.2f, 5.2f);
                tx += ts + 1.8f;
            }
            else if (line.Link != null && line.Link.person != null && line.Link.person.Length > 0)
            {
                DrawTraitToken(row, line.Link.person[0], nx, 1.6f, ts);
                tx += ts + 1.8f;
            }
            string label = line.Label ?? string.Empty;
            float nameW = Mathf.Max(0f, Mathf.Min(MeasureForm(row, label, FormTextSize) + 1f, x0 - 2f - tx));
            FormText(row, label, FormTextSize, Theme.DossierInk, TextAlignmentOptions.MidlineLeft, false,
                tx, 0.2f, nameW, nameH, 0f, fit: true);
            int planks = 0;
            if (line.Link != null)
            {
                if (Hot(row, 0)) { FormRule(row, Theme.DossierInk, tx, nameH - 0.6f, nameW, 0.4f); }
                FormHit(row, 0, tx, 0f, nameW, nameH);
                row.TipZones.Add(new Rect(tx, 0f, nameW, nameH));
                row.TipTexts.Add(Shared.Loc.Get("sheet.click_to_target", "Click to target {0}", label));
                planks = 1;
            }
            else
            {
                FormNoHit(row, 0);
            }
            if (!string.IsNullOrEmpty(line.Label2))
            {
                FormText(row, line.Label2, CaptionSize * 0.9f, line.Style == 1 ? Theme.DossierSoft : tint, TextAlignmentOptions.MidlineLeft, true,
                    nx, nameH, x0 - 2f - nx, 5f, CaptionSpacing * 0.3f, fit: true);
            }
            // the changes at their minutes: the nights' dawn marks, or a square a change
            int n = line.MarkX != null ? line.MarkX.Length : 0;
            for (int i = 0; i < n; i++)
            {
                float mx = x0 + line.MarkX[i] * (x1 - x0);
                if (line.Style == 1)
                {
                    // Zz: green a night indoors paid, grey at the top; none did, a red
                    // cross where the big Z stands - a shape of its own, not the Zz in red
                    const float s = 7f;
                    float cy = FormLaneH * 0.5f - s * 0.5f;
                    int state = line.Marks != null && i < line.Marks.Length ? line.Marks[i] : 0;
                    if (state == 1) { FormImage(row, "kit_sleep", 0, PaperGood, mx - s * 0.5f, cy, s, s); }
                    else if (state == 2) { FormImage(row, "kit_sleep_none", 0, MoralBad, mx - s * 0.5f, cy, s, s); }
                    else if (state == 3) { FormImage(row, "kit_sleep", 0, Alpha(Theme.DossierSoft, 0.7f), mx - s * 0.5f, cy, s, s); }
                    row.TipZones.Add(new Rect(mx - s * 0.5f, 0f, s, FormLaneH));
                }
                else
                {
                    float d = line.Fracs != null && i < line.Fracs.Length ? line.Fracs[i] : 0f;
                    Color c = d > 0f ? PaperGood : line.Index == (int)MoodSeverity.Warn ? PaperWarn : MoralBad;
                    float cy = FormLaneH * 0.5f - LaneCell * 0.5f;
                    FormImage(row, null, 0, c, mx - LaneCell * 0.5f, cy, LaneCell, LaneCell);
                    row.TipZones.Add(new Rect(mx - LaneCell * 0.5f - 0.6f, 0f, LaneCell + 1.2f, FormLaneH));
                }
                row.TipTexts.Add(line.Foots != null && i < line.Foots.Length ? line.Foots[i] : string.Empty);
            }
            // the box column: the three days' sum at the right, bold, what comes next
            // before it, and the fix under them
            if (!string.IsNullOrEmpty(line.Value2))
            {
                FormText(row, line.Value2, FormTextSize, line.Value2Tint, TextAlignmentOptions.MidlineRight, true,
                    right - 18f, 0f, 18f, nameH + 1f);
            }
            if (!string.IsNullOrEmpty(line.Value))
            {
                FormText(row, line.Value, FormTextSize * 0.7f, line.Number > 0 ? Theme.DossierSoft : tint,
                    TextAlignmentOptions.MidlineRight, false, boxX, 0f, right - 19f - boxX, nameH + 1f, 0f, fit: true);
            }
            if (fold)
            {
                // the fix folded under a chevron at the lane's end, below the numbers
                const float cw = 6f, ch = 4f;
                float cx = right - cw - 0.5f, cy = nameH + 0.6f;
                Color ink = line.On || Hot(row, 1) ? Theme.DossierInk : Theme.DossierSoft;
                FormImage(row, "kit_chevron", 0, ink, cx, cy, cw, ch);
                FormHit(row, 1, cx - 2f, nameH, cw + 2.5f, h - nameH);
                row.TipZones.Add(new Rect(cx - 2f, nameH, cw + 2.5f, h - nameH));
                row.TipTexts.Add(line.On ? Shared.Loc.Get("company.fix.fold", "Click to fold the fix") : Shared.Loc.Get("company.fix.unfold", "There is a fix: click to see it"));
                planks = 2;
            }
            if (!line.On)
            {
                FormRule(row, Theme.DossierRule, left, h - 0.5f, right - left);
            }
            if (!string.IsNullOrEmpty(line.Tip))
            {
                row.TipZones.Add(new Rect(left, 0f, right - left, h));
                row.TipTexts.Add(line.Tip);
            }
            return planks;
        }

        // A LANE'S FIX, UNFOLDED: what would help on the left, its button on the right,
        // on the lane's wash so it reads as the lane's
        private const float FixTextH = 8f;

        private static float FixRowH(SheetLine line)
        {
            return line.Options != null && line.Options.Length > 0 ? FixTextH + 11f : FixTextH + 2f;
        }

        private static int DrawFixRow(SheetRow row, SheetLine line, float w, float h)
        {
            float left = SheetPadX;
            float right = w - SheetPadX;
            FormImage(row, null, 0, MetOpenWash, left - 1.5f, 0f, right - left + 3f, h);
            FormNoHit(row, 0);
            // the advice on its own line, the buttons under it from the right
            int n = line.Options != null ? line.Options.Length : 0;
            float tx = left + 6.5f;
            if (!string.IsNullOrEmpty(line.Label))
            {
                FormText(row, line.Label, FormTextSize * 0.8f, Theme.DossierInk, TextAlignmentOptions.MidlineLeft, false,
                    tx, 0.5f, right - tx, FixTextH, 0f, fit: true);
            }
            const float bs = FormTextSize * 0.75f, bh = 8f;
            float by = FixTextH + 1f, bx = right;
            for (int i = n - 1; i >= 0; i--)
            {
                float bw = MeasureForm(row, line.Options[i], bs) + 6f;
                bx -= bw;
                DrawSmallButton(row, i + 1, line.Options[i], bx, by, bw, bh, bs);
                if (line.Tips != null && i < line.Tips.Length && !string.IsNullOrEmpty(line.Tips[i]))
                {
                    row.TipZones.Add(new Rect(bx, by, bw, bh));
                    row.TipTexts.Add(line.Tips[i]);
                }
                bx -= 2f;
            }
            FormRule(row, Theme.DossierRule, left, h - 0.5f, right - left);
            return n + 1;
        }

        // a cause lane's fix: the form's button, small enough to sit under the numbers
        private static void DrawSmallButton(SheetRow row, int k, string label, float x, float y, float bw, float bh, float size)
        {
            Color ink = Theme.DossierInk;
            bool down = Down(row, k);
            if (Hot(row, k))
            {
                FormImage(row, "kit_dossier_chip", 8, Alpha(ink, down ? 0.22f : 0.1f), x, y, bw, bh);
            }
            FormImage(row, "kit_dossier_button", 12, ink, x, y, bw, bh);
            FormText(row, label ?? string.Empty, size, ink, TextAlignmentOptions.Center, false, x, y + (down ? 0.3f : 0f), bw, bh);
            FormHit(row, k, x, y, bw, bh);
        }

        // CAPTIONS: Style 0 RELATIONS' head; 1 the causes' head, 3 DAYS and NEXT;
        // 2 a caption and a line of names (not met in three days)
        private static void DrawMoodCaps(SheetRow row, SheetLine line, float w, float h)
        {
            float left = SheetPadX;
            float right = w - SheetPadX;
            CompanyAxis(w, out float x0, out float x1, out float boxX);
            float cs = CaptionSize * 0.86f, sp = CaptionSpacing * 0.4f;
            switch (line.Style)
            {
                case 0:
                {
                    // RELATIONS' head: the squares' key over the ticks, the days
                    // under it over their columns, and the two columns' captions;
                    // the night columns shaded through the days' band only, a margin
                    // clear of the key (Forgejo #147), the now line from under NOW
                    const float band = 7f;
                    TickGround(row, line.SeriesNow, x0, x1, h, 12.2f, band);
                    const float ks = 3f, ky = 1.6f;
                    float kx = x0;
                    // the types are theirs (Index the liked, Arg the disliked):
                    // +10 near a Mean one, +5 near anyone else, -5 near an Acts-tough one
                    bool theirs = line.Index >= 0 || line.Arg >= 0;
                    LoveKey(row, LoveLedger.TickMark.Liked, "+10", ref kx, ky, ks, cs, line.Index);
                    LoveKey(row, LoveLedger.TickMark.Plain, theirs ? Shared.Loc.Get("company.key.others", "+5 others") : "+5", ref kx, ky, ks, cs);
                    LoveKey(row, LoveLedger.TickMark.Disliked, "-5", ref kx, ky, ks, cs, line.Arg);
                    string perMeeting = Shared.Loc.Get("company.key.per_meeting", "LOVE A MEETING");
                    float lw = MeasureForm(row, perMeeting, cs) * 1.25f + 2f;
                    FormText(row, perMeeting, cs, Theme.DossierSoft, TextAlignmentOptions.MidlineLeft, true,
                        kx, ky - 0.7f, lw, ks + 1.4f, sp);
                    kx += lw + 4f;
                    FormImage(row, null, 0, MoralBad, kx, ky + ks * 0.5f - 0.4f, ks, 0.8f);
                    FormText(row, Shared.Loc.Get("company.key.mood_cost", "-1 MORAL"), cs, MoralBad, TextAlignmentOptions.MidlineLeft, true,
                        kx + ks + 1.2f, ky - 0.7f, Mathf.Max(0f, x1 - kx - ks - 1.2f), ks + 1.4f, sp, fit: true);
                    // a day's label over its first tick, left out where it would run into NOW's
                    const float dy = band + 0.6f;
                    int[] ticks = GroundTicks(line.SeriesNow);
                    float colW = (x1 - x0) / LaneTicks;
                    float nowRight = x0 + ticks.Length * colW;
                    string nowWord = Shared.Loc.Get("chart.now", "NOW");
                    float nowX = nowRight - MeasureForm(row, nowWord, cs) * 0.5f - 3f;
                    for (int i = 0; i < ticks.Length; i++)
                    {
                        if (TickSlot(ticks[i]) != 0) { continue; }
                        string day = Shared.Loc.Get("chart.day", "DAY {0}", Mathf.FloorToInt(LoveLedger.TickDay(ticks[i])));
                        float mx = x0 + i * colW;
                        if (mx + 1f + MeasureForm(row, day, cs) + 2.5f < nowX)
                        {
                            FormText(row, day, cs, Theme.DossierSoft, TextAlignmentOptions.MidlineLeft, true, mx + 1f, dy, 24f, 4.4f, sp);
                        }
                    }
                    // NOW over the now line, which runs down from under it through the lanes
                    FormText(row, nowWord, cs, Theme.DossierInk, TextAlignmentOptions.Center, true, nowRight - 10f, dy, 20f, 4.4f, sp);
                    FormText(row, Shared.Loc.Get("company.head.love", "LOVE"), cs, Theme.DossierSoft, TextAlignmentOptions.MidlineRight, true,
                        boxX + LaneLoveRight - 16f, dy, 16f, 4.4f, sp);
                    FormText(row, Shared.Loc.Get("company.head.back", "BACK"), cs, Theme.DossierSoft, TextAlignmentOptions.MidlineRight, true,
                        boxX + LaneBackRight - 16f, dy, 16f, 4.4f, sp);
                    FormRule(row, Theme.DossierRule, left, h - 0.5f, right - left);
                    break;
                }
                case 1 when line.Band == 1:
                    // the Social page's: a caption over the cause lanes, the shading run
                    // on, and the box column's two captions
                    LaneGround(row, line.SeriesNow, x0, x1, h);
                    FormText(row, Shared.Loc.Get("company.what_moves", "WHAT MOVES THEM"), cs, Theme.DossierInk, TextAlignmentOptions.MidlineLeft, true,
                        left, 1.5f, x0 - left - 2f, h - 2f, sp, fit: true);
                    FormText(row, MoodNextCaption, cs, Theme.DossierSoft, TextAlignmentOptions.MidlineRight, true,
                        boxX, 1.5f, right - 19f - boxX, h - 2f, sp);
                    FormText(row, MoodPastCaption, cs, Theme.DossierSoft, TextAlignmentOptions.MidlineRight, true,
                        right - 18f, 1.5f, 18f, h - 2f, sp);
                    break;
                case 1:
                    // WHAT MOVES THEM: a section's head - spaced capitals and a rule -
                    // with the columns' captions standing at its right end, over them
                    float cy = h - 6f;
                    float ruleEnd = right;
                    if (line.Columns != null)
                    {
                        MoodColumnEdges(row, line.Columns, right, out float pastRight, out float pastW,
                            out float nextRight, out float nextW);
                        if (pastW > 0f)
                        {
                            FormText(row, MoodPastCaption, CaptionSize, Theme.DossierSoft, TextAlignmentOptions.MidlineRight, true,
                                pastRight - pastW - 4f, cy - 3f, pastW + 4f, 6f, sp);
                            ruleEnd = Mathf.Min(ruleEnd, pastRight - MeasureForm(row, MoodPastCaption, CaptionSize) * 1.25f - 4f);
                        }
                        if (nextW > 0f)
                        {
                            FormText(row, MoodNextCaption, CaptionSize, Theme.DossierSoft, TextAlignmentOptions.MidlineRight, true,
                                nextRight - nextW - 4f, cy - 3f, nextW + 4f, 6f, sp);
                            ruleEnd = Mathf.Min(ruleEnd, nextRight - MeasureForm(row, MoodNextCaption, CaptionSize) * 1.25f - 4f);
                        }
                    }
                    TextMeshProUGUI title = FormText(row, Shared.Loc.Get("company.what_moves", "WHAT MOVES THEM"), SectionSize, Theme.DossierInk,
                        TextAlignmentOptions.MidlineLeft, true, left, cy - 4f, right - left, 8f, CaptionSpacing + 6f);
                    float ruleX = left + TextWidth(title) + 4f;
                    FormRule(row, Theme.DossierInk, ruleX, cy, ruleEnd - ruleX);
                    break;
                default:
                    // measured without the spacing, which the capitals take on top
                    float cw = MeasureForm(row, line.Label ?? string.Empty, cs) * 1.25f + 4f;
                    FormText(row, line.Label ?? string.Empty, cs, Theme.DossierSoft, TextAlignmentOptions.MidlineLeft, true,
                        left, 0f, cw, h, sp);
                    FormText(row, line.Value ?? string.Empty, FormTextSize * 0.78f, Theme.DossierInk, TextAlignmentOptions.MidlineLeft,
                        false, left + cw + 3f, 0f, right - left - cw - 3f, h, 0f, fit: true);
                    break;
            }
            if (!string.IsNullOrEmpty(line.Tip))
            {
                row.TipZones.Add(new Rect(left, 0f, right - left, h));
                row.TipTexts.Add(line.Tip);
            }
        }
    }
}
