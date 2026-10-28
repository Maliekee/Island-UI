using System.Collections.Generic;
using IslandUI.UI;
using TMPro;
using UnityEngine;

namespace IslandUI
{
    // InventoryWindow — MET TODAY, the Social page's ledger (docs/social-links.md
    // section 9). Not a forecast: what the last
    // six love ticks did, read from the love ledger (Model/LoveLedger.cs). One row
    // per person met - their type's token, their name, their mood - then a square
    // per tick in what this person felt when they met (+10 liked, +5 anyone else,
    // -5 disliked), hollow when they did not meet, a dash where a bond faded apart;
    // the sums each way, and a note. Trouble first; the plain ones, +5 both ways
    // whenever they met, fold into a line of names.
    internal static partial class InventoryWindow
    {
        private const float FormMetHeadH = 20f;
        private const float MetTimeH = 5f;   // the head's line of time: "a day ago" to NOW
        private const float MetMoonH = 6f;   // the head's line of moons over the night columns
        private const float FormMetPersonH = 13f;
        private const float FormMetFoldH = 21f;
        private const float MetCell = 6.5f;
        private const float MetCellGap = 1.6f;
        private const float MetNowGap = 3f;   // the now line's room, between the six and the next

        private static readonly Color MetPlain = Theme.ParseColor("#9A8670", Color.gray);
        private static readonly Color MetNight = Theme.ParseColor("#E6D9BF", Color.gray);

        // a row's rank, which is also its order; the last two fold into lines of names
        private const int MetBad = 0, MetWarn = 1, MetSoft = 2, MetGood = 3, MetPlainRank = 4, MetApartRank = 5;

        // ---------- the read, at most once a second ----------

        private sealed class MetRead
        {
            public float At;
            public int[] Ticks;
            public List<LoveLedger.Meeting> Met;
        }

        private static readonly Dictionary<CommonStates, MetRead> MetReads = new Dictionary<CommonStates, MetRead>();

        private static MetRead MetOf(CommonStates who)
        {
            float now = Time.unscaledTime;
            if (MetReads.TryGetValue(who, out MetRead read) && now - read.At < 1f)
            {
                return read;
            }
            if (MetReads.Count > 64)
            {
                MetReads.Clear();
            }
            int[] ticks = LoveLedger.LastTicks(MoodLedger.Now(_mn));
            read = new MetRead { At = now, Ticks = ticks, Met = LoveLedger.MetIn(who, ticks) };
            MetReads[who] = read;
            return read;
        }

        // ---------- the page's lines ----------

        private static void WriteMetToday(List<SheetLine> lines, CommonStates who)
        {
            NPCManager npcs = _mn != null ? _mn.npcMN : null;
            MetRead read = MetOf(who);
            string me = string.IsNullOrEmpty(who.charaName) ? "?" : who.charaName;
            MetActs.Clear();
            // ONE LIST, worst first: trouble, then the liked, then the plain, then
            // the fading. The first page is everything that is not plain (six at
            // least); Show more adds a page, Show less goes back to the first.
            var rows = new List<MetRow>();
            foreach (LoveLedger.Meeting m in read.Met)
            {
                CommonStates other = Friend(npcs, m.Partner);
                if (other == null)
                {
                    continue;
                }
                string name = string.IsNullOrEmpty(other.charaName) ? "?" : other.charaName;
                string note = MetNote(npcs, who, other, m, me, out int rank);
                var marks = new int[LoveLedger.TicksADay + 1];
                for (int i = 0; i < LoveLedger.TicksADay; i++) { marks[i] = (int)m.Mine[i]; }
                marks[LoveLedger.TicksADay] = NextMark(npcs, who, other, me, name, NextHour(read.Ticks), out string nextTip);
                bool open = IsMetOpen(who, m.Partner);
                rows.Add(new MetRow
                {
                    Name = name,
                    Rank = rank * 10000f + (rank < MetPlainRank ? Mathf.Min(m.MineSum, m.TheirSum) : 0f),
                    Plain = rank >= MetPlainRank,
                    Line = new SheetLine
                    {
                        Kind = LineMetPerson, Enabled = true,
                        // someone not in the world (carried, a prisoner) is no link
                        Label = name,
                        Arg = m.Partner, On = open, Link = other, Away = AwayTip(other),
                        Style = other.person != null && other.person.Length > 0 ? other.person[0] : -1,
                        LabelTint = Theme.DossierInk,
                        Icons = new[] { MoodFace(other, out Color mood) }, Tint = mood, Frac = MoodFill(other),
                        Marks = marks, Cans = NightTicks(read.Ticks), Tips = new[] { nextTip },
                        Value = Signed(m.MineSum), Value2 = Signed(m.TheirSum),
                        Label2 = note, Index = rank,
                        Tip = open ? Shared.Loc.Get("social.click_close", "Click to close") : Shared.Loc.Get("met.row.help", "Click for why, and what would end it"),
                    },
                    Why = open ? MetWhyLine(who, other, m, read.Ticks, me, name, rank) : (SheetLine?)null,
                });
            }
            rows.Sort((a, b) => a.Rank != b.Rank ? a.Rank.CompareTo(b.Rank)
                : string.Compare(a.Name, b.Name, System.StringComparison.OrdinalIgnoreCase));

            int met = 0;
            foreach (MetRow r in rows) { if (r.Line.Index != MetApartRank) { met++; } }   // the fading did not meet
            SheetLine head = Section(Shared.Loc.Get("met.section", "WHO THEY MEET"), met > 0 ? Shared.Loc.Get("met.count", "{0} met", met) : string.Empty);
            head.Tips = new[] { Shared.Loc.Get("met.section.help", "Who they met at the last six love ticks, and what each did to the feeling") };
            lines.Add(head);
            if (rows.Count == 0)
            {
                bool loveOff = who.sexInfo != null && who.sexInfo.Length > 8 && who.sexInfo[8] == 1;
                lines.Add(new SheetLine
                {
                    Kind = LineMetFold, Enabled = true, Arg = -1,
                    Label = loveOff ? Shared.Loc.Get("met.love_off", "Love off") : Shared.Loc.Get("met.none", "Nothing on record"),
                    Value = loveOff ? Shared.Loc.Get("met.love_off.help", "They build no love and no grudges with anyone")
                        : Shared.Loc.Get("met.none.help", "No one's area overlapped theirs at the last six ticks"),
                });
                lines.Add(new SheetLine { Kind = LineSpace });
                return;
            }
            int first = 0;
            foreach (MetRow r in rows) { if (!r.Plain) { first++; } }
            first = Mathf.Min(rows.Count, Mathf.Max(MetFirstPage, first));
            int shown = Mathf.Clamp(MetShown(who, first), first, rows.Count);
            var hours = new string[LoveLedger.TicksADay + 1];
            for (int i = 0; i < LoveLedger.TicksADay; i++) { hours[i] = LoveLedger.TickHour(read.Ticks[i]).ToString("00"); }
            hours[LoveLedger.TicksADay] = LoveLedger.TickHour(read.Ticks[LoveLedger.TicksADay - 1] + 1).ToString("00");
            lines.Add(new SheetLine
            {
                Kind = LineMetHead, Enabled = true, Options = hours, Cans = NightTicks(read.Ticks),
                Label = me.ToUpperInvariant(), Label2 = Shared.Loc.Get("met.head.they", "THEY"),
            });
            for (int i = 0; i < shown; i++)
            {
                lines.Add(rows[i].Line);
                if (rows[i].Why.HasValue) { lines.Add(rows[i].Why.Value); }
            }
            int rest = rows.Count - shown;
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
        }

