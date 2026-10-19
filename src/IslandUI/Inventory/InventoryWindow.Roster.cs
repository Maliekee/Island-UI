using System.Collections.Generic;
using IslandUI.UI;
using TMPro;
using UnityEngine;

namespace IslandUI
{
    // InventoryWindow — the ROSTER, the third tab of the left slot.
    //
    // §6.22, D53: the roster is the In-reach list with the reach limit removed.
    // Everyone employed, wherever they are, on the same parchment, in the same
    // frame, with the same filter well and the same scroll — because it answers
    // the left rail's one question, WHAT IS THERE, asked of people rather than of
    // containers. Vanilla's friend list paged by NPC kind, sorted nothing, and
    // said neither who is following nor where anyone is; this sorts (D53), says
    // both, and is informative only (D56 — the job glyph shows state, changing it
    // is the Orders tab).
    //
    // D55: the row IS the column header at row size. The gauge is written once
    // (StripLine, in the Strip file) and drawn here at a smaller pitch, so a
    // heart on a row and a heart on a plate can never come to mean different
    // things. What a row adds is what a header does not need: the kind stripe,
    // where they are, why their moral is red, and the marks on them.
    internal static partial class InventoryWindow
    {
        // ---------- the sorts ----------

        private const int SortVicinity = 0;
        private const int SortKind = 1;
        private const int SortState = 2;
        private static readonly string[] SortNames = { "Vicinity", "Kind", "State", "Mood" };
        // Vicinity first: "who can I touch" is the question the slot's other
        // occupant answers, so the roster opens as its continuation (D53).
        private static int _rosterSort = SortVicinity;

        // ---------- the row's measurements ----------

        // THE HEIGHT IS BUDGETED TO THE LIST FIRST: about 30% of the window
        // went to static controls, with too much white space and type set too
        // large. Measured
        // before the pass: of the slot's 220 units, 37 went to the frame and the
        // title/filter row, 30 to the roster's own control row and its gap, and
        // 153 to the list - which held 5.3 rows of 49 people. The three numbers
        // below and the one-line bar take that to ~7.1 rows in the same window.
        private const float RosterRowH = 22f;    // a name line over the gauge, as the header is
        private const float RosterHeadH = 11f;   // a section heading
        private const float RosterBarH = 13f;    // the control row over the list, in ONE line
        // The gap BETWEEN rows, which is the roster's and not the pool's. A list
        // of people is read down a column and wants the rows near each other; a
        // stack of container plates wants air between them, because each is its
        // own object. GroupMemberGap is 4, and 4 between 22-unit rows was
        // most of the white space in the list.
        private const float RosterGap = 1.5f;
        private const float RosterStripeW = 2.5f;
        private const float RosterPadX = 7f;
        private const float RosterNameH = 10f;   // the name line; the gauge takes the rest
        private const float RosterMark = 11f;    // the star
        // The ring's 13 units go to the GAUGE, not to these words: the words are
        // short ("in reach", "bad company") and were already floating in a column
        // wider than they need, which is half of why the row's right end read
        // oddly. The gauge is the part that degrades when the column narrows.
        private const float RosterMidW = 66f;    // job and where, and the cause under them
        private const float RosterJob = 9f;
        private const float RosterFollow = 8f;
        private const float RosterSmall = 7f;    // the where/cause type, under the name's size

        // THE SCROLL BAR'S OWN COLUMN: a row's hover overlapped the scroll bar.
        // The kit bar is placed against
        // the FRAME's right inner edge, which was tuned for the crafting window,
        // where the bands stop a column short of the frame and leave it room. A
        // roster row runs the full width of the list, so the bar was drawn over
        // the last few units of every row - and the row's hover lit under it.
        //
        // ALWAYS RESERVED, never only while the list overflows: a gutter that
        // came and went with the row count would resize every row the moment a
        // filter matched one fewer person. Same rule as the baby's slot on the
        // gauge run.
        private const float RosterScrollGutter = 10f;   // KitBarW 6.5 plus air

        // The gap between members of the reach pool. Two different lists share
        // that pool - the roster's rows and the vicinity's container plates - and
        // they want different air, so the pool asks rather than reading the
        // setting. Both the bind (which measures the content) and the layout
        // (which stacks it) must ask the same question, or the scroll and the
        // drawing disagree by a gap per row.
        private static float ReachGap()
        {
            return RosterView ? RosterGap : GroupMemberGap;
        }

        // THE CARD IS THE SAME ROW, DRAWN LARGER: the list's icons were too small
        // in the target window. A list row is sized by how many of them must
        // stack in the slot; the card is ONE row with a whole column's width and no
        // neighbours, and inheriting the list's pitch spent none of that. So the card
        // scales the run - and the bar and its ruler with it, or the gauge would come
        // apart at the seams.
        //
        // 1.9 WAS SIZED FOR A COLUMN THAT DOES NOT EXIST. The target
        // frame is about 190 units wide - near enough the roster's own - so nearly
        // doubling the run demanded more room than the row had, and the arithmetic
        // gave it back in the only place it could: the icons collapsed to 3.5 units
        // against the list's 5.5, and the bar shrank with them. The card was SMALLER
        // than the row it enlarges. A quarter over the list is what the width can
        // actually pay for, and it is the difference the eye reads as "the same row,
        // bigger" rather than as a different component.
        private const float CardRowH = 30f;
        private const float CardNameH = 12f;
        private const float CardScale = 1.25f;

        private static readonly Color RosterRule = new Color(0.45f, 0.36f, 0.26f, 0.30f);
        private static readonly Color RosterHot = new Color(1f, 0.97f, 0.90f, 0.30f);
        private static readonly Color RosterRingInk = new Color(0.45f, 0.36f, 0.26f, 0.55f);

        // ---------- what the list holds ----------

        private struct RosterEntry
        {
            public CommonStates Who;
            public float Dist;      // to the player, for the vicinity sort
            public bool InReach;
            public bool Following;
            public bool AtHome;
            public int Kind;        // UIManager.GetNPCColorID: vanilla's own bucket
            public int Job;         // NPCMove.WorkType, 0 = none
        }

        private static readonly List<RosterEntry> Roster = new List<RosterEntry>();
        private static int _rosterFollowers;
        private static int _rosterCapacity;

        // THE ONE YOU ARE NOT PLAYING: vanilla's friend list never shows the
        // Yona and Man main characters; here the one not being played shows up.
        // A switch makes them an ordinary NPC (NPCManager.NPCToPlayer: NPCMove on,
        // npcType Friend; employ is Friend all along) but never a friendList entry.
        // That list is saved, and jobs, housing and raids walk it, so they are
        // added where we draw - the roster and the vicinity's bags - and never to
        // the game's list. Present only when a switch to them would be allowed
        // (GameManager.ChangePlayer: there, and dead == 0). They take jobs like
        // anyone, so they sort with everyone; capacity is vanilla's and ignores
        // them, as vanilla does.
        internal static CommonStates Partner(ManagersScript mn)
        {
            CommonStates[] players = mn != null && mn.gameMN != null ? mn.gameMN.playerCommons : null;
            if (players == null || players.Length < 2)
            {
                return null;
            }
            CommonStates who = players[GameManager.selectPlayer == 0 ? 1 : 0];
            if (who == null || who.dead != 0)
            {
                return null;
            }
            List<CommonStates> friends = mn.npcMN != null ? mn.npcMN.friendList : null;
            return friends != null && friends.Contains(who) ? null : who;
        }

        // Your people as this window lists them: the game's friend records, and
        // the partner (Partner).
        private static List<CommonStates> People(NPCManager npcs)
        {
            var people = new List<CommonStates>(npcs.friendList);
            CommonStates partner = Partner(_mn);
            if (partner != null)
            {
                people.Add(partner);
            }
            return people;
        }

