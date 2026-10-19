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

        private static readonly string[] MoodSections = { "Rebelling", "Close to rebelling", "Unhappy", "At risk", "Content" };

        private static string MoodSection(RosterEntry e)
        {
            return MoodSections[MoodRank(e)];
        }

        // the filter's word for the unhappy, beside names and kinds
        private static bool MoodQuery(string q, CommonStates who)
        {
            return q.Length >= 3 && "unhappy".StartsWith(q, System.StringComparison.Ordinal) && MoodCheck.Unhappy(who);
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
                _rosterMoodTag = PointsTag.Create(dock, "Unhappy");
            }
            if (_unhappy == 0)
            {
                _rosterMoodTag.Hide();
                return;
            }
            _rosterMoodTag.Show(PointsTag.Kind.Mood, _unhappy.ToString());
            var face = roster.Face.rectTransform;
            float corner = face.anchoredPosition.x + face.sizeDelta.x - 1f;
            float y = -face.anchoredPosition.y - PointsTag.Height * 0.5f;
            Theme.Place(_rosterMoodTag.Rect, corner - _rosterMoodTag.Width * 0.5f, y, _rosterMoodTag.Width, PointsTag.Height);
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
            return label + "\n" + _unhappy + " unhappy"
                + (_nearRebellion > 0 ? ", " + _nearRebellion + " close to rebelling" : string.Empty)
                + "\nSort by Mood to see why";
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
            string reading = who.moral.ToString("0") + (Mathf.Abs(today) >= 0.5f ? "  " + today.ToString("+0;-0") + " today" : string.Empty);
            if (dossier)
            {
                lines.Add(Section("MOOD", reading));
            }
            else
            {
                lines.Add(new SheetLine { Kind = LineSpace });
                lines.Add(Head("Mood", reading));
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
                line.Tip = f.Text + (f.Fix != null ? "\nFix: " + f.Fix : string.Empty);
                lines.Add(line);
            }
            string moved = MovedBy(who);
            if (moved != null)
            {
                SheetLine line = Row("Last day", moved);
                line.Tip = "What moved their mood since this time yesterday, by cause";
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
                parts.Add(keys[i] + " " + sums[keys[i]].ToString("+0;-0;0"));
            }
            return string.Join(" · ", parts.ToArray());
        }

        private static string CauseWord(MoodEvent e)
        {
            CommonStates other = e.Partner >= 0 && _mn != null && _mn.npcMN != null ? _mn.npcMN.GetFriend(e.Partner) : null;
            switch (e.Cause)
            {
                case MoodCause.Sleep: return "sleep";
                case MoodCause.BadCompany: return other != null ? other.charaName : "company";
                case MoodCause.Crush: return other != null ? "pining " + other.charaName : "crush";
                case MoodCause.NoBed: return "no bed";
                case MoodCause.Sex: return "sex";
                case MoodCause.Relief: return "relief";
                case MoodCause.Forced: return "forcing";
                case MoodCause.Food: return "food";
                case MoodCause.Execution: return "execution";
                case MoodCause.Torture: return "torture";
                case MoodCause.Training: return "training";
                case MoodCause.Rebel: return "rebel";
                default: return "other";
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
                            Add("Allow houses", "Let them go in and out of houses, and home to sleep",
                                () => { who.sexInfo[7] = 0; MoodChanged(); });
                        }
                        else if (f.Fix != null)
                        {
                            Add("Orders", "Their area, combat order and permissions", () => OpenOrders());
                        }
                        break;
                    case "company":
                        if (f.Other != null)
                        {
                            CommonStates other = f.Other;
                            Add(Short(other.charaName), "Open " + other.charaName, () => PressRosterRow(other));
                        }
                        if (who.sexInfo != null && who.sexInfo.Length > 8 && who.sexInfo[8] == 0)
                        {
                            Add("Love off", "Stop them building love - and grudges - with their neighbours",
                                () => { who.sexInfo[8] = 1; MoodChanged(); });
                        }
                        break;
                    case "crush":
                    case "bed":
                        if (f.Other != null)
                        {
                            CommonStates other = f.Other;
                            Add(Short(other.charaName), "Open " + other.charaName, () => PressRosterRow(other));
                        }
                        Add("Orders", "Their area and permissions: Sex is there", () => OpenOrders());
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

        private static void MoodAction(int sub)
        {
            if (sub >= 0 && sub < MoodActs.Count)
            {
                MoodActs[sub]();
            }
        }

        private static void OpenOrders()
        {
            Aspect[] set = _footSets[GroupCharacter];
            int index = set != null ? System.Array.IndexOf(set, Aspect.Orders) : -1;
            if (index >= 0)
            {
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

        // THE CHECKLIST. The mood in a box beside its last three days, then one printed
        // line per cause: a tick, a cross or a bang in the cause's colour, what it
        // is, what it costs, and its one fix at the end of the line.
        private const float FormMoodNowH = 30f;
        private const float FormMoodCauseH = 15f;
        // the mood's colours on paper: the HUD's amber and green are too pale there
        private static readonly Color PaperWarn = Theme.ParseColor("#B86E0E", new Color(0.72f, 0.43f, 0.05f, 1f));
        private static readonly Color PaperGood = Theme.ParseColor("#4A8A3A", new Color(0.29f, 0.54f, 0.23f, 1f));

        private static void WriteMoodForm(List<SheetLine> lines, CommonStates who)
        {
            List<MoodFinding> found = MoodFindings(who);
            float today = MoodLedger.Net(_mn, who, 1f);
            SheetLine head = Section("MOOD", who.moral.ToString("0")
                + (Mathf.Abs(today) >= 0.5f ? "  ·  " + today.ToString("+0;-0") + " today" : string.Empty));
            head.Tips = new[] { "Their mood, -100 to 100: at -100 they rebel. A night indoors is the main way back up." };
            lines.Add(head);
            lines.Add(MoodNowLine(who));
            MoodActs.Clear();
            foreach (MoodFinding f in found)
            {
                if (f.Short == null)
                {
                    continue;
                }
                var line = new SheetLine
                {
                    Kind = LineMoodCause, Enabled = true, Arg = -1,
                    Label = Capital(f.Short), Value = f.Effect ?? string.Empty,
                    Index = (int)f.Severity, ValueTint = PaperTint(f.Severity),
                    Tip = f.Text + (f.Fix != null ? "\nFix: " + f.Fix : string.Empty),
                };
                if (FixFor(who, f, out string label, out string tip, out System.Action act))
                {
                    line.Arg = MoodActs.Count;
                    MoodActs.Add(act);
                    line.Options = new[] { label };
                    line.Tips = new[] { tip };
                }
                lines.Add(line);
            }
            lines.Add(new SheetLine { Kind = LineSpace });
        }

        private static Color PaperTint(MoodSeverity s)
        {
            return s == MoodSeverity.Bad ? MoralBad : s == MoodSeverity.Warn ? PaperWarn
                : s == MoodSeverity.Good ? PaperGood : Theme.DossierSoft;
        }

        // A cause's one fix: the switch itself where the fix is a switch, Orders
        // where it is a slider or a choice.
        private static bool FixFor(CommonStates who, MoodFinding f, out string label, out string tip, out System.Action act)
        {
            label = tip = null;
            act = null;
            if (f.Severity < MoodSeverity.Warn || who.sexInfo == null || who.sexInfo.Length < 9)
            {
                return false;
            }
            switch (f.Key)
            {
                case "sleep":
                    if (who.sexInfo[7] != 0)
                    {
                        label = "Allow houses";
                        tip = "Let them go in and out of houses, and home to sleep";
                        act = () => { who.sexInfo[7] = 0; MoodChanged(); };
                    }
                    else if (f.Fix != null)
                    {
                        label = "Orders";
                        tip = f.Fix;
                        act = () => OpenOrders();
                    }
                    break;
                case "company":
                    if (who.sexInfo[8] == 0)
                    {
                        label = "Love off";
                        tip = "Stop them building love - and grudges - with their neighbours. Their love for you stops growing too.";
                        act = () => { who.sexInfo[8] = 1; MoodChanged(); };
                    }
                    break;
                case "crush":
                case "bed":
                    label = "Orders";
                    tip = f.Fix;
                    act = () => OpenOrders();
                    break;
            }
            return act != null;
        }

        // their last three days: the ledger's changes as points, merged where they
        // fall within the same hour, the pre-history left blank rather than guessed
        private static SheetLine MoodNowLine(CommonStates who)
        {
            IList<MoodEvent> events = MoodLedger.Events(who);
            float now = MoodLedger.Now(_mn), from = now - 3f;
            var xs = new List<float>();
            var ys = new List<float>();
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Day < from) { continue; }
                float x = Mathf.Clamp01((events[i].Day - from) / 3f);
                if (xs.Count == 0)
                {
                    xs.Add(x);
                    ys.Add(events[i].After - events[i].Delta);
                }
                if (x - xs[xs.Count - 1] < 1f / 72f)
                {
                    ys[ys.Count - 1] = events[i].After;
                }
                else
                {
                    xs.Add(x);
                    ys.Add(events[i].After);
                }
            }
            xs.Add(1f);
            ys.Add(who.moral);
            if (xs.Count == 1)
            {
                xs.Insert(0, 0.96f);
                ys.Insert(0, who.moral);
            }
            string moved = MovedBy(who);
            return new SheetLine
            {
                Kind = LineMoodNow, Enabled = true,
                Value = who.moral.ToString("0"), ValueTint = who.moral < 0f ? MoralBad : PaperGood,
                SeriesX = xs.ToArray(), SeriesY = ys.ToArray(),
                Tip = moved != null ? "Since this time yesterday: " + moved : "No change on record in the last three days",
            };
        }

        private static void DrawMoodNow(SheetRow row, SheetLine line, float w, float h)
        {
            float left = SheetPadX;
            float right = w - SheetPadX;
            const float boxW = 34f;
            float boxH = h - 5f;
            const float by = 2f;
            FormImage(row, "kit_dossier_box", 6, Theme.DossierInk, left, by, boxW, boxH);
            FormText(row, "NOW", CaptionSize, Theme.DossierSoft, TextAlignmentOptions.TopLeft, true,
                left + 2.5f, by + 2f, boxW - 4f, 6f, CaptionSpacing);
            FormText(row, line.Value ?? string.Empty, 11f, line.ValueTint, TextAlignmentOptions.BottomRight, true,
                left + 2f, by, boxW - 4.5f, boxH - 1.5f);
            // the trend: a zero rule, and the mood as steps, scaled to what it spans
            float x0 = left + boxW + 7f;
            float x1 = right - 5f;
            float top = by + 1f;
            float bottom = h - 9f;
            float[] xs = line.SeriesX;
            float[] ys = line.SeriesY;
            float lo = 0f, hi = 0f;
            for (int i = 0; ys != null && i < ys.Length; i++)
            {
                lo = Mathf.Min(lo, ys[i]);
                hi = Mathf.Max(hi, ys[i]);
            }
            lo = Mathf.Max(-100f, lo - 6f);
            hi = Mathf.Min(100f, hi + 6f);
            float span = Mathf.Max(1f, hi - lo);
            FormRule(row, Theme.DossierRule, x0, top + hi / span * (bottom - top), x1 - x0);
            FormText(row, "0", CaptionSize, Theme.DossierSoft, TextAlignmentOptions.MidlineLeft, false,
                x1 + 1f, top + hi / span * (bottom - top) - 3f, 5f, 6f);
            Color ink = line.ValueTint;
            const float t = 0.8f;
            for (int i = 0; xs != null && ys != null && i + 1 < xs.Length && i + 1 < ys.Length; i++)
            {
                float ax = x0 + xs[i] * (x1 - x0);
                float bx = x0 + xs[i + 1] * (x1 - x0);
                float ay = top + (hi - ys[i]) / span * (bottom - top);
                float byy = top + (hi - ys[i + 1]) / span * (bottom - top);
                if (bx - ax > 0.05f)
                {
                    FormImage(row, null, 0, ink, ax, ay - t * 0.5f, bx - ax + t * 0.5f, t);
                }
                if (Mathf.Abs(byy - ay) > 0.05f)
                {
                    FormImage(row, null, 0, ink, bx - t * 0.5f, Mathf.Min(ay, byy) - t * 0.5f, t, Mathf.Abs(byy - ay) + t);
                }
            }
            FormText(row, "3 days ago", CaptionSize, Theme.DossierSoft, TextAlignmentOptions.BottomLeft, false,
                x0, h - 8f, 30f, 7f);
            FormText(row, "now", CaptionSize, Theme.DossierSoft, TextAlignmentOptions.BottomRight, false,
                x1 - 20f, h - 8f, 20f, 7f);
            if (!string.IsNullOrEmpty(line.Tip))
            {
                row.TipZones.Add(new Rect(left, 0f, right - left, h));
                row.TipTexts.Add(line.Tip);
            }
        }

        private static int DrawMoodCause(SheetRow row, SheetLine line, float w, float h)
        {
            float left = SheetPadX;
            float right = w - SheetPadX;
            const float bs = 7.5f;
            float by = (h - bs) * 0.5f;
            Color tint = line.ValueTint;
            FormImage(row, "kit_dossier_check", 0, tint, left, by, bs, bs);
            var severity = (MoodSeverity)line.Index;
            string mark = severity == MoodSeverity.Good ? "kit_check" : severity == MoodSeverity.Bad ? "kit_cross"
                : severity == MoodSeverity.Warn ? "kit_bang" : null;
            if (mark != null)
            {
                FormImage(row, mark, 0, tint, left + 1f, by + 1f, bs - 2f, bs - 2f);
            }
            float textX = left + bs + 4f;
            float end = right;
            int planks = 0;
            if (line.Options != null && line.Options.Length > 0 && !string.IsNullOrEmpty(line.Options[0]))
            {
                float bw = MeasureForm(row, line.Options[0], FormTextSize) + 10f;
                float bx = right - bw;
                DrawButton(row, 0, line.Options[0], false, bx, (h - FormButtonH) * 0.5f, bw);
                if (line.Tips != null && line.Tips.Length > 0 && !string.IsNullOrEmpty(line.Tips[0]))
                {
                    row.TipZones.Add(new Rect(bx, 0f, bw, h));
                    row.TipTexts.Add(line.Tips[0]);
                }
                planks = 1;
                end = bx - 5f;
            }
            float valueW = MeasureForm(row, line.Value ?? string.Empty, FormTextSize) + 1f;
            FormText(row, line.Value ?? string.Empty, FormTextSize, tint, TextAlignmentOptions.MidlineRight, false,
                end - valueW, 0f, valueW, h);
            FormText(row, line.Label ?? string.Empty, FormTextSize, Theme.DossierInk, TextAlignmentOptions.MidlineLeft,
                false, textX, 0f, Mathf.Max(8f, end - valueW - 4f - textX), h, 0f, fit: true);
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