        private const int MetFirstPage = 6;
        private const int MetPage = 8;

        private sealed class MetRow
        {
            public string Name;
            public float Rank;
            public bool Plain;
            public SheetLine Line;
            public SheetLine? Why;
        }

        // how many of a person's met rows are shown; -1 the first page
        private static CommonStates _metShownWho;
        private static int _metShown = -1;

        private static int MetShown(CommonStates who, int first)
        {
            return ReferenceEquals(_metShownWho, who) && _metShown >= 0 ? _metShown : first;
        }

        // the more-or-less line's press: 0 a page more, 1 back to the first
        private static void PressMetMore(CommonStates who, int first, int sub)
        {
            int now = MetShown(who, first);
            _metShownWho = who;
            _metShown = sub == 0 ? now + MetPage : first;
        }

        // the six ticks' nights, and the next tick's after them
        private static bool[] NightTicks(int[] ticks)
        {
            var night = new bool[ticks.Length + 1];
            for (int i = 0; i < ticks.Length; i++) { night[i] = LoveLedger.TickAtNight(ticks[i]); }
            night[ticks.Length] = LoveLedger.TickAtNight(ticks[ticks.Length - 1] + 1);
            return night;
        }

        private static string NextHour(int[] ticks)
        {
            return LoveLedger.TickHour(ticks[ticks.Length - 1] + 1).ToString("00") + ":00";
        }

        // THE NEXT TICK, FORECAST as the world arcs forecast it (Links.cs): areas
        // that overlap now meet, each way at the rule of their types; apart, a
        // bond held either way fades. -1 when nothing will happen between them.
        // Where people are at the tick decides it, so it is a forecast, and says so.
        private static int NextMark(NPCManager npcs, CommonStates who, CommonStates other, string me, string name, string hour,
            out string tip)
        {
            tip = null;
            if (npcs == null || LoveOff(who) || LoveOff(other))
            {
                return -1;
            }
            if (npcs.NPCDistanceCheck(who, other))
            {
                int mine = RuleOf(who, other), theirs = RuleOf(other, who);
                tip = "<b>" + Shared.Loc.Get("met.next_tick", "Next tick, {0}", hour) + "</b>\n" + Shared.Loc.Get("met.next.meet", "Their areas overlap now, so they meet:") + "\n"
                    + me + "  " + Signed(mine) + "\n" + name + "  " + Signed(theirs);
                return (int)(mine >= 10 ? LoveLedger.TickMark.Liked : mine < 0 ? LoveLedger.TickMark.Disliked : LoveLedger.TickMark.Plain);
            }
            if (!LoveBetween(npcs, who, other, out float _) && !LoveBetween(npcs, other, who, out float _))
            {
                return -1;
            }
            tip = "<b>" + Shared.Loc.Get("met.next_tick", "Next tick, {0}", hour) + "</b>\n" + Shared.Loc.Get("met.next.apart", "Their areas do not overlap now: apart, a bond held either way fades 5");
            return (int)LoveLedger.TickMark.Apart;
        }

        private static string Signed(float v)
        {
            return Mathf.Abs(v) < 0.5f ? "0" : v.ToString("+0;-0");
        }

        private static bool Any(LoveLedger.TickMark[] marks, LoveLedger.TickMark mark)
        {
            for (int i = 0; i < marks.Length; i++) { if (marks[i] == mark) { return true; } }
            return false;
        }

