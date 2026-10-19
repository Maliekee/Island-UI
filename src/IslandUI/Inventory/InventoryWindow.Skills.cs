using System.Collections.Generic;
using IslandUI.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI
{
    // InventoryWindow — The Skills aspect.
    internal static partial class InventoryWindow
    {
        // The Skills aspect - yours only, as vanilla has it: the skill level with
        // its exp band, the points, then the twelve skills as SkillManager keeps
        // them. Names come from the game's own event texts (what SkillDescription
        // titles its tooltip with), icons off the vanilla rows' LVBG images.
        private static void WriteSkills(List<SheetLine> lines)
        {
            SkillManager sk = _mn != null ? _mn.skillMN : null;
            if (sk == null)
            {
                lines.Add(Row("Skills", "—"));
                return;
            }
            double frac = sk.maxExp > 0.0 ? sk.exp / sk.maxExp : 0.0;
            lines.Add(new SheetLine { Label = "Skill level " + sk.skillLevel, Kind = LineBar,
                Value = sk.exp.ToString("F0") + " / " + sk.maxExp.ToString("F0"),
                Frac = (float)frac, Enabled = true, Tinted = true, Tint = SkillExpTint() });
            lines.Add(Head("Points", sk.skillPoint.ToString()));
            for (int i = 0; sk.skills != null && i < sk.skills.Length; i++)
            {
                int max = SkillMax(i);
                bool maxed = max > 0 && sk.skills[i] >= max;
                int cost = SkillCost(sk, i);
                lines.Add(new SheetLine { Label = SkillName(i), Kind = LineSkill, Arg = i, Icon = SkillIcon(i),
                    Value = "Lv " + sk.skills[i] + "   " + (maxed ? "Max" : cost + " pt"),
                    Enabled = !maxed && sk.skillPoint >= cost, Tip = SkillTip(i) });
            }
        }

        // WHAT A SKILL IS FOR (hovering one explains its purpose), in the game's own words - SkillDescription's text, rebuilt
        // rather than called, since calling it opens vanilla's box: the title, the
        // purpose, then what your level gives now and what the next one gives. The
        // whole ladder is the vanilla box's; a tip keeps the two that matter.
        // Lines Lv1-5 exist for every skill and Lv6 for the four
        // long ones, whose live readout (Self-healing's rate, Carry's weights,
        // Smash's and Grapple's power) follows, computed as vanilla computes it.
        private static string SkillTip(int id)
        {
            SkillManager sk = _mn != null ? _mn.skillMN : null;
            if (sk == null || sk.skills == null || id < 0 || id >= sk.skills.Length || _mn.eventMN == null)
            {
                return null;
            }
            // the sheet rewrites its lines as it repaints: one build per skill,
            // level and language
            int lang = SaveManager.SaveSettingStatic.lang;
            if (lang != _skillTipLang)
            {
                SkillTips.Clear();
                _skillTipLang = lang;
            }
            int key = id * 1000 + sk.skills[id];
            if (SkillTips.TryGetValue(key, out string cached))
            {
                return cached;
            }
            string built = BuildSkillTip(sk, id);
            if (built != null)
            {
                SkillTips[key] = built;
            }
            return built;
        }

        private static readonly Dictionary<int, string> SkillTips = new Dictionary<int, string>();
        private static int _skillTipLang = -1;

        private static string BuildSkillTip(SkillManager sk, int id)
        {
            EventText text;
            try
            {
                text = _mn.eventMN.GetEventText("Skill_" + id.ToString("00"));
            }
            catch (System.Exception)
            {
                return null;
            }
            if (text == null || text.list == null || text.list.Count < 6)
            {
                return null;
            }
            bool longSkill = id == 0 || id == 7 || id == 8 || id == 9;
            int top = longSkill && text.list.Count > 6 ? 6 : 5;
            int lv = sk.skills[id];
            int max = SkillMax(id);
            var sb = new System.Text.StringBuilder();
            sb.Append("<b>").Append(SkillName(id)).Append("</b>");
            string purpose = TipLine(text.list[0].texts);
            if (purpose.Length > 0)
            {
                sb.Append('\n').Append(purpose);
            }
            if (lv >= 1)
            {
                sb.Append("\n\nNow, Lv ").Append(lv).Append(": ").Append(TipLine(text.list[Mathf.Min(lv, top)].texts));
            }
            if ((max == 0 || lv < max) && lv + 1 <= top)
            {
                sb.Append(lv >= 1 ? "\n" : "\n\n").Append("Next, Lv ").Append(lv + 1).Append(": ")
                    .Append(TipLine(text.list[lv + 1].texts));
            }
            string live = longSkill && text.list.Count > 7 ? SkillLive(id, lv, text.list[7].texts) : null;
            if (!string.IsNullOrEmpty(live))
            {
                // vanilla's box prints it green, apart from the ladder
                sb.Append("\n\n<color=#A8D8A0>").Append(live).Append("</color>");
            }
            return sb.ToString();
        }

        private static string TipLine(string s)
        {
            return string.IsNullOrEmpty(s) ? string.Empty : s.Replace("/n", "\n").Trim();
        }

        // UIManager.SkillDescription's last line for the four, figure for figure
        private static string SkillLive(int id, int lv, string line)
        {
            line = TipLine(line);
            switch (id)
            {
                case 0:
                    return line + " " + (lv * 2) + "/5s";
                case 7:
                    return line.Replace("XX", (lv + 1).ToString()).Replace("YY", lv.ToString()).Replace("ZZ", (lv - 1).ToString());
                case 8:
                    return line + " " + (lv < 3 ? 200f : lv < 5 ? 250f : 300f + (lv - 5) * 2f) + "%";
                case 9:
                    return line + " " + (lv < 3 ? 2f : lv < 5 ? 3f : 4f + (lv - 5));
            }
            return null;
        }

        // SkillManager.SkillUp's caps: 10 for Carry, none for Self-healing, Smash
        // and Grapple, 5 for the rest. 0 means no cap.
        private static int SkillMax(int id)
        {
            switch (id)
            {
                case 7: return 10;
                case 0: case 8: case 9: return 0;
                default: return 5;
            }
        }

        // SkillManager.CostCheckSkill
        private static int SkillCost(SkillManager sk, int id)
        {
            return 5 + sk.skills[id] * 5;
        }

        private static readonly string[] SkillFallbackNames =
        {
            "Self-healing", "Stomach", "Harvest", "Patience", "Observation", "Persuasive",
            "Construction", "Carry", "Smash", "Grapple", "Leader", "Swim",
        };
        private static Sprite[] _skillIcons;

        private static string[] _skillNames;

        // The name vanilla's own row shows. Each SkillText_NN carries a
        // LocalizeStringEvent whose StringReference is the localised label; the
        // row's text only receives it once the event has run, which on a panel
        // never opened this session it has not (the rows still read their
        // prefab Japanese, and RefreshString on the inactive component changed
        // nothing). So the string is asked for directly, by reflection, since
        // the Localization package is not referenced. Failing that, the
        // tooltip's title (SkillDescription's) with its parenthetical stripped -
        // it reads "Swim（未実装）" - and failing that, the English list.
        private static string SkillName(int id)
        {
            if (_skillNames == null)
            {
                _skillNames = new string[SkillFallbackNames.Length];
            }
            if (id < 0 || id >= _skillNames.Length)
            {
                return "Skill " + id;
            }
            if (_skillNames[id] != null)
            {
                return _skillNames[id];
            }
            string name = null;
            SkillManager sk = _mn != null ? _mn.skillMN : null;
            if (sk != null && sk.skillsText != null && id < sk.skillsText.Length && sk.skillsText[id] != null)
            {
                name = LocalisedLabel(sk.skillsText[id]);
            }
            if (string.IsNullOrEmpty(name))
            {
                try
                {
                    EventText text = _mn != null && _mn.eventMN != null
                        ? _mn.eventMN.GetEventText("Skill_" + id.ToString("00")) : null;
                    if (text != null && text.list != null && text.list.Count > 0)
                    {
                        name = StripParenthetical(text.list[0].name);
                    }
                }
                catch (System.Exception e)
                {
                    Plugin.Log.LogWarning("Sheet: skill name " + id + " unavailable (" + e.Message + ")");
                }
            }
            if (string.IsNullOrEmpty(name))
            {
                return SkillFallbackNames[id];   // not cached: the game may still answer later
            }
            _skillNames[id] = name.Trim();
            return _skillNames[id];
        }

        private static string StripParenthetical(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return s;
            }
            int cut = s.IndexOfAny(new[] { '(', '（' });
            return cut > 0 ? s.Substring(0, cut).Trim() : s.Trim();
        }

        // LocalizeStringEvent.StringReference.GetLocalizedString(), by name
        private static string LocalisedLabel(GameObject go)
        {
            try
            {
                foreach (Component c in go.GetComponents<Component>())
                {
                    if (c == null || c.GetType().Name != "LocalizeStringEvent")
                    {
                        continue;
                    }
                    System.Reflection.PropertyInfo prop = c.GetType().GetProperty("StringReference");
                    object reference = prop != null ? prop.GetValue(c, null) : null;
                    System.Reflection.MethodInfo get = reference != null
                        ? reference.GetType().GetMethod("GetLocalizedString", System.Type.EmptyTypes) : null;
                    string s = get != null ? get.Invoke(reference, null) as string : null;
                    if (!string.IsNullOrEmpty(s))
                    {
                        return s;
                    }
                }
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning("Sheet: localised label of " + go.name + " unavailable (" + e.Message + ")");
            }
            return null;
        }

        private static Sprite SkillIcon(int id)
        {
            SkillManager sk = _mn != null ? _mn.skillMN : null;
            if (sk == null || sk.skillsText == null || id < 0 || id >= sk.skillsText.Length)
            {
                return null;
            }
            if (_skillIcons == null || _skillIcons.Length != sk.skillsText.Length)
            {
                _skillIcons = new Sprite[sk.skillsText.Length];
            }
            if (_skillIcons[id] == null && sk.skillsText[id] != null)
            {
                Transform box = sk.skillsText[id].transform.Find("LVBG");
                Image img = box != null ? box.GetComponent<Image>() : null;
                _skillIcons[id] = img != null ? img.sprite : null;
            }
            return _skillIcons[id];
        }

        // SkillManager.SkillUp for the played character, minus its menuNPC
        // resolution: the Skills aspect is yours only, so the carrier is you.
        private static void RaiseSkill(int id)
        {
            SkillManager sk = _mn != null ? _mn.skillMN : null;
            if (sk == null || sk.skills == null || id < 0 || id >= sk.skills.Length)
            {
                return;
            }
            int times = Input.GetKey(KeyCode.LeftShift) ? 10 : 1;
            int max = SkillMax(id);
            for (int i = 0; i < times; i++)
            {
                if (max > 0 && sk.skills[id] >= max)
                {
                    break;
                }
                int cost = SkillCost(sk, id);
                if (sk.skillPoint < cost)
                {
                    break;
                }
                sk.skillPoint -= cost;
                sk.skills[id]++;
                sk.SkillsChange(id);   // the skill's effect, as vanilla applies it for the player
            }
            CommonStates player = _mn.gameMN != null && _mn.gameMN.pMove != null ? _mn.gameMN.pMove.common : null;
            if (player != null)
            {
                sk.LoadSkills(player);   // vanilla's own panel, should it be open
            }
        }
    }
}