        // Everyone the game keeps a friend record for, minus the ones vanilla
        // leaves out of its own list: the unemployed, the carried (dead == 5) and
        // whoever is not in the world right now. Outlaws and the dead are the
        // Outlaw panel's, and stay there until §6.22's "Gone" section exists.
        private static void CollectRoster()
        {
            Roster.Clear();
            _rosterFollowers = 0;
            _rosterCapacity = 0;
            NPCManager npcs = _mn != null ? _mn.npcMN : null;
            if (npcs == null || npcs.friendList == null)
            {
                return;
            }
            _rosterFollowers = npcs.follower != null ? npcs.follower.Count : npcs.followerCount;
            _rosterCapacity = npcs.baseFollowerCapa
                + (_mn.skillMN != null && _mn.skillMN.skills != null && _mn.skillMN.skills.Length > 10
                    ? _mn.skillMN.skills[10] : 0);
            Vector3 here = _mn.gameMN != null && _mn.gameMN.pMove != null
                ? _mn.gameMN.pMove.transform.position : Vector3.zero;
            List<CommonStates> people = People(npcs);
            for (int i = 0; i < people.Count; i++)
            {
                CommonStates who = people[i];
                if (who == null || !who.Employed() || who.dead == 5
                    || who.gameObject == null || !who.gameObject.activeSelf)
                {
                    continue;
                }
                NPCMove move = who.nMove;
                var entry = new RosterEntry
                {
                    Who = who,
                    Dist = Vector3.Distance(here, who.transform.position),
                    Following = move != null && move.npcType == NPCMove.NPCType.Follow,
                    Kind = _mn.uiMN != null ? _mn.uiMN.GetNPCColorID(who.npcID) : 0,
                    Job = move != null ? (int)move.workType : 0,
                };
                entry.InReach = ReachRule.Live(who, here, ReachRule.Near);   // D145, the unit's own
                // AT HOME is inside the territory they were given, not standing on
                // its exact centre - NPCMove.OnTerritory is the second thing and
                // is almost never true of someone who is living there.
                entry.AtHome = move != null
                    && Vector3.Distance(who.transform.position, move.territory)
                        <= Mathf.Max(1f, move.territoryRad);
                Roster.Add(entry);
            }
        }

        // The filter well filters NAMES here, as it filters item keys next door
        // (D47a). One field, one row, two questions - which is the point of the
        // slot sharing it.
        // THE FILTER MATCHES THE KIND AS WELL AS THE NAME: typing "native"
        // filters to the native types. A name finds ONE
        // person, which is the narrow half of what a filter over forty-nine people
        // is for; the kind finds a group, and the group is usually what you are
        // after - every native, every beast.
        //
        // It is the same string the Kind SORT already sections by and Profile
        // already prints (KindName -> StatsData.GetLocalizedCharaName), so a
        // query matches what the window says out loud rather than an internal
        // name, and it is localised for free.
        //
        // Cached per npcID: the filter runs over every entry on every rebind, and
        // GetStatsData is a lookup into the game's own table. The names never
        // change for an npcID, so once is enough.
        private static readonly Dictionary<int, string> KindQuery = new Dictionary<int, string>();

        private static string KindNameLower(CommonStates who)
        {
            string kind;
            if (KindQuery.TryGetValue(who.npcID, out kind))
            {
                return kind;
            }
            kind = KindName(_mn != null ? _mn.npcMN : null, who) ?? string.Empty;
            kind = kind.ToLowerInvariant();
            KindQuery[who.npcID] = kind;
            return kind;
        }

        private static bool RosterMatches(CommonStates who)
        {
            string query = _filter != null ? _filter.Query : null;
            if (string.IsNullOrEmpty(query))
            {
                return true;
            }
            string q = query.Trim().ToLowerInvariant();
            if (q.Length == 0)
            {
                return true;
            }
            string name = who.charaName;
            if (!string.IsNullOrEmpty(name) && name.ToLowerInvariant().Contains(q))
            {
                return true;
            }
            return KindNameLower(who).Contains(q) || MoodQuery(q, who);
        }

        // ---------- the sections ----------

        // Name is the tiebreak in EVERY sort (D53), so the same two people never
        // swap places for no reason the eye can see.
        private static int ByName(RosterEntry a, RosterEntry b)
        {
            return string.Compare(a.Who.charaName ?? string.Empty,
                b.Who.charaName ?? string.Empty, System.StringComparison.OrdinalIgnoreCase);
        }

        // What a person's STATE is, for the state sort: following, then working by
        // job, then idle. The job number orders the working section so one job's
        // people stand together.
        private static int StateRank(RosterEntry e)
        {
            if (e.Following) { return 0; }
            return e.Job != 0 ? 1 + e.Job : 100;
        }

        private static string StateSection(RosterEntry e)
        {
            if (e.Following) { return "Following"; }
            return e.Job != 0 ? JobName(e.Job) : "Idle";
        }

        private static readonly string[] JobNames =
            { "Idle", "Food", "Wood", "Stone", "Harvest", "Planter", "Breed", "Dig", "Training", "Fishing", "Repair" };

        private static string JobName(int job)
        {
            return job >= 0 && job < JobNames.Length ? JobNames[job] : "Working";
        }

        // VANILLA'S BUCKETS, BORROWING A NAME. GetNPCColorID sorts everyone into
        // ten colour buckets and the game gives them no words at all - its own
        // pages are numbered buttons wearing a count - so the heading takes the
        // localised type name of the first person in the bucket, which is a real
        // name from the game rather than an invented one. It is not the whole
        // truth (bucket 0 holds nine npcIDs), and it stands until the look decides
        // whether these sections want names of their own.
        private static readonly Dictionary<int, string> KindSections = new Dictionary<int, string>();

        // ...except where the borrowed name is a PERSON'S. Bucket 5 is the game's
        // named, one-of-a-kind characters - npcIDs 2-8, 71, 110-121, 162-163, the
        // ones with a story of their own (Cassie is 113, the reindeer 117) - whose
        // "type" is their own name, so the section was called after whoever came
        // first - the story characters were grouped as a kind called Cassie.
        private const int StoryBucket = 5;

        private static string KindSection(RosterEntry e)
        {
            string name;
            if (KindSections.TryGetValue(e.Kind, out name))
            {
                return name;
            }
            if (e.Kind == StoryBucket)
            {
                KindSections[e.Kind] = "Story characters";
                return "Story characters";
            }
            name = KindName(_mn != null ? _mn.npcMN : null, e.Who);
            if (string.IsNullOrEmpty(name))
            {
                name = "Kind " + e.Kind.ToString("0");
            }
            KindSections[e.Kind] = name;
            return name;
        }

        private static string VicinitySection(RosterEntry e)
        {
            return e.InReach ? "In reach" : e.AtHome ? "At home" : "Away";
        }

        // THE SECTION HAS TO LEAD THE SORT, OR THE HEADINGS REPEAT (found the
        // moment the taller list showed seven rows instead of five: "In reach /
        // At home / Away / At home").
        //
        // The vicinity sort ordered by raw distance alone, on the reasoning that
        // the reach band would fall out of the order rather than be imposed on it.
        // That is true of In reach, which IS a distance - and false of the other
        // two, which are not: someone away can stand nearer than someone at home,
        // so the three buckets interleave and the bind emits a heading every time
        // the label changes. The band still falls out of the order; the two
        // sections that are not distances are ranked ahead of it.
        private static int VicinityRank(RosterEntry e)
        {
            return e.InReach ? 0 : e.AtHome ? 1 : 2;
        }

        private static string SectionOf(RosterEntry e)
        {
            switch (_rosterSort)
            {
                case SortKind: return KindSection(e);
                case SortState: return StateSection(e);
                case SortMood: return MoodSection(e);
                default: return VicinitySection(e);
            }
        }

        private static void SortRoster()
        {
            Roster.Sort((a, b) =>
            {
                int c;
                switch (_rosterSort)
                {
                    case SortKind:
                        c = a.Kind.CompareTo(b.Kind);
                        break;
                    case SortState:
                        c = StateRank(a).CompareTo(StateRank(b));
                        break;
                    case SortMood:
                        // worst section first, then lowest mood within it
                        c = MoodRank(a).CompareTo(MoodRank(b));
                        if (c == 0)
                        {
                            c = a.Who.moral.CompareTo(b.Who.moral);
                        }
                        break;
                    default:
                        // section first (VicinityRank), then nearest first within
                        // it - so the reach band still falls out of the distance
                        // order and the two sections that are not distances do not
                        // interleave with it
                        c = VicinityRank(a).CompareTo(VicinityRank(b));
                        if (c == 0)
                        {
                            c = a.Dist.CompareTo(b.Dist);
                        }
                        break;
                }
                return c != 0 ? c : ByName(a, b);
            });
        }