        private static bool Met(LoveLedger.TickMark t)
        {
            return t == LoveLedger.TickMark.Liked || t == LoveLedger.TickMark.Plain || t == LoveLedger.TickMark.Disliked;
        }

        // THE NOTE: the worst that is happening between the two, from this
        // person's side (a bare verb) or theirs (the verb and this person's name),
        // and when they meet if it is only at night or only by day
        private static string MetNote(NPCManager npcs, CommonStates who, CommonStates other,
            LoveLedger.Meeting m, string me, out int rank)
        {
            bool mineKnown = LoveBetween(npcs, who, other, out float mine);
            bool theirsKnown = LoveBetween(npcs, other, who, out float theirs);
            bool iDislike = Any(m.Mine, LoveLedger.TickMark.Disliked);
            bool theyDislike = Any(m.Theirs, LoveLedger.TickMark.Disliked);
            bool iCant = iDislike && mineKnown && mine <= -10f;
            bool theyCant = theyDislike && theirsKnown && theirs <= -10f;
            bool iLike = Any(m.Mine, LoveLedger.TickMark.Liked);
            bool theyLike = Any(m.Theirs, LoveLedger.TickMark.Liked);
            string note;
            if (iCant && theyCant) { note = Shared.Loc.Get("social.cant_stand_each", "can't stand each other"); rank = MetBad; }
            else if (iCant) { note = Shared.Loc.Get("social.cant_stand", "can't stand"); rank = MetBad; }
            else if (theyCant) { note = Shared.Loc.Get("social.cant_stand_who", "can't stand {0}", me); rank = MetBad; }
            else if (iDislike && theyDislike) { note = Shared.Loc.Get("met.note.dislike_each", "dislike each other"); rank = MetWarn; }
            else if (iDislike) { note = Shared.Loc.Get("met.note.dislikes", "dislikes"); rank = MetWarn; }
            else if (theyDislike) { note = Shared.Loc.Get("met.note.dislikes_who", "dislikes {0}", me); rank = MetWarn; }
            else if (m.DroppedAt >= 0) { note = Shared.Loc.Get("met.note.dropped_at", "dropped at {0}", LoveLedger.TickHour(m.DroppedAt).ToString("00") + ":00"); rank = MetSoft; }
            else if (iLike && theyLike) { note = Shared.Loc.Get("met.note.like_each", "like each other"); rank = MetGood; }
            else if (iLike) { note = Shared.Loc.Get("met.note.likes", "likes"); rank = MetGood; }
            else if (theyLike) { note = Shared.Loc.Get("met.note.likes_who", "likes {0}", me); rank = MetGood; }
            else
            {
                // plain whenever they meet; never meeting at all, a bond fading
                bool anyMet = false;
                for (int i = 0; i < LoveLedger.TicksADay; i++) { anyMet |= Met(m.Mine[i]) || Met(m.Theirs[i]); }
                note = anyMet ? string.Empty : Shared.Loc.Get("met.note.fading", "fading apart");
                rank = anyMet ? MetPlainRank : MetApartRank;
            }
            if (rank >= MetPlainRank)
            {
                return note;
            }
            // when they meet: the night's ticks, the day's, or both
            bool day = false, night = false, housemates = false;
            int[] keys = MetOf(who).Ticks;
            for (int i = 0; i < LoveLedger.TicksADay; i++)
            {
                if (!Met(m.Mine[i]) && !Met(m.Theirs[i])) { continue; }
                if (LoveLedger.TickAtNight(keys[i]))
                {
                    night = true;
                    housemates |= m.MyPlace[i] != 0 && m.MyPlace[i] == m.TheirPlace[i];
                }
                else
                {
                    day = true;
                }
            }
            if (night && !day) { note = housemates ? Shared.Loc.Get("met.note.housemates", "{0}, housemates at night", note) : Shared.Loc.Get("met.note.nights", "{0}, nights", note); }
            else if (day && !night) { note = Shared.Loc.Get("met.note.by_day", "{0}, by day", note); }
            return note;
        }

        // ---------- drawing ----------

        // the columns, shared by the heads and the rows
        private static void MetColumns(float w, out float left, out float right, out float strip,
            out float mineX, out float theirX, out float noteX)
        {
            left = SheetPadX;
            right = w - SheetPadX;
            strip = left + 50f;   // the person: token, name, mood face
            float stripEnd = MetCellX(strip, LoveLedger.TicksADay) + MetCell;   // the six, now, the next
            mineX = stripEnd + 3f;      // each sum is 14 wide, right-aligned
            theirX = mineX + 18f;
            noteX = theirX + 18f;
        }

        // the six ticks, oldest first, and past the now line the next one
        private static float MetCellX(float strip, int i)
        {
            return strip + i * (MetCell + MetCellGap) + (i >= LoveLedger.TicksADay ? MetNowGap : 0f);
        }

        private static float MetNowX(float strip)
        {
            return MetCellX(strip, LoveLedger.TicksADay) - (MetCellGap + MetNowGap) * 0.5f;
        }

        private static void MetNightBands(SheetRow row, bool[] night, float strip, float h, float top = 0f)
        {
            for (int i = 0; night != null && i < night.Length; i++)
            {
                if (night[i])
                {
                    FormImage(row, null, 0, MetNight, MetCellX(strip, i) - MetCellGap * 0.5f, top, MetCell + MetCellGap, h - top);
                }
            }
        }

