using System.Collections.Generic;
using IslandUI.UI;
using TMPro;
using UnityEngine;

namespace IslandUI
{
    // InventoryWindow — BONDS, under WHO THEY MEET (docs/social-links.md section
    // 10, step 4): the bonds the game acts on, each a person.
    //   desires   the game's pick of partner (NPCManager.GetSexableLover: the
    //             highest love of 50 or more, areas overlapping, sex allowed),
    //             and whether they answer - a no is what pining is
    //   grudge    everyone either way at -10 or below: this person's love for
    //             them, or theirs for this person
    // THE SAME ROW AS A MET ONE: the person first, the bond's word where the
    // ticks are, the love each way in the two number columns, a note - so the
    // page reads as one table. Every row presses open to why, as a met row does.
    internal static partial class InventoryWindow
    {
        private const float FormBondH = 13f;   // a met row's height: one table
        private const int BondWants = 0, BondCantStand = 1, BondCantStandThem = 2, BondFamily = 3, BondRelation = 4;
        private const int BondNone = -1;   // a row that names no one: "No one", "Sex off"

        private static CommonStates _bondOpenWho;
        private static int _bondOpenKey = -1;

        private static int BondKey(int slot, int partner)
        {
            return slot * 1000000 + partner;
        }

        private static bool IsBondOpen(CommonStates who, int key)
        {
            return ReferenceEquals(_bondOpenWho, who) && _bondOpenKey == key;
        }

        private static void ToggleBondOpen(CommonStates who, int key)
        {
            if (key < 0)
            {
                return;
            }
            if (IsBondOpen(who, key))
            {
                _bondOpenKey = -1;
                return;
            }
            _bondOpenWho = who;
            _bondOpenKey = key;
        }

        private static void WriteBonds(List<SheetLine> lines, CommonStates who)
        {
            NPCManager npcs = _mn != null ? _mn.npcMN : null;
            if (npcs == null)
            {
                return;
            }
            string me = string.IsNullOrEmpty(who.charaName) ? "?" : who.charaName;
            // AN EMPTY BOND TAKES NO LINE: no one desired, no grudge either way, and
            // the section is left out with them
            var bonds = new List<SheetLine>();

            // WANTS: the game's pick, and whether they answer
            bool sexOff = who.sexInfo != null && who.sexInfo.Length > 9 && who.sexInfo[9] != 0;
            CommonStates pick = !sexOff && who.lovers != null && _mn.sexMN != null ? MoodCheck.SexableLover(_mn, who) : null;
            if (pick != null)
            {
                string answer = PickAnswer(npcs, who, pick, out bool pining, out string why);
                SheetLine wants = BondLine(Shared.Loc.Get("bonds.word.desires", "desires"), pick, BondKey(BondWants, pick.friendID), answer, pining ? 0 : 3, who);
                if (!wants.On) { wants.Tip = Shared.Loc.Get("bonds.desires.help", "The game's pick of partner. Click for why"); }
                bonds.Add(wants);
                if (IsBondOpen(who, BondKey(BondWants, pick.friendID)))
                {
                    bonds.Add(WantsWhy(who, pick, why, pining));
                }
            }

            // GRUDGES, either way, one row a person, worst first
            var grudges = new Dictionary<CommonStates, float>();
            for (int i = 0; who.lovers != null && i < who.lovers.Count; i++)
            {
                CommonStates.Lovers l = who.lovers[i];
                CommonStates other = l != null ? Friend(npcs, l.friendID) : null;
                if (other != null && l.love <= -10f)
                {
                    grudges[other] = l.love;
                }
            }
            foreach (CommonStates other in People(npcs))
            {
                if (other != null && !ReferenceEquals(other, who) && LoveBetween(npcs, other, who, out float love) && love <= -10f)
                {
                    grudges[other] = grudges.TryGetValue(other, out float mine) ? Mathf.Min(mine, love) : love;
                }
            }
            var worst = new List<KeyValuePair<CommonStates, float>>(grudges);
            worst.Sort((x, y) => x.Value.CompareTo(y.Value));
            foreach (KeyValuePair<CommonStates, float> g in worst)
            {
                CommonStates other = g.Key;
                bool iCant = LoveBetween(npcs, who, other, out float mine) && mine <= -10f;
                bool theyCant = LoveBetween(npcs, other, who, out float theirs) && theirs <= -10f;
                int key = BondKey(BondCantStand, other.friendID);
                bonds.Add(BondLine(Shared.Loc.Get("bonds.word.grudge", "grudge"), other, key,
                    iCant && theyCant ? Shared.Loc.Get("social.cant_stand_each", "can't stand each other") : iCant ? Shared.Loc.Get("social.cant_stand", "can't stand") : Shared.Loc.Get("social.cant_stand_who", "can't stand {0}", me), 0, who));
                if (IsBondOpen(who, key))
                {
                    bonds.Add(GrudgeWhy(who, other));
                }
            }
            if (bonds.Count > 0)
            {
                lines.Add(Section(Shared.Loc.Get("bonds.section", "BONDS")));
                lines.AddRange(bonds);
            }
        }

