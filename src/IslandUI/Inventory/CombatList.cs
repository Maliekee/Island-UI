using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace IslandUI
{
    // THE COMBAT LIST (Forgejo #63): every enemy in the fight, ranked, for the
    // plaque at the top centre and the light markers over their heads. Two
    // sources, both only read:
    //  - YOU HIT IT. GameManager.EnemyLifeChange runs on every hit the player
    //    deals (CommonStates.NPCDamage and StunDamage, and the self-hits of the
    //    meat wall and the reflect shields); a postfix stamps the time. Vanilla's
    //    bar keeps its target 10 s after the last hit (enemyLifeTime), and so does
    //    this list.
    //  - IT IS ON YOU. GameManager.battler, the enemies whose tmpEnemy is the
    //    player: the game's own "in combat". It can hold duplicates, and dead or
    //    disabled entries its removal loop skipped, so it is read through the
    //    test that loop removes by.
    //  - IT FIGHTS YOUR PEOPLE. A blow traded between one of yours and one not
    //    (CommonStates.NPCDamage and StunDamage, either way round) - vanilla raises
    //    its bar only for the player's own blows, so the foe a follower fought wore
    //    no marker. It joins for HitWindow after the last blow, within FriendReach,
    //    for the markers alone: the plaque stays the fight that is yours (Mine).
    // Ranked: yours before the ones only your people fight, then the ones you hit first, the latest hit at the top (vanilla's
    // viewCommon), then the ones on you, the nearest first. Every enemy is
    // tracked; only the drawing is capped (CombatMarkers). Entries are pooled and
    // the sort uses one comparer, so a frame allocates nothing.
    //
    // THE BOSS FIRST (Forgejo #74, Settings.BossPriority): before that rule, a
    // tier - the fight's target, a boss, the rest, a summon - so hitting a
    // spider's brood or a pole of Planton never takes the plaque off the boss,
    // and the bosses take the markers first. Within a tier the rule is as above.
    // A third source, for the fight's target: vanilla keeps no boss bar and no
    // boss flag, but every story fight hands StoryManager.BossBattleCheck its
    // list, and those join as on you from the fight's start.
    //
    // YOUR PEOPLE IN THE FIGHT (Forgejo #98) are a list of their own beside it,
    // ranked apart and capped apart (Friends, below): the plaque, its "+N" and
    // the enemies' marker slots never count them.
    internal static class CombatList
    {
        internal const float HitWindow = 10f;
        // A dead, downed or departed enemy stays this long, its bar seen at 0,
        // before the next in line takes its place. Vanilla kept a killed target
        // up for the rest of its 10 s, which held the plaque on a body.
        internal const float Linger = 1.5f;
        // A kill stays longer, its plaque turned to Slain (InventoryWindow.Anime):
        // all of this while no one else is in the fight, and past Linger it gives
        // way to anyone who still is (Rank). The player's to set
        // (Settings.KillLinger); it was 5 s, which held a finished fight too long
        internal static float KillLinger => Settings.KillLinger != null ? Settings.KillLinger.Value : 3f;
        // At the cap, a marked enemy ranks as if this much nearer, so two at
        // nearly the same distance do not trade the last marker every frame.
        private const float MarkedNearer = .8f;

        internal sealed class Entry
        {
            internal CommonStates Who;
            internal float HitAt;      // Time.time of your last hit on it
            internal bool OnYou;       // in battler this frame
            internal float LiveAt;     // the last time it was hit-fresh or on you, alive
            internal float Distance;   // from the player across the ground, m
            internal bool Marked;      // drawn with a marker (CombatMarkers sets it)
            internal int Seen;         // the frame battler last listed it
            internal int Tier;         // FightTier to SummonTier, this frame
            internal bool Minion;      // a MinionsScript on it, read when the entry is made
            internal float SidedAt;    // Time.time of the last blow traded with one of yours
            internal bool Mine;        // ever hit by you or on you: the plaque's to show
            internal bool Hit(float now) => now - HitAt < HitWindow;
            internal bool Sided(float now) => now - SidedAt < HitWindow && Distance <= FriendReach;
            internal bool Alive => Who != null && Who.dead == 0;
        }

        private static readonly List<Entry> Ranked = new List<Entry>();
        private static readonly Stack<Entry> Spare = new Stack<Entry>();
        private static int _frame = -1;
        private static float _now;

        internal static int Count => Ranked.Count;
        internal static Entry At(int i) => Ranked[i];
        internal static float Now => _now;

        // How many of the list are on you, this frame.
        internal static int OnYouCount { get; private set; }

        [HarmonyPatch(typeof(GameManager), nameof(GameManager.EnemyLifeChange))]
        private static class Hits
        {
            [HarmonyPostfix]
            private static void Postfix(CommonStates tmpCommon)
            {
                if (tmpCommon == null) { return; }
                Entry e = Find(tmpCommon, true);
                e.HitAt = Time.time;
                e.Mine = true;
            }
        }

        [HarmonyPatch(typeof(CommonStates), nameof(CommonStates.NPCDamage))]
        private static class SidedHits
        {
            [HarmonyPostfix]
            private static void Postfix(CommonStates __instance, CommonStates attacker) => Traded(__instance, attacker);
        }

        [HarmonyPatch(typeof(CommonStates), nameof(CommonStates.StunDamage))]
        private static class SidedStuns
        {
            [HarmonyPostfix]
            private static void Postfix(CommonStates __instance, CommonStates attacker) => Traded(__instance, attacker);
        }

        // the player's own blows are EnemyLifeChange's (Hits); a self-hit, or two
        // of yours sparring, or two strangers, is no side of yours
        private static void Traded(CommonStates struck, CommonStates attacker)
        {
            if (struck == null || attacker == null || ReferenceEquals(struck, attacker)
                || attacker.CompareTag("Player") || struck.CompareTag("Player")) { return; }
            bool ours = Yours(attacker);
            if (ours == Yours(struck)) { return; }
            CommonStates foe = ours ? struck : attacker;
            Entry e = Find(foe, foe.dead == 0 && foe.life > 0.0);
            if (e != null) { e.SidedAt = Time.time; }
        }

        // each enemy's entry by reference: a raid's battler is read against the
        // list once a frame, and a scan of the list for each was 11 000 looks
        private sealed class SameOne : IEqualityComparer<CommonStates>
        {
            public bool Equals(CommonStates a, CommonStates b) => ReferenceEquals(a, b);
            public int GetHashCode(CommonStates c) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(c);
        }
        private static readonly Dictionary<CommonStates, Entry> ByWho = new Dictionary<CommonStates, Entry>(new SameOne());

        private static Entry Find(CommonStates who, bool make) => Find(who, make, Ranked, ByWho);

        private static Entry Find(CommonStates who, bool make, List<Entry> list, Dictionary<CommonStates, Entry> by)
        {
            if (by.TryGetValue(who, out Entry found)) { return found; }
            if (!make) { return null; }
            Entry e = Spare.Count > 0 ? Spare.Pop() : new Entry();
            by[who] = e;
            e.Who = who;
            e.HitAt = e.SidedAt = float.NegativeInfinity;
            e.OnYou = e.Marked = e.Mine = false;
            e.LiveAt = Time.time;
            e.Distance = 0f;
            e.Seen = -1;
            e.Tier = NormalTier;
            e.Minion = who.GetComponent<MinionsScript>() != null;
            list.Add(e);
            return e;
        }

        internal static Entry Of(CommonStates who) => who != null ? Find(who, false) : null;

        // Once a frame, before anything draws from it.
        internal static void Tick(GameManager gm, NPCManager npcs)
        {
            if (Time.frameCount == _frame) { return; }
            _frame = Time.frameCount;
            long heap = Heap();
            long started = Stopwatch.GetTimestamp();
            _now = Time.time;
            GameObject player = gm != null ? gm.player : null;
            List<NPCMove> battler = gm != null ? gm.battler : null;
            if (player != null && battler != null)
            {
                for (int i = 0; i < battler.Count; i++)
                {
                    NPCMove m = battler[i];
                    // running from you is not on you: a blow makes you the target of
                    // whatever it lands on, so the deer you hit is in battler while it
                    // flees (RunAway) - and pulsed red (target-matrix D4)
                    if (m == null || m.tmpEnemy != player || m.actType == NPCMove.ActType.Dead || m.actType == NPCMove.ActType.RunAway
                        || !m.gameObject.activeSelf || m.common == null || m.common.dead != 0) { continue; }
                    Find(m.common, true).Seen = _frame;
                }
            }
            bool tiers = Settings.BossPriority.Value;
            if (npcs != null && _bosses == null) { Bosses(npcs); }   // the plaque's boss look reads it, tiers or not
            if (_fight != null)
            {
                if (GameManager.eventBattle) { _fightOn = true; }
                else if (_fightOn || _frame - _fightFrom > FightGrace) { _fight = null; }
            }
            // the fight's targets are on you from its start, the first hit or not:
            // vanilla sets the boss on you itself (tmpEnemy), and a boss it set
            // never comes into battler, which only its own sight check fills
            if (tiers && player != null && _fight != null)
            {
                for (int i = 0; i < _fight.Length; i++)
                {
                    if (InFight(_fight[i])) { Find(_fight[i], true).Seen = _frame; }
                }
            }
            for (int i = Entkings.Count - 1; i >= 0; i--)
            {
                if (Entkings[i] == null) { Entkings.RemoveAt(i); }
            }
            Vector3 from = player != null ? player.transform.position : Vector3.zero;
            int onYou = 0;
            for (int i = Ranked.Count - 1; i >= 0; i--)
            {
                Entry e = Ranked[i];
                if (e.Who == null || player == null)
                {
                    Drop(i);
                    continue;
                }
                e.OnYou = e.Seen == _frame;
                if (e.OnYou) { onYou++; e.Mine = true; }
                Vector3 d = e.Who.transform.position - from;
                d.y = 0f;
                e.Distance = d.magnitude;
                bool live = e.Alive && e.Who.gameObject.activeInHierarchy && (e.OnYou || e.Hit(_now) || e.Sided(_now));
                if (live) { e.LiveAt = _now; }
                else if (_now - e.LiveAt >= (e.Alive ? Linger : KillLinger))
                {
                    Drop(i);
                    continue;
                }
                e.Tier = tiers ? TierOf(e) : NormalTier;
            }
            OnYouCount = onYou;
            if (Ranked.Count > 1) { Ranked.Sort(Rank.Instance); }
            TickFriends(player, npcs);
            Measure(Stopwatch.GetTimestamp() - started);
            Counted(heap);
        }

        private static void Drop(int i) => Drop(i, Ranked, ByWho);

        private static void Drop(int i, List<Entry> list, Dictionary<CommonStates, Entry> by)
        {
            Entry e = list[i];
            // by its reference, which a destroyed one still has
            by.Remove(e.Who);
            e.Who = null;
            list.RemoveAt(i);
            Spare.Push(e);
        }

        internal static void Clear()
        {
            for (int i = Ranked.Count - 1; i >= 0; i--) { Drop(i); }
            OnYouCount = 0;
            ClearFriends();
        }

        // a kill past the short linger, kept only for a fight that has no one else
        private static bool Waning(Entry e) => !e.Alive && _now - e.LiveAt >= Linger;

        private sealed class Rank : IComparer<Entry>
        {
            internal static readonly Rank Instance = new Rank();

            public int Compare(Entry a, Entry b)
            {
                bool aw = Waning(a), bw = Waning(b);
                if (aw != bw) { return aw ? 1 : -1; }
                if (a.Mine != b.Mine) { return a.Mine ? -1 : 1; }
                if (a.Tier != b.Tier) { return a.Tier.CompareTo(b.Tier); }
                bool ah = a.Hit(_now), bh = b.Hit(_now);
                if (ah != bh) { return ah ? -1 : 1; }
                if (ah) { return b.HitAt.CompareTo(a.HitAt); }
                float ka = a.Distance * (a.Marked ? MarkedNearer : 1f);
                float kb = b.Distance * (b.Marked ? MarkedNearer : 1f);
                return ka.CompareTo(kb);
            }
        }

        // ---------- the boss first (Forgejo #74) ----------

        internal const int FightTier = 0, BossTier = 1, NormalTier = 2, SummonTier = 3;

        // THE FIGHT'S TARGET: the array a story fight hands BossBattleCheck, for
        // as long as GameManager.eventBattle holds - the check sets it at its
        // first step and clears it at its end, won, lost or given up. The prefix
        // runs when the coroutine is made, a step before; one never started is
        // let go after FightGrace frames. Planton's is its core (commons[0]),
        // never the poles you strike while its barrier holds, so the core keeps
        // the plaque through them, as vanilla's check keeps the fight on it.
        private const int FightGrace = 30;
        private static CommonStates[] _fight;
        private static int _fightFrom;
        private static bool _fightOn;

        [HarmonyPatch(typeof(StoryManager), nameof(StoryManager.BossBattleCheck))]
        private static class Fight
        {
            [HarmonyPrefix]
            private static void Prefix(CommonStates[] bossCommon)
            {
                _fight = bossCommon;
                _fightFrom = Time.frameCount;
                _fightOn = false;
            }
        }

        // THE STORY FIGHT'S SIDE, for Stance: while the fight holds, its list and
        // everyone of their group are hostile - the fight is the game saying so. A
        // boss is scripted past NPCMove (Planton strikes through AttackPlanton's own
        // target, its poles are npcType Event and never look), so Stance's own
        // came-for-you test never sees it. Kept here, with the fight, so Stance's
        // rules stay the plain ones. Never your own group.
        internal static bool StorySide(CommonStates who, int yourGroup)
        {
            CommonStates[] fight = GameManager.eventBattle ? _fight : null;
            if (fight == null || who == null) { return false; }
            for (int i = 0; i < fight.Length; i++)
            {
                CommonStates c = fight[i];
                if (c == null) { continue; }
                if (ReferenceEquals(c, who) || c.groupID == who.groupID && c.groupID != yourGroup) { return true; }
            }
            return false;
        }

        // still to beat, by the check's own test
        private static bool InFight(CommonStates c) =>
            c != null && c.life > 0.0 && c.faint > 0.0 && c.dead == 0 && c.gameObject.activeInHierarchy;

        private static bool FightTarget(CommonStates c)
        {
            if (_fight == null) { return false; }
            for (int i = 0; i < _fight.Length; i++)
            {
                if (ReferenceEquals(_fight[i], c)) { return InFight(c); }
            }
            return false;
        }

        // A BOSS by its kind, met in its fight or out of it - the gorillas
        // respawned after theirs, the spider in its cave: every npcPrefab named
        // boss_* (and hallo_boss_01), read once, and the three fought as bosses
        // under other names.
        // The kind is the prefab's place in that array: a spawn writes it into
        // npcID (NPCManager.UniqueSpawn and the rest); the prefab's own npcID is
        // left unset on many, 0 or another's.
        private static readonly int[] HandBosses = { 110, 121, 122 };   // the giant, and two named otherwise
        private static HashSet<int> _bosses;

        private static void Bosses(NPCManager npcs)
        {
            GameObject[] prefabs = npcs.npcPrefab;
            if (prefabs == null) { return; }
            _bosses = new HashSet<int>(HandBosses);
            for (int i = 0; i < prefabs.Length; i++)
            {
                GameObject p = prefabs[i];
                if (p != null && (p.name.StartsWith("boss_", System.StringComparison.Ordinal)
                    || p.name.Contains("_boss_"))) { _bosses.Add(i); }
            }
        }

        // A SUMMON: what a boss brings into its fight. The spider's brood and the
        // bird's eggs wear a MinionsScript (AttackScript.Spawn, AttackNecks.Spawn);
        // the Entking's three ents wear nothing, and are known by its spawnEnemy,
        // which the prefix on its Spawn finds it by. An ent of the wild is no summon.
        private static readonly List<AttackEntking> Entkings = new List<AttackEntking>();

        [HarmonyPatch(typeof(AttackEntking), nameof(AttackEntking.Spawn))]
        private static class EntSpawn
        {
            [HarmonyPrefix]
            private static void Prefix(AttackEntking __instance)
            {
                if (!Entkings.Contains(__instance)) { Entkings.Add(__instance); }
            }
        }

        private static bool Summoned(Entry e)
        {
            if (e.Minion) { return true; }
            for (int i = 0; i < Entkings.Count; i++)
            {
                AttackEntking king = Entkings[i];
                if (king == null || king.spawnEnemy == null || king.common == null || king.common.dead != 0) { continue; }
                for (int j = 0; j < king.spawnEnemy.Length; j++)
                {
                    if (ReferenceEquals(king.spawnEnemy[j], e.Who)) { return true; }
                }
            }
            return false;
        }

        // A BOSS TO THE PLAQUE (InventoryWindow.Anime): a boss kind, or one of the
        // fight's targets, beaten or not - its plate stays the boss's to the end
        internal static bool IsBoss(Entry e)
        {
            if (e == null || e.Who == null) { return false; }
            if (_bosses != null && _bosses.Contains(e.Who.npcID)) { return true; }
            for (int i = 0; _fight != null && i < _fight.Length; i++)
            {
                if (ReferenceEquals(_fight[i], e.Who)) { return true; }
            }
            return false;
        }

        private static int TierOf(Entry e)
        {
            if (FightTarget(e.Who)) { return FightTier; }
            if (Summoned(e)) { return SummonTier; }
            return _bosses != null && _bosses.Contains(e.Who.npcID) ? BossTier : NormalTier;
        }

        private static string TierName(int tier) =>
            tier == FightTier ? "fight" : tier == BossTier ? "boss" : tier == SummonTier ? "summon" : "normal";

        // For the harness, which has no boss fight left to start in its save:
        //  "fight <name> [<name>...]"  the nearest living one of each name in the
        //      list goes through vanilla's own BossBattleCheck, started on the
        //      story manager as a fight does, without the followers' teleport
        //  "end"            stops that check, and the battle with it
        //  "boss <npcID>"   that kind is a boss, in memory, until the next load
        //  "summon <name>"  the nearest of that name wears a MinionsScript, as a
        //      spider's brood does
        //  "hit <name> [rate]"  you strike the nearest of that name, through its
        //      own CommonStates.NPCDamage, as a blow of yours does (rate 1)
        //  "guard [off]"    you take no damage while it tests, in memory
        //  ""               the tiers as they stand: the fight, the boss kinds
        // A name is a charaName or an npcID; "10@2" is the third nearest of them.
        private static Coroutine _harnessFight;

        internal static string HarnessTier(string spec)
        {
            string[] words = (spec ?? "").Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            ManagersScript mn = Object.FindObjectOfType<ManagersScript>();
            GameObject player = mn != null && mn.gameMN != null ? mn.gameMN.player : null;
            if (player == null || mn.story == null) { return "no game"; }
            string how = words.Length > 0 ? words[0] : "";
            if (how == "fight")
            {
                var who = new List<CommonStates>();
                for (int i = 1; i < words.Length; i++)
                {
                    CommonStates c = Nearest(words[i], player, who);
                    if (c == null) { return "no " + words[i]; }
                    who.Add(c);
                }
                if (who.Count == 0) { return "fight whom?"; }
                if (_harnessFight != null) { mn.story.StopCoroutine(_harnessFight); }
                _harnessFight = mn.story.StartCoroutine(mn.story.BossBattleCheck(who.ToArray(), teleport: false));
            }
            else if (how == "end")
            {
                if (_harnessFight != null) { mn.story.StopCoroutine(_harnessFight); }
                _harnessFight = null;
                GameManager.eventBattle = false;
            }
            else if (how == "boss" && words.Length > 1 && int.TryParse(words[1], out int id))
            {
                if (mn.npcMN != null && _bosses == null) { Bosses(mn.npcMN); }
                if (_bosses != null) { _bosses.Add(id); }
            }
            else if (how == "summon" && words.Length > 1)
            {
                CommonStates c = Nearest(words[1], player, null);
                if (c == null) { return "no " + words[1]; }
                if (c.GetComponent<MinionsScript>() == null) { c.gameObject.AddComponent<MinionsScript>(); }
                Entry e = Of(c);
                if (e != null) { e.Minion = true; }
            }
            else if (how == "hit" && words.Length > 1)
            {
                CommonStates c = Nearest(words[1], player, null);
                if (c == null) { return "no " + words[1]; }
                float rate = words.Length > 2 && float.TryParse(words[2], out float r) ? r : 1f;
                c.NPCDamage(player.GetComponent<CommonStates>(), rate);
            }
            else if (how == "guard")
            {
                CommonStates you = player.GetComponent<CommonStates>();
                you.invincible = words.Length < 2 || words[1] != "off";
                return "invincible=" + you.invincible;
            }
            var sb = new StringBuilder();
            sb.Append("eventBattle=").Append(GameManager.eventBattle).Append(" fight=");
            if (_fight == null) { sb.Append('-'); }
            else
            {
                for (int i = 0; i < _fight.Length; i++)
                {
                    CommonStates c = _fight[i];
                    sb.Append(i > 0 ? "," : "").Append(c != null ? c.charaName + "#" + c.npcID + (InFight(c) ? "" : "(beaten)") : "null");
                }
            }
            sb.Append(" entkings=").Append(Entkings.Count).Append(" bosses=");
            if (_bosses == null) { sb.Append('-'); }
            else
            {
                var ids = new List<int>(_bosses);
                ids.Sort();
                sb.Append(string.Join(",", ids));
            }
            return sb.ToString();
        }

        // the nearest living one of that name, or npcID, not you and not taken;
        // "name@k" skips the k nearer
        private static CommonStates Nearest(string word, GameObject player, List<CommonStates> taken)
        {
            int at = word.IndexOf('@');
            int skip = at > 0 && int.TryParse(word.Substring(at + 1), out int k) ? k : 0;
            string name = at > 0 ? word.Substring(0, at) : word;
            bool byId = int.TryParse(name, out int id);
            var found = new List<CommonStates>();
            foreach (CommonStates c in Object.FindObjectsOfType<CommonStates>())
            {
                if (c == null || c.gameObject == player || c.life <= 0.0 || c.dead != 0 || (taken != null && taken.Contains(c))
                    || (byId ? c.npcID != id : c.charaName != name)) { continue; }
                found.Add(c);
            }
            Vector3 from = player.transform.position;
            found.Sort((a, b) => (a.transform.position - from).sqrMagnitude.CompareTo((b.transform.position - from).sqrMagnitude));
            return skip < found.Count ? found[skip] : null;
        }

        // ---------- your people in the fight (Forgejo #98) ----------

        // WHO OF YOURS IS IN THE FIGHT is the game's own: one of yours - a
        // follower, or anyone of yours at home (NPCManager.follower and
        // friendList) - whose target is a living one not of yours, and who is
        // after them (Chase), between blows (Interval), striking (Attack,
        // AttackSP) or struck (Damage). That target is how vanilla fights for you:
        // a follower takes the nearest of those on you (NPCMove.OrderTypeMove,
        // BattlerCheck), any of yours one it sees (Targeting), and a blow makes
        // the striker it (CommonStates.NPCDamage).
        //
        // ONLY ONCE BLOWS ARE TRADED, AND FOR HitWindow AFTER THE LAST. A chase
        // alone is not a fight: the people at home chase a shark they never
        // reach, a few seconds at a time, and their markers blinked on the rim.
        // From a first swing or blow taken it is, for as long as it stays on a
        // foe and a blow came in the last 10 s, as an enemy you hit stays. It
        // drops the target when the foe falls or leaves its reach, and between
        // foes it stands a moment with none: the marker holds FriendHold past
        // that, then goes. Only the fight near you, as an ambush reaches.
        internal const float FriendHold = 3f;
        internal const float FriendReach = 30f;

        private static readonly List<Entry> Friends = new List<Entry>();
        private static readonly Dictionary<CommonStates, Entry> FriendByWho = new Dictionary<CommonStates, Entry>(new SameOne());

        internal static int FriendCount => Friends.Count;
        internal static Entry FriendAt(int i) => Friends[i];
        internal static Entry FriendOf(CommonStates who) => who != null ? Find(who, false, Friends, FriendByWho) : null;

        // one of yours: the target marker's jade (InventoryWindow.EmblemOf), by its stance
        internal static bool Yours(CommonStates who) => Stance.Yours(who);

        private static void TickFriends(GameObject player, NPCManager npcs)
        {
            if (player == null || npcs == null || !Settings.FriendlyMarkers.Value || Settings.CombatMarkersCap <= 0 && !Settings.AlwaysFriendMarkers.Value)
            {
                ClearFriends();
                return;
            }
            Vector3 from = player.transform.position;
            Scan(npcs.follower, player, from);
            Scan(npcs.friendList, player, from);
            for (int i = Friends.Count - 1; i >= 0; i--)
            {
                Entry e = Friends[i];
                if (e.Who == null)
                {
                    Drop(i, Friends, FriendByWho);
                    continue;
                }
                if (e.Seen == _frame) { e.LiveAt = _now; }
                else if (_now - e.LiveAt >= FriendHold)
                {
                    Drop(i, Friends, FriendByWho);
                    continue;
                }
                Vector3 d = e.Who.transform.position - from;
                d.y = 0f;
                e.Distance = d.magnitude;
            }
            if (Friends.Count > 1) { Friends.Sort(FriendRank.Instance); }
        }

        private static void Scan(List<CommonStates> people, GameObject player, Vector3 from)
        {
            if (people == null) { return; }
            for (int i = 0; i < people.Count; i++)
            {
                CommonStates c = people[i];
                // one you struck is on the enemies' list, and wears its marker there
                if (!Fighting(c, player) || ByWho.ContainsKey(c)) { continue; }
                Vector3 d = c.transform.position - from;
                d.y = 0f;
                if (d.sqrMagnitude > FriendReach * FriendReach) { continue; }
                NPCMove.ActType act = c.nMove.actType;
                bool blow = act == NPCMove.ActType.Attack || act == NPCMove.ActType.AttackSP || act == NPCMove.ActType.Damage;
                Entry e = Find(c, blow, Friends, FriendByWho);
                if (e == null) { continue; }
                if (blow) { e.HitAt = _now; }
                if (e.Hit(_now)) { e.Seen = _frame; }
            }
        }

        private static bool Fighting(CommonStates c, GameObject player)
        {
            if (c == null || c.dead != 0 || c.nMove == null) { return false; }
            NPCMove m = c.nMove;
            switch (m.actType)
            {
                case NPCMove.ActType.Chase:
                case NPCMove.ActType.Attack:
                case NPCMove.ActType.AttackSP:
                case NPCMove.ActType.Interval:
                case NPCMove.ActType.Damage:
                    break;
                default:
                    return false;
            }
            GameObject foe = m.tmpEnemy;
            if (foe == null || ReferenceEquals(foe, player) || !c.gameObject.activeInHierarchy || !Yours(c)) { return false; }
            CommonStates them = foe.GetComponent<CommonStates>();
            return them != null && them.dead == 0 && !Yours(them);
        }

        private static void ClearFriends()
        {
            for (int i = Friends.Count - 1; i >= 0; i--) { Drop(i, Friends, FriendByWho); }
        }

        // followers first, the ones beside you, then the nearest; a marked one as
        // if nearer, as the enemies' are
        private sealed class FriendRank : IComparer<Entry>
        {
            internal static readonly FriendRank Instance = new FriendRank();

            private static bool Follows(Entry e) => e.Who != null && e.Who.nMove != null && e.Who.nMove.npcType == NPCMove.NPCType.Follow;

            public int Compare(Entry a, Entry b)
            {
                bool af = Follows(a), bf = Follows(b);
                if (af != bf) { return af ? -1 : 1; }
                float ka = a.Distance * (a.Marked ? MarkedNearer : 1f);
                float kb = b.Distance * (b.Marked ? MarkedNearer : 1f);
                return ka.CompareTo(kb);
            }
        }

        // ---------- the harness's view ----------

        private const int Frames = 300;
        private static readonly long[] TickCost = new long[Frames], DrawCost = new long[Frames];
        private static int _costAt, _drawAt;
        // what the ranking and the markers allocate, while the harness asks
        private static bool _counting;
        private static long _allocated;
        private static int _countedFrames;

        private static void Measure(long ticks)
        {
            TickCost[_costAt] = ticks;
            _costAt = (_costAt + 1) % Frames;
        }

        // the markers' frame (InventoryWindow.TickCombatMarkers)
        internal static void MeasureDraw(long ticks)
        {
            DrawCost[_drawAt] = ticks;
            _drawAt = (_drawAt + 1) % Frames;
        }

        internal static long Heap() => _counting ? System.GC.GetTotalMemory(false) : 0L;

        internal static void Counted(long heap)
        {
            if (!_counting) { return; }
            long grown = System.GC.GetTotalMemory(false) - heap;
            if (grown > 0) { _allocated += grown; }
            _countedFrames++;
        }

        // harness: count the heap's growth across the ranking and the drawing,
        // from now; Probe says what it came to
        internal static string HarnessCount(bool on)
        {
            _counting = on;
            _allocated = 0;
            _countedFrames = 0;
            return on ? "counting" : "off";
        }

        private static void Cost(StringBuilder sb, string name, long[] cost)
        {
            long sum = 0, max = 0;
            for (int i = 0; i < Frames; i++) { sum += cost[i]; max = System.Math.Max(max, cost[i]); }
            double ms = 1000.0 / Stopwatch.Frequency;
            sb.Append(name).Append(" ").Append((sum * ms / Frames).ToString("0.0000")).Append(" ms avg, ")
                .Append((max * ms).ToString("0.0000")).Append(" ms max");
        }

        private static string Foe(GameObject go)
        {
            CommonStates c = go.GetComponent<CommonStates>();
            return c == null ? go.name : c.charaName + "#" + c.npcID + (c.nMove != null ? "/" + c.nMove.npcType : "") + " dead=" + c.dead;
        }

        // harness: the list as ranked now, and what ranking and drawing it cost
        // over the last 300 frames
        internal static string Probe()
        {
            var sb = new StringBuilder();
            sb.Append(Ranked.Count).Append(" in combat, ").Append(OnYouCount).Append(" on you; ");
            Cost(sb, "rank", TickCost);
            sb.Append("; ");
            Cost(sb, "draw", DrawCost);
            if (_counting)
            {
                sb.Append("; heap grew ").Append(_allocated).Append(" B over ").Append(_countedFrames / 2).Append(" frames");
            }
            for (int i = 0; i < Ranked.Count; i++)
            {
                Entry e = Ranked[i];
                CommonStates w = e.Who;
                sb.Append("\n").Append(i + 1).Append(". ").Append(w != null ? w.charaName : "?")
                    .Append(" L").Append(w != null ? w.level : 0)
                    .Append(" hp ").Append(w != null ? w.life.ToString("0") + "/" + w.maxLife.ToString("0") : "-")
                    .Append(" faint ").Append(w != null ? w.faint.ToString("0") + "/" + w.maxFaint.ToString("0") : "-")
                    .Append(" ").Append(e.Distance.ToString("0.0")).Append(" m ").Append(TierName(e.Tier));
                if (e.Hit(_now)) { sb.Append(" hit ").Append((_now - e.HitAt).ToString("0.0")).Append(" s ago"); }
                if (e.OnYou) { sb.Append(" ON YOU"); }
                if (!e.Alive) { sb.Append(" down"); }
                if (e.Marked) { sb.Append(" marked"); }
            }
            sb.Append("\n").Append(Friends.Count).Append(" of yours in it");
            for (int i = 0; i < Friends.Count; i++)
            {
                Entry e = Friends[i];
                CommonStates w = e.Who;
                NPCMove m = w != null ? w.nMove : null;
                sb.Append("\n+").Append(i + 1).Append(". ").Append(w != null ? w.charaName : "?")
                    .Append(" ").Append(m != null ? m.npcType + "/" + m.actType : "-")
                    .Append(" on ").Append(m != null && m.tmpEnemy != null ? Foe(m.tmpEnemy) : "-")
                    .Append(" hp ").Append(w != null ? w.life.ToString("0") + "/" + w.maxLife.ToString("0") : "-")
                    .Append(" faint ").Append(w != null ? w.faint.ToString("0") + "/" + w.maxFaint.ToString("0") : "-")
                    .Append(" ").Append(e.Distance.ToString("0.0")).Append(" m");
                sb.Append(" blow ").Append((_now - e.HitAt).ToString("0.0")).Append(" s ago");
                if (e.Seen != _frame) { sb.Append(" held ").Append((_now - e.LiveAt).ToString("0.0")).Append(" s"); }
                if (e.Marked) { sb.Append(" marked"); }
            }
            return sb.ToString();
        }
    }
}