        private static int DrawMetHead(SheetRow row, SheetLine line, float w, float h)
        {
            MetColumns(w, out float left, out float right, out float strip, out float mineX, out float theirX, out float noteX);
            // the shading alone did not say "night": a moon over each night column.
            // Without ticks (Cans null) it is All relations' head: Value captions
            // the strip's column, and Tip is the head's tip
            float top = line.Cans != null ? MetTimeH + MetMoonH : 0f;
            MetNightBands(row, line.Cans, strip, h, line.Cans != null ? MetTimeH : 0f);
            const float ms = 4.6f;
            for (int i = 0; line.Cans != null && i < line.Cans.Length; i++)
            {
                if (line.Cans[i])
                {
                    FormImage(row, "kit_moon", 0, Theme.DossierSoft, MetCellX(strip, i) + (MetCell - ms) * 0.5f, MetTimeH + 1f, ms, ms);
                }
            }
            for (int i = 0; line.Options != null && i < line.Options.Length; i++)
            {
                bool next = i >= LoveLedger.TicksADay;   // the next tick's hour, in ink
                FormText(row, line.Options[i], CaptionSize, next ? Theme.DossierInk : Theme.DossierSoft, TextAlignmentOptions.Center, next,
                    MetCellX(strip, i) - 1f, top + 1.5f, MetCell + 2f, h - top - 2f);
            }
            if (line.Options != null)
            {
                // TIME RUNS TO THE NOW LINE, on a line of its own over the moons:
                // "a day ago" at the strip's start, NOW over the now line, and the
                // line down through every row
                float nowX = MetNowX(strip);
                FormText(row, Shared.Loc.Get("met.head.day_ago", "a day ago"), CaptionSize * 0.85f, Theme.DossierSoft,
                    TextAlignmentOptions.MidlineLeft, false, strip, 0f, 30f, MetTimeH);
                FormText(row, Shared.Loc.Get("chart.now", "NOW"), CaptionSize * 0.8f, Theme.DossierInk, TextAlignmentOptions.Center, true,
                    nowX - 5f, 0f, 10f, MetTimeH);
                FormImage(row, null, 0, Theme.DossierInk, nowX - Hair * 0.5f, MetTimeH, Hair, h - MetTimeH);
            }
            FormText(row, line.Label ?? string.Empty, CaptionSize, Theme.DossierSoft, TextAlignmentOptions.MidlineRight, true,
                mineX, top + 1.5f, 14f, h - top - 2f, 0f, fit: true);
            FormText(row, line.Label2 ?? string.Empty, CaptionSize, Theme.DossierSoft, TextAlignmentOptions.MidlineRight, true,
                theirX, top + 1.5f, 14f, h - top - 2f);
            if (line.Cans == null)
            {
                FormText(row, line.Value ?? string.Empty, CaptionSize, Theme.DossierSoft, TextAlignmentOptions.MidlineLeft, true,
                    strip, top + 1.5f, mineX - strip, h - top - 2f, 0f, fit: true);
                if (!string.IsNullOrEmpty(line.Tip))
                {
                    row.TipZones.Add(new Rect(strip, 0f, noteX - strip, h));
                    row.TipTexts.Add(line.Tip);
                }
                return 0;
            }
            row.TipZones.Add(new Rect(strip, 0f, mineX - strip, h));
            row.TipTexts.Add(Shared.Loc.Get("met.ticks.help", "The last six love ticks, oldest first up to now; the shaded ones, under a moon, are at night, when most sleep indoors. A hollow square: they did not meet. A dash: apart, the bond fading -5. A dot: before the record. Past the now line, the next tick as it stands now: pale if they will meet, dashed if apart"));
            return 0;
        }

        private static int DrawMetPerson(SheetRow row, SheetLine line, float w, float h)
        {
            if (line.SeriesX != null)
            {
                return DrawMoodLane(row, line, w, h);   // a lane of RELATIONS (Company.cs)
            }
            MetColumns(w, out float left, out float right, out float strip, out float mineX, out float theirX, out float noteX);
            // the token and the name are links (planks 0, 1); the rest of the row is
            // the press that opens it (2), washed while hovered or open
            if (Hot(row, 2) || line.On)
            {
                FormImage(row, null, 0, MetOpenWash, left - 1.5f, 0f, right - left + 3f, h);
            }
            MetNightBands(row, line.Cans, strip, h);
            float cy = h * 0.5f;
            DrawCompactPerson(row, line, line.Label, left, h, strip - 15f - left, 0, 1);
            // the six ticks, paler the older, each saying on hover what it was worth;
            // the now line; the next tick's forecast as a ghost of its mark
            int six = LoveLedger.TicksADay;
            for (int i = 0; line.Marks != null && i < line.Marks.Length && i < six; i++)
            {
                var mark = (LoveLedger.TickMark)line.Marks[i];
                MetCellDraw(row, mark, MetCellX(strip, i), cy - MetCell * 0.5f, 0.45f + 0.55f * i / (six - 1));
                row.TipZones.Add(new Rect(MetCellX(strip, i) - MetCellGap * 0.5f, 0f, MetCell + MetCellGap, h));
                row.TipTexts.Add(TickWord(mark));
            }
            FormImage(row, null, 0, Theme.DossierInk, MetNowX(strip) - Hair * 0.5f, 0f, Hair, h);
            if (line.Marks != null && line.Marks.Length > six && line.Marks[six] >= 0)
            {
                MetCellDraw(row, (LoveLedger.TickMark)line.Marks[six], MetCellX(strip, six), cy - MetCell * 0.5f, 1f, ghost: true);
                if (line.Tips != null && line.Tips.Length > 0 && line.Tips[0] != null)
                {
                    row.TipZones.Add(new Rect(MetCellX(strip, six) - MetNowGap * 0.5f, 0f, MetCell + MetNowGap, h));
                    row.TipTexts.Add(line.Tips[0]);
                }
            }
            MetSum(row, line.Value, mineX, h);
            MetSum(row, line.Value2, theirX, h);
            Color noteInk = line.Index == MetBad ? MoralBad : line.Index == MetWarn ? PaperWarn
                : line.Index == MetGood ? PaperGood : Theme.DossierSoft;
            FormText(row, line.Label2 ?? string.Empty, FormTextSize * 0.85f, noteInk, TextAlignmentOptions.MidlineLeft, false,
                noteX, 0f, right - noteX, h, 0f, fit: true);
            if (!line.On)
            {
                FormRule(row, Theme.DossierRule, left, h - 0.5f, right - left);
            }
            if (!string.IsNullOrEmpty(line.Tip))
            {
                row.TipZones.Add(new Rect(left, 0f, right - left, h));
                row.TipTexts.Add(line.Tip);
            }
            FormHit(row, 2, left, 0f, right - left, h);
            return 3;
        }