        // ---------- the line ----------

        // A roster row's line: the gauge every size of this component shares
        // (D55), plus what only a row says.
        private static SheetLine RosterLine(CommonStates who)
        {
            SheetLine line = StripLine(who);
            line.Kind = LineRoster;
            line.Label = who.charaName ?? string.Empty;
            line.Value = "Lv " + who.level.ToString("0");
            Color kind = NPCColor(who) ?? (_mn != null && _mn.uiMN != null
                ? _mn.uiMN.GetNPCColor(who.npcID) : Theme.Ink);
            line.LabelTint = kind;
            line.LabelTinted = true;
            line.Tint = kind;         // the stripe: no gauge on a row is drawn from Tint
            line.Tinted = true;
            line.TintIcon = false;    // the job glyph keeps its own colours
            NPCMove move = who.nMove;
            int job = move != null ? (int)move.workType : 0;
            line.Icon = job != 0 && _mn != null && _mn.uiMN != null
                ? Emotion(_mn.uiMN.GetWorkImage(move.workType)) : null;
            line.Following = move != null && move.npcType == NPCMove.NPCType.Follow;
            line.Starred = who.employ == CommonStates.Employ.Favorite;
            line.Where = WhereWord(who, move);
            line.Cause = DiscontentWord(who, out line.CauseWarn);
            // the target the dock and the indicator show, sheet open or not
            line.Picked = ReferenceEquals(Focus() as CommonStates, who);
            return line;
        }

        // THE CARD (D66): the roster row at frame width, at the head of
        // the target column. Candidate C4 of card-1.png won on how the shapes DEGRADE
        // - every one of them looks acceptable holding a person, and only this one's
        // corpse fill reads as deliberate rather than half-empty - and on the kind
        // stripe, the one device that names a subject's type without words.
        //
        // The plate above carries the name, so the row drops it: D55's one writer at
        // three sizes, differing by a single field rather than none.
        private static SheetLine CardLine(CommonStates who)
        {
            SheetLine line = RosterLine(who);
            line.Card = true;
            line.Label = "Lv " + who.level.ToString("0");
            line.LabelTint = Theme.Ink;   // the stripe already says the kind, so the level
            line.LabelTinted = true;      // reads plain and strong instead of in the kind colour
            // The kind is the character TYPE's localised name, and for a unique
            // character that IS their own name - "Merry" under a plate saying Merry,
            // which is the duplication D66 dropped the name to avoid. So a kind that
            // only repeats the plate is not said twice.
            string kind = KindName(_mn != null ? _mn.npcMN : null, who);
            line.Value = string.Equals(kind, who.charaName, System.StringComparison.OrdinalIgnoreCase)
                ? string.Empty : kind;
            line.Starred = false;       // the star is a chip on the frame's rim
            return line;
        }

        private static string WhereWord(CommonStates who, NPCMove move)
        {
            Vector3 here = _mn != null && _mn.gameMN != null && _mn.gameMN.pMove != null
                ? _mn.gameMN.pMove.transform.position : Vector3.zero;
            if (ReachRule.Live(who, here, ReachRule.Near))
            {
                return "in reach";
            }
            if (move != null && Vector3.Distance(who.transform.position, move.territory)
                <= Mathf.Max(1f, move.territoryRad))
            {
                return "at home";
            }
            return "away";
        }

        // WHY the moral is red, or will be: DiscontentWord (InventoryWindow.Mood.cs),
        // off the mood ledger and checks - the game's own lastMoralCause forgets
        // after a night's sleep.

        // ---------- the row's parts, and where they go ----------

        private static void EnsureRosterParts(SheetRow row)
        {
            row.Hover = Theme.KitImage(row.Go.transform, "Hover", "kit_field", 12, RosterHot, stretch: true);
            row.Hover.transform.SetAsFirstSibling();
            row.Hover.raycastTarget = true;   // the whole row is the target (D53: a row opens the column)
            row.Hover.gameObject.SetActive(false);
            row.Stripe = Theme.KitImage(row.Go.transform, "Stripe", "kit_dot", 0, Color.white, stretch: true);
            row.Stripe.gameObject.SetActive(false);
            row.Line = Theme.KitImage(row.Go.transform, "Rule", "kit_hairline", 4, RosterRule, stretch: true);
            row.Line.gameObject.SetActive(false);
            row.Follow = Theme.KitImage(row.Go.transform, "Follow", "kit_pointer", 0, Theme.Ink);
            row.Follow.preserveAspect = true;
            row.Follow.gameObject.SetActive(false);
            row.Mark = Theme.KitImage(row.Go.transform, "Star", "kit_star", 0, Color.white);
            row.Mark.preserveAspect = true;
            row.Mark.gameObject.SetActive(false);
            // AN EMPTY RING (D56): the row keeps room for the shortcut play will
            // ask for, and starts with none. The ring is what says the room is
            // there on purpose rather than by accident.
            row.Ring = Theme.KitImage(row.Go.transform, "Ring", "kit_ring", 0, RosterRingInk);
            row.Ring.preserveAspect = true;
            row.Ring.gameObject.SetActive(false);
            row.Sub = Theme.MakeKitText(row.Go.transform, "Where", RosterSmall, Theme.InkDim,
                TextAlignmentOptions.MidlineLeft);
            row.Sub.enableWordWrapping = false;
            row.Sub.overflowMode = TextOverflowModes.Overflow;
            row.Sub.gameObject.SetActive(false);
            row.Note = Theme.MakeKitText(row.Go.transform, "Cause", RosterSmall, MoralBad,
                TextAlignmentOptions.MidlineLeft);
            row.Note.enableWordWrapping = false;
            row.Note.overflowMode = TextOverflowModes.Overflow;
            row.Note.gameObject.SetActive(false);
        }

        private static void ShowRosterParts(SheetRow row, bool on)
        {
            if (row.Stripe == null)
            {
                return;
            }
            SetShown(row.Hover, false);   // hover alone decides this one
            SetShown(row.Stripe, on);
            SetShown(row.Line, on);
            SetShown(row.Ring, false);
            if (!on)
            {
                SetShown(row.Follow, false);
                SetShown(row.Mark, false);
                SetShown(row.Sub, false);
                SetShown(row.Note, false);
            }
        }

        private static void SetShown(Component c, bool on)
        {
            if (c != null && c.gameObject.activeSelf != on)
            {
                c.gameObject.SetActive(on);
            }
        }

