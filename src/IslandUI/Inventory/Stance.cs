using System.Collections.Generic;
using UnityEngine;

namespace IslandUI
{
    // WHAT THEY ARE TO YOU, from what they have SHOWN you (docs/target-matrix.md D4).
    // Nothing is read from what a creature is to guess what it will do - not its
    // charaType (100 is only "a click attacks it"), not its npcType, not a patrol.
    // It is hostile once it has come for you, and remembered from then on.
    //
    // CAME FOR YOU is the game's own state: you are its target (NPCMove.tmpEnemy)
    // and it chases or strikes - the "!" over its head when it spots you
    // (NPCMove.Targeting), the fight a blow starts (CommonStates' damage sets it).
    // Read every frame from the game's list of those after you, GameManager.battler,
    // which drops its own (GameManager.cs:684). Fleeing is not hostility (RunAway):
    // the deer you hit, the bird you startle, stay neutral. Striking back is: a
    // villager you hit that chases you is hostile from then on.
    //
    // COMING FOR ONE OF YOURS is coming for you: the same test with a follower as
    // its target. Those never enter battler (NPCMove.Targeting adds only the ones
    // after the player), so they are read from the people of the scene
    // (ScenePeople) twice a second.
    //
    // A STORY FIGHT'S SIDE is hostile while the fight lasts - CombatList.StorySide,
    // the boss case, kept with the fight and out of these rules.
    //
    // REMEMBERED for the session, per individual; FORGOTTEN when the game makes them
    // yours - employed and out of revolt. The revolting follower (discontent 4) is the
    // case: still employed, but targetable, so neutral (amber) until it comes for you
    // or yours, then hostile; brought to zero life the game calms it (+20 morale, its
    // target dropped: NPCMove.cs:2698), the revolt ends and it is yours again. A
    // recruited captive the same way.
    internal enum StanceKind
    {
        Yours,
        Neutral,
        Hostile,
        Body,
    }

    internal static class Stance
    {
        private static readonly HashSet<int> Shown = new HashSet<int>();   // instance ids: hostile, remembered
        private static readonly List<int> Forget = new List<int>();
        private static readonly Dictionary<int, CommonStates> ShownWho = new Dictionary<int, CommonStates>();
        private static readonly List<CommonStates> People = new List<CommonStates>();
        private const float ScanEvery = .5f;
        private static int _frame = -1;
        private static float _scanAt = float.NegativeInfinity;
        private static int _yourGroup;

        // the game's own "after you": you are its target, and it is not running from you
        private static bool CameFor(NPCMove m, GameObject player)
        {
            return m != null && player != null && ReferenceEquals(m.tmpEnemy, player) && Coming(m);
        }

        // ...or one of yours is: a follower it chases or strikes
        private static bool CameForYours(NPCMove m)
        {
            if (m == null || m.tmpEnemy == null || !Coming(m)) { return false; }
            CommonStates t = m.tmpEnemy.GetComponent<CommonStates>();
            return t != null && Yours(t);
        }

        // chasing or striking its target, not running from it
        private static bool Coming(NPCMove m)
        {
            switch (m.actType)
            {
                case NPCMove.ActType.Chase:
                case NPCMove.ActType.Attack:
                case NPCMove.ActType.AttackSP:
                case NPCMove.ActType.Interval:
                    return true;
                default:
                    return false;
            }
        }

        // once a frame: remember who came for you, forget who the game made yours
        internal static void Tick(ManagersScript mn)
        {
            if (Time.frameCount == _frame) { return; }
            _frame = Time.frameCount;
            GameManager game = mn != null ? mn.gameMN : null;
            GameObject player = game != null ? game.player : null;
            CommonStates you = player != null ? player.GetComponent<CommonStates>() : null;
            if (you != null) { _yourGroup = you.groupID; }
            if (game != null && game.battler != null && player != null)
            {
                for (int i = 0; i < game.battler.Count; i++)
                {
                    NPCMove m = game.battler[i];
                    if (m == null || !CameFor(m, player)) { continue; }
                    CommonStates c = m.common;
                    if (c == null || c.gameObject == player) { continue; }
                    Show(c, "came for you");
                }
            }
            if (player != null && Time.time - _scanAt >= ScanEvery)
            {
                _scanAt = Time.time;
                foreach (CommonStates c in ScenePeople.Live(People))
                {
                    if (c == null || c.gameObject == player || c.dead != 0 || Yours(c) || !CameForYours(c.nMove)) { continue; }
                    Show(c, "came for " + c.nMove.tmpEnemy.name);
                }
            }
            if (Shown.Count == 0) { return; }
            Forget.Clear();
            foreach (KeyValuePair<int, CommonStates> kv in ShownWho)
            {
                CommonStates c = kv.Value;
                if (c == null || c.Employed() && !Rebelling(c))
                {
                    Forget.Add(kv.Key);
                }
            }
            for (int i = 0; i < Forget.Count; i++)
            {
                Shown.Remove(Forget[i]);
                ShownWho.Remove(Forget[i]);
            }
        }

        private static void Show(CommonStates c, string why)
        {
            int id = c.GetInstanceID();
            if (Shown.Add(id))
            {
                ShownWho[id] = c;
                Plugin.Log.LogInfo("Stance: " + c.charaName + " " + why + " - hostile from now on");
            }
        }

        internal static bool Remembered(CommonStates who) => who != null && Shown.Contains(who.GetInstanceID());

        // in revolt (discontent 4): still employed, but fair game - a click attacks it
        internal static bool Rebelling(CommonStates who) => who != null && who.debuff != null && who.debuff.discontent >= 4;

        // employed, out of revolt and not remembered against you: green
        internal static bool Yours(CommonStates who) => who != null && who.Employed() && !Rebelling(who) && !Remembered(who);

        // what they are to you, alive or not: the colour of their bar
        internal static StanceKind Living(CommonStates who)
        {
            if (who == null) { return StanceKind.Neutral; }
            if (Remembered(who)) { return StanceKind.Hostile; }
            if (who.Employed()) { return Rebelling(who) ? StanceKind.Neutral : StanceKind.Yours; }
            return CombatList.StorySide(who, _yourGroup) ? StanceKind.Hostile : StanceKind.Neutral;
        }

        // ...and the marker's: a body is grey over the rest
        internal static StanceKind Of(CommonStates who)
        {
            return who != null && who.dead != 0 ? StanceKind.Body : Living(who);
        }

        // the health fill in the stance's colour - the plaque, the target marker, the light markers
        internal static string HealthFill(CommonStates who)
        {
            switch (Living(who))
            {
                case StanceKind.Yours: return "combat_hp_fill_jade";
                case StanceKind.Hostile: return "combat_hp_fill";
                default: return "combat_hp_fill_amber";
            }
        }

        // harness: who is remembered
        internal static string Probe()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(Shown.Count).Append(" remembered hostile");
            foreach (CommonStates c in ShownWho.Values)
            {
                if (c != null) { sb.Append("\n").Append(c.charaName).Append(" dead=").Append(c.dead).Append(" employ=").Append(c.employ); }
            }
            return sb.ToString();
        }
    }
}