        // what one tick square means, in the colours PERSONALITY prints the types in,
        // the square itself before the words
        private static string TickWord(LoveLedger.TickMark mark)
        {
            switch (mark)
            {
                case LoveLedger.TickMark.Liked: return TipIcon("tick_liked") + Shared.Loc.Get("met.tick.liked", "+10 love: the type they like");
                case LoveLedger.TickMark.Plain: return TipIcon("tick_plain") + Shared.Loc.Get("met.tick.plain", "+5 love: they met");
                case LoveLedger.TickMark.Disliked: return TipIcon("tick_disliked") + Shared.Loc.Get("met.tick.disliked", "-5 love: the type they dislike");
                case LoveLedger.TickMark.Apart: return TipIcon("tick_apart") + Shared.Loc.Get("met.tick.apart", "Apart: the bond fading, -5 love");
                case LoveLedger.TickMark.Unknown: return Shared.Loc.Get("social.before_record", "Before the record");
                default: return TipIcon("tick_notmet") + Shared.Loc.Get("met.tick.not_met", "Did not meet");
            }
        }

        // THE MARKS IN THE TIPS THAT EXPLAIN THEM: the Social page's ticks, dawns
        // and refusals as inline icons (InlineIcons), drawn as the sheet draws them
        private static bool _tipIconsDefined;

        private static string TipIcon(string name)
        {
            if (!_tipIconsDefined)
            {
                _tipIconsDefined = true;
                InlineIcons.Define("tick_liked", new[] { Layer("#square", TraitLike) });
                InlineIcons.Define("tick_plain", new[] { Layer("#square", MetPlain) });
                InlineIcons.Define("tick_disliked", new[] { Layer("#square", TraitDislike) });
                InlineIcons.Define("tick_notmet", new[] { Layer("#dot", Theme.DossierSoft) });
                InlineIcons.Define("tick_apart", new[] { Layer("#rim", Theme.DossierSoft), Layer("#dash", TraitDislike) });
                InlineIcons.Define("refusal", new[] { Layer("#bar", PaperWarn) });
                InlineIcons.Define("mood_cost", new[] { Layer("#dash", MoralBad) });
                InlineIcons.Define("dawn_in", new[] { Layer("kit_sleep", PaperGood) });
                InlineIcons.Define("dawn_out", new[] { Layer("kit_sleep_none", MoralBad) });
                InlineIcons.Define("dawn_full", new[] { Layer("kit_sleep", Alpha(Theme.DossierSoft, 0.7f)) });
            }
            return InlineIcons.Tag(name) + " ";
        }

        private static void MetSum(SheetRow row, string text, float x, float h)
        {
            text = text ?? string.Empty;
            Color ink = text.StartsWith("+") ? PaperGood : text.StartsWith("-") ? MoralBad : Theme.DossierSoft;
            FormText(row, text, FormTextSize, ink, TextAlignmentOptions.MidlineRight, false, x, 0f, 14f, h);
        }

        // a tick's mark at `alpha`; a `ghost` is the next tick's forecast, every
        // kind in a dashed rim - a met mark pale inside its colour's, an apart one
        // in the soft ink with its dash
        private static void MetCellDraw(SheetRow row, LoveLedger.TickMark mark, float x, float y, float alpha = 1f, bool ghost = false,
            float s = MetCell)
        {
            const float t = 0.5f;
            Color fill = mark == LoveLedger.TickMark.Liked ? TraitLike : mark == LoveLedger.TickMark.Plain ? MetPlain
                : mark == LoveLedger.TickMark.Disliked ? TraitDislike : Color.clear;
            if (fill.a > 0f)
            {
                if (ghost)
                {
                    FormImage(row, null, 0, Alpha(fill, 0.3f), x, y, s, s);
                    DashedRim(row, fill, x, y, s, t);
                    return;
                }
                FormImage(row, null, 0, Alpha(fill, alpha), x, y, s, s);
                return;
            }
            if (mark == LoveLedger.TickMark.Unknown)
            {
                FormImage(row, null, 0, Alpha(Theme.DossierRule, alpha), x + s * 0.5f - 0.5f, y + s * 0.5f - 0.5f, 1f, 1f);
                return;
            }
            if (s < MetCell && mark == LoveLedger.TickMark.NotMet && !ghost)
            {
                // small, as on the lanes: an empty tick is a faint dot, or the row is a fence
                FormImage(row, null, 0, Alpha(Theme.DossierRule, alpha), x + s * 0.5f - 0.6f, y + s * 0.5f - 0.6f, 1.2f, 1.2f);
                return;
            }
            // not met: hollow; a bond fading apart: hollow with a dash; a forecast, dashed
            Color rim = Alpha(Theme.DossierSoft, alpha);
            if (ghost)
            {
                DashedRim(row, rim, x, y, s, t);
            }
            else
            {
                Rim(row, rim, x, y, s, t);
            }
            if (mark == LoveLedger.TickMark.Apart)
            {
                FormImage(row, null, 0, Alpha(TraitDislike, ghost ? 0.6f : alpha), x + 1.2f, y + s * 0.5f - 0.4f, s - 2.4f, 0.8f);
            }
        }

