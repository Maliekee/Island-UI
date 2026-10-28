namespace IslandUI
{
    // THE SHEETS' HOVER TIPS: hovering a part of a sheet explains its purpose.
    // Vanilla has words for the skills (Skill_NN, see
    // SkillTip) and for the stances and jobs (Orders.cs); for everything else it
    // has labels only (TextManager.descText), so these are ours - each one what the
    // decompiled code does, not what the label suggests. Where you and a villager differ there are two.
    // A figure the code does not settle - how much HP a Health point buys is per
    // species, in prefab data - is left out rather than guessed.
    internal static partial class InventoryWindow
    {
        // Every tip is read at use, in the player's language. A "Label  value" row is
        // joined here with two spaces (TipRow), never inside a translation.
        private static string TipHead(string title) => "<b>" + title + "</b>";
        private static string TipRow(string label, string value) => label + "  " + value;

        // the level-up table, shared by the character level, attribute points and skill level
        private static string TipLevelSteps() =>
            TipRow(Shared.Loc.Get("inv.tip.below_level", "Below level {0}", 10), "1") + "\n"
            + TipRow(Shared.Loc.Get("inv.tip.from_level", "From level {0}", 10), "2") + "\n"
            + TipRow(Shared.Loc.Get("inv.tip.from_level", "From level {0}", 20), Shared.Loc.Get("inv.tip.and_so_on", "{0}, and so on", 3));

        private static string TipAlwaysRun => Shared.Loc.Get("inv.tip.always_run", "Run whenever you move, as if the run key were held.");
        // the SPEED caption's: the toggle's key is named where the keymap has put it
        private static string TipAlwaysRunKey
        {
            get
            {
                UnityEngine.KeyCode key = KeyRemap.Bound(UnityEngine.KeyCode.X);
                return key == UnityEngine.KeyCode.None ? TipAlwaysRun
                    : TipAlwaysRun + "\n" + Shared.Loc.Get("inv.tip.always_run.key", "{0} switches it too.", UI.KitKeyBind.Name(key));
            }
        }
        private static string TipShowName => Shared.Loc.Get("inv.tip.show_name", "Their name floats above them in the world.");
        private static string TipShowNameOwn => Shared.Loc.Get("inv.tip.show_name_own", "Your name floats above you in the world.");
        private static string TipFavourite => Shared.Loc.Get("inv.tip.favourite", "Listed under Favorites in the roster, and followed on the map.");

        private static string TipLevel => TipHead(Shared.Loc.Get("inv.tip.level", "Character level")) + "\n"
            + Shared.Loc.Get("inv.tip.level.own", "This person's own. Each level-up gives attribute points:") + "\n" + TipLevelSteps();
        private static string TipExpOwn => TipHead(Shared.Loc.Get("inv.tip.exp", "Experience")) + "\n"
            + TipRow(Shared.Loc.Get("inv.tip.exp.from", "From"), Shared.Loc.Get("inv.tip.exp.from_what", "gathering, kills, work and some foods")) + "\n"
            + TipRow(Shared.Loc.Get("inv.tip.exp.also_fills", "Also fills"), Shared.Loc.Get("inv.tip.exp.skill_level", "the skill level")) + "\n"
            + Shared.Loc.Get("inv.tip.exp.full_bar", "A full bar is a level; the rest carries over.");
        private static string TipExpTheirs => TipHead(Shared.Loc.Get("inv.tip.exp", "Experience")) + "\n"
            + Shared.Loc.Get("inv.tip.exp.full_bar", "A full bar is a level; the rest carries over.") + "\n"
            + TipRow(Shared.Loc.Get("inv.tip.exp.as_follower", "As a follower"), Shared.Loc.Get("inv.tip.exp.quarter", "a quarter of yours"));
        private static string TipSurgeries => Shared.Loc.Get("inv.tip.surgeries", "Body shapes or colours changed on the operating bed.");

        private static string TipHpOwn => TipHead(Shared.Loc.Get("inv.tip.hp", "Hit points")) + "\n"
            + TipRow(Shared.Loc.Get("inv.tip.at_0", "At 0"), Shared.Loc.Get("inv.tip.hp.lose_heart", "you lose a heart and go down")) + "\n"
            + TipRow(Shared.Loc.Get("inv.tip.hp.last_heart", "At 0, last heart"), Shared.Loc.Get("inv.tip.hp.you_die", "you die"));
        private static string TipHpTheirs => TipHead(Shared.Loc.Get("inv.tip.hp", "Hit points")) + "\n"
            + TipRow(Shared.Loc.Get("inv.tip.at_0", "At 0"), Shared.Loc.Get("inv.tip.hp.they_go_down", "they go down")) + "\n"
            + TipRow(Shared.Loc.Get("inv.tip.hp.one_of_yours", "One of yours"), Shared.Loc.Get("inv.tip.hp.gets_up", "gets up after 90 seconds with a little HP"));
        private static string TipStunOwn => TipHead(Shared.Loc.Get("inv.tip.stun", "Stun")) + "\n"
            + TipRow(Shared.Loc.Get("inv.tip.drained_by", "Drained by"), Shared.Loc.Get("inv.tip.stun.weapons", "stun weapons")) + "\n"
            + TipRow(Shared.Loc.Get("inv.tip.refilled_by", "Refilled by"), Shared.Loc.Get("inv.tip.stun.bed", "sleeping in a bed")) + "\n"
            + TipRow(Shared.Loc.Get("inv.tip.at_0", "At 0"), Shared.Loc.Get("inv.tip.stun.you_die", "you die, hearts or not"));
        private static string TipStunTheirs => TipHead(Shared.Loc.Get("inv.tip.stun", "Stun")) + "\n"
            + TipRow(Shared.Loc.Get("inv.tip.drained_by", "Drained by"), Shared.Loc.Get("inv.tip.stun.weapons", "stun weapons")) + "\n"
            + TipRow(Shared.Loc.Get("inv.tip.refilled_by", "Refilled by"), Shared.Loc.Get("inv.tip.stun.resting", "resting, which uses water")) + "\n"
            + TipRow(Shared.Loc.Get("inv.tip.at_0", "At 0"), Shared.Loc.Get("inv.tip.stun.knocked_out", "they are knocked out"));
        private static string TipFoodOwn => TipHead(Shared.Loc.Get("inv.tip.food", "Food")) + "\n"
            + TipRow(Shared.Loc.Get("inv.tip.empty", "Empty"), Shared.Loc.Get("inv.tip.lose_hp", "you lose HP over time and stop healing")) + "\n"
            + Shared.Loc.Get("inv.tip.food.hunger", "Dying of hunger skips your hearts.");
        private static string TipFoodTheirs => TipHead(Shared.Loc.Get("inv.tip.food", "Food")) + "\n"
            + TipRow(Shared.Loc.Get("inv.tip.resting", "Resting"), Shared.Loc.Get("inv.tip.food.spend_to_heal", "they spend it to heal")) + "\n"
            + TipRow(Shared.Loc.Get("inv.tip.empty", "Empty"), Shared.Loc.Get("inv.tip.food.stop_healing", "they stop healing"));
        private static string TipWaterOwn => TipHead(Shared.Loc.Get("inv.tip.water", "Water")) + "\n"
            + TipRow(Shared.Loc.Get("inv.tip.empty", "Empty"), Shared.Loc.Get("inv.tip.lose_hp", "you lose HP over time and stop healing")) + "\n"
            + Shared.Loc.Get("inv.tip.water.thirst", "Dying of thirst skips your hearts.");
        private static string TipWaterTheirs => TipHead(Shared.Loc.Get("inv.tip.water", "Water")) + "\n"
            + TipRow(Shared.Loc.Get("inv.tip.spent_on", "Spent on"), Shared.Loc.Get("inv.tip.water.stun_work", "recovering stun, and work")) + "\n"
            + TipRow(Shared.Loc.Get("inv.tip.empty", "Empty"), Shared.Loc.Get("inv.tip.water.neither", "they do neither"));
        private static string TipLibidoTheirs => TipHead(Shared.Loc.Get("inv.tip.libido", "Libido")) + "\n"
            + Shared.Loc.Get("inv.tip.libido.refills", "Refills over time.") + "\n"
            + TipRow(Shared.Loc.Get("inv.tip.at_n", "At {0}", 20), Shared.Loc.Get("inv.tip.libido.seek", "they seek sex with a lover")) + "\n"
            + TipRow(Shared.Loc.Get("inv.tip.past_n", "Past {0}", 90), Shared.Loc.Get("inv.tip.libido.refusal", "a refusal costs moral"));
        private static string TipMoralOwn => TipHead(Shared.Loc.Get("inv.tip.moral", "Moral")) + "\n"
            + Shared.Loc.Get("inv.tip.moral.revolt_chance", "A villager in revolt attacks you with a chance of 100 minus this, in percent.");
        private static string TipMoralTheirs => TipHead(Shared.Loc.Get("inv.tip.moral", "Moral")) + "\n"
            + TipRow(Shared.Loc.Get("inv.tip.at_n", "At {0}", -100), Shared.Loc.Get("inv.tip.moral.revolt", "they revolt and fight")) + "\n"
            + TipRow(Shared.Loc.Get("inv.tip.raised_by", "Raised by"), Shared.Loc.Get("inv.tip.moral.raised", "sleep, sex, some foods")) + "\n"
            + TipRow(Shared.Loc.Get("inv.tip.lowered_by", "Lowered by"), Shared.Loc.Get("inv.tip.moral.lowered", "rejection"));
        private static string TipPregnant => Shared.Loc.Get("inv.tip.pregnant", "How far along the pregnancy is.");
        private static string TipRename => Shared.Loc.Get("inv.tip.rename", "Click to rename. Return keeps it, Escape keeps the old name.");

        private static string TipAttributePoints => TipHead(Shared.Loc.Get("inv.tip.attribute_points", "Attribute points")) + "\n"
            + Shared.Loc.Get("inv.tip.attribute_points.spend", "To spend on attributes. A level-up gives:") + "\n" + TipLevelSteps();
        private static string[] TipAttributes => new[]
        {
            TipHead(Shared.Loc.Get("inv.tip.health", "Health")) + "\n" + Shared.Loc.Get("inv.tip.health.help", "Each point raises max HP by what their kind gains per point."),
            TipHead(Shared.Loc.Get("inv.tip.strength", "Strength")) + "\n" + Shared.Loc.Get("inv.tip.strength.help", "Each point raises base attack by what their kind gains per point."),
            TipHead(Shared.Loc.Get("inv.tip.speed", "Speed")) + "\n" + Shared.Loc.Get("inv.tip.speed_point.help", "Each point adds 0.005 to base move speed."),
        };
        private static string TipAttributeCost => "\n\n" + TipRow(Shared.Loc.Get("inv.tip.cost", "Cost"), Shared.Loc.Get("inv.tip.attribute_cost", "1 point, plus 1 for every 10 already bought"));

        private static string TipAttack => TipHead(Shared.Loc.Get("inv.tip.attack", "Base attack")) + "\n"
            + TipRow(Shared.Loc.Get("inv.tip.attack.deals", "A hit deals"), Shared.Loc.Get("inv.tip.attack.help", "this, plus the weapon's attack (in brackets), less the target's guard"));
        private static string TipGuardOwn => TipHead(Shared.Loc.Get("inv.tip.guard", "Guard")) + "\n"
            + Shared.Loc.Get("inv.tip.guard.percent", "Percent of damage blocked while you guard:") + "\n"
            + TipRow(Shared.Loc.Get("inv.tip.guard.shield", "With a shield"), Shared.Loc.Get("inv.tip.guard.shield_value", "the shield's value")) + "\n"
            + TipRow(Shared.Loc.Get("inv.tip.guard.armed", "Armed"), "25") + "\n"
            + TipRow(Shared.Loc.Get("inv.tip.guard.bare", "Bare-handed"), "10");
        private static string TipGuardTheirs => TipHead(Shared.Loc.Get("inv.tip.guard", "Guard")) + "\n"
            + Shared.Loc.Get("inv.tip.guard.theirs", "The figure the game shows. Only you can guard.");
        private static string TipSpeed => TipHead(Shared.Loc.Get("inv.tip.speed", "Speed")) + "\n"
            + Shared.Loc.Get("inv.tip.speed.help", "Walking speed after equipment, overload and slowing; running multiplies it.");
        private static string TipClass => TipHead(Shared.Loc.Get("inv.tip.class", "Combat class")) + "\n"
            + Shared.Loc.Get("inv.tip.class.help", "Changes attack reach, timing and animations, and gives some kinds special attacks.");


        private static string TipFirstPartner => Shared.Loc.Get("inv.tip.first_partner", "The first person they had sex with, consenting or not.");
        private static string[] TipHistory => new[]
        {
            Shared.Loc.Get("inv.tip.history.sex", "Sex acts with every partner, consenting or forced, either way."),
            Shared.Loc.Get("inv.tip.history.pregnant", "Times they became pregnant."),
            Shared.Loc.Get("inv.tip.history.births", "Children they gave birth to, by a father among their lovers."),
            Shared.Loc.Get("inv.tip.history.masturbated", "Times they masturbated alone."),
        };

        private static string TipSkillLevel => TipHead(Shared.Loc.Get("inv.tip.skill_level", "Skill level")) + "\n"
            + Shared.Loc.Get("inv.tip.skill_level.help", "One for the whole save, not per character. It earns the EXP you earn, beside your character level. Each level gives skill points:") + "\n" + TipLevelSteps();
        private static string TipSkillExp => Shared.Loc.Get("inv.tip.skill_exp", "The skill level's EXP. A full bar is a level.");
        private static string TipSkillPoints => TipHead(Shared.Loc.Get("inv.tip.skill_points", "Skill points")) + "\n"
            + Shared.Loc.Get("inv.tip.skill_points.spend", "To spend on skills.") + "\n"
            + TipRow(Shared.Loc.Get("inv.tip.skill_points.next", "Next level costs"), Shared.Loc.Get("inv.tip.skill_points.cost", "5, plus 5 for each level the skill has"));

        private static string TipFollowing => Shared.Loc.Get("inv.tip.following", "They follow you and earn a quarter of your EXP. Each follower takes room in your party.");
        private static string TipSendToPlace => Shared.Loc.Get("inv.tip.send_to_place", "Sends them to their place at once. Not for followers or prisoners.");
        private static string[] TipStanding => new[]
        {
            Shared.Loc.Get("inv.tip.set_place", "Click the ground to set their place; right-click cancels."),
            TipSendToPlace,
        };
        private static string TipTerritory => Shared.Loc.Get("inv.tip.territory", "How far they wander around their place. At 0 they stay on the spot.");
        private static string TipChaseRange => Shared.Loc.Get("inv.tip.combat_area", "How far past their territory they chase an enemy before giving up. Followers ignore it.");
        private static string TipWaitingPose => Shared.Loc.Get("inv.tip.waiting_pose", "What they do while waiting on their place - only with territory 0, when not following.");
        private static string TipFacing => Shared.Loc.Get("inv.tip.facing", "Which way they face on their place, with territory 0. Pressing the chosen one clears it.");
        private static string TipFacingLeft => TipHead(Shared.Loc.Get("inv.tip.face_left", "Face left")) + "\n" + TipFacing;
        private static string TipFacingRight => TipHead(Shared.Loc.Get("inv.tip.face_right", "Face right")) + "\n" + TipFacing;
        private static string TipFollowingOn => TipHead(Shared.Loc.Get("inv.tip.following_on", "Following you"));
        private static string TipFollowingOff => TipHead(Shared.Loc.Get("inv.tip.following_off", "Not following: they keep to their place"));
        private static string TipPlaceWhileFollowing => Shared.Loc.Get("inv.tip.place_while_following", "While they follow you their place waits: it takes over again when they stop.");
        private static string TipEditDialogue => Shared.Loc.Get("inv.tip.edit_dialogue", "Change the lines they say.");
        private static string[] TipOrderActions => new[]
        {
            Shared.Loc.Get("inv.tip.call_over", "Brings them to you, if they are within 10 m. Not during events."),
            Shared.Loc.Get("inv.tip.outlaw", "After you confirm, they leave as an outlaw: they fight back if hit, and are gone after 2 minutes unless downed."),
        };

        // the Permissions toggles, by the sexInfo flag each one toggles
        private static string TipPermission(int id)
        {
            switch (id)
            {
                case 7: return Shared.Loc.Get("inv.tip.perm.houses", "Lets them go in and out of houses by themselves, and home to sleep at night.");
                case 8: return Shared.Loc.Get("inv.tip.perm.love", "Lets them build love with the villagers nearby, and with you while following.");
                case 9: return Shared.Loc.Get("inv.tip.perm.sex", "Lets them seek sex, or accept it, once their libido is 20 or more.");
                case 10: return Shared.Loc.Get("inv.tip.perm.force", "Lets them force sex on a nearby villager once their libido is 5 or more.");
            }
            return null;
        }
    }
}