        // The row, laid out from its right edge inward: the marks first, since
        // their room is fixed, then the middle band, then the name and the gauge
        // take what is left. A narrow column loses width from the gauge, which is
        // the part that degrades gracefully.
        private static void LayoutRosterRow(SheetRow row, SheetLine line, float width, float h)
        {
            // the card is the same row at frame width, and it has no marks at its end:
            // the star and the eye are chips on the frame's rim, and D56's ring is
            // a list shortcut, meaningless when you are already looking at them. So the
            // whole right end goes to the words instead.
            bool card = line.Card;
            float nameH = card ? CardNameH : RosterNameH;
            float right = width - 4f;
            float ringX = right;                       // unused: the ring is not drawn
            float starX = card ? right : right - RosterMark;
            float midRight = card ? right - 2f : starX - 5f;
            float midX = Mathf.Max(RosterPadX + 60f, midRight - RosterMidW);
            float leftRight = midX - 6f;

            Theme.Place(row.Hover, 0f, 0f, width, h);
            Theme.Place(row.Stripe, 0f, 0f, RosterStripeW, h);
            Theme.Place(row.Line, 0f, h - 0.6f, width, 0.6f);

            // the identity line. A roster row reads NAME ....... Lv n; the card, whose
            // plate already said the name, reads Lv n  Kind instead - so the level takes
            // the label's place at the left and the kind runs on after it.
            // on the cork a snapshot is pinned at the strip's left, and the name and
            // the gauge start after it (InventoryWindow.Shed.cs)
            float padX = !card && CorkRows ? RosterPadX + CorkPhotoRoom : RosterPadX;
            float nameW = leftRight - padX;
            float levelW = 34f;
            float followW = line.Following ? RosterFollow + 2f : 0f;
            if (card)
            {
                Theme.Place(row.Label, RosterPadX, 1.5f, levelW, nameH);
                if (line.Following)
                {
                    Theme.Place(row.Follow, leftRight - RosterFollow - 2f,
                        (nameH - RosterFollow) * 0.5f + 2f, RosterFollow, RosterFollow);
                }
                Theme.Place(row.Value, RosterPadX + levelW + 6f, 1.5f,
                    Mathf.Max(20f, nameW - levelW - followW - 6f), nameH);
            }
            else
            {
                Theme.Place(row.Label, padX, 1.5f, Mathf.Max(20f, nameW - levelW - followW), RosterNameH);
                if (line.Following)
                {
                    Theme.Place(row.Follow, leftRight - levelW - RosterFollow - 2f,
                        (RosterNameH - RosterFollow) * 0.5f + 2f, RosterFollow, RosterFollow);
                }
                Theme.Place(row.Value, leftRight - levelW, 1.5f, levelW, RosterNameH);
            }

            // the gauge, under it: the header's strip at the row's pitch
            LayoutRosterStrip(row, line, padX, leftRight, nameH + 1f, h - nameH - 2f);

            // job and where, and the cause under them
            float job = card ? RosterJob * 1.5f : RosterJob;
            Theme.Place(row.Icon, midX, card ? 4f : 3f, job, job);
            float wordX = midX + (line.Icon != null ? job + 3f : 0f);
            Theme.Place(row.Sub, wordX, 2.5f, Mathf.Max(10f, midRight - wordX), nameH);
            Theme.Place(row.Note, midX, h - nameH - 2.5f, Mathf.Max(10f, midRight - midX), nameH);

            // the marks
            Theme.Place(row.Mark, starX, (h - RosterMark) * 0.5f, RosterMark, RosterMark);
            Theme.Place(row.Ring, ringX, (h - RosterMark) * 0.5f, RosterMark, RosterMark);
        }

        // The strip's geometry at ROW size. The header's own LayoutStrip is left
        // alone: it owns a plate, a subtitle line and a fixed inset, none of which
        // a row has. What is shared is everything that DECIDES anything - the
        // icon set, their scales, the love bands, the colours - because that lives
        // in the line and in PaintStrip, which both sizes call.
        private static void LayoutRosterStrip(SheetRow row, SheetLine line, float x0, float rightEdge,
            float top, float h)
        {
            row.Bare = true;        // no oak plate under a row
            row.IconsOnly = false;  // ...but the bars are here, as on the header
            float k = line.Card ? CardScale : 1f;
            float boxH = StripBoxH * k;
            float stamH = StripStamH * k;
            float r = StripRimW * k;
            // THE BAR ABSORBS THE SLACK ON A CARD. A fraction is right for the list,
            // where every row is the same width and the run fills what is left. At
            // frame width the run stops growing - its pitch is capped, and rightly,
            // since a giant heart says nothing more than a small one - so a fraction
            // left a hole between the last icon and the words beside it. The icons
            // keep their ceiling and the BAR takes the rest, which is the one part
            // of the gauge that reads better long.
            float span = rightEdge - x0;
            float barW;
            if (line.Card)
            {
                float runMax = StripIcons * 14f * k + 2f * 4f * k;
                barW = Mathf.Round(Mathf.Clamp(span - runMax - 7f, span * 0.36f, span * 0.62f));
            }
            else
            {
                barW = Mathf.Round(span * 0.36f);
            }
            float barY = top + (h - (boxH + r + stamH)) * 0.5f;
            float stamY = barY + boxH + r;
            Theme.Place(row.BarRim, x0 - r, barY - r, barW + 2f * r, boxH + r + stamH + 2f * r);
            Theme.Place(row.BarTrack, x0, barY, barW, boxH);
            Theme.Place(row.BarFill, x0, barY, 0f, boxH);
            PlaceTicks(row, line, x0, barW, barY, boxH, k);
            Theme.Place(row.Stam, x0, stamY, barW, stamH);
            Theme.Place(row.StamFill, x0, stamY, 0f, stamH);
            row.BarH = boxH;
            row.StamH = stamH;

            bool baby = line.Fracs != null && line.Fracs.Length > 7 && line.Fracs[7] >= 0f;
            // THE BABY'S SLOT IS ALWAYS RESERVED: kept as a placeholder and simply not
            // drawn, rather than shrinking the other icons when it appears. The pitch divides what is left
            // among the icons, so counting only the icons PRESENT made a pregnancy shrink
            // every other icon on the row - the run changed size for a reason that has
            // nothing to do with the icons that changed. Reserve the full set and the two
            // hairlines whatever this line carries; the baby is merely not drawn.
            const int icons = StripIcons;
            const int seps = 2;
            float iconsX = x0 + barW + 7f;
            float avail = rightEdge - iconsX;
            // ONE FORMULA, SCALED. The card used to state its own
            // arithmetic, because the first attempt at scaling the list's had
            // quietly changed the list too. Two formulas then drifted: the card's
            // floor was 6 where the list's was 7, so at the target frame's width
            // the BIGGER component drew SMALLER icons than the row it enlarges.
            //
            // The list's numbers are the ones that were judged right, so they are the
            // ones written here, with k applied where a scale belongs - the
            // ceiling, the separators, the trim. At k = 1 this IS the list's line,
            // character for character, so the two sizes cannot part again.
            //
            // THE FLOOR IS NOT SCALED: it is what the room can pay for, not what
            // the size would like. Scaling it let a card demand a run wider than
            // its space and draw the last icons over the words beside them.
            float pitch = Mathf.Clamp((avail - seps * 4f * k) / Mathf.Max(1, icons), 7f, 14f * k);
            // ...but never taller than the band it is centred in. The width
            // alone decided this while the row was 25 units and the band 12;
            // at 22 and 10 a wide column would have grown the icons up
            // into the name line above them.
            float s = Mathf.Min(pitch - 1.5f * k, h);
            float y = top + (h - s) * 0.5f;
            float x = iconsX;
            int sep = 0;
            for (int i = 0; i < StripIcons; i++)
            {
                if (i == StripIcons - 1 && !baby)
                {
                    break;
                }
                float si = s * StripIconScale[i];
                if (i == 2 || i == 5)
                {
                    Theme.Place(row.Seps[sep], x - 0.1f, top + (h - (s + 1.5f * k)) * 0.5f,
                        StripSepW * k, s + 1.5f * k);
                    x += 4f * k;
                    sep++;
                }
                float ix = x + (s - si) * 0.5f;
                float iy = y + (s - si) * 0.5f;
                float rim = 0.6f * k;
                Theme.Place(row.IconRims[i], ix - rim, iy - rim, si + 2f * rim, si + 2f * rim);
                Theme.Place(row.IconBacks[i], ix, iy, si, si);
                Theme.Place(row.IconFills[i], ix, iy, si, si);
                if (i == LoveIcon)
                {
                    Theme.Place(row.LoveMid, ix, iy, si, si);
                    Theme.Place(row.LoveTop, ix, iy, si, si);
                }
                x += pitch;
            }
            // the level-up arrow takes the next slot, after its own hairline: the
            // baby's when there is none (PointsTag)
            if (row.LevelIcon != null)
            {
                Theme.Place(row.LevelSep, x - 0.1f, top + (h - (s + 1.5f * k)) * 0.5f, StripSepW * k, s + 1.5f * k);
                float si = s * LevelIconScale;
                Theme.Place(row.LevelIcon, x + 4f * k + (s - si) * 0.5f, y + (s - si) * 0.5f, si, si);
                row.LevelSlot = true;
            }
            row.TrackX = x0;
            row.TrackW = barW;
            row.DrewBaby = baby;
            ShowStrip(row, true);
        }