        private static void DashedRim(SheetRow row, Color ink, float x, float y, float s, float t)
        {
            const float dash = 1.3f;
            for (float d = 0f; d < s; d += dash * 2f)
            {
                float len = Mathf.Min(dash, s - d);
                FormImage(row, null, 0, ink, x + d, y, len, t);
                FormImage(row, null, 0, ink, x + d, y + s - t, len, t);
                FormImage(row, null, 0, ink, x, y + d, t, len);
                FormImage(row, null, 0, ink, x + s - t, y + d, t, len);
            }
        }

        private static void Rim(SheetRow row, Color ink, float x, float y, float s, float t)
        {
            FormImage(row, null, 0, ink, x, y, s, t);
            FormImage(row, null, 0, ink, x, y + s - t, s, t);
            FormImage(row, null, 0, ink, x, y, t, s);
            FormImage(row, null, 0, ink, x + s - t, y, t, s);
        }

        private const float FormMetMoreH = 11f;

        private static float MetFoldHeight(SheetLine line)
        {
            return line.Arg >= 0 || line.Arg == NoneFoldArg ? FormMetMoreH : FormMetFoldH;
        }

        // AN EMPTY LIST SAYS SO: its section, then "None" in the soft ink, in
        // place of a key over no rows
        private const int NoneFoldArg = -2;

        private static SheetLine NoneLine()
        {
            return new SheetLine { Kind = LineMetFold, Enabled = true, Arg = NoneFoldArg, Label = Shared.Loc.Get("social.none", "None") };
        }

        // THE MORE-OR-LESS LINE: "Show 8 more" (plank 0) and how many are not
        // shown on the left, "Show less" (plank 1) on the right. Arg -1 is the
        // empty page's message instead: a caption and a sentence.
        private static int DrawMetFold(SheetRow row, SheetLine line, float w, float h)
        {
            float left = SheetPadX;
            float right = w - SheetPadX;
            if (line.Arg == NoneFoldArg)
            {
                FormText(row, line.Label ?? string.Empty, FormTextSize * 0.85f, Theme.DossierSoft, TextAlignmentOptions.MidlineLeft, false,
                    left, 0f, right - left, h);
                return 0;
            }
            if (line.Arg < 0)
            {
                FormText(row, (line.Label ?? string.Empty) + ":", FormTextSize * 0.8f, Theme.DossierSoft,
                    TextAlignmentOptions.MidlineLeft, false, left, 1f, right - left, 8f);
                FormText(row, line.Value ?? string.Empty, FormTextSize, Theme.DossierInk, TextAlignmentOptions.MidlineLeft, false,
                    left, 9f, right - left, 10f, 0f, fit: true);
                return 0;
            }
            float size = FormTextSize * 0.85f;
            string more = line.Options != null && line.Options.Length > 0 ? line.Options[0] : null;
            string less = line.Options != null && line.Options.Length > 1 ? line.Options[1] : null;
            float x = left;
            if (more != null)
            {
                float mw = MeasureForm(row, more, size) + 1f;
                FormText(row, more, size, Theme.DossierInk, TextAlignmentOptions.MidlineLeft, false, x, 0f, mw, h);
                FormRule(row, Hot(row, 0) ? Theme.DossierInk : Theme.DossierSoft, x, h * 0.5f + 3f, mw - 1f, Hot(row, 0) ? 0.5f : 0.3f);
                FormHit(row, 0, x - 1f, 0f, mw + 2f, h);
                x += mw + 4f;
            }
            else
            {
                FormNoHit(row, 0);
            }
            if (!string.IsNullOrEmpty(line.Label))
            {
                FormText(row, "·  " + line.Label, size, Theme.DossierSoft, TextAlignmentOptions.MidlineLeft, false,
                    x, 0f, right - x, h);
            }
            if (less != null)
            {
                float lw = MeasureForm(row, less, size) + 1f;
                FormText(row, less, size, Theme.DossierInk, TextAlignmentOptions.MidlineRight, false, right - lw, 0f, lw, h);
                FormRule(row, Hot(row, 1) ? Theme.DossierInk : Theme.DossierSoft, right - lw + 1f, h * 0.5f + 3f, lw - 1f, Hot(row, 1) ? 0.5f : 0.3f);
                FormHit(row, 1, right - lw - 1f, 0f, lw + 2f, h);
                return 2;
            }
            return more != null ? 1 : 0;
        }

        // ---------- a row opened: why, and what would end it ----------