        // FAMILY: the father and the mother the game recorded at birth (status 8
        // and 9, RandomCharacter: the father is the mother's pregnancy's), and
        // every one of yours with this person as a parent. Story characters and
        // the island's own have none, so the section is left out. A parent who is
        // no one of yours - gone, or a kind of native - is their name alone. Man
        // and Yona have no parents on record: theirs is their children.
        private static void WriteFamily(List<SheetLine> lines, CommonStates who)
        {
            NPCManager npcs = _mn != null ? _mn.npcMN : null;
            if (npcs == null)
            {
                return;
            }
            var family = new List<SheetLine>();
            void Parent(string caption, int slot)
            {
                if (who.npcID <= 1 || who.status == null || slot >= who.status.Length || who.status[slot] == -1)
                {
                    return;
                }
                int id = who.status[slot];
                CommonStates p = Friend(npcs, id);
                if (p != null && p.npcID <= 1)
                {
                    // you (or the other one you play): no one to target, so no link
                    family.Add(BondLine(caption, null, BondNone, string.Empty, 2, who, p, Shared.Loc.Get("bonds.family.you", "{0} (you)", p.charaName)));
                }
                else if (p != null)
                {
                    family.Add(FamilyLine(caption, p, who));
                }
                else
                {
                    string name = FriendNameOf(npcs, id);
                    if (!string.IsNullOrEmpty(name))
                    {
                        family.Add(BondLine(caption, null, BondNone, Shared.Loc.Get("bonds.family.not_yours", "no one of yours"), 2, who, null, name));
                    }
                }
            }
            Parent(Shared.Loc.Get("bonds.family.father", "father"), 8);
            Parent(Shared.Loc.Get("bonds.family.mother", "mother"), 9);
            foreach (CommonStates c in ChildrenOf(npcs, who))
            {
                family.Add(FamilyLine(Shared.Loc.Get("bonds.family.child", "child"), c, who));
            }
            if (family.Count == 0)
            {
                return;
            }
            lines.Add(Section(Shared.Loc.Get("bonds.family.section", "FAMILY"), family.Count.ToString()));
            lines.AddRange(family);
        }

        // harness: everyone of yours with a parent on record, and who
        internal static string FamilyDump()
        {
            NPCManager npcs = _mn != null ? _mn.npcMN : null;
            var sb = new System.Text.StringBuilder();
            foreach (CommonStates c in People(npcs))
            {
                if (c != null && c.status != null && c.status.Length > 9 && (c.status[8] != -1 || c.status[9] != -1))
                {
                    sb.Append(c.charaName).Append(": ").Append(FriendNameOf(npcs, c.status[8])).Append(" + ")
                      .Append(FriendNameOf(npcs, c.status[9])).Append("; ");
                }
            }
            return sb.Length > 0 ? sb.ToString() : "no one";
        }

        // ALL RELATIONS, a section of the Social page like the others, its first
        // RelFirst rows and a line to show all of them and fold them back (it was
        // a window of one-way love bars behind a button at the foot, and most
        // loves sit in the hundreds, so the bars said little).
        // The game reads a feeling by a few lines - 50, a lover it will pick; -10,
        // can't stand - so every relation is grouped by where the two feelings
        // stand against them, both ways: lovers both ways, one way, neither, can't
        // stand. Each is the table's row: the person, what the last day's ticks
        // moved this person's feeling by, the love each way, the relation in
        // words; it opens to why, as a met row does. Everyone with a feeling
        // either way is here, not only those this person holds one for.
        private static CommonStates _allRelationsWho;   // whose All relations shows every row
        private const int RelFirst = 10;
        private const int RelFoldArg = 1000000;   // the fold line's Arg: past any met page's first