        // Everything on a row that is not the gauge. PaintStrip has already run.
        private static void PaintRosterRow(SheetRow row, SheetLine line)
        {
            if (row.Stripe == null)
            {
                return;
            }
            SetShown(row.Stripe, true);
            SetShown(row.Line, true);
            // D56'S EMPTY RING IS GONE. It was "the room for the
            // shortcut play will ask for", drawn so the room would read as
            // deliberate - and in practice exactly that end of the row was the
            // part that read oddly. A ring that does nothing, on every
            // row, at the far right, past a gap, is what it was seeing. The room
            // goes back to the words (RosterMidW), and the shortcut can ask for its
            // own space when it exists.
            SetShown(row.Ring, false);
            row.Stripe.color = line.Tint;
            SetShown(row.Follow, line.Following);
            SetShown(row.Mark, line.Starred);
            bool where = !string.IsNullOrEmpty(line.Where);
            SetShown(row.Sub, where);
            if (where && !string.Equals(row.Sub.text, line.Where))
            {
                row.Sub.text = line.Where;
            }
            bool cause = !string.IsNullOrEmpty(line.Cause);
            SetShown(row.Note, cause);
            if (cause && !string.Equals(row.Note.text, line.Cause))
            {
                row.Note.text = line.Cause;
            }
            if (cause)
            {
                Color causeTint = line.CauseWarn ? MoodWarn : MoralBad;
                if (row.Note.color != causeTint) { row.Note.color = causeTint; }
            }
            // the row answers the pointer: the ground lights, and a press darkens
            // it, exactly as the kit's other pressable surfaces do
            bool hot = ReferenceEquals(_sheetHot, row);
            // THE ONE YOU HAVE TARGETED KEEPS THE LIGHT: the hover's own ground,
            // held while they are the target. Not on the card, which IS
            // the target.
            bool picked = line.Picked && !line.Card;
            SetShown(row.Hover, hot || picked);
            if (hot || picked)
            {
                bool down = hot && ReferenceEquals(_sheetPressed, row) && Input.GetMouseButton(0);
                row.Hover.color = down
                    ? new Color(RosterHot.r * 0.8f, RosterHot.g * 0.8f, RosterHot.b * 0.8f, RosterHot.a * 1.6f)
                    : RosterHot;
            }
        }

        // ---------- the control row ----------

        // D53: "one scrollable list under a heading control". The three sorts and
        // Camp-wide actions use solid slate; the active sort uses a lighter
        // stone fill. Both follow the roster palette.
        private static SheetLine RosterBarLine()
        {
            return new SheetLine
            {
                Kind = LineRosterBar,
                Enabled = true,
                // The count left the bar for the header's count slot, which
                // was showing the roster's total - a number the sections already
                // add up to. The line it was on went with it.
                Label = string.Empty,
                Value = string.Empty,
                Arg = _rosterSort,
                Options = new[] { SortNames[0], SortNames[1], SortNames[2], SortNames[3], "All names", "Send home" },
            };
        }

        private const int RosterBarSorts = 4;
        private const float RosterVerbW = 16f;
        private static readonly string[] RosterVerbGlyphs = { "names", "home" };
        private static readonly string[] RosterVerbTips =
        {
            "All names: show or hide every villager's name over their head",
            "Send home: everyone walks back to their own area",
        };
        private const float RosterBarLineH = 13f;
        private const float RosterBarGap = 3f;

        // ONE LINE. It was two, because the first attempt put
        // five controls AND a count across the slot's 225 units and got 34 each,
        // with the words running over one another. The count is what
        // did not fit: moved to the header's own count slot - which was showing a
        // total the sections already add up to - the five controls have 218 units
        // between them, and the second line was pure cost to the list.
        //
        // The split across the one line is still by KIND, which is what the two
        // lines were really saying: the sorts change what this window SHOWS and
        // take the left, over the list they order; the verbs act on the CAMP and
        // sit right, where a plank that does something belongs.
        private static void LayoutRosterBar(SheetRow row, SheetLine line, float width, float h)
        {
            int n = Mathf.Min(line.Options.Length, row.Planks.Length);
            float right = width - 4f;
            float y = (h - SheetPlankH) * 0.5f;
            // THE VERBS ARE BUTTONS: 'all names' and 'send home' are
            // a glyph on a chip each, named by their tooltips, so the four sorts
            // keep their words and get the room back
            int verbs = Mathf.Max(0, n - RosterBarSorts);
            float verbW = RosterVerbW;
            row.TipZones.Clear();
            row.TipTexts.Clear();
            float verbsLeft = right - verbs * verbW - (verbs - 1) * RosterBarGap;
            float vx = verbsLeft;
            for (int i = RosterBarSorts; i < n; i++)
            {
                Theme.Place(row.Planks[i], vx, y, verbW, SheetPlankH);
                Theme.Place(row.PlankTexts[i], 0f, 0f, verbW, SheetPlankH);
                Theme.Place(row.PlankIcons[i], 2.5f, 2f, verbW - 5f, SheetPlankH - 4f);
                int verb = i - RosterBarSorts;
                if (verb < RosterVerbTips.Length)
                {
                    row.TipZones.Add(new Rect(vx, y, verbW, SheetPlankH));
                    row.TipTexts.Add(RosterVerbTips[verb]);
                }
                vx += verbW + RosterBarGap;
            }
            // the sorts, filling what is left of the line
            float sortRight = verbs > 0 ? verbsLeft - 6f : right;
            float sortW = (sortRight - RosterPadX - (RosterBarSorts - 1) * RosterBarGap) / RosterBarSorts;
            float sx = RosterPadX;
            for (int i = 0; i < RosterBarSorts && i < n; i++)
            {
                Theme.Place(row.Planks[i], sx, y, sortW, SheetPlankH);
                Theme.Place(row.PlankTexts[i], 0f, 0f, sortW, SheetPlankH);
                sx += sortW + RosterBarGap;
            }
            Theme.Place(row.Label, RosterPadX, 0f, 0f, h);
            for (int i = n; i < row.Planks.Length; i++)
            {
                SetShown(row.Planks[i], false);
            }
        }

        private static void PaintRosterBar(SheetRow row, SheetLine line)
        {
            if (row.Planks == null || line.Options == null)
            {
                return;
            }
            bool anyDown = Input.GetMouseButton(0);
            int n = Mathf.Min(line.Options.Length, row.Planks.Length);
            for (int i = 0; i < n; i++)
            {
                SetShown(row.Planks[i], true);
                bool sort = i < RosterBarSorts;
                string word = sort ? line.Options[i] : string.Empty;
                if (row.PlankTexts[i].text != word)
                {
                    row.PlankTexts[i].text = word;
                }
                UnityEngine.UI.Image glyph = row.PlankIcons[i];
                int verb = i - RosterBarSorts;
                if (!sort && verb < RosterVerbGlyphs.Length)
                {
                    Sprite mark = HudGlyphs.Get(RosterVerbGlyphs[verb]);
                    if (glyph.sprite != mark) { glyph.sprite = mark; }
                    if (!glyph.enabled) { glyph.enabled = true; }
                }
                else if (glyph.enabled)
                {
                    glyph.enabled = false;
                }
                bool on = sort && i == line.Arg;
                bool hot = ReferenceEquals(_sheetHot, row) && _sheetHotSub == i;
                bool down = hot && ReferenceEquals(_sheetPressed, row) && _sheetPressedSub == i && anyDown;
                // Slate actions and a lighter selected sort belong to the roster's
                // stone frame. Use a neutral sprite, not a tint over brown wood.
                var face = row.Planks[i];
                face.sprite = Theme.RoundedFill;
                face.type = UnityEngine.UI.Image.Type.Sliced;
                face.pixelsPerUnitMultiplier = Theme.KitPpu;
                face.enabled = !sort || on || hot;
                face.color = sort
                    ? (down ? Theme.RosterControlDown
                        : on ? Theme.RosterSelected : Color.Lerp(Theme.RosterSelected, Color.white, 0.12f))
                    : (down ? Theme.RosterControlDown : hot ? Theme.RosterControlHot : Theme.RosterControl);
                row.PlankTexts[i].color = sort && !down
                    ? (on ? Theme.RosterInk : Theme.RosterSoftInk) : Theme.RosterControlText;
                row.PlankTexts[i].fontStyle = on ? FontStyles.Bold : FontStyles.Normal;
                glyph.color = Theme.RosterControlText;
            }
            ShedRosterBar(row, line, n);
        }