        private static readonly Color MetOpenWash = new Color(0.86f, 0.80f, 0.68f, 0.35f);
        private static readonly Color MetWhyPaper = Theme.ParseColor("#EEE5D1", Color.white);
        private static readonly List<System.Action> MetActs = new List<System.Action>();
        private static CommonStates _metOpenWho;
        private static int _metOpenPartner = -1;
        private const float MetWhyLineH = 7.5f;
        private const float MetWhyPad = 3f;

        private static bool IsMetOpen(CommonStates who, int partner)
        {
            return ReferenceEquals(_metOpenWho, who) && _metOpenPartner == partner;
        }

        // one row open at a time; its own press closes it
        private static void ToggleMetOpen(CommonStates who, int partner)
        {
            if (IsMetOpen(who, partner))
            {
                _metOpenPartner = -1;
                return;
            }
            _metOpenWho = who;
            _metOpenPartner = partner;
        }

        private static void MetAction(int index)
        {
            if (index >= 0 && index < MetActs.Count)
            {
                MetActs[index]();
            }
        }

        // where a person's own area is by day: indoors it is the interior's door,
        // and the one they were placed at waits in preTerritory (NPCManager.HouseIn)
        private static Vector3 DayCentre(NPCMove move)
        {
            return move.house != null ? move.preTerritory : move.territory;
        }

        private static string Hours(List<int> keys)
        {
            var parts = new List<string>();
            foreach (int k in keys) { parts.Add(LoveLedger.TickHour(k).ToString("00") + ":00"); }
            if (parts.Count <= 1)
            {
                return parts.Count == 1 ? parts[0] : string.Empty;
            }
            return Shared.Loc.Get("met.hours.and", "{0} and {1}", string.Join(Shared.Loc.Get("met.hours.comma", ", "), parts.GetRange(0, parts.Count - 1).ToArray()), parts[parts.Count - 1]);
        }

        // WHY, IN WORDS: when they meet (and when not), where they sleep if the
        // night brings them together, the rule each way that is not the plain +5,
        // and how far apart their day areas are against their reach. Then what
        // would end it, and its one switch - on whoever holds the grudge.
        private static SheetLine MetWhyLine(CommonStates who, CommonStates other,
            LoveLedger.Meeting m, int[] ticks, string me, string name, int rank)
        {
            TextManager tm = GameWords.Text(_mn);
            var say = new List<string>();
            var met = new List<int>();
            var apart = new List<int>();
            int known = 0;
            bool nightIndoors = false, sameHouse = false;
            for (int i = 0; i < ticks.Length; i++)
            {
                if (m.Mine[i] == LoveLedger.TickMark.Unknown && m.Theirs[i] == LoveLedger.TickMark.Unknown)
                {
                    continue;
                }
                known++;
                if (Met(m.Mine[i]) || Met(m.Theirs[i]))
                {
                    met.Add(ticks[i]);
                    if (LoveLedger.TickAtNight(ticks[i]) && m.MyPlace[i] != 0 && m.TheirPlace[i] != 0)
                    {
                        nightIndoors = true;
                        sameHouse |= m.MyPlace[i] == m.TheirPlace[i];
                    }
                }
                else
                {
                    apart.Add(ticks[i]);
                }
            }
            if (met.Count > 0 && met.Count == known)
            {
                say.Add(known == ticks.Length ? Shared.Loc.Get("met.why.every_tick", "They met at every tick, day and night.") : Shared.Loc.Get("met.why.every_known", "They met at every tick on record."));
            }
            else if (met.Count > 0)
            {
                say.Add(Shared.Loc.Get("met.why.met_at", "They met at {0}; not at {1}.", Hours(met), Hours(apart)));
            }
            else
            {
                say.Add(Shared.Loc.Get("met.why.not_met", "They did not meet at the last ticks: their areas no longer overlap."));
            }
            if (nightIndoors)
            {
                say.Add(sameHouse
                    ? Shared.Loc.Get("met.why.same_house", "At night they sleep in the same house, so they meet.")
                    : Shared.Loc.Get("met.why.near_houses", "At night they sleep in different houses, close enough that they still meet."));
            }
            void Rule(string a, int[] aType, string b, LoveLedger.TickMark[] marks)
            {
                if (Any(marks, LoveLedger.TickMark.Disliked))
                {
                    say.Add(Shared.Loc.Get("met.why.rule_dislikes", "{0} DISLIKES {1}, which {2} IS: −5 a tick.", a, PersonText(tm, aType, 2), b));
                }
                else if (Any(marks, LoveLedger.TickMark.Liked))
                {
                    say.Add(Shared.Loc.Get("met.why.rule_likes", "{0} LIKES {1}, which {2} IS: +10 a tick.", a, PersonText(tm, aType, 1), b));
                }
            }
            Rule(me, who.person, name, m.Mine);
            Rule(name, other.person, me, m.Theirs);
            float need = 0f;
            if (who.nMove != null && other.nMove != null)
            {
                float dist = Vector3.Distance(DayCentre(who.nMove), DayCentre(other.nMove));
                float reach = who.nMove.territoryRad + other.nMove.territoryRad;
                say.Add(Shared.Loc.Get("met.why.distance", "Their areas by day: {0} m apart; they meet within {1} m.", dist.ToString("0"), reach.ToString("0")));
                need = reach - dist;
            }
            var line = new SheetLine { Kind = LineMetWhy, Enabled = true, Arg = -1, Values = say.ToArray(), Index = rank };
            if (rank == MetBad || rank == MetWarn)
            {
                bool mineSour = Any(m.Mine, LoveLedger.TickMark.Disliked);
                CommonStates holder = mineSour ? who : other;
                // apart, a grudge is dropped at the next tick - unless they have
                // had sex, which keeps any bond (CommonStates.LoveChange)
                bool kept = HadSex(holder, mineSour ? other : who);
                var ways = new List<string> { Shared.Loc.Get("mood.fix.love_off", "Love off") };
                if (!kept && need > 0f) { ways.Add(Shared.Loc.Get("met.end.area_away", "one area {0} m further away", Mathf.CeilToInt(need + 1f))); }
                if (!kept && nightIndoors) { ways.Add(sameHouse ? Shared.Loc.Get("met.end.other_house", "another house for one of them") : Shared.Loc.Get("met.end.houses_apart", "houses further apart")); }
                say.Add(kept ? Shared.Loc.Get("met.why.grudge_kept", "They have had sex, so the grudge stays even apart: only Love off stops it growing.")
                    : Shared.Loc.Get("met.why.grudge_dropped", "Apart for a single tick, the grudge is dropped; it starts again from 0 when they meet."));
                line.Values = say.ToArray();
                line.Label = Shared.Loc.Get("met.end.ends_it", "Ends it: {0}", string.Join(Shared.Loc.Get("met.end.or", ", or "), ways.ToArray()));
                if (holder.sexInfo != null && holder.sexInfo.Length > 8 && holder.sexInfo[8] == 0)
                {
                    line.Arg = MetActs.Count;
                    MetActs.Add(() => { holder.sexInfo[8] = 1; MoodChanged(); });
                    line.Options = new[] { mineSour ? Shared.Loc.Get("mood.fix.love_off", "Love off") : Shared.Loc.Get("met.end.love_off_who", "{0}: love off", Short(holder.charaName)) };
                    line.Tips = new[] { holder.charaName != null
                        ? Shared.Loc.Get("met.end.love_off_who.help", "Love off: {0} builds no love and no grudges, toward you neither", holder.charaName)
                        : Shared.Loc.Get("met.end.love_off_they.help", "Love off: they build no love and no grudges, toward you neither") };
                }
            }
            else if (rank == MetGood)
            {
                line.Label = Shared.Loc.Get("met.end.keeps_it", "Keeps it: their areas overlapping");
            }
            else if (m.DroppedAt >= 0)
            {
                line.Label = Shared.Loc.Get("met.end.gone", "The bond is gone; it starts again from nothing if they meet");
            }
            return line;
        }