        private static bool AllRelationsOpen(CommonStates who)
        {
            return who != null && ReferenceEquals(_allRelationsWho, who);
        }

        private static void ToggleAllRelations(CommonStates who)
        {
            _allRelationsWho = AllRelationsOpen(who) ? null : who;
        }

        // harness: All relations opened on whoever is named, the one you play too
        internal static string RelationsOpen(string name)
        {
            // yours first, as the sheets hold them; the one you play is not among them
            var all = _mn != null && _mn.npcMN != null ? People(_mn.npcMN) : new List<CommonStates>();
            all.AddRange(Object.FindObjectsOfType<CommonStates>());
            foreach (CommonStates c in all)
            {
                if (c != null && string.Equals(c.charaName, name, System.StringComparison.OrdinalIgnoreCase))
                {
                    _allRelationsWho = c;
                    _forceRefresh = true;
                    _nextRefresh = 0f;
                    Refresh();
                    return "open: " + c.charaName;
                }
            }
            return "no one named '" + name + "'";
        }

        // harness: whoever is named's edges - friendID, love, and whom it resolves to
        internal static string LoversDump(string name)
        {
            NPCManager npcs = _mn != null ? _mn.npcMN : null;
            var sb = new System.Text.StringBuilder();
            foreach (CommonStates c in npcs != null ? People(npcs) : new List<CommonStates>())
            {
                if (c == null || c.lovers == null || !string.Equals(c.charaName, name, System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                foreach (CommonStates.Lovers l in c.lovers)
                {
                    if (l == null) { continue; }
                    CommonStates f = Friend(npcs, l.friendID);
                    sb.Append(l.friendID).Append('=').Append(l.love.ToString("0")).Append(':')
                      .Append(f != null ? f.charaName + "/npc" + f.npcID + "/dead" + f.dead : "none '" + FriendNameOf(npcs, l.friendID) + "'")
                      .Append("; ");
                }
                return sb.ToString();
            }
            return "no one named '" + name + "'";
        }

        // harness: whoever is named's mood ledger - day, change, after, cause - and now
        internal static string MoodDump(string name)
        {
            NPCManager npcs = _mn != null ? _mn.npcMN : null;
            foreach (CommonStates c in npcs != null ? People(npcs) : new List<CommonStates>())
            {
                if (c == null || !string.Equals(c.charaName, name, System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                var sb = new System.Text.StringBuilder();
                sb.Append("now ").Append(MoodLedger.Now(_mn).ToString("0.00")).Append(" moral ").Append(c.moral.ToString("0")).Append(": ");
                foreach (MoodEvent e in MoodLedger.Events(c))
                {
                    sb.Append(e.Day.ToString("0.00")).Append(' ').Append(e.Delta.ToString("+0;-0")).Append('=')
                      .Append(e.After.ToString("0")).Append(' ').Append(e.Cause).Append("; ");
                }
                return sb.ToString();
            }
            return "no one named '" + name + "'";
        }

        // the one you play now: no one to target, so their name alone. The other
        // one is targeted as the roster's Partner row is
        private static bool Played(CommonStates c)
        {
            CommonStates[] players = _mn != null && _mn.gameMN != null ? _mn.gameMN.playerCommons : null;
            int at = GameManager.selectPlayer;
            return players != null && at >= 0 && at < players.Length && ReferenceEquals(players[at], c);
        }

        private const int RelBoth = 0, RelOneWay = 1, RelNeither = 2, RelCantStand = 3;

        private static int RelationGroup(float mine, float back, bool backKnown)
        {
            if (mine <= -10f || (backKnown && back <= -10f)) { return RelCantStand; }
            bool iLove = mine >= 50f, theyLove = backKnown && back >= 50f;
            return iLove && theyLove ? RelBoth : iLove || theyLove ? RelOneWay : RelNeither;
        }

        // the one you play has no feelings of their own, so their page lists
        // only those toward them: RELATIONS, as a villager's lanes are called
        private static string AllRelationsTitle(CommonStates who) => who.npcID <= 1
            ? Shared.Loc.Get("company.section", "RELATIONS") : Shared.Loc.Get("bonds.all.section", "ALL RELATIONS");

        private static void WriteAllRelations(List<SheetLine> lines, CommonStates who)
        {
            NPCManager npcs = _mn != null ? _mn.npcMN : null;
            if (npcs == null)
            {
                return;
            }
            if (who.npcID <= 1)
            {
                WriteFeelingsForYou(lines, who, npcs);
                return;
            }
            string me = string.IsNullOrEmpty(who.charaName) ? "?" : who.charaName;
            // everyone either way: their edges, then the edges toward them
            var mine = new Dictionary<int, float>();
            var named = new Dictionary<int, CommonStates>();
            var order = new List<int>();
            for (int i = 0; who.lovers != null && i < who.lovers.Count; i++)
            {
                CommonStates.Lovers l = who.lovers[i];
                if (l == null || mine.ContainsKey(l.friendID)) { continue; }
                // a feeling the game keeps for someone no longer anywhere - no
                // person, no name - leads nowhere, so it is left out
                CommonStates f = Friend(npcs, l.friendID);
                if (f == null && string.IsNullOrEmpty(FriendNameOf(npcs, l.friendID))) { continue; }
                mine[l.friendID] = l.love;
                named[l.friendID] = f;
                order.Add(l.friendID);
            }
            foreach (CommonStates other in People(npcs))
            {
                if (other != null && !ReferenceEquals(other, who) && !named.ContainsKey(other.friendID)
                    && LoveBetween(npcs, other, who, out float _))
                {
                    named[other.friendID] = other;
                    order.Add(other.friendID);
                }
            }
            if (order.Count == 0)
            {
                lines.Add(Section(AllRelationsTitle(who)));
                lines.Add(NoneLine());
                return;
            }
            // what the three days moved, as the lanes and the chart over them count
            var today = new Dictionary<int, LoveLedger.Meeting>();
            foreach (LoveLedger.Meeting m in LoveLedger.MetIn(who, LoveLedger.LastTicks(MoodLedger.Now(_mn), LaneTicks)))
            {
                today[m.Partner] = m;
            }
            // the strongest feeling either way first
            var backs = new Dictionary<int, float>();
            var rank = new Dictionary<int, float>();
            foreach (int id in order)
            {
                CommonStates other = named[id];
                bool known = mine.TryGetValue(id, out float love);
                float back = 0f;
                if (other != null && LoveBetween(npcs, other, who, out back)) { backs[id] = back; }
                rank[id] = Mathf.Max(known ? love : float.MinValue, backs.ContainsKey(id) ? back : float.MinValue);
            }
            order.Sort((x, y) => rank[y].CompareTo(rank[x]));
            var groups = new List<List<SheetLine>>[4];
            foreach (int id in order)
            {
                bool known = mine.TryGetValue(id, out float love);
                bool backKnown = backs.TryGetValue(id, out float back);
                int g = RelationGroup(known ? love : 0f, back, backKnown);
                if (groups[g] == null) { groups[g] = new List<List<SheetLine>>(); }
                groups[g].Add(RelationLines(who, named[id], id, known, love, back, backKnown, g,
                    today.TryGetValue(id, out LoveLedger.Meeting met) ? met : null, me, npcs));
            }
            // the columns' heads, as WHO THEY MEET's
            lines.Add(new SheetLine
            {
                Kind = LineMetHead, Enabled = true, Label = me.ToUpperInvariant(), Label2 = Shared.Loc.Get("met.head.they", "THEY"), Value = Shared.Loc.Get("mood.chart.past", "3 DAYS"),
                Tip = Shared.Loc.Get("bonds.head.days", "3 days") + "  " + Shared.Loc.Get("bonds.head.days.help", "what the love ticks of the last three days moved {0}'s feeling by", me) + "\n"
                    + me + "  " + Shared.Loc.Get("bonds.head.mine.help", "{0}'s love for them", me) + "\n"
                    + Shared.Loc.Get("bonds.head.they", "They") + "  " + Shared.Loc.Get("bonds.head.they.help", "their love for {0}", me),
            });
            WriteRelationGroups(lines, who, groups, order.Count);
        }

        // the section: its groups one after another - no caption over each, the
        // note at a row's end says which it is - the first RelFirst relations
        // unless all are shown, then the line to show all of them or fold them
        // back to the first
        private static void WriteRelationGroups(List<SheetLine> lines, CommonStates who, List<List<SheetLine>>[] groups,
            int total)
        {
            int headAt = lines.Count - 1;   // the columns' head, just written: the section goes over it
            lines.Insert(headAt, Section(AllRelationsTitle(who), total.ToString()));
            bool all = AllRelationsOpen(who);
            int shown = 0;
            for (int g = 0; g < groups.Length && (all || shown < RelFirst); g++)
            {
                if (groups[g] == null) { continue; }
                foreach (List<SheetLine> relation in groups[g])
                {
                    if (!all && shown >= RelFirst) { break; }
                    lines.AddRange(relation);
                    shown++;
                }
            }
            int rest = total - shown;
            if (rest > 0 || (all && total > RelFirst))
            {
                lines.Add(new SheetLine
                {
                    Kind = LineMetFold, Enabled = true, Arg = RelFoldArg,
                    Options = new[] { rest > 0 ? Shared.Loc.Get("social.show_more", "Show {0} more", rest) : null, all ? Shared.Loc.Get("social.show_less", "Show less") : null },
                });
            }
        }

        // YOUR OWN PAGE IS ONE WAY: the one you play holds no feelings - their
        // edges stay at 0 - so read both ways nearly everyone was "one way", a
        // row of "loves you, not back". Here every relation is what someone feels
        // for you, grouped by it, with its one number and the feeling in words. The love ticks leave you
        // out (what moves a feeling for you is what you do: a dance, a gift, sex),
        // so there is no day's move and no why to open: a row presses through to
        // the person, as a family row does.
        private static void WriteFeelingsForYou(List<SheetLine> lines, CommonStates who, NPCManager npcs)
        {
            var rows = new List<KeyValuePair<CommonStates, float>>();
            foreach (CommonStates other in People(npcs))
            {
                if (other != null && !ReferenceEquals(other, who) && LoveBetween(npcs, other, who, out float love))
                {
                    rows.Add(new KeyValuePair<CommonStates, float>(other, love));
                }
            }
            if (rows.Count == 0)
            {
                lines.Add(Section(AllRelationsTitle(who)));
                lines.Add(NoneLine());
                return;
            }
            rows.Sort((x, y) => y.Value.CompareTo(x.Value));
            string[] words =
            {
                Shared.Loc.Get("bonds.you.loves", "loves you"), Shared.Loc.Get("bonds.you.fond", "fond of you"),
                Shared.Loc.Get("bonds.you.cold", "cold to you"), Shared.Loc.Get("bonds.you.cant_stand", "can't stand you"),
            };
            int[] ranks = { MetGood, MetSoft, MetSoft, MetBad };
            var groups = new List<List<SheetLine>>[4];
            foreach (KeyValuePair<CommonStates, float> r in rows)
            {
                CommonStates other = r.Key;
                float love = r.Value;
                int g = love >= 50f ? 0 : love >= 0f ? 1 : love > -10f ? 2 : 3;
                string name = string.IsNullOrEmpty(other.charaName) ? "?" : other.charaName;
                bool played = Played(other);
                SheetLine line = played
                    ? BondLine(string.Empty, null, BondNone, string.Empty, MetSoft, who, other, name)
                    : FamilyLine(string.Empty, other, who);
                line.Value = null;   // yours is always 0: theirs alone, in the column for theirs
                line.Value2 = love.ToString("0");
                line.Values = new[] { words[g] };
                line.Index = ranks[g];
                if (groups[g] == null) { groups[g] = new List<List<SheetLine>>(); }
                groups[g].Add(new List<SheetLine> { line });
            }
            lines.Add(new SheetLine
            {
                Kind = LineMetHead, Enabled = true, Label2 = Shared.Loc.Get("bonds.you.head", "FOR YOU"),
                Tip = Shared.Loc.Get("bonds.you.head.help", "Their love for you"),
            });
            WriteRelationGroups(lines, who, groups, rows.Count);
        }

        // a relation's row, and its why under it when it is open
        private static List<SheetLine> RelationLines(CommonStates who, CommonStates other, int id, bool known, float love,
            float back, bool backKnown, int group, LoveLedger.Meeting met, string me, NPCManager npcs)
        {
            var result = new List<SheetLine>();
            string name = other != null ? other.charaName : FriendNameOf(npcs, id);
            if (string.IsNullOrEmpty(name)) { name = "?"; }
            string note;
            int noteRank = MetSoft;
            switch (group)
            {
                case RelBoth: note = Shared.Loc.Get("social.note.lovers", "lovers"); noteRank = MetGood; break;
                case RelOneWay: note = known && love >= 50f ? Shared.Loc.Get("social.note.loves_not_back", "loves {0}, not back", name) : Shared.Loc.Get("social.note.loved_not_back", "{0} loves {1}, not back", name, me); break;
                case RelCantStand:
                    bool iCant = known && love <= -10f, theyCant = backKnown && back <= -10f;
                    note = iCant && theyCant ? Shared.Loc.Get("social.cant_stand_each", "can't stand each other") : iCant ? Shared.Loc.Get("social.cant_stand", "can't stand") : Shared.Loc.Get("social.cant_stand_who", "can't stand {0}", me);
                    noteRank = MetBad;
                    break;
                default: note = Shared.Loc.Get("social.note.acquainted", "acquainted"); break;
            }
            // the last day's move of this person's feeling: what the ticks did
            float moved = met != null ? met.MineSum : 0f;
            string word = met != null ? moved.ToString("+0;-0;0") : "—";   // under 3 DAYS
            bool you = other != null && Played(other);
            int key = other != null && !you ? BondKey(BondRelation, other.friendID) : BondNone;
            SheetLine line = other == null || you
                ? BondLine(word, null, BondNone, note, noteRank, who, you ? other : null, name)
                : BondLine(word, other, key, note, noteRank, who);
            if (line.Value == null && known) { line.Value = love.ToString("0"); }
            line.ValueTint = met == null || moved == 0f ? Theme.DossierSoft : moved > 0f ? PaperGood : MoralBad;
            line.ValueTinted = true;
            if (line.Arg >= 0 && line.Arg / 1000000 == BondRelation && !line.On)
            {
                line.Tip = Shared.Loc.Get("bonds.moved.help", "The last day's ticks moved {0}'s feeling for {1} by this. Click for why", me, name);
            }
            result.Add(line);
            if (key >= 0 && line.On)
            {
                result.Add(RelationWhy(who, other, known ? love : 0f, met != null));
            }
            return result;
        }

        // why a relation stands where it does: the met row's why when they met at
        // the last six ticks; else that they did not, and what that does to it
        private static SheetLine RelationWhy(CommonStates who, CommonStates other, float love, bool met)
        {
            if (met)
            {
                return GrudgeWhy(who, other);
            }
            bool sex = HadSex(who, other) || HadSex(other, who);
            return new SheetLine
            {
                Kind = LineMetWhy, Enabled = true, Arg = -1, Index = MetSoft,
                Values = new[] { Shared.Loc.Get("bonds.why.not_met", "They did not meet at the last six ticks: their areas do not overlap.") },
                Label = love <= 0f ? null
                    : sex ? Shared.Loc.Get("bonds.why.bond_kept", "They have had sex, so the bond stays even apart")
                    : Shared.Loc.Get("bonds.why.bond_fades", "Apart, a feeling fades 5 a tick and is dropped at 0"),
            };
        }

        // a family row presses through to the person: there is no why to open
        private static SheetLine FamilyLine(string caption, CommonStates other, CommonStates who)
        {
            SheetLine line = BondLine(caption, other, BondKey(BondFamily, other.friendID), string.Empty, 2, who);
            line.On = false;
            line.Tip = line.Link != null && AwayWord(other) == null ? Shared.Loc.Get("sheet.click_to_target", "Click to target {0}", other.charaName) : line.Tip;
            return line;
        }

        // a bond's row: the compact person, the bond's word, the love each way
        // (when it is known), a note in the rank's ink (0 bad, 2 soft, 3 good).
        // A person who is no one to press - you, or a parent gone - is `plain`,
        // their name alone; `lovedBy` is whose love the numbers are then
        private static SheetLine BondLine(string word, CommonStates other, int key, string note, int rank, CommonStates who,
            CommonStates lovedBy = null, string plain = null)
        {
            // someone not in the world wears a mark after their name, its tip saying
            // where they are; a press goes to what holds them (PressLink), as a
            // family row's goes to the person
            string away = AwayTip(other);
            if (away != null)
            {
                key = BondKey(BondFamily, other.friendID);
            }
            bool open = other != null && IsBondOpen(who, key);
            CommonStates them = other ?? lovedBy;
            NPCManager npcs = _mn != null ? _mn.npcMN : null;
            float hers = 0f, theirs = 0f;
            bool hersKnown = them != null && LoveBetween(npcs, who, them, out hers);
            bool theirsKnown = them != null && LoveBetween(npcs, them, who, out theirs);
            return new SheetLine
            {
                Kind = LineBond, Enabled = true, Arg = other != null ? key : -1, On = open, Link = other,
                Label = word ?? string.Empty,
                Label2 = other != null ? (string.IsNullOrEmpty(other.charaName) ? "?" : other.charaName) : plain,
                Away = away,
                Style = other != null && other.person != null && other.person.Length > 0 ? other.person[0] : -1,
                LabelTint = Theme.DossierInk,
                Icons = other != null ? new[] { MoodFace(other, out Color mood) } : null,
                Frac = other != null ? MoodFill(other) : 0f,
                Tint = other != null ? MoralTint(other) : Color.clear,
                Value = hersKnown ? hers.ToString("0") : null,
                Value2 = theirsKnown ? theirs.ToString("0") : null,
                Values = new[] { note ?? string.Empty }, Index = rank,
                Tip = other == null ? null : away ?? (open ? Shared.Loc.Get("social.click_close", "Click to close") : Shared.Loc.Get("social.click_why", "Click for why")),
            };
        }

        // what the pick answers when asked (NPCManager.ShallWeSex), in a few words
        private static string PickAnswer(NPCManager npcs, CommonStates who, CommonStates pick, out bool pining, out string why)
        {
            pining = false;
            why = null;
            SexManager.SexInviteState answer = npcs.ShallWeSex(who, pick);
            if (answer == SexManager.SexInviteState.Yes)
            {
                return Shared.Loc.Get("bonds.pick.yes", "answers yes");
            }
            if (answer == SexManager.SexInviteState.Cant)
            {
                why = Shared.Loc.Get("bonds.pick.cant.why", "{0} cannot right now (busy, following you, or Sex off)", pick.charaName);
                return Shared.Loc.Get("bonds.pick.cant", "can't now");
            }
            why = who.houseID != pick.houseID ? Shared.Loc.Get("bonds.pick.why.house", "{0} lives in another house", pick.charaName)
                : !npcs.LoveLoveCheck(who, pick) ? Shared.Loc.Get("bonds.pick.why.no_love", "{0} doesn't love them back", pick.charaName)
                : pick.libido < 20f ? Shared.Loc.Get("bonds.pick.why.no_mood", "{0} isn't in the mood", pick.charaName)
                : Shared.Loc.Get("bonds.pick.why.no", "{0} says no", pick.charaName);
            if (npcs.FindSexualGratification(who) != null)
            {
                return Shared.Loc.Get("bonds.pick.toilet", "says no: a toilet instead");
            }
            pining = true;
            return Shared.Loc.Get("bonds.pick.pining", "says no: pining");
        }

        private static SheetLine WantsWhy(CommonStates who, CommonStates pick, string why, bool pining)
        {
            var say = new List<string>();
            float love = 0f;
            LoveBetween(_mn.npcMN, who, pick, out love);
            say.Add(Shared.Loc.Get("bonds.pick.highest", "{0} is their highest love ({1}) among those in their area.", pick.charaName, love.ToString("0")));
            say.Add(Shared.Loc.Get("bonds.pick.rule", "With libido at 20 or more they ask; a yes needs one house and love back."));
            say.Add(why != null ? Shared.Loc.Get("bonds.pick.no", "No: {0}.", why) : Shared.Loc.Get("bonds.pick.yes.why", "Yes: they share a house and {0} loves them back.", pick.charaName));
            var line = new SheetLine { Kind = LineMetWhy, Enabled = true, Arg = -1, Values = say.ToArray() };
            if (pining)
            {
                say.Add(Shared.Loc.Get("bonds.pick.pining.cost", "At libido 90 a no costs them 5 mood, several times a day."));
                line.Values = say.ToArray();
                line.Index = MetBad;
                line.Label = Shared.Loc.Get("bonds.pick.ends_it", "Ends it: both in one house, an occupied toilet within 10 m, or Sex off");
            }
            else
            {
                line.Index = MetSoft;
            }
            return line;
        }

        // a grudge's why: their MET TODAY row's, when they met; else that they did not
        private static SheetLine GrudgeWhy(CommonStates who, CommonStates other)
        {
            MetRead read = MetOf(who);
            string me = string.IsNullOrEmpty(who.charaName) ? "?" : who.charaName;
            string name = string.IsNullOrEmpty(other.charaName) ? "?" : other.charaName;
            foreach (LoveLedger.Meeting m in read.Met)
            {
                if (m.Partner == other.friendID)
                {
                    MetNote(_mn.npcMN, who, other, m, me, out int rank);
                    return MetWhyLine(who, other, m, read.Ticks, me, name, rank);
                }
            }
            return new SheetLine
            {
                Kind = LineMetWhy, Enabled = true, Arg = -1, Index = MetSoft,
                Values = new[] { Shared.Loc.Get("bonds.grudge.none", "Nothing on record between them at the last six ticks.") },
                Label = HadSex(who, other) || HadSex(other, who)
                    ? Shared.Loc.Get("bonds.grudge.kept", "They have had sex, so the grudge stays even apart")
                    : Shared.Loc.Get("bonds.grudge.dropped", "Apart, a grudge is dropped at the next tick"),
            };
        }

        private static int DrawBond(SheetRow row, SheetLine line, float w, float h)
        {
            MetColumns(w, out float left, out float right, out float strip, out float mineX, out float theirX, out float noteX);
            bool pressable = line.Arg >= 0;
            if (pressable && (Hot(row, 2) || line.On))
            {
                FormImage(row, null, 0, MetOpenWash, left - 1.5f, 0f, right - left + 3f, h);
            }
            if (line.Link != null)
            {
                DrawCompactPerson(row, line, line.Label2, left, h, strip - 15f - left, 0, 1);
            }
            else
            {
                // no one to press: their name alone, where a name stands, or a dash
                FormNoHit(row, 0);
                FormNoHit(row, 1);
                FormText(row, line.Label2 ?? "—", FormTextSize, line.Label2 != null ? Theme.DossierInk : Theme.DossierSoft,
                    TextAlignmentOptions.MidlineLeft, false, left + 9f, 0f, strip - 4f - left - 9f, h, 0f, fit: true);
            }
            FormText(row, line.Label ?? string.Empty, FormTextSize * 0.85f, line.ValueTinted ? line.ValueTint : Theme.DossierLabel,
                TextAlignmentOptions.MidlineLeft, false, strip, 0f, mineX - strip, h, 0f, fit: true);
            bool numbers = line.Value != null || line.Value2 != null;
            if (numbers)
            {
                BondLove(row, line.Value, mineX, h);
                BondLove(row, line.Value2, theirX, h);
                row.TipZones.Add(new Rect(mineX, 0f, noteX - mineX, h));
                row.TipTexts.Add(line.Label2 != null
                    ? Shared.Loc.Get("bonds.love_each_way", "Love each way: theirs for {0}, and back", line.Label2)
                    : Shared.Loc.Get("bonds.love_each_way.them", "Love each way: theirs for them, and back"));
            }
            string note = line.Values != null && line.Values.Length > 0 ? line.Values[0] : string.Empty;
            float nx = numbers ? noteX : mineX;
            Color ink = line.Index == MetBad ? MoralBad : line.Index == MetGood ? PaperGood : Theme.DossierSoft;
            FormText(row, note, FormTextSize * 0.85f, ink, TextAlignmentOptions.MidlineLeft, false,
                nx, 0f, right - nx, h, 0f, fit: true);
            if (!line.On)
            {
                FormRule(row, Theme.DossierRule, left, h - 0.5f, right - left);
            }
            if (!string.IsNullOrEmpty(line.Tip))
            {
                row.TipZones.Add(new Rect(left, 0f, right - left, h));
                row.TipTexts.Add(line.Tip);
            }
            if (!pressable)
            {
                return 0;
            }
            FormHit(row, 2, left, 0f, right - left, h);
            return 3;
        }

        // a love in a number column, where a met row's sum stands; smaller, since
        // loves run to the hundreds. A love is a level, not a change, so it has
        // no plus (a met row's sums and the last day's move are changes, and do)
        private static void BondLove(SheetRow row, string text, float x, float h)
        {
            text = text ?? string.Empty;
            float.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v);
            Color ink = v > 0f ? PaperGood : v < 0f ? MoralBad : Theme.DossierSoft;
            FormText(row, text, FormTextSize * 0.85f, ink, TextAlignmentOptions.MidlineRight, false, x - 2f, 0f, 16f, h, 0f, fit: true);
        }
    }
}
