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
        private const string TipAlwaysRun = "Run whenever you move, as if the run key were held.";
        private const string TipShowName = "Their name floats above them in the world.";
        private const string TipShowNameOwn = "Your name floats above you in the world.";
        private const string TipFavourite = "Listed under Favorites in the roster, and followed on the map.";

        private const string TipLevel = "Character level, this person's own. Each level-up gives attribute points: 1 below level 10, 2 from 10, 3 from 20, and so on.";
        private const string TipExpOwn = "Experience, from gathering, kills, work and some foods. The same EXP also fills the skill level. A full bar is a level; the rest carries over.";
        private const string TipExpTheirs = "Experience. A full bar is a level; the rest carries over. A follower earns a quarter of yours.";
        private const string TipSurgeries = "Body shapes or colours changed on the operating bed.";

        private const string TipHpOwn = "Hit points. At 0 you lose a heart and go down; losing the last heart kills you.";
        private const string TipHpTheirs = "Hit points. At 0 they go down; one of your people gets up after 90 seconds with a little HP.";
        private const string TipStunOwn = "Stun. Stun weapons drain it and sleeping in a bed refills it. At 0 you die, hearts or not.";
        private const string TipStunTheirs = "Stun. Stun weapons drain it; resting refills it, using water. At 0 they are knocked out.";
        private const string TipFoodOwn = "Food. Empty, you lose HP over time and stop healing; dying of hunger skips your hearts.";
        private const string TipFoodTheirs = "Food. Resting, they spend it to heal; empty, they stop healing.";
        private const string TipWaterOwn = "Water. Empty, you lose HP over time and stop healing; dying of thirst skips your hearts.";
        private const string TipWaterTheirs = "Water. Spent to recover stun and to work; empty, they do neither.";
        private const string TipLibidoTheirs = "Libido. Refills over time. At 20 they seek sex with a lover; past 90 a refusal costs moral.";
        private const string TipMoralOwn = "Moral. A villager in revolt attacks you with a chance of 100 minus this, in percent.";
        private const string TipMoralTheirs = "Moral. At -100 they revolt and fight. Sleep, sex and some foods raise it; rejection lowers it.";
        private const string TipPregnant = "How far along the pregnancy is.";

        private const string TipAttributePoints = "Points to spend on attributes. A level-up gives 1 below level 10, 2 from 10, 3 from 20, and so on.";
        private static readonly string[] TipAttributes =
        {
            "Health. Each point raises max HP by what their kind gains per point.",
            "Strength. Each point raises base attack by what their kind gains per point.",
            "Speed. Each point adds 0.005 to base move speed.",
        };
        private const string TipAttributeCost = "\nA point costs 1, plus 1 for every 10 already bought.";

        private const string TipAttack = "Base attack. A hit deals it plus the weapon's attack (in brackets), less the target's guard.";
        private const string TipGuardOwn = "Percent of damage blocked while you guard: your shield's value, else 25 armed or 10 bare-handed.";
        private const string TipGuardTheirs = "The guard figure the game shows. Only you can guard.";
        private const string TipSpeed = "Walking speed after equipment, overload and slowing; running multiplies it.";
        private const string TipClass = "Combat class: changes attack reach, timing and animations, and gives some kinds special attacks.";

        private const string TipPersonality = "Villagers who like this personality grow fonder; those who dislike it lose affection.";
        private const string TipLikes = "Villagers nearby with this personality win love twice as fast.";
        private const string TipDislikes = "Villagers nearby with this personality lose love instead of gaining it.";
        private const string TipParent = "Recorded when they were born to a villager.";

        private const string TipFirstPartner = "The first person they had sex with, consenting or not.";
        private static readonly string[] TipHistory =
        {
            "Sex acts with every partner, consenting or forced, either way.",
            "Times they became pregnant.",
            "Children they gave birth to, by a father among their lovers.",
            "Times they masturbated alone.",
        };

        private const string TipSkillLevel = "Skill level, one for the whole save rather than per character. It earns the EXP you earn, beside your character level; each level gives skill points: 1 below level 10, 2 from 10, 3 from 20, and so on.";
        private const string TipSkillExp = "The skill level's EXP. A full bar is a level.";
        private const string TipSkillPoints = "Skill points to spend. The next level of a skill costs 5, plus 5 for each level it has.";

        private const string TipFollowing = "They follow you and earn a quarter of your EXP. Each follower takes room in your party.";
        private static readonly string[] TipStanding =
        {
            "Click the ground to set their place; right-click cancels.",
            "Sends them to their place at once. Not for followers or prisoners.",
        };
        private const string TipTerritory = "How far they wander around their place. At 0 they stay on the spot.";
        private const string TipCombatArea = "How far past their territory they chase an enemy before giving up. Followers ignore it.";
        private const string TipWaitingPose = "What they do while waiting on their place - only with territory 0, when not following.";
        private const string TipFacing = "Which way they face on their place, with territory 0. Pressing the chosen one clears it.";
        private const string TipEditDialogue = "Change the lines they say.";
        private static readonly string[] TipOrderActions =
        {
            "Sends them to their place at once. Not for followers or prisoners.",
            "Brings them to you, if they are within 10 m. Not during events.",
            "After you confirm, they leave as an outlaw: they fight back if hit, and are gone after 2 minutes unless downed.",
        };

        // the Communication permissions, by the sexInfo flag each one toggles
        private static string TipPermission(int id)
        {
            switch (id)
            {
                case 7: return "Lets them go in and out of houses by themselves, and home to sleep at night.";
                case 8: return "Lets them build love with the villagers nearby, and with you while following.";
                case 9: return "Lets them seek sex, or accept it, once their libido is 20 or more.";
                case 10: return "Lets them force sex on a nearby villager once their libido is 5 or more.";
            }
            return null;
        }
    }
}
