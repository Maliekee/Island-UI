using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace IslandUI
{
    // THE OBSERVATION SKILL (Forgejo #75): what you may know of someone who is
    // not yours. Vanilla has one place that shows it, the bar a hit brings up
    // at the top of the screen (GameManager.EnemyLifeChange), and one skill
    // that fills it in, skills[4] in the survivor's one book (SkillManager:
    // SkillsChange case 4 turns the bar's fields on, one a level):
    //   always   the name, and the health and faint bars as bars
    //   1        the level
    //   2        the health numbers
    //   3        the faint numbers
    //   4        the age
    //   5        the personality
    // Nothing else of a stranger is ever shown: the status panel opens on the
    // employed alone (UIManager.NPCMenuOpen), so their attributes, needs and
    // relations are never seen at any level. Yours - the employed - show
    // everything, as that panel does. The rank is read live, so a level
    // learnt mid-fight fills the plaque in at once.
    internal static class Observe
    {
        internal const int Level = 1;
        internal const int Life = 2;
        internal const int Faint = 3;
        internal const int Age = 4;
        internal const int Personality = 5;
        // what vanilla never shows a stranger, at any rank
        internal const int Never = int.MaxValue;

        private const int Skill = 4;

        // the game's own word for what you do not know (UIManager.LoverInfo), and
        // its one-letter form for a coin, as the combat plaque's level has it
        internal static string Unknown => Shared.Loc.Get("observe.unknown", "???");
        internal static string UnknownShort => Shared.Loc.Get("observe.unknown.short", "?");

        internal static int Rank()
        {
            ManagersScript mn = MoodLedger.Managers();
            int[] skills = mn != null && mn.skillMN != null ? mn.skillMN.skills : null;
            return skills != null && skills.Length > Skill ? skills[Skill] : 0;
        }

        // one of yours: the status panel's own test
        internal static bool Known(CommonStates who)
        {
            return who == null || who.Employed();
        }

        internal static bool Sees(CommonStates who, int fact)
        {
            return Known(who) || (fact != Never && Rank() >= fact);
        }

        // For the harness, which cannot spend points: the rank set, and vanilla's
        // bar fields with it as SkillsChange leaves them - "" reads them,
        // "restore" puts back exactly what the first set found. Never saved.
        private static int _heldRank = -1;
        private static bool[] _heldFields;

        internal static string HarnessRank(string arg)
        {
            ManagersScript mn = MoodLedger.Managers();
            GameManager gm = mn != null ? mn.gameMN : null;
            if (gm == null || mn.skillMN == null || mn.skillMN.skills == null) { return "no game"; }
            var fields = new TMPro.TextMeshProUGUI[]
            {
                gm.enemyLevelText, gm.enemyLifeValue, gm.enemyFaintValue, gm.enemyAge, gm.enemyCharaType,
            };
            arg = (arg ?? string.Empty).Trim();
            if (arg == "restore")
            {
                if (_heldRank < 0) { return "nothing held"; }
                mn.skillMN.skills[Skill] = _heldRank;
                for (int i = 0; i < fields.Length; i++) { fields[i].gameObject.SetActive(_heldFields[i]); }
                _heldRank = -1;
                _heldFields = null;
            }
            else if (int.TryParse(arg, out int rank))
            {
                if (_heldRank < 0)
                {
                    _heldRank = mn.skillMN.skills[Skill];
                    _heldFields = new bool[fields.Length];
                    for (int i = 0; i < fields.Length; i++) { _heldFields[i] = fields[i].gameObject.activeSelf; }
                }
                mn.skillMN.skills[Skill] = rank;
                for (int i = 0; i < fields.Length; i++) { fields[i].gameObject.SetActive(rank > i); }
            }
            var sb = new StringBuilder("rank ").Append(mn.skillMN.skills[Skill]).Append(" fields");
            for (int i = 0; i < fields.Length; i++) { sb.Append(' ').Append(fields[i].gameObject.activeSelf ? 1 : 0); }
            if (_heldRank >= 0) { sb.Append(" (held ").Append(_heldRank).Append(')'); }
            return sb.ToString();
        }

        // ...the skill's own words, as its page in the book prints them
        internal static string HarnessText()
        {
            ManagersScript mn = MoodLedger.Managers();
            EventText text = mn != null && mn.eventMN != null ? mn.eventMN.GetEventText("Skill_04") : null;
            if (text == null || text.list == null) { return "no text"; }
            var sb = new StringBuilder();
            for (int i = 0; i < text.list.Count; i++)
            {
                sb.Append(i).Append(": ").Append(text.list[i].name).Append(" | ").Append(text.list[i].texts).Append('\n');
            }
            return sb.ToString();
        }

        // ...and who is about: everyone within 60 m, nearest first
        internal static string HarnessPeople()
        {
            ManagersScript mn = MoodLedger.Managers();
            PlayerMove p = mn != null && mn.gameMN != null ? mn.gameMN.pMove : null;
            if (p == null) { return "no game"; }
            var near = new List<KeyValuePair<float, CommonStates>>();
            foreach (CommonStates c in Object.FindObjectsOfType<CommonStates>())
            {
                if (c == null || ReferenceEquals(c, p.common)) { continue; }
                float d = Vector3.Distance(c.transform.position, p.transform.position);
                if (d <= 60f) { near.Add(new KeyValuePair<float, CommonStates>(d, c)); }
            }
            near.Sort((a, b) => a.Key.CompareTo(b.Key));
            var sb = new StringBuilder();
            foreach (var kv in near)
            {
                CommonStates c = kv.Value;
                sb.Append(string.IsNullOrEmpty(c.charaName) ? "?" : c.charaName).Append(" npc=").Append(c.npcID)
                    .Append(" type=").Append(c.charaType).Append(" employ=").Append(c.employ)
                    .Append(" dead=").Append(c.dead).Append(" lv=").Append(c.level).Append(" age=").Append(c.age)
                    .Append(" d=").Append(kv.Key.ToString("0")).Append(" | ");
            }
            return sb.Length > 0 ? sb.ToString() : "nobody";
        }
    }
}