        // CommonStates.HaveSexLog on the holder's bond with them
        private static bool HadSex(CommonStates holder, CommonStates other)
        {
            for (int i = 0; holder.lovers != null && i < holder.lovers.Count; i++)
            {
                CommonStates.Lovers l = holder.lovers[i];
                if (l != null && l.friendID == other.friendID)
                {
                    return holder.HaveSexLog(l);
                }
            }
            return false;
        }

        private static float MetWhyHeight(SheetLine line)
        {
            int n = line.Values != null ? line.Values.Length : 0;
            float lever = !string.IsNullOrEmpty(line.Label) ? MetWhyLineH + 1f : 0f;
            float button = line.Options != null && line.Options.Length > 0 ? FormButtonH + 2f : 0f;
            return 2f * MetWhyPad + n * MetWhyLineH + lever + button + 2f;
        }

        private static int DrawMetWhy(SheetRow row, SheetLine line, float w, float h)
        {
            float left = SheetPadX;
            float right = w - SheetPadX;
            float bx = left + 3f, bw = right - bx;
            FormImage(row, null, 0, MetWhyPaper, bx, 0f, bw, h - 2f);
            FormRule(row, Theme.DossierRule, bx, h - 2f, bw);
            float tx = bx + 3f, tw = bw - 6f;
            float y = MetWhyPad;
            for (int i = 0; line.Values != null && i < line.Values.Length; i++)
            {
                FormText(row, line.Values[i], FormTextSize * 0.85f, Theme.DossierInk, TextAlignmentOptions.MidlineLeft, false,
                    tx, y, tw, MetWhyLineH, 0f, fit: true);
                y += MetWhyLineH;
            }
            // what ends it on a line of its own, its switch on the line under it
            if (!string.IsNullOrEmpty(line.Label))
            {
                y += 1f;
                Color ink = line.Index == MetGood ? PaperGood : line.Index <= MetWarn ? MoralBad : Theme.DossierSoft;
                FormText(row, line.Label, FormTextSize * 0.85f, ink, TextAlignmentOptions.MidlineLeft, false,
                    tx, y, tw, MetWhyLineH, 0f, fit: true);
                y += MetWhyLineH;
            }
            if (line.Options == null || line.Options.Length == 0)
            {
                return 0;
            }
            y += 1f;
            float bwid = MeasureForm(row, line.Options[0], FormTextSize) + 10f;
            float bxr = right - 3f - bwid;
            DrawButton(row, 0, line.Options[0], false, bxr, y, bwid);
            FormTip(row, line.Tips, 0, bxr, y, bwid, FormButtonH);
            if (line.Tips != null && line.Tips.Length > 0)
            {
                FormText(row, line.Tips[0], FormTextSize * 0.7f, Theme.DossierSoft, TextAlignmentOptions.MidlineLeft, false,
                    tx, y, bxr - 4f - tx, FormButtonH, 0f, fit: true);
            }
            return 1;
        }
    }
}
