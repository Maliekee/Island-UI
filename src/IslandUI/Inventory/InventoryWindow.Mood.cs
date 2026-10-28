using System.Collections.Generic;
using IslandUI.UI;
using TMPro;
using UnityEngine;

namespace IslandUI
{
    // THE MOOD, IN THE WINDOWS. What the ledger remembers and the checks predict
    // (Model/MoodLedger.cs, Model/MoodCheck.cs), where you already look:
    //  - every row's red word names a cause that no longer vanishes after a night's
    //    sleep, and an amber one warns of a drain that has not cost anything yet
    //  - the roster's Mood sort puts the rebels, the nearly-rebels and the unhappy
    //    on top, and the filter matches "unhappy"
    //  - the Roster chip on the HUD counts the unhappy on a ruby tag
    //  - the Social page spells it out as a checklist: every cause, what it
    //    costs, and its fix one press away
    internal static partial class InventoryWindow
    {
        private static readonly Color MoodWarn = Theme.ParseColor("#E8A33A", new Color(0.91f, 0.64f, 0.23f, 1f));
        private static readonly Color MoodGood = Theme.ParseColor("#6BBF59", new Color(0.42f, 0.75f, 0.35f, 1f));

        // ---------- the findings, read at most once a second ----------

        private sealed class MoodRead
        {
            public float At;
            public List<MoodFinding> Findings;
        }

        private static readonly Dictionary<CommonStates, MoodRead> MoodReads = new Dictionary<CommonStates, MoodRead>();

        private static List<MoodFinding> MoodFindings(CommonStates who)
        {
            float now = Time.unscaledTime;
            if (who != null && MoodReads.TryGetValue(who, out MoodRead read) && now - read.At < 1f)
            {
                return read.Findings;
            }
            if (MoodReads.Count > 256)
            {
                MoodReads.Clear();   // the dead and a previous scene's people
            }
            read = new MoodRead { At = now, Findings = MoodCheck.Check(_mn, who) };
            if (who != null)
            {
                MoodReads[who] = read;
            }
            return read.Findings;
        }

        // The row's word: red for why they are unhappy, amber for what will make
        // them so. Nothing for the content with nothing coming.
        private static string DiscontentWord(CommonStates who)
        {
            return DiscontentWord(who, out _);
        }

        private static string DiscontentWord(CommonStates who, out bool warn)
        {
            warn = false;
            if (who == null)
            {
                return string.Empty;
            }
            string word = MoodCheck.Word(_mn, who, MoodFindings(who));
            if (string.IsNullOrEmpty(word))
            {
                return string.Empty;
            }
            warn = !MoodCheck.Unhappy(who);
            return word;
        }

        // ---------- the Mood sort ----------

        private const int SortMood = 3;

        private static int MoodRank(RosterEntry e)
        {
            CommonStates who = e.Who;
            int level = who.debuff != null ? who.debuff.discontent : 0;
            if (level == 4) { return 0; }
            if (level == 3) { return 1; }
            if (MoodCheck.Unhappy(who)) { return 2; }
            return MoodCheck.Headline(MoodFindings(who), out _) != null ? 3 : 4;
        }

        private static string MoodSection(RosterEntry e)
        {
            switch (MoodRank(e))
            {
                case 0: return Shared.Loc.Get("roster.mood.rebelling", "Rebelling");
                case 1: return Shared.Loc.Get("roster.mood.close", "Close to rebelling");
                case 2: return Shared.Loc.Get("roster.mood.unhappy", "Unhappy");
                case 3: return Shared.Loc.Get("roster.mood.at_risk", "At risk");
                default: return Shared.Loc.Get("roster.mood.content", "Content");
            }
        }

        // the filter's word for the unhappy, beside names and kinds
        private static bool MoodQuery(string q, CommonStates who)
        {
            return q.Length >= 3 && MoodCheck.Unhappy(who)
                && Shared.Loc.Get("roster.filter.unhappy", "unhappy").ToLowerInvariant().StartsWith(q, System.StringComparison.Ordinal);
        }

        // ---------- the HUD: the unhappy, counted on the Roster chip ----------

        private static PointsTag _rosterMoodTag;
        private static int _unhappy, _nearRebellion;

        private static void TickHudMood(HudEntry roster)
        {
            CountUnhappy();
            if (roster == null || roster.Face == null)
            {
                return;
            }
            Transform dock = roster.Face.transform.parent;
            if (_rosterMoodTag == null || _rosterMoodTag.Rect == null)
            {
                _rosterMoodTag = PointsTag.Create(dock, Shared.Loc.Get("roster.mood.unhappy", "Unhappy"), true);
            }
            if (_unhappy == 0)
            {
                _rosterMoodTag.Hide();
                return;
            }
            _rosterMoodTag.Show(PointsTag.Kind.Mood, _unhappy.ToString(), PointsTag.PanelPill);
            var face = roster.Face.rectTransform;
            // the board's "35": over the roster tile's top-right corner, a little in
            // from it and hanging below the edge more than above it
            float corner = face.anchoredPosition.x + face.sizeDelta.x - 2.5f;
            float h = _rosterMoodTag.H;
            float y = -face.anchoredPosition.y - h * 0.5f + 1.5f;
            Theme.Place(_rosterMoodTag.Rect, corner - _rosterMoodTag.Width * 0.5f, y, _rosterMoodTag.Width, h);
        }

        private static float _unhappyCountedAt = -10f;

        private static void CountUnhappy()
        {
            if (Time.unscaledTime - _unhappyCountedAt < 0.5f)
            {
                return;
            }
            _unhappyCountedAt = Time.unscaledTime;
            _unhappy = 0;
            _nearRebellion = 0;
            NPCManager npcs = _mn != null ? _mn.npcMN : null;
            if (npcs == null || npcs.friendList == null)
            {
                return;
            }
            foreach (CommonStates who in People(npcs))
            {
                if (who == null || !npcs.IsActiveFriend(who) || !MoodCheck.Unhappy(who))
                {
                    continue;
                }
                _unhappy++;
                if (who.debuff != null && who.debuff.discontent >= 3) { _nearRebellion++; }
            }
        }

        private static string RosterChipTip(string label)
        {
            if (_unhappy == 0)
            {
                return label;
            }
            return "<b>" + label + "</b>\n"
                + (_nearRebellion > 0
                    ? Shared.Loc.Get("hud.roster.unhappy_near", "{0} unhappy, {1} close to rebelling", _unhappy, _nearRebellion)
                    : Shared.Loc.Get("hud.roster.unhappy", "{0} unhappy", _unhappy))
                + "\n" + Shared.Loc.Get("hud.roster.unhappy.help", "Sort by Mood to see why");
        }

        // ---------- Overview: the whole story ----------

        private const int ActionsMood = 4;
        private static readonly List<System.Action> MoodActs = new List<System.Action>();

        private static void WriteMood(List<SheetLine> lines, CommonStates who, bool dossier)
        {
            if (who == null || _mn == null)
            {
                return;
            }
            List<MoodFinding> found = MoodFindings(who);
            float today = MoodLedger.Net(_mn, who, 1f);
            string reading = who.moral.ToString("0") + (Mathf.Abs(today) >= 0.5f ? "  " + Shared.Loc.Get("mood.today", "{0} today", today.ToString("+0;-0")) : string.Empty);
            if (dossier)
            {
                lines.Add(Section(Shared.Loc.Get("mood.section.moral", "MORAL"), reading));
            }
            else
            {
                lines.Add(new SheetLine { Kind = LineSpace });
                lines.Add(Head(Shared.Loc.Get("mood.head", "Mood"), reading));
            }
            foreach (MoodFinding f in found)
            {
                if (f.Short == null)
                {
                    continue;
                }
                SheetLine line = Row(Capital(f.Short), f.Effect ?? string.Empty);
                line.ValueTinted = true;
                line.ValueTint = f.Severity == MoodSeverity.Bad ? MoralBad
                    : f.Severity == MoodSeverity.Warn ? MoodWarn
                    : f.Severity == MoodSeverity.Good ? MoodGood : Theme.DossierSoft;
                line.Tip = f.Text + (f.Fix != null ? "\n" + KitTooltip.FixMark + f.Fix : string.Empty);
                lines.Add(line);
            }
            string moved = MovedBy(who);
            if (moved != null)
            {
                SheetLine line = Row(Shared.Loc.Get("mood.last_day", "Last day"), moved);
                line.Tip = Shared.Loc.Get("mood.last_day.help", "What moved their mood since this time yesterday, by cause");
                lines.Add(line);
            }
            WriteMoodActs(lines, who, found);
        }