        // ---------- the bind ----------

        // The list, bound the way the vicinity's containers are: every row into
        // the reach pool under the masked viewport, the ones outside the window
        // bound LEAN (a height and nothing else), a spacer in the main pool
        // reserving the space. LayoutReachContent then stacks them and the wheel
        // moves the whole thing - none of which had to learn what a row is.
        private static void BindRoster()
        {
            CollectRoster();
            _nearbyTotal = Roster.Count;
            if (_filter.Active)
            {
                for (int i = Roster.Count - 1; i >= 0; i--)
                {
                    if (!RosterMatches(Roster[i].Who))
                    {
                        Roster.RemoveAt(i);
                    }
                }
            }
            _nearbyMatched = Roster.Count;
            SortRoster();

            BindGroupFrame(GroupReach, ReachColumn);
            bool slot = ReadSlot();
            float width = slot ? Slot.InnerW : Mathf.Max(160f, _inventoryW[GroupReach]);
            float memberGap = ReachGap();
            // The rows stop short of the bar's column; the control row above the
            // list does not, because the bar only spans the list.
            float rowW = slot ? width : width - RosterScrollGutter;
            // the control row sits above the list and does not scroll with it, so
            // the viewport gives up its height
            float capH = Mathf.Max(RosterRowH,
                (slot ? SlotViewHeight() : NearbyMaxHeight)
                    - RosterBarH - memberGap);
            EnsureReachContent();

            // What the list is made of: a heading whenever the section changes,
            // then a row per person. Walked once to measure, once to bind, so a
            // row knows whether it is inside the window before it is drawn.
            float lo = _reachScroll;
            float hi = _reachScroll + capH;
            float walk = 0f;
            string section = null;
            _bindingReach = true;
            ReachOrder.Clear();
            for (int i = 0; i < Roster.Count; i++)
            {
                RosterEntry e = Roster[i];
                string want = SectionOf(e);
                if (!string.Equals(want, section))
                {
                    section = want;
                    bool leanHead = walk + RosterHeadH < lo - RosterRowH || walk > hi + RosterRowH;
                    BindRosterHead(want, rowW, leanHead);
                    walk += RosterHeadH + memberGap;
                }
                bool lean = walk + RosterRowH < lo - RosterRowH || walk > hi + RosterRowH;
                BindRosterRow(e.Who, rowW, lean);
                walk += RosterRowH + memberGap;
            }
            _bindingReach = false;

            float contentH = 0f;
            for (int i = 0; i < _reachPlatesUsed; i++)
            {
                contentH += ReachPlates[i].Height + (i > 0 ? memberGap : 0f);
            }
            _reachContentH = contentH;
            _reachViewH = Mathf.Min(capH, contentH);

            _reachSpacer = TakePlate();
            _reachSpacer.Column = ReachColumn;
            _reachSpacer.Group = GroupReach;
            _reachSpacer.IsViewport = true;
            _reachSpacer.Shown = 0;
            HideCells(_reachSpacer);
            _reachSpacer.Height = _reachViewH;
            _reachSpacer.LaidOutCols = -1;
            _reachSpacer.Back.enabled = false;
            _reachSpacer.Edge.enabled = false;
            _reachSpacer.Header.gameObject.SetActive(false);
            _reachSpacer.Window.Rect.sizeDelta = new Vector2(width, _reachViewH);
            if (!_reachSpacer.Window.Go.activeSelf)
            {
                _reachSpacer.Window.Go.SetActive(true);
            }
            _lastColumnWidth[ReachColumn] = Mathf.Max(_lastColumnWidth[ReachColumn], width);
            _lastGroupBound = GroupReach;

            BindRosterBar(width);
            BindTitle(RosterCaption(), ReachColumn, group: GroupReach);
            BindHeadingControls(Plates[_platesUsed - 1]);
        }

        private static string RosterCaption()
        {
            if (PlateNamed())
            {
                return "";   // the plate says it; the field carries the count
            }
            return _filter.Alive ? "Roster" : "Roster  " + _nearbyTotal;
        }

        // One pooled plate, one row. A sheet plate in every respect the rest of
        // the file cares about - TickSheets finds it, RepaintSheets keeps it live -
        // and IsRoster is what tells the repaint to re-derive the line.
        private static Plate BindRosterPlate(float width, float h, bool lean)
        {
            Plate plate = TakePlate();
            plate.Column = ReachColumn;
            plate.Group = GroupReach;
            plate.Container = null;
            plate.IsGroupFrame = false;
            plate.IsTitle = false;
            plate.Shown = 0;
            plate.Cols = 0;
            plate.LaidOutCols = -1;
            HideCells(plate);
            if (plate.Header.gameObject.activeSelf)
            {
                plate.Header.gameObject.SetActive(false);
            }
            plate.Back.enabled = false;
            plate.Edge.enabled = false;
            plate.Height = h;
            plate.Window.Rect.sizeDelta = new Vector2(width, h);
            Theme.Place(plate.Window.Content, 0f, 0f, width, h);
            plate.LaidOutW = width;
            plate.LaidOutH = h;
            if (!plate.Window.Go.activeSelf)
            {
                plate.Window.Go.SetActive(true);
            }
            if (lean)
            {
                // outside the window: a height and nothing else, as a lean
                // container band is. Nothing off screen is painted, so a roster of
                // sixty costs what the dozen you can see cost.
                plate.RowsShown = 0;
                if (plate.Rows != null)
                {
                    for (int i = 0; i < plate.Rows.Count; i++)
                    {
                        if (plate.Rows[i].Go.activeSelf) { plate.Rows[i].Go.SetActive(false); }
                    }
                }
            }
            return plate;
        }

        private static SheetRow TakeRosterRow(Plate plate, SheetLine line)
        {
            if (plate.Rows == null)
            {
                plate.Rows = new List<SheetRow>();
            }
            while (plate.Rows.Count < 1)
            {
                plate.Rows.Add(CreateSheetRow(plate));
            }
            SheetRow row = plate.Rows[0];
            EnsureRowExtras(row, line);
            for (int i = 1; i < plate.Rows.Count; i++)
            {
                if (plate.Rows[i].Go.activeSelf) { plate.Rows[i].Go.SetActive(false); }
            }
            plate.RowsShown = 1;
            plate.IsSheet = true;
            return row;
        }

        private static void BindRosterRow(CommonStates who, float width, bool lean)
        {
            Plate plate = BindRosterPlate(width, RosterRowH, lean);
            plate.Owner = who;
            plate.IsRoster = true;
            if (lean)
            {
                return;
            }
            SheetLine line = RosterLine(who);
            SheetRow row = TakeRosterRow(plate, line);
            Theme.Place(row.Go.transform, 0f, 0f, width, RosterRowH);
            LayoutRosterRow(row, line, width, RosterRowH);
            PaintSheetRow(row, line);
            if (!row.Go.activeSelf)
            {
                row.Go.SetActive(true);
            }
            SheetPlates.Add(plate);
        }

        private static void BindRosterHead(string text, float width, bool lean)
        {
            Plate plate = BindRosterPlate(width, RosterHeadH, lean);
            plate.Owner = null;   // static type: RepaintSheets leaves it alone
            if (lean)
            {
                return;
            }
            var line = new SheetLine { Kind = LineHead, Label = text, Value = string.Empty };
            SheetRow row = TakeRosterRow(plate, line);
            Theme.Place(row.Go.transform, 0f, 0f, width, RosterHeadH);
            PaintSheetRow(row, line);
            Theme.Place(row.Label, RosterPadX, 0f, width - RosterPadX - SheetPadX, RosterHeadH);
            Theme.Place(row.Value, width, 0f, 0f, RosterHeadH);
            ShedRosterHead(row, text);
            if (!row.Go.activeSelf)
            {
                row.Go.SetActive(true);
            }
            SheetPlates.Add(plate);
        }

