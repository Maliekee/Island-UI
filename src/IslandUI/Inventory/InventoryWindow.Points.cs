using IslandUI.UI;
using UnityEngine;

namespace IslandUI
{
    // POINTS TO SPEND (PointsTag): when a tag shows, and the Player dock's pair.
    //
    // A tag shows only while its points BUY something: a point may cost more than 1,
    // so at least one skill or attribute has to be able to level up before it shows.
    // Skill points: the cheapest next level
    // of a skill not at its cap, 5 + 5 per level (SkillCost). Attribute points: the
    // cheapest next point of Health, Strength or Speed, 1 + 1 per 10 it has (StatCost).
    // Points left over below both prices say nothing - they are not a thing to do.
    internal static partial class InventoryWindow
    {
        private static bool SkillsToSpend(out int points)
        {
            SkillManager sk = _mn != null ? _mn.skillMN : null;
            points = sk != null ? sk.skillPoint : 0;
            if (sk == null || sk.skills == null || points <= 0)
            {
                return false;
            }
            for (int i = 0; i < sk.skills.Length; i++)
            {
                int max = SkillMax(i);
                if ((max <= 0 || sk.skills[i] < max) && points >= SkillCost(sk, i))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool AttributesToSpend(CommonStates who)
        {
            if (who == null || who.statusPoint <= 0 || who.status == null || who.status.Length < StatNames.Length)
            {
                return false;
            }
            for (int i = 0; i < StatNames.Length; i++)
            {
                if (who.statusPoint >= StatCost(who, i))
                {
                    return true;
                }
            }
            return false;
        }

        private static CommonStates You()
        {
            return _mn != null && _mn.gameMN != null && _mn.gameMN.pMove != null ? _mn.gameMN.pMove.common : null;
        }

        private static string AttributeTip(CommonStates who)
        {
            return "<b>" + Shared.Loc.Get("player.points.attributes", "Attribute points to spend") + "</b>\n" + who.statusPoint;
        }

        // ---- the Player dock: your Your character chip wears both ----

        private static PointsTag _dockSkills, _dockAttributes;

        // Every HUD tick, beside the chips, each only while its points buy something: on
        // the chip's top-right corner, the skill book's rightmost and attributes' to its
        // left (the blue skill icon on the right, the yellow attribute icon on the left;
        // one alone sits on the right).
        private static void TickDockPoints(HudEntry details)
        {
            if (details == null || details.Face == null)
            {
                return;
            }
            Transform dock = details.Face.transform.parent;
            // made after the chips, so they draw over them; a rebuilt HUD makes them again
            if (_dockSkills == null || _dockSkills.Rect == null)
            {
                _dockSkills = PointsTag.Create(dock, "SkillPoints", true);
                _dockAttributes = PointsTag.Create(dock, "AttributePoints", true);
            }
            CommonStates you = You();
            bool skills = SkillsToSpend(out int sp);
            bool attributes = AttributesToSpend(you);
            if (skills) { _dockSkills.Show(PointsTag.Kind.Skill, sp.ToString(), PointsTag.DockPill); } else { _dockSkills.Hide(); }
            if (attributes) { _dockAttributes.Show(PointsTag.Kind.Attribute, you.statusPoint.ToString(), PointsTag.DockPill); } else { _dockAttributes.Hide(); }
            if (!skills && !attributes)
            {
                return;
            }
            const float gap = 1f;
            var face = details.Face.rectTransform;
            float corner = face.anchoredPosition.x + face.sizeDelta.x - 1f;
            float y = -face.anchoredPosition.y - PointsTag.DockPill * 0.5f;
            // the rightmost tag is centred on the corner, the other laid to its left
            PointsTag last = skills ? _dockSkills : _dockAttributes;
            float x = corner - last.Width * 0.5f;
            Theme.Place(last.Rect, x, y, last.Width, PointsTag.DockPill);
            if (skills && attributes)
            {
                Theme.Place(_dockAttributes.Rect, x - gap - _dockAttributes.Width, y, _dockAttributes.Width, PointsTag.DockPill);
            }
            // a level-up bumps the tag it paid into (InventoryWindow.LevelUp.cs)
            if (skills) { ScaleFromMiddle(_dockSkills.Rect, PopScale(SkillBurst, .45f)); }
            if (attributes) { ScaleFromMiddle(_dockAttributes.Rect, PopScale(CharacterBurst, .45f)); }
        }

        // the dock's tags as words, for the harness: "skills 41, attributes 80"
        internal static string PointsDump()
        {
            CommonStates you = You();
            bool skills = SkillsToSpend(out int sp);
            return "skills " + sp + (skills ? " (to spend)" : "") + ", attributes "
                + (you != null ? you.statusPoint.ToString() : "?") + (AttributesToSpend(you) ? " (to spend)" : "")
                + "; dock " + (_dockSkills != null && _dockSkills.Shown ? "S" : "-")
                + (_dockAttributes != null && _dockAttributes.Shown ? "A" : "-");
        }
    }
}