        // the ledger's last day, the three biggest movers
        private static string MovedBy(CommonStates who)
        {
            IList<MoodEvent> events = MoodLedger.Events(who);
            float since = MoodLedger.Now(_mn) - 1f;
            var sums = new Dictionary<string, float>();
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Day < since) { continue; }
                string key = CauseWord(events[i]);
                sums.TryGetValue(key, out float sum);
                sums[key] = sum + events[i].Delta;
            }
            if (sums.Count == 0)
            {
                return null;
            }
            var keys = new List<string>(sums.Keys);
            keys.Sort((a, b) => Mathf.Abs(sums[b]).CompareTo(Mathf.Abs(sums[a])));
            var parts = new List<string>();
            for (int i = 0; i < keys.Count && i < 3; i++)
            {
                parts.Add(keys[i] + " " + TipSigned(sums[keys[i]]));
            }
            return string.Join(Shared.Loc.Separator, parts.ToArray());
        }

        private static string CauseWord(MoodEvent e)
        {
            CommonStates other = e.Partner >= 0 && _mn != null && _mn.npcMN != null ? _mn.npcMN.GetFriend(e.Partner) : null;
            switch (e.Cause)
            {
                case MoodCause.Sleep: return Shared.Loc.Get("mood.cause.sleep", "sleep");
                case MoodCause.BadCompany: return other != null ? other.charaName : Shared.Loc.Get("mood.cause.company", "company");
                case MoodCause.Crush: return other != null ? Shared.Loc.Get("mood.cause.pining", "pining {0}", other.charaName) : Shared.Loc.Get("mood.cause.crush", "crush");
                case MoodCause.NoBed: return Shared.Loc.Get("mood.cause.no_bed", "no bed");
                case MoodCause.Sex: return Shared.Loc.Get("mood.cause.sex", "sex");
                case MoodCause.Relief: return Shared.Loc.Get("mood.cause.relief", "relief");
                case MoodCause.Forced: return Shared.Loc.Get("mood.cause.forced", "forcing");
                case MoodCause.Food: return Shared.Loc.Get("mood.cause.food", "food");
                case MoodCause.Execution: return Shared.Loc.Get("mood.cause.execution", "execution");
                case MoodCause.Torture: return Shared.Loc.Get("mood.cause.torture", "torture");
                case MoodCause.Training: return Shared.Loc.Get("mood.cause.training", "training");
                case MoodCause.Rebel: return Shared.Loc.Get("mood.cause.rebel", "rebel");
                default: return Shared.Loc.Get("mood.cause.other", "other");
            }
        }

        private static string Capital(string s)
        {
            return string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
        }

        // THE FIXES, ONE PRESS AWAY: the ones that are a switch are the switch
        // itself; the ones that are a slider open Orders, where the slider is; a
        // person at the heart of it is a press on their name.
        private static void WriteMoodActs(List<SheetLine> lines, CommonStates who, List<MoodFinding> found)
        {
            MoodActs.Clear();
            var labels = new List<string>();
            var tips = new List<string>();
            void Add(string label, string tip, System.Action act)
            {
                if (labels.Count >= 3 || labels.Contains(label)) { return; }
                labels.Add(label);
                tips.Add(tip);
                MoodActs.Add(act);
            }
            foreach (MoodFinding f in found)
            {
                if (f.Severity < MoodSeverity.Warn)
                {
                    continue;
                }
                switch (f.Key)
                {
                    case "sleep":
                        if (who.sexInfo != null && who.sexInfo.Length > 7 && who.sexInfo[7] != 0)
                        {
                            Add(Shared.Loc.Get("mood.fix.houses", "Allow houses"), Shared.Loc.Get("mood.fix.houses.help", "Let them go in and out of houses, and home to sleep"),
                                () => { who.sexInfo[7] = 0; MoodChanged(); });
                        }
                        else if (f.Fix != null)
                        {
                            Add(Shared.Loc.Get("mood.fix.orders", "Orders"), Shared.Loc.Get("mood.fix.orders.help", "Their area, combat order and permissions"), () => OpenOrders());
                        }
                        break;
                    case "company":
                        if (f.Other != null)
                        {
                            CommonStates other = f.Other;
                            Add(Short(other.charaName), Shared.Loc.Get("mood.fix.open", "Open {0}", other.charaName), () => PressRosterRow(other));
                        }
                        if (who.sexInfo != null && who.sexInfo.Length > 8 && who.sexInfo[8] == 0)
                        {
                            Add(Shared.Loc.Get("mood.fix.love_off", "Love off"), Shared.Loc.Get("mood.fix.love_off.short", "Stop them building love - and grudges - with their neighbours"),
                                () => { who.sexInfo[8] = 1; MoodChanged(); });
                        }
                        break;
                    case "crush":
                    case "bed":
                        if (f.Other != null)
                        {
                            CommonStates other = f.Other;
                            Add(Short(other.charaName), Shared.Loc.Get("mood.fix.open", "Open {0}", other.charaName), () => PressRosterRow(other));
                        }
                        Add(Shared.Loc.Get("mood.fix.orders", "Orders"), Shared.Loc.Get("mood.fix.orders.sex", "Their area and permissions: Sex is there"), () => OpenOrders(PulsePlace | PulseSex));
                        break;
                }
            }
            if (labels.Count == 0)
            {
                return;
            }
            lines.Add(new SheetLine
            {
                Kind = LineActions, Enabled = true, Arg = ActionsMood,
                Options = labels.ToArray(), Tips = tips.ToArray(),
            });
        }

        private static string Short(string name)
        {
            return string.IsNullOrEmpty(name) || name.Length <= 9 ? name : name.Substring(0, 8) + "…";
        }

        // A LANE'S FIX FOLDS: the Social page's lane carries a chevron, and its press
        // unfolds a row under it with the fix - one open at a time, its own press
        // folds it. The Args say which row it is: {0, its key} the lane, {1, an act
        // a button} the fix's row
        private static readonly List<string> MoodFixKeys = new List<string>();
        private static CommonStates _moodFixWho;
        private static string _moodFixKey;

        private static bool IsMoodFixOpen(CommonStates who, string key)
        {
            return ReferenceEquals(_moodFixWho, who) && _moodFixKey != null && _moodFixKey == key;
        }

        private static void ToggleMoodFix(CommonStates who, int index)
        {
            string key = index >= 0 && index < MoodFixKeys.Count ? MoodFixKeys[index] : null;
            if (key == null || IsMoodFixOpen(who, key))
            {
                _moodFixKey = null;
                return;
            }
            _moodFixWho = who;
            _moodFixKey = key;
        }

        private static void MoodAction(int sub)
        {
            if (sub >= 0 && sub < MoodActs.Count)
            {
                MoodActs[sub]();
            }
        }

        // ...and the cause's control glows there for a moment (PulseOrders):
        // the place by default, the flame too where sex is the cause
        private static void OpenOrders(int pulse = PulsePlace)
        {
            Aspect[] set = _footSets[GroupCharacter];
            int index = set != null ? System.Array.IndexOf(set, Aspect.Orders) : -1;
            if (index >= 0)
            {
                PulseOrders(pulse);
                PickAspect(GroupCharacter, index);
            }
        }

        private static void MoodChanged()
        {
            MoodReads.Clear();
            _forceRefresh = true;
            _nextRefresh = 0f;
        }

        // ---------- the Social page: the checklist ----------

        // THE CHECKLIST, KEYED TO THE GRAPH. Their last three days as a step chart
        // that ends in the MORAL box, a numbered square under every change, and
        // below it one printed line per cause: the number its changes carry, what
        // it is, what it moved them by in those days, what it costs now, and its
        // one fix at the end of the line. A cause no check names before it happens
        // (a meal, sex) still gets a line once the ledger saw it move them. The
        // head's pill says how far they have come - or, near the end, how far
        // they are from rebelling.
        private const float FormMoodNowH = 46f;
        private const float MoodMoonH = 6f;      // the line of moons over the night columns, as on WHO THEY MET
        private const float FormMoodCapsH = 6f;   // the columns' captions, under the chart
        private const float FormMoodCauseH = 15f;
        private const float MoodRugH = 6.4f;      // the rug of numbered marks under the chart
        // the mood's colours on paper: the HUD's amber and green are too pale there
        private static readonly Color PaperWarn = Theme.ParseColor("#B86E0E", new Color(0.72f, 0.43f, 0.05f, 1f));
        private static readonly Color PaperGood = Theme.ParseColor("#4A8A3A", new Color(0.29f, 0.54f, 0.23f, 1f));
        // the chart's paper before the record began, and the number on a mark
        private static readonly Color MoodVoid = Theme.ParseColor("#EEE5D0", Color.white);
        private static readonly Color MoodMarkInk = Theme.ParseColor("#F6EEDA", Color.white);
        private const int MoodNumbersMax = 9;

        private sealed class MoodCauseRow
        {
            public MoodFinding Finding;  // null: a cause the ledger saw that no check names
            public string Key;           // the ledger group it sums (MoodKey), or null
            public string Label;
            public MoodSeverity Severity;
            public CommonStates Other;
            public float Sum;            // what that group moved them by in three days
            public bool Moved;
            public int Number;           // its changes' number on the chart; 0 = none
        }

        // THE PAST AND THE FORECAST, IN TWO COLUMNS. A cause line says what the
        // ledger saw it do (3 DAYS: the sum over the chart's span) and what the
        // check reads it will do (NEXT: "+20 at dawn", "no recovery") - the chart
        // is the first, never the second, so each has its own column under its
        // own caption. Every line lays out against the widest of all of them, so
        // the columns stand straight under the captions.
        private sealed class MoodColumns
        {
            public readonly List<string> Fixes = new List<string>();
            public readonly List<string> Next = new List<string>();
            public readonly List<string> Past = new List<string>();
        }

        private static string MoodPastCaption => Shared.Loc.Get("mood.chart.past", "3 DAYS");
        private static string MoodNextCaption => Shared.Loc.Get("mood.chart.next", "NEXT");

        // the right edges of the two columns and the start of the fix, on `row`'s font
        private static void MoodColumnEdges(SheetRow row, MoodColumns cols, float right,
            out float pastRight, out float pastW, out float nextRight, out float nextW)
        {
            float fixW = 0f;
            foreach (string s in cols.Fixes) { fixW = Mathf.Max(fixW, MeasureForm(row, s, FormTextSize) + 10f); }
            nextW = 0f;
            foreach (string s in cols.Next) { nextW = Mathf.Max(nextW, MeasureForm(row, s, FormTextSize) + 1f); }
            pastW = 0f;
            foreach (string s in cols.Past) { pastW = Mathf.Max(pastW, MeasureForm(row, s, FormTextSize) + 2f); }
            if (nextW > 0f) { nextW = Mathf.Max(nextW, MeasureForm(row, MoodNextCaption, CaptionSize) + 3f); }
            if (pastW > 0f) { pastW = Mathf.Max(pastW, MeasureForm(row, MoodPastCaption, CaptionSize) + 3f); }
            nextRight = fixW > 0f ? right - fixW - 5f : right;
            pastRight = nextW > 0f ? nextRight - nextW - 6f : nextRight;
        }

        // `company`: the Social page's MOOD - the chart takes a gutter for the
        // lanes' names and the causes are lanes on its clock - and RELATIONS
        // after it, a section of its own (Company.cs)
        private static void WriteMoodForm(List<SheetLine> lines, CommonStates who, bool company = false)
        {
            List<MoodCauseRow> rows = MoodCauseRows(who, MoodFindings(who));
            _moodNumberTints.Clear();
            foreach (MoodCauseRow r in rows)
            {
                if (r.Number > 0) { _moodNumberTints[r.Number] = PaperTint(r.Severity); }
            }
            SheetLine now = MoodNowLine(who, rows);
            var cols = new MoodColumns();
            now.Columns = rows.Count > 0 && !company ? cols : null;
            now.Arg = company ? 1 : 0;
            SheetLine head = Section(Shared.Loc.Get("mood.section.moral", "MORAL"), MoodPill(who, now, out Color pillTint));
            head.ValueTint = pillTint;
            head.Tips = new[] { Shared.Loc.Get("mood.section.moral.help", "Their moral, -100 to 100: at -100 they rebel. A night indoors is the main way back up.") };
            lines.Add(head);
            // on the Social page what moves it follows the chart: each cause a
            // lane on the chart's clock, the nights among them
            int band = company ? 1 : 0;
            now.Band = band;
            lines.Add(now);
            if (company)
            {
                lines.Add(new SheetLine { Kind = LineMoodCaps, Enabled = true, Style = 1, Columns = cols, Band = band, SeriesNow = now.SeriesNow });
            }
            bool nights = false;
            MoodActs.Clear();
            MoodFixKeys.Clear();
            foreach (MoodCauseRow r in rows)
            {
                MoodFinding f = r.Finding;
                string tip = f != null ? f.Text : Shared.Loc.Get("mood.cause_row.help", "What the ledger saw move their moral.");
                if (r.Moved)
                {
                    tip += "\n\n" + Shared.Loc.Get("mood.cause_row.three_days", "Last three days") + "  " + TipSigned(r.Sum);
                }
                if (f != null && f.Fix != null)
                {
                    tip += "\n" + KitTooltip.FixMark + f.Fix;
                }
                var line = new SheetLine
                {
                    Kind = LineMoodCause, Enabled = true, Arg = -1,
                    Label = r.Label, Value = f != null ? f.Effect ?? string.Empty : string.Empty,
                    Index = (int)r.Severity, ValueTint = PaperTint(r.Severity),
                    Value2 = r.Moved ? r.Sum.ToString("+0;-0") : string.Empty,
                    Value2Tint = r.Sum < 0f ? MoralBad : PaperGood,
                    Number = r.Number, Tip = tip, Link = r.Other, Band = band,
                };
                int firstAct = MoodActs.Count;
                List<MoodFix> fixes = f != null ? FixesFor(who, f) : null;
                if (fixes != null && fixes.Count > 0)
                {
                    // the Mood page's row has room for the first; the lane's fold has all
                    line.Arg = firstAct;
                    foreach (MoodFix fix in fixes) { MoodActs.Add(fix.Act); }
                    line.Options = new[] { fixes[0].Label };
                    line.Tips = new[] { fixes[0].Tip };
                    cols.Fixes.Add(fixes[0].Label);
                }
                if (!string.IsNullOrEmpty(line.Value)) { cols.Next.Add(line.Value); }
                if (!string.IsNullOrEmpty(line.Value2)) { cols.Past.Add(line.Value2); }
                if (company)
                {
                    line = r.Key == SleepKey && !nights ? NightsLane(who, now.SeriesNow, line) : CauseLane(who, r, now.SeriesNow, line);
                    nights |= line.Style == 1;
                }
                line.Columns = cols;
                // the Social page's lane keeps its fix folded: the row under it, on a
                // press - what would help, and a button for each step that is a press
                bool fold = company && f != null && (f.Fix != null || (fixes != null && fixes.Count > 0));
                string key = r.Key ?? r.Label;
                bool unfold = fold && IsMoodFixOpen(who, key);
                if (fold)
                {
                    line.Args = new[] { 0, MoodFixKeys.Count };
                    MoodFixKeys.Add(key);
                }
                line.On = unfold;
                lines.Add(line);
                if (unfold)
                {
                    int n = fixes != null ? fixes.Count : 0;
                    var args = new int[n + 1];
                    var labels = new string[n];
                    var tips = new string[n];
                    args[0] = 1;
                    for (int i = 0; i < n; i++)
                    {
                        args[i + 1] = firstAct + i;
                        labels[i] = fixes[i].Label;
                        tips[i] = fixes[i].Tip;
                    }
                    lines.Add(new SheetLine
                    {
                        Kind = LineMoodCause, Band = 1, Style = 3, Enabled = true, Arg = -1, Args = args,
                        Label = f.Fix, Options = labels, Tips = tips, Columns = cols,
                    });
                }
            }
            if (company)
            {
                if (!nights)
                {
                    lines.Add(NightsLane(who, now.SeriesNow, new SheetLine { Kind = LineMoodCause, Enabled = true, Arg = -1, ValueTint = Theme.DossierSoft }));
                }
                lines.Add(new SheetLine { Kind = LineSpace });
                WriteLanes(lines, who, rows, now.SeriesNow);
            }
            lines.Add(new SheetLine { Kind = LineSpace });
        }

        // A NUMBER IN A TIP IN ITS SHEET COLOUR: a change green up and red down, as
        // the cause rows print it; the moral red below zero
        private static string TipSigned(float v)
        {
            string text = v.ToString("+0;-0;0");
            return Mathf.Abs(v) < 0.5f ? text
                : "<color=#" + ColorUtility.ToHtmlStringRGB(v > 0f ? PaperGood : MoralBad) + ">" + text + "</color>";
        }

        private static string TipMoral(float v)
        {
            string text = v.ToString("0");
            return v < 0f ? "<color=#" + ColorUtility.ToHtmlStringRGB(MoralBad) + ">" + text + "</color>" : text;
        }

        // a moment of the ledger's clock as the sheet prints it: "Day 118, 06:12"
        private static string DayClock(float day)
        {
            return Shared.Loc.Get("mood.day_clock", "Day {0}, {1}", Mathf.FloorToInt(day), Clock(day));
        }

        private static string Clock(float day)
        {
            int minutes = Mathf.Clamp(Mathf.FloorToInt((day - Mathf.Floor(day)) * 1440f), 0, 1439);
            return (minutes / 60).ToString("00") + ":" + (minutes % 60).ToString("00");
        }

        // the head's pill: near the end, how far from rebelling; otherwise how far
        // they have come since the chart's first point on record
        private static string MoodPill(CommonStates who, SheetLine now, out Color tint)
        {
            if (who.moral <= -70f)
            {
                tint = Theme.DossierRed;
                return Shared.Loc.Get("mood.pill.from_rebelling", "{0} from rebelling", (who.moral + 100f).ToString("0"));
            }
            float d = who.moral - now.SeriesY[0];
            if (Mathf.Abs(d) < 0.5f)
            {
                tint = Theme.DossierSoft;
                return Shared.Loc.Get("mood.pill.steady", "steady");
            }
            int days = Mathf.RoundToInt((1f - now.SeriesX[0]) * 3f);
            tint = d > 0f ? PaperGood : MoralBad;
            return days <= 1 ? Shared.Loc.Get("mood.pill.in_a_day", "{0} in a day", d.ToString("+0;-0"))
                : Shared.Loc.Plural("mood.pill.in_days", "{1} in {0} day", "{1} in {0} days", days, d.ToString("+0;-0"));
        }

        // the ledger's changes in the groups a line sums: a cause and whom it was
        // about. Sleep is one group, wherever they slept.
        private static string MoodKey(MoodEvent e)
        {
            return (int)e.Cause + ":" + (e.Cause == MoodCause.Sleep ? -1 : e.Partner);
        }

        // the group a check's finding is the forecast of, or null when it is none
        private static string MoodKey(MoodFinding f)
        {
            int other = f.Other != null ? f.Other.friendID : -1;
            switch (f.Key)
            {
                case "sleep":
                case "far": return (int)MoodCause.Sleep + ":-1";
                case "company": return (int)MoodCause.BadCompany + ":" + other;
                case "crush": return (int)MoodCause.Crush + ":" + other;
                case "bed": return (int)MoodCause.NoBed + ":" + other;
                default: return null;
            }
        }

        // a group no check names, printed as a cause line. A name stays as written.
        private static string MoodLedgerLabel(MoodEvent e, CommonStates other)
        {
            switch (e.Cause)
            {
                case MoodCause.Sleep: return Shared.Loc.Get("mood.label.sleep", "Sleeps indoors");
                case MoodCause.BadCompany: return other != null ? Shared.Loc.Get("mood.label.company_with", "Bad company: {0}", other.charaName) : Shared.Loc.Get("mood.label.company", "Bad company");
                case MoodCause.Crush: return other != null ? Shared.Loc.Get("mood.label.pining_for", "Pining {0}", other.charaName) : Shared.Loc.Get("mood.label.pining", "Pining");
                case MoodCause.NoBed: return other != null ? Shared.Loc.Get("mood.label.no_bed_with", "No bed with {0}", other.charaName) : Shared.Loc.Get("mood.label.no_bed", "No bed");
                case MoodCause.Sex: return other != null ? Shared.Loc.Get("mood.label.sex_with", "Sex with {0}", other.charaName) : Shared.Loc.Get("mood.label.sex", "Sex");
                default: return Capital(CauseWord(e));
            }
        }

        // the lines: the checks' findings, each with what its group moved them by,
        // then the groups no check names. Those that moved them come first, the
        // most first, and are numbered; then those that have not yet, in the
        // checks' order.
        private static List<MoodCauseRow> MoodCauseRows(CommonStates who, List<MoodFinding> found)
        {
            IList<MoodEvent> events = MoodLedger.Events(who);
            float from = MoodLedger.Now(_mn) - 3f;
            var sums = new Dictionary<string, float>();
            var first = new Dictionary<string, MoodEvent>();
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Day < from) { continue; }
                string key = MoodKey(events[i]);
                sums.TryGetValue(key, out float sum);
                sums[key] = sum + events[i].Delta;
                if (!first.ContainsKey(key)) { first[key] = events[i]; }
            }
            var rows = new List<MoodCauseRow>();
            var used = new HashSet<string>();
            foreach (MoodFinding f in found)
            {
                if (f.Short == null) { continue; }
                string key = MoodKey(f);
                if (key != null && !used.Add(key)) { key = null; }
                float sum = 0f;
                bool moved = key != null && sums.TryGetValue(key, out sum) && Mathf.Abs(sum) >= 0.5f;
                rows.Add(new MoodCauseRow
                {
                    Finding = f, Key = key, Label = Capital(f.Short), Severity = f.Severity,
                    Other = f.Other, Sum = sum, Moved = moved,
                });
            }
            foreach (KeyValuePair<string, float> kv in sums)
            {
                if (used.Contains(kv.Key) || Mathf.Abs(kv.Value) < 0.5f) { continue; }
                MoodEvent e = first[kv.Key];
                CommonStates other = e.Partner >= 0 && _mn != null && _mn.npcMN != null ? _mn.npcMN.GetFriend(e.Partner) : null;
                rows.Add(new MoodCauseRow
                {
                    Key = kv.Key, Label = MoodLedgerLabel(e, other),
                    Severity = kv.Value > 0f ? MoodSeverity.Good : MoodSeverity.Bad,
                    Other = other, Sum = kv.Value, Moved = true,
                });
            }
            var order = new List<MoodCauseRow>(rows);
            order.Sort((a, b) => a.Moved != b.Moved ? (a.Moved ? -1 : 1)
                : a.Moved && Mathf.Abs(a.Sum) != Mathf.Abs(b.Sum) ? Mathf.Abs(b.Sum).CompareTo(Mathf.Abs(a.Sum))
                : rows.IndexOf(a).CompareTo(rows.IndexOf(b)));
            int n = 0;
            foreach (MoodCauseRow r in order)
            {
                if (r.Moved && n < MoodNumbersMax) { r.Number = ++n; }
            }
            return order;
        }

        private static Color PaperTint(MoodSeverity s)
        {
            return s == MoodSeverity.Bad ? MoralBad : s == MoodSeverity.Warn ? PaperWarn
                : s == MoodSeverity.Good ? PaperGood : Theme.DossierSoft;
        }

        // A CAUSE'S FIXES, every step its advice names that is a press: the switch
        // itself where the step is a switch, Orders where it is their area or a
        // permission. Steps that are play - knock them down, spend the night near
        // them, build a bed - stay words in the advice.
        private struct MoodFix
        {
            public string Label;
            public string Tip;
            public System.Action Act;
        }

        private static List<MoodFix> FixesFor(CommonStates who, MoodFinding f)
        {
            var fixes = new List<MoodFix>();
            void Add(string label, string tip, System.Action act)
            {
                fixes.Add(new MoodFix { Label = label, Tip = tip, Act = act });
            }
            if (f.Severity < MoodSeverity.Warn || who.sexInfo == null || who.sexInfo.Length < 10)
            {
                return fixes;
            }
            CommonStates other = f.Other;
            switch (f.Key)
            {
                case "sleep":
                    if (who.sexInfo[7] != 0)
                    {
                        Add(Shared.Loc.Get("mood.fix.houses", "Allow houses"), Shared.Loc.Get("mood.fix.houses.help", "Let them go in and out of houses, and home to sleep"),
                            () => { who.sexInfo[7] = 0; MoodChanged(); });
                    }
                    // following is undone by talking to them, not in Orders
                    if (who.nMove == null || who.nMove.npcType != NPCMove.NPCType.Follow)
                    {
                        Add(Shared.Loc.Get("mood.fix.orders", "Orders"), Shared.Loc.Get("mood.fix.orders.help", "Their area, combat order and permissions"), () => OpenOrders());
                    }
                    break;
                case "company":
                    if (who.sexInfo[8] == 0)
                    {
                        Add(Shared.Loc.Get("mood.fix.love_off", "Love off"), Shared.Loc.Get("mood.fix.love_off.help", "Stop them building love - and grudges - with their neighbours. Their love for you stops growing too."),
                            () => { who.sexInfo[8] = 1; MoodChanged(); });
                    }
                    // the game skips a pair when either has Love off, so theirs does it too
                    if (other != null && other.sexInfo != null && other.sexInfo.Length > 8 && other.sexInfo[8] == 0
                        && _mn != null && _mn.npcMN != null && _mn.npcMN.IsActiveFriend(other))
                    {
                        Add(Shared.Loc.Get("mood.fix.love_off_other", "Love off: {0}", Short(other.charaName)), Shared.Loc.Get("mood.fix.love_off_other.help", "Stop {0} building love - and grudges - with their neighbours. Their love for you stops growing too.", other.charaName),
                            () => { other.sexInfo[8] = 1; MoodChanged(); });
                    }
                    Add(Shared.Loc.Get("mood.fix.orders", "Orders"), other != null ? Shared.Loc.Get("mood.fix.orders.away_from", "Their area: move it away from {0}", other.charaName)
                        : Shared.Loc.Get("mood.fix.orders.away", "Their area: move it away from them"), () => OpenOrders());
                    break;
                case "crush":
                    Add(Shared.Loc.Get("mood.fix.orders", "Orders"), other != null ? Shared.Loc.Get("mood.fix.orders.with", "Their area and house: put them in one with {0}", other.charaName)
                        : Shared.Loc.Get("mood.fix.orders.with_them", "Their area and house: put them in one with them"), () => OpenOrders());
                    if (who.sexInfo[9] == 0)
                    {
                        Add(Shared.Loc.Get("mood.fix.sex_off", "Sex off"), Shared.Loc.Get("mood.fix.sex_off.help", "They stop asking, so no one turns them down - and no sex, so none of its +3 mood either"),
                            () => { who.sexInfo[9] = 1; MoodChanged(); });
                    }
                    break;
                case "bed":
                    Add(Shared.Loc.Get("mood.fix.orders", "Orders"), Shared.Loc.Get("mood.fix.orders.free_bed", "Their area: move it to a house with a free bed"), () => OpenOrders());
                    break;
            }
            return fixes;
        }

        // their last three days: the ledger's changes as points, merged where they
        // fall within the same hour. Back to when the ledger began, no change on
        // record means none happened, so the line runs level there (it was a stub
        // at "now" for someone whose mood had not moved); before that it is left
        // blank rather than guessed. The MARKS are the numbered lines' changes,
        // one per line per quarter day, so a night's sleep is one square, not one
        // per tick.
        private static SheetLine MoodNowLine(CommonStates who, List<MoodCauseRow> rows)
        {
            IList<MoodEvent> events = MoodLedger.Events(who);
            float now = MoodLedger.Now(_mn), from = now - 3f;
            var numbered = new Dictionary<string, MoodCauseRow>();
            foreach (MoodCauseRow r in rows)
            {
                if (r.Number > 0 && r.Key != null) { numbered[r.Key] = r; }
            }
            var bins = new SortedDictionary<int, List<MoodCauseRow>>();
            var binX = new Dictionary<int, float>();
            var binSums = new Dictionary<long, float>();
            var xs = new List<float>();
            var ys = new List<float>();
            // what moved it at each point, for the point's tip: a cause and its sum
            var why = new List<Dictionary<string, float>>();
            var whyAt = new List<float>();
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Day < from) { continue; }
                float x = Mathf.Clamp01((events[i].Day - from) / 3f);
                if (numbered.TryGetValue(MoodKey(events[i]), out MoodCauseRow mark))
                {
                    // the clock's quarter days (00, 06, 12, 18), not the window's:
                    // the window moves with the clock, and a mark's span with it
                    int bin = Mathf.FloorToInt(events[i].Day * 4f);
                    if (!bins.TryGetValue(bin, out List<MoodCauseRow> here))
                    {
                        bins[bin] = here = new List<MoodCauseRow>();
                        binX[bin] = Mathf.Clamp01(((bin + 0.5f) / 4f - from) / 3f);
                    }
                    if (!here.Contains(mark)) { here.Add(mark); }
                    long sumKey = (long)bin * 100 + mark.Number;
                    binSums.TryGetValue(sumKey, out float binSum);
                    binSums[sumKey] = binSum + events[i].Delta;
                }
                if (xs.Count == 0)
                {
                    xs.Add(x);
                    ys.Add(events[i].After - events[i].Delta);
                    why.Add(new Dictionary<string, float>());
                    whyAt.Add(events[i].Day);
                }
                if (x - xs[xs.Count - 1] < 1f / 72f)
                {
                    ys[ys.Count - 1] = events[i].After;
                }
                else
                {
                    xs.Add(x);
                    ys.Add(events[i].After);
                    why.Add(new Dictionary<string, float>());
                    whyAt.Add(events[i].Day);
                }
                CommonStates other = events[i].Partner >= 0 && _mn != null && _mn.npcMN != null ? _mn.npcMN.GetFriend(events[i].Partner) : null;
                string label = MoodLedgerLabel(events[i], other);
                Dictionary<string, float> at = why[why.Count - 1];
                at.TryGetValue(label, out float sofar);
                at[label] = sofar + events[i].Delta;
            }
            float start = Mathf.Clamp01((MoodLedger.RecordSince(_mn) - from) / 3f);
            if (xs.Count > 0 && start < xs[0])
            {
                xs.Insert(0, start);
                ys.Insert(0, ys[0]);
                why.Insert(0, null);
                whyAt.Insert(0, 0f);
            }
            xs.Add(1f);
            ys.Add(who.moral);
            why.Add(null);
            whyAt.Add(0f);
            if (xs.Count == 1)
            {
                xs.Insert(0, Mathf.Min(start, 0.96f));
                ys.Insert(0, who.moral);
                why.Insert(0, null);
                whyAt.Insert(0, 0f);
            }
            // each level's span, from the events that began and ended it - fixed
            // times, not the window's moving edge; one that began before the record
            // or the window says only when it ended
            var levelTips = new string[Mathf.Max(0, xs.Count - 1)];
            for (int i = 0; i + 1 < xs.Count; i++)
            {
                bool began = why[i] != null && why[i].Count > 0;
                bool ended = i + 2 < xs.Count && why[i + 1] != null && why[i + 1].Count > 0;
                string span = began && ended ? Shared.Loc.Get("mood.span.from_to", "{0} to {1}", DayClock(whyAt[i]), DayClock(whyAt[i + 1]))
                    : began ? Shared.Loc.Get("mood.span.since", "Since {0}", DayClock(whyAt[i]))
                    : ended ? Shared.Loc.Get("mood.span.until", "Until {0}", DayClock(whyAt[i + 1]))
                    : Shared.Loc.Get("mood.span.none", "No change on record");
                levelTips[i] = "<b>" + Shared.Loc.Get("mood.level", "Moral {0}", TipMoral(ys[i])) + "</b>\n" + span;
            }
            var pointTips = new string[xs.Count];
            for (int i = 0; i < xs.Count; i++)
            {
                if (why[i] == null || why[i].Count == 0) { continue; }
                var sb = new System.Text.StringBuilder("<b>").Append(DayClock(whyAt[i])).Append("</b>");
                foreach (KeyValuePair<string, float> c in why[i])
                {
                    sb.Append('\n').Append(c.Key).Append("  ").Append(TipSigned(c.Value));
                }
                sb.Append("\n\n").Append(Shared.Loc.Get("mood.moral_after", "Moral after")).Append("  ").Append(TipMoral(ys[i]));
                pointTips[i] = sb.ToString();
            }
            var markX = new List<float>();
            var markN = new List<int>();
            var markTint = new List<Color>();
            var markTips = new List<string>();
            foreach (KeyValuePair<int, List<MoodCauseRow>> bin in bins)
            {
                bin.Value.Sort((a, b) => a.Number.CompareTo(b.Number));
                float binFrom = bin.Key / 4f, binTo = (bin.Key + 1) / 4f;
                foreach (MoodCauseRow r in bin.Value)
                {
                    markX.Add(binX[bin.Key]);
                    markN.Add(r.Number);
                    markTint.Add(PaperTint(r.Severity));
                    binSums.TryGetValue((long)bin.Key * 100 + r.Number, out float sum);
                    markTips.Add("<b>" + r.Number + "  " + r.Label + "</b>\n"
                        + Shared.Loc.Get("mood.span.from_to", "{0} to {1}", DayClock(binFrom), bin.Key % 4 == 3 ? "24:00" : Clock(binTo))
                        + "  " + TipSigned(sum));
                }
            }
            string moved = MovedBy(who);
            return new SheetLine
            {
                Kind = LineMoodNow, Enabled = true,
                Value = who.moral.ToString("0"), ValueTint = who.moral < 0f ? MoralBad : PaperGood,
                Frac = Mathf.Clamp01(Mathf.Abs(who.moral) / 100f), Tint = MoralTint(who),
                Warns = new[] { who.moral < 0f },
                SeriesX = xs.ToArray(), SeriesY = ys.ToArray(), SeriesNow = now,
                MarkX = markX.ToArray(), Marks = markN.ToArray(), Tints = markTint.ToArray(),
                Tips = markTips.ToArray(), Options = pointTips, Foots = levelTips,
                Tip = moved != null ? Shared.Loc.Get("mood.now.moved", "Since this time yesterday: {0}", moved)
                    : Shared.Loc.Get("mood.now.none", "No change on record in the last three days"),
            };
        }

        // THE MORAL TODAY ENDS THE LINE: the box sits at the graph's right edge, where
        // "now" is, and is the Character page's MORAL box - caption, number, bar,
        // and the alarm's red wash and bold rim below zero - so the one number
        // reads the same on both pages. The chart: nights shaded as on WHO THEY
        // MET, the line in ink over a light green or red fill to a dashed zero,
        // scaled to what it spans - and to -100, with the floor drawn, once they
        // are near it. Under it the rug of numbered marks, then the midnights.
        private static void DrawMoodNow(SheetRow row, SheetLine line, float w, float h)
        {
            float left = SheetPadX;
            float right = w - SheetPadX;
            float boxW = line.Arg == 1 ? CompanyBoxW : 40f;
            float boxH = BoxHeight(StyleVital);
            const float by = 2f;
            float boxX = right - boxW;
            float[] xs = line.SeriesX;
            float[] ys = line.SeriesY;
            int n = xs != null && ys != null ? Mathf.Min(xs.Length, ys.Length) : 0;
            // on the Social page the names' gutter comes off the left, so the
            // cause lanes under it share the chart's hours
            float x0 = line.Arg == 1 ? left + LaneGutter : left;
            float x1 = boxX - 6f;
            float top = by + MoodMoonH;
            if (line.Arg == 1)
            {
                FormText(row, Shared.Loc.Get("mood.chart.caption1", "THE MORAL,"), CaptionSize * 0.86f, Theme.DossierSoft, TextAlignmentOptions.TopLeft, true,
                    left, top + 1f, LaneGutter - 3f, 5f, CaptionSpacing * 0.4f);
                FormText(row, Shared.Loc.Get("mood.chart.caption2", "THREE DAYS"), CaptionSize * 0.86f, Theme.DossierSoft, TextAlignmentOptions.TopLeft, true,
                    left, top + 5.5f, LaneGutter - 3f, 5f, CaptionSpacing * 0.4f);
            }
            float bottom = top + boxH;
            float rug0 = bottom + 1.2f;
            float rug1 = line.Arg == 1 ? rug0 : bottom + MoodRugH + 1.2f;   // on the Social page the cause lanes are the rug
            float from = line.SeriesNow - 3f;
            float lo = 0f, hi = 0f;
            for (int i = 0; i < n; i++)
            {
                lo = Mathf.Min(lo, ys[i]);
                hi = Mathf.Max(hi, ys[i]);
            }
            lo -= 8f;
            hi += 8f;
            if (hi - lo < 50f)
            {
                float mid = (hi + lo) * 0.5f;
                lo = mid - 25f;
                hi = mid + 25f;
            }
            if (lo < -60f) { lo = -100f; }
            lo = Mathf.Max(-100f, lo);
            hi = Mathf.Min(100f, hi);
            float span = Mathf.Max(1f, hi - lo);
            System.Func<float, float> yv = v => top + (hi - v) / span * (bottom - top);
            System.Func<float, float> xx = f => x0 + f * (x1 - x0);
            float xs0 = n > 0 ? xx(xs[0]) : x0;
            for (int d = Mathf.FloorToInt(from) - 1; d <= Mathf.FloorToInt(line.SeriesNow); d++)
            {
                float a = Mathf.Max(0f, (d + 21f / 24f - from) / 3f);
                float b = Mathf.Min(1f, (d + 1f + 6f / 24f - from) / 3f);
                if (b > a)
                {
                    // the shading alone did not say "night": a moon over each column,
                    // on a line of its own over the chart
                    FormImage(row, null, 0, MetNight, xx(a), by, xx(b) - xx(a), (line.Arg == 1 ? h : rug1) - by);
                    const float ms = 4.6f;
                    if (xx(b) - xx(a) >= ms + 1f)
                    {
                        FormImage(row, "kit_moon", 0, Theme.DossierSoft, (xx(a) + xx(b) - ms) * 0.5f, by + 0.7f, ms, ms);
                        row.TipZones.Add(new Rect(xx(a), by, xx(b) - xx(a), MoodMoonH));
                        row.TipTexts.Add("<b>" + Shared.Loc.Get("mood.night", "Night") + "</b>\n"
                            + Shared.Loc.Get("mood.night.help", "21:00 to 06:00. A night indoors pays at dawn:\nup to +10, or +20 while below zero"));
                    }
                }
            }
            if (xs0 > x0 + 0.1f)
            {
                FormImage(row, null, 0, MoodVoid, x0, by, xs0 - x0, rug1 - by);   // over the moons' line too: no nights are known there
                if (xs0 - x0 > 26f)
                {
                    FormText(row, Shared.Loc.Get("mood.chart.no_record", "NO RECORD"), CaptionSize, Theme.DossierSoft, TextAlignmentOptions.Center, true,
                        x0, by, xs0 - x0, rug1 - by, CaptionSpacing * 0.5f);
                }
            }
            if (lo <= -100f)
            {
                FormImage(row, null, 0, Alpha(MoralBad, 0.1f), xs0, yv(-80f), x1 - xs0, yv(-100f) - yv(-80f));
                FormRule(row, Theme.DossierRed, xs0, yv(-100f), x1 - xs0, 0.5f);
                FormText(row, Shared.Loc.Get("mood.chart.rebels_at", "REBELS AT {0}", "\u2212100"), CaptionSize * 0.82f, Theme.DossierRed, TextAlignmentOptions.MidlineLeft,
                    true, xs0 + 1.5f, yv(-100f) - 4.5f, 40f, 5f, CaptionSpacing * 0.4f);
            }
            for (int i = 0; i + 1 < n; i++)
            {
                float ax = xx(xs[i]), bx = xx(xs[i + 1]);
                float y0 = yv(0f), y1 = yv(ys[i]);
                if (bx - ax > 0.05f && Mathf.Abs(y1 - y0) > 0.05f)
                {
                    FormImage(row, null, 0, Alpha(ys[i] >= 0f ? PaperGood : MoralBad, 0.16f),
                        ax, Mathf.Min(y0, y1), bx - ax, Mathf.Abs(y1 - y0));
                }
            }
            FormDash(row, Alpha(Theme.DossierInk, 0.55f), xs0, yv(0f), x1 - xs0, false, 0.35f, 1.2f, 0.9f);
            Color ink = Theme.DossierInk;
            const float t = 0.75f;
            bool flat = true;
            for (int i = 0; i + 1 < n; i++)
            {
                float ax = xx(xs[i]);
                float bx = xx(xs[i + 1]);
                float ay = yv(ys[i]);
                float byy = yv(ys[i + 1]);
                if (bx - ax > 0.05f)
                {
                    FormImage(row, null, 0, ink, ax, ay - t * 0.5f, bx - ax + t * 0.5f, t);
                }
                if (Mathf.Abs(byy - ay) > 0.05f)
                {
                    FormImage(row, null, 0, ink, bx - t * 0.5f, Mathf.Min(ay, byy) - t * 0.5f, t, Mathf.Abs(byy - ay) + t);
                    flat = false;
                }
            }
            if (n > 0)
            {
                FormImage(row, null, 0, ink, x1 - 1.1f, yv(ys[n - 1]) - 1.1f, 2.2f, 2.2f);
            }
            // HOVER, the changes first: each step's causes and where it left them;
            // then each level: the moral it held, and from when to when
            for (int i = 0; line.Options != null && i < n && i < line.Options.Length; i++)
            {
                if (line.Options[i] == null) { continue; }
                row.TipZones.Add(new Rect(xx(xs[i]) - 1.8f, top, 3.6f, bottom - top));
                row.TipTexts.Add(line.Options[i]);
            }
            for (int i = 0; line.Foots != null && i + 1 < n && i < line.Foots.Length; i++)
            {
                float ax = xx(xs[i]), bx = xx(xs[i + 1]);
                if (bx - ax < 0.5f) { continue; }
                row.TipZones.Add(new Rect(ax, top, bx - ax, bottom - top));
                row.TipTexts.Add(line.Foots[i]);
            }
            // the rug: one numbered square per line per quarter day, the number its line below
            FormRule(row, Theme.DossierRule, xs0, rug0 - 0.6f, x1 - xs0);
            const float s = 4.6f;
            float sy = (rug0 + rug1) * 0.5f + 0.2f - s * 0.5f;
            float[] mx = line.MarkX;
            int[] mn = line.Marks;
            Color[] mt = line.Tints;
            int marks = line.Arg != 1 && mx != null && mn != null && mt != null ? Mathf.Min(mx.Length, Mathf.Min(mn.Length, mt.Length)) : 0;
            float last = float.MinValue;
            for (int i = 0; i < marks;)
            {
                int j = i;
                while (j < marks && mx[j] == mx[i]) { j++; }
                float gw = (j - i) * s + (j - i - 1) * 0.6f;
                float sx = Mathf.Min(Mathf.Max(Mathf.Max(xx(mx[i]) - s * 0.5f, xs0), last + 1f), x1 - gw);
                for (int k = i; k < j; k++)
                {
                    FormImage(row, null, 0, mt[k], sx, sy, s, s);
                    if (line.Tips != null && k < line.Tips.Length)
                    {
                        row.TipZones.Add(new Rect(sx - 0.3f, sy - 0.3f, s + 0.6f, s + 0.6f));
                        row.TipTexts.Add(line.Tips[k]);
                    }
                    FormText(row, mn[k].ToString(), s * 0.78f, MoodMarkInk, TextAlignmentOptions.Center, true, sx, sy + 0.1f, s, s);
                    sx += s + 0.6f;
                }
                last = sx - 0.6f;
                i = j;
            }
            if (flat && marks == 0 && line.Arg != 1)
            {
                FormText(row, Shared.Loc.Get("mood.chart.no_change", "NO CHANGE"), CaptionSize * 0.82f, Theme.DossierSoft, TextAlignmentOptions.Center, true,
                    xs0, rug0, x1 - xs0, rug1 - rug0, CaptionSpacing * 0.4f);
            }
            float labelY = rug1 + 1.2f;
            const float labelSize = CaptionSize * 0.86f;
            // a day's label is left out where it would run into NOW's
            float nowX = x1 - MeasureForm(row, Shared.Loc.Get("chart.now", "NOW"), labelSize) - 3f;
            for (int d = Mathf.FloorToInt(from) + 1; d <= Mathf.FloorToInt(line.SeriesNow); d++)
            {
                float mxd = xx((d - from) / 3f);
                FormDash(row, Alpha(Theme.DossierSoft, 0.8f), mxd, top, (line.Arg == 1 ? h : rug1 + 1.5f) - top, true, 0.3f, 0.8f, 0.8f);
                if (mxd + 1f + MeasureForm(row, Shared.Loc.Get("chart.day", "DAY {0}", d), labelSize) + 2.5f < nowX)
                {
                    FormText(row, Shared.Loc.Get("chart.day", "DAY {0}", d), CaptionSize * 0.86f, Theme.DossierSoft, TextAlignmentOptions.MidlineLeft, true,
                        mxd + 1f, labelY, 24f, 4.4f, CaptionSpacing * 0.4f);
                }
            }
            FormText(row, Shared.Loc.Get("chart.now", "NOW"), CaptionSize * 0.86f, Theme.DossierInk, TextAlignmentOptions.MidlineRight, true,
                x1 - 20f, labelY, 20f, 4.4f, CaptionSpacing * 0.4f);
            if (line.Columns != null)
            {
                MoodColumnEdges(row, line.Columns, right, out float pastRight, out float pastW,
                    out float nextRight, out float nextW);
                float capY = h - FormMoodCapsH;
                if (pastW > 0f)
                {
                    FormText(row, MoodPastCaption, CaptionSize, Theme.DossierSoft, TextAlignmentOptions.BottomRight, true,
                        pastRight - pastW - 4f, capY, pastW + 4f, FormMoodCapsH - 0.5f, CaptionSpacing * 0.4f);
                }
                if (nextW > 0f)
                {
                    FormText(row, MoodNextCaption, CaptionSize, Theme.DossierSoft, TextAlignmentOptions.BottomRight, true,
                        nextRight - nextW - 4f, capY, nextW + 4f, FormMoodCapsH - 0.5f, CaptionSpacing * 0.4f);
                }
            }
            bool warn = line.Warns != null && line.Warns.Length > 0 && line.Warns[0];
            if (warn)
            {
                FormImage(row, null, 0, Alpha(Theme.DossierRed, 0.1f), boxX, top, boxW, boxH);
            }
            FormImage(row, warn ? "kit_dossier_box_bold" : "kit_dossier_box", warn ? 7 : 6,
                warn ? Theme.DossierRed : Theme.DossierInk, boxX, top, boxW, boxH);
            FormText(row, Shared.Loc.Get("dossier.moral", "MORAL"), CaptionSize, Theme.DossierSoft, TextAlignmentOptions.TopLeft, true,
                boxX + 3f, top + 2.2f, boxW - 6f, 6f, CaptionSpacing * 0.6f);
            FormText(row, line.Value ?? string.Empty, BoxValueSize, warn ? Theme.DossierRed : Theme.DossierInk,
                TextAlignmentOptions.MidlineRight, true, boxX + 3f, top + 5.5f, boxW - 6f, 11f);
            FormBar(row, boxX + 3f, top + boxH - 4.5f, boxW - 6f, 2.6f, line.Frac, line.Tint);
            if (!string.IsNullOrEmpty(line.Tip))
            {
                row.TipZones.Add(new Rect(left, 0f, right - left, h));
                row.TipTexts.Add(line.Tip);
            }
        }

        // a printed dashed rule, across from (x, y) or down from it
        private static void FormDash(SheetRow row, Color color, float x, float y, float length, bool down,
            float thick, float on, float off)
        {
            for (float at = 0f; at < length; at += on + off)
            {
                float l = Mathf.Min(on, length - at);
                if (down)
                {
                    FormImage(row, null, 0, color, x - thick * 0.5f, y + at, thick, l);
                }
                else
                {
                    FormImage(row, null, 0, color, x + at, y - thick * 0.5f, l, thick);
                }
            }
        }

        // A CAUSE LINE: its number on the chart above in a square of its colour -
        // or, before it has moved them, the tick, cross or bang - what it is, what
        // it moved them by in three days, what it costs now, and its fix
        private static int DrawMoodCause(SheetRow row, SheetLine line, float w, float h)
        {
            if (line.Band == 1)
            {
                return DrawCauseLane(row, line, w, h);   // the Social page's: the cause on the clock (Company.cs)
            }
            float left = SheetPadX;
            float right = w - SheetPadX;
            const float bs = 7.5f;
            float by = (h - bs) * 0.5f;
            Color tint = line.ValueTint;
            bool numbered = line.Number > 0;
            if (numbered)
            {
                const float ns = 6.4f;
                float ny = (h - ns) * 0.5f;
                FormImage(row, null, 0, tint, left + 0.5f, ny, ns, ns);
                FormText(row, line.Number.ToString(), ns * 0.78f, MoodMarkInk, TextAlignmentOptions.Center, true,
                    left + 0.5f, ny + 0.1f, ns, ns);
            }
            else
            {
                FormImage(row, "kit_dossier_check", 0, tint, left, by, bs, bs);
                var severity = (MoodSeverity)line.Index;
                string mark = severity == MoodSeverity.Good ? "kit_check" : severity == MoodSeverity.Bad ? "kit_cross"
                    : severity == MoodSeverity.Warn ? "kit_bang" : null;
                if (mark != null)
                {
                    FormImage(row, mark, 0, tint, left + 1f, by + 1f, bs - 2f, bs - 2f);
                }
            }
            float textX = left + bs + 4f;
            float end = right;
            int planks = 0;
            // the fix is plank 1; plank 0 is the name in the cause, a link (Links.cs)
            if (line.Options != null && line.Options.Length > 0 && !string.IsNullOrEmpty(line.Options[0]))
            {
                float bw = MeasureForm(row, line.Options[0], FormTextSize) + 10f;
                float bx = right - bw;
                DrawButton(row, 1, line.Options[0], false, bx, (h - FormButtonH) * 0.5f, bw);
                if (line.Tips != null && line.Tips.Length > 0 && !string.IsNullOrEmpty(line.Tips[0]))
                {
                    row.TipZones.Add(new Rect(bx, 0f, bw, h));
                    row.TipTexts.Add(line.Tips[0]);
                }
                planks = 2;
                end = bx - 5f;
            }
            float pastRight = end, pastW = 0f, nextRight = end, nextW = 0f;
            if (line.Columns != null)
            {
                MoodColumnEdges(row, line.Columns, right, out pastRight, out pastW, out nextRight, out nextW);
            }
            if (!string.IsNullOrEmpty(line.Value))
            {
                float valueW = Mathf.Max(nextW, MeasureForm(row, line.Value, FormTextSize) + 1f);
                FormText(row, line.Value, FormTextSize, numbered ? Theme.DossierSoft : tint,
                    TextAlignmentOptions.MidlineRight, false, nextRight - valueW, 0f, valueW, h);
            }
            if (!string.IsNullOrEmpty(line.Value2))
            {
                float sumW = Mathf.Max(pastW, MeasureForm(row, line.Value2, FormTextSize) + 2f);
                FormText(row, line.Value2, FormTextSize, line.Value2Tint, TextAlignmentOptions.MidlineRight, true,
                    pastRight - sumW, 0f, sumW, h);
            }
            end = Mathf.Min(end, (pastW > 0f ? pastRight - pastW : nextW > 0f ? nextRight - nextW : end) - 4f);
            string label = line.Label ?? string.Empty;
            FormText(row, label, FormTextSize, Theme.DossierInk, TextAlignmentOptions.MidlineLeft,
                false, textX, 0f, Mathf.Max(8f, end - textX), h, 0f, fit: true);
            string name = line.Link != null ? line.Link.charaName : null;
            int at = !string.IsNullOrEmpty(name) ? label.LastIndexOf(name, System.StringComparison.Ordinal) : -1;
            if (at >= 0)
            {
                float nx = textX + MeasureForm(row, label.Substring(0, at), FormTextSize);
                float nw = MeasureForm(row, name, FormTextSize);
                if (Hot(row, 0))
                {
                    FormRule(row, Theme.DossierInk, nx, h * 0.5f + 3.4f, nw, 0.5f);
                }
                FormHit(row, 0, nx, 0f, nw, h);
                row.TipZones.Add(new Rect(nx, 0f, nw, h));
                row.TipTexts.Add(Shared.Loc.Get("sheet.click_to_target", "Click to target {0}", name));
                planks = Mathf.Max(planks, 1);
            }
            else
            {
                FormNoHit(row, 0);
            }
            FormRule(row, Theme.DossierRule, left, h - 0.5f, right - left);
            if (!string.IsNullOrEmpty(line.Tip))
            {
                row.TipZones.Add(new Rect(left, 0f, end - left, h));
                row.TipTexts.Add(line.Tip);
            }
            return planks;
        }

        // a word's printed width at `size`, measured on the row's own blank label
        private static float MeasureForm(SheetRow row, string text, float size)
        {
            TextMeshProUGUI probe = row.Label;
            float keep = probe.fontSize;
            probe.fontSize = size;
            float width = probe.GetPreferredValues(text, 400f, 40f).x;
            probe.fontSize = keep;
            return width;
        }
    }
}