        // The control row, in the MAIN pool: it belongs to the frame, not to the
        // list, so the wheel leaves it where it is.
        private static void BindRosterBar(float width)
        {
            SheetLine line = RosterBarLine();
            Plate plate = TakePlate();
            plate.Column = ReachColumn;
            plate.Group = GroupReach;
            plate.Container = null;
            plate.IsGroupFrame = false;
            plate.IsTitle = false;
            plate.Owner = null;
            plate.Shown = 0;
            plate.Cols = 0;
            plate.LaidOutCols = -1;
            HideCells(plate);
            if (plate.Header.gameObject.activeSelf)
            {
                plate.Header.gameObject.SetActive(false);
            }
            plate.Back.enabled = false;
            plate.Edge.enabled = false;
            plate.Height = RosterBarH;
            plate.Window.Rect.sizeDelta = new Vector2(width, RosterBarH);
            Theme.Place(plate.Window.Content, 0f, 0f, width, RosterBarH);
            plate.LaidOutW = width;
            plate.LaidOutH = RosterBarH;
            plate.IsRoster = true;   // ...with no Owner: the list's row, not a person's
            if (!plate.Window.Go.activeSelf)
            {
                plate.Window.Go.SetActive(true);
            }
            SheetRow row = TakeRosterRow(plate, line);
            Theme.Place(row.Go.transform, 0f, 0f, width, RosterBarH);
            LayoutRosterBar(row, line, width, RosterBarH);
            PaintSheetRow(row, line);
            if (!row.Go.activeSelf)
            {
                row.Go.SetActive(true);
            }
            _lastGroupBound = GroupReach;
            SheetPlates.Add(plate);
        }

        // Vanilla's roster button, landed on the slot (RosterOpener). Pressed with
        // the roster already up it closes the window, which is what the vanilla
        // panel's own toggle did; pressed on the vicinity list it swaps the tab,
        // because the button asked for the roster, not for the slot.
        // Returns false when the window cannot serve it, so vanilla's panel can.
        internal static bool ToggleRoster(ManagersScript mn)
        {
            if (mn == null || !mn.allSet || mn.uiMN == null || !EnsureBuilt(mn))
            {
                return false;
            }
            if (RosterView)
            {
                ToggleReach();
                return true;
            }
            ShowSlotView(ViewRoster);
            return true;
        }

        // What the slot's list puts in the frame BESIDE the scrolled list - the
        // roster's control row above it, the vicinity's foot below it. Layout asks
        // so the frame still lands on the slot's top edge.
        private static float RosterExtraH()
        {
            if (RosterView)
            {
                return RosterBarH + GroupMemberGap;
            }
            return _reachFootBound ? ReachFootH() + GroupMemberGap : 0f;
        }

        // For the harness: what vanilla says a target offers, read rather
        // than re-derived. Row -1 asks about the current focus.
        internal static string VerbDump(int row)
        {
            CommonStates who = row >= 0 && row < Roster.Count ? Roster[row].Who : _target as CommonStates;
            return TargetVerbs.Dump(_mn, who);
        }

        // For the harness: the prop in focus - what it is, what wear it
        // carries, and what vanilla lit for it. Answers the question a screenshot
        // cannot: whether a missing wear bar means we failed to draw it or the
        // object has no durability at all.
        internal static string PropDump()
        {
            var prop = _target as ItemInfo;
            if (prop == null)
            {
                return "no prop in focus (target is " + (_target == null ? "null" : _target.GetType().Name) + ")";
            }
            var sb = new System.Text.StringBuilder();
            sb.Append(prop.itemKey).Append(" life ").Append(prop.life)
              .Append(" / ").Append(prop.lifeMax);
            var slot = prop.GetComponent<InventorySlot>();
            sb.Append(" | slots ").Append(slot != null && slot.slots != null ? slot.slots.Length : 0);
            var breed = prop.GetComponent<BreedInfo>();
            sb.Append(" | breed ").Append(breed != null ? breed.breedType.ToString() : "-");
            PropVerbs.ReadPlacement(_mn);
            sb.Append(" | placement ").Append(PropVerbs.Placement.Count);
            for (int i = 0; i < PropVerbs.Placement.Count; i++)
            {
                sb.Append(" ").Append(PropVerbs.Placement[i].Name)
                  .Append(PropVerbs.Placement[i].Icon != null ? "(icon)" : "(NO ICON)");
            }
            sb.Append(" | ").Append(PropVerbs.Dump(_mn, prop));
            return sb.ToString();
        }

        // For the harness: why a given prop does or does not reach our
        // window. A pen opening vanilla's menu can mean three different things -
        // it is not plain storage, it is not admitted by the focus, or the focus
        // never resolves to it - and only the predicates can say which.
        internal static string PenDump()
        {
            var sb = new System.Text.StringBuilder();
            ItemInfo[] all = UnityEngine.Object.FindObjectsOfType<ItemInfo>();
            Vector3 me = _mn != null && _mn.gameMN != null && _mn.gameMN.pMove != null
                ? _mn.gameMN.pMove.transform.position : Vector3.zero;
            int shown = 0;
            for (int i = 0; i < all.Length && shown < 12; i++)
            {
                ItemInfo info = all[i];
                if (info == null || Vector3.Distance(info.transform.position, me) > 12f)
                {
                    continue;
                }
                var slot = info.GetComponent<InventorySlot>();
                var breed = info.GetComponent<BreedInfo>();
                if (slot == null && breed == null)
                {
                    continue;
                }
                shown++;
                sb.Append(info.itemKey)
                  .Append(" type=").Append(slot != null ? slot.type.ToString() : "-")
                  .Append(" breed=").Append(breed != null ? breed.breedType.ToString() : "-")
                  .Append(" slots=").Append(slot != null && slot.slots != null ? slot.slots.Length : 0)
                  .Append(" life=").Append(info.lifeMax)
                  .Append(" plain=").Append(slot != null && VanillaContainerUI.IsPlainStorage(slot))
                  .Append(" rack=").Append(slot != null && ContainerPolicy.IsViewedRack(slot))
                  .Append(" replaceable=").Append(slot != null && IsReplaceable(slot))
                  // DefenceInfo is what makes a building attackable at all -
                  // AttackCollider looks for it and calls DefenceDamage; lifeMax
                  // is only the pool that damage draws down
                  .Append(" def=").Append(info.GetComponent<DefenceInfo>() != null
                      ? info.GetComponent<DefenceInfo>().defenceType.ToString() : "-")
                  .Append(" own=").Append(_mn != null && _mn.buildMN != null && _mn.buildMN.OwnCheck(info))
                  .AppendLine();
            }
            return shown == 0 ? "nothing with a slot or a breed within 12m" : sb.ToString();
        }

        // For the harness: does the speed limit bite? The clamp only does anything
        // when speedLimit < speed, so the numbers are the answer.
        internal static string SpeedDump()
        {
            var sb = new System.Text.StringBuilder();
            var s = _mn != null && _mn.uiMN != null ? _mn.uiMN.speedLimitSlider : null;
            sb.Append("slider ").Append(s != null ? s.minValue + ".." + s.maxValue : "?");
            var list = _mn != null && _mn.npcMN != null ? _mn.npcMN.friendList : null;
            for (int i = 0; list != null && i < list.Count && i < 6; i++)
            {
                CommonStates w = list[i];
                if (w == null) { continue; }
                sb.Append(" | ").Append(w.charaName)
                  .Append(" speed=").Append(w.speed.ToString("0.00"))
                  .Append(" limit=").Append(w.speedLimit.ToString("0.00"))
                  .Append(" runRate=").Append(w.runRate.ToString("0.00"))
                  .Append(" moveNPC=").Append(w.MoveSpeedNPC().ToString("0.00"));
            }
            return sb.ToString();
        }

        // For the harness: the prop census (D82). Reads and reports, touches nothing.
        internal static string PropShapes()
        {
            return PropCensus.Run(_mn);
        }

        // ---------- what a press does ----------

        // For the harness (tests/scenarios): the sort control and a row by its
        // place in the list, as a click on either would. Both are polled, so
        // /ui/click cannot reach them - the same reason SheetAct exists.
        internal static bool RosterAct(int sort, int row)
        {
            if (!RosterView)
            {
                return false;
            }
            if (sort >= 0)
            {
                PressRosterBar(sort);
            }
            else if (row >= 0 && row < Roster.Count)
            {
                PressRosterRow(Roster[row].Who);
                return true;
            }
            _forceRefresh = true;
            _nextRefresh = 0f;
            Refresh();
            return true;
        }

        // For the harness: the two interactions a roster pick and a Follow press
        // must survive, begun as vanilla's own interact begins them. A body search
        // on the nearest stranger's body (PlayerMove.SearchNPC), or a world click
        // on a roster row's person (PlayerMove.CheckNPC).
        internal static string SearchNearestBody()
        {
            PlayerMove pMove = _mn != null && _mn.gameMN != null ? _mn.gameMN.pMove : null;
            if (pMove == null)
            {
                return "no player";
            }
            CommonStates best = null;
            float bestD = float.MaxValue;
            foreach (CommonStates c in Object.FindObjectsOfType<CommonStates>())
            {
                if (c == null || c.dead != 1 || c.Employed() || c.bag == null)
                {
                    continue;
                }
                float d = (c.transform.position - pMove.transform.position).sqrMagnitude;
                if (d < bestD)
                {
                    bestD = d;
                    best = c;
                }
            }
            if (best == null)
            {
                return "no body";
            }
            pMove.target = best.gameObject;
            pMove.StartCoroutine(pMove.SearchNPC(best));
            return best.charaName + " at " + Mathf.Sqrt(bestD).ToString("0.0");
        }

        internal static string ClickRosterPerson(int row)
        {
            return row >= 0 && row < Roster.Count ? ClickPerson(Roster[row].Who) : "no such row";
        }

        // ...or anyone of yours by name (a crafter, say)
        internal static string ClickPersonNamed(string name)
        {
            NPCManager npcs = _mn != null ? _mn.npcMN : null;
            for (int i = 0; npcs != null && npcs.friendList != null && i < npcs.friendList.Count; i++)
            {
                CommonStates who = npcs.friendList[i];
                if (who != null && who.charaName == name)
                {
                    return ClickPerson(who);
                }
            }
            return "nobody named " + name;
        }

        // As a real click leaves it: pMove.target is the NPC's interaction
        // collider (layer 14), a child of the character (PlayerMove.InteractNPC)
        private static string ClickPerson(CommonStates who)
        {
            PlayerMove pMove = _mn != null && _mn.gameMN != null ? _mn.gameMN.pMove : null;
            if (pMove == null || who == null || who.nMove == null)
            {
                return "no player or no mover";
            }
            GameObject hit = who.gameObject;
            foreach (Collider c in who.GetComponentsInChildren<Collider>())
            {
                if (c.gameObject.layer == 14 && c.transform.parent == who.transform)
                {
                    hit = c.gameObject;
                    break;
                }
            }
            pMove.target = hit;
            pMove.StartCoroutine(pMove.CheckNPC(who.nMove));
            return who.charaName + " npcID " + who.npcID + (hit == who.gameObject ? " (no layer-14 child)" : "");
        }

        // ...and an act on the unit by its vanilla name, as its socket would press it
        internal static bool UnitAct(string name)
        {
            CommonStates who = _unitFor ?? _target as CommonStates;
            if (who == null)
            {
                return false;
            }
            PressUnitAct(who, name);
            return true;
        }

        // ...past the reach rule (D73), for a person the harness cannot walk to
        internal static bool UnitActFar(string name)
        {
            CommonStates who = _unitFor ?? _target as CommonStates;
            if (who == null || _mn == null)
            {
                return false;
            }
            TargetVerbs.ReadIncludingElsewhere(_mn, who);
            IList<TargetVerbs.Verb> live = TargetVerbs.Current;
            for (int i = 0; i < live.Count; i++)
            {
                if (live[i].Name == name)
                {
                    PressVerb(who, live[i]);
                    return true;
                }
            }
            return false;
        }

        internal static string TargetName()
        {
            return _target is CommonStates c ? c.charaName : _target != null ? _target.ToString() : "none";
        }

        private static void PressRosterBar(int sub)
        {
            if (sub < 0)
            {
                return;
            }
            if (sub < RosterBarSorts)
            {
                if (_rosterSort == sub)
                {
                    return;
                }
                _rosterSort = sub;
                _reachScroll = 0f;   // a re-sorted list has nowhere to hold a position
                return;
            }
            if (_mn == null)
            {
                return;
            }
            if (sub == RosterBarSorts && _mn.uiMN != null)
            {
                _mn.uiMN.ALLNameVisible();
            }
            else if (_mn.npcMN != null)
            {
                _mn.npcMN.ALLNPCToTerritory();
            }
        }

        // D53/D54: a row picks a person at any distance. It used to open their sheet
        // as vanilla's FriendSlot.FriendView does (menuNPC, then StatusView) through
        // §6.25 D93's roster DOOR, which landed on Orders or, without it, the first
        // page.
        //
        // A PICK IS A CLICK: a roster pick does not force the relations sheet; it
        // selects the character by the same mechanics as a normal click, and if
        // the inventory was open last it opens too - unless they are out of
        // reach. So no door and no StatusView: the pick opens vanilla's own NPC
        // menu, as a world click's CheckNPC does - which lights the npcID acts - and
        // the target comes up as it would from the world. The sheet and the bag
        // window are the player's standing choice (D107): open ones stay open for
        // this person, the bag only within reach, and closed ones stay closed.
        private static void PressRosterRow(CommonStates who)
        {
            if (who == null || _mn == null || _mn.uiMN == null || _mn.gameMN == null)
            {
                return;
            }
            // a second press on the one already picked lets them go
            if (_mn.gameMN.menuNPC == who && ReferenceEquals(Focus(), who))
            {
                NoteEnd("picked again");
                _mn.uiMN.NPCMenuClose();
                return;
            }
            // A PICK ENDS A BODY SEARCH FIRST (a roster
            // pick made while searching a corpse drew the CORPSE's sheet).
            // ResolveTarget asks searchNPC before menuNPC, so the pick lost; and
            // vanilla's SearchNPC coroutine nulls menuNPC on its way out, so the
            // pick cannot be set beside a live search either. The search is ended
            // the way vanilla ends it - its target goes - and the pick lands on
            // the tick after it has unwound (LandPendingPick).
            // ...and so does any interaction vanilla is holding open (a person's menu
            // under CheckNPC, a chest under UseProp): its loop would close THIS menu
            // on its way out. Letting its target go ends it the way a click
            // elsewhere does, and the pick lands once nowAction has cleared.
            PlayerMove pMove = _mn.gameMN.pMove;
            bool searching = pMove != null && pMove.searchNPC != null && !ReferenceEquals(pMove.searchNPC, who);
            if (pMove != null && (searching || _mn.gameMN.nowAction))
            {
                pMove.target = null;
                _pendingPick = who;
                _pendingPickUntil = Time.unscaledTime + 1.5f;
                return;
            }
            _pendingPick = null;
            ArmPick(who);
            _mn.uiMN.NPCMenuOpen(who);
            _forceRefresh = true;
            _nextRefresh = 0f;
        }

        private static CommonStates _pendingPick;
        private static float _pendingPickUntil;

        // Tick: a pick waiting on a body search to unwind. A second away from
        // the press it is dropped rather than landing on a stale intent.
        private static void LandPendingPick()
        {
            if (_pendingPick == null)
            {
                return;
            }
            CommonStates who = _pendingPick;
            if (Time.unscaledTime > _pendingPickUntil || who == null || _mn.gameMN == null)
            {
                _pendingPick = null;
                return;
            }
            if ((_mn.gameMN.pMove != null && _mn.gameMN.pMove.searchNPC != null) || _mn.gameMN.nowAction)
            {
                return;
            }
            _pendingPick = null;
            PressRosterRow(who);
        }
    }
}
