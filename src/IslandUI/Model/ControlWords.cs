namespace IslandUI
{
    // THE GAME'S WORDS ON OUR CONTROLS, IN A LANGUAGE THE GAME HAS NO TEXT FOR (Forgejo #110).
    //
    // The target dock's verbs, a prop's acts, the waiting poses and the skill names are the
    // game's words, read off its buttons and text tables. In Japanese, English and Chinese
    // they stay the game's (GameWords: in the mods' language where the game has it). In
    // Russian, Portuguese, Spanish and German the game has none, and a fan translation most
    // likely writes over its English - so the game still reports English, the player picks
    // their language on our Language page, and a control of ours would wear the game's
    // English or the fan's words, whichever is installed. These keys are ours instead, so
    // the controls are translated whether or not such a translation exists.
    //
    // What only describes (a personality, an item's state, an orientation) keeps following
    // the game: with a fan translation it reads in the fan's words, as the rest of the game.
    //
    // Each is keyed on the game's identifier - a button's name, an index into its text
    // table - never on its words. Null for one we have no key for: the game's words stand.
    internal static class ControlWords
    {
        // a verb on vanilla's NPC panel, by its GameObject's name (TargetVerbs)
        internal static string Verb(string name)
        {
            switch (name)
            {
                case "talk": return Shared.Loc.Get("unit.act.talk", "Talk");
                case "carry": return Shared.Loc.Get("unit.act.carry", "Carry");
                case "follow": return Shared.Loc.Get("unit.act.follow", "Follow");
                case "goodbye": return Shared.Loc.Get("unit.act.leave", "Leave");
                case "change": return Shared.Loc.Get("unit.act.change", "Change");
                case "delivery": return Shared.Loc.Get("unit.act.give_birth", "Give birth");
                case "craft": return Shared.Loc.Get("unit.act.craft", "Craft");
                case "vacuum": return Shared.Loc.Get("unit.act.vacuum", "Vacuum");
                case "shop": return Shared.Loc.Get("unit.act.trade", "Trade");
                case "token": return Shared.Loc.Get("unit.act.token_shop", "Token shop");
                case "shop_sweets": return Shared.Loc.Get("unit.act.token_shop_2", "Token shop 2");
                case "talkedit": return Shared.Loc.Get("orders.edit_dialogue", "Edit dialogue");
            }
            return null;
        }

        // a prop's act, by its index in TextManager.actButtonTexts: the words
        // UIManager.PropPanelStateChange writes on the button (PropVerbs)
        internal static string PropAct(int text)
        {
            switch (text)
            {
                case 0: return Shared.Loc.Get("unit.prop.use", "Use");
                case 1: return Shared.Loc.Get("unit.prop.execute", "Execute");
                case 2: return Shared.Loc.Get("unit.prop.insert", "Insert");
                case 3: return Shared.Loc.Get("unit.prop.pull_away", "Leave");
                case 4: return Shared.Loc.Get("unit.prop.piss", "Piss");
                case 5: return Shared.Loc.Get("unit.prop.move", "Move");
                case 6: return Shared.Loc.Get("unit.prop.finish", "Bust a Nut");
                case 7: return Shared.Loc.Get("unit.prop.hold", "Hold");
                case 8: return Shared.Loc.Get("unit.prop.speed", "Speed");
                case 9: return Shared.Loc.Get("unit.prop.show_face", "Show Face");
                case 10: return Shared.Loc.Get("unit.prop.caress", "Caress");
                case 11: return Shared.Loc.Get("unit.prop.position_2", "Position 2");
                case 12: return Shared.Loc.Get("unit.prop.drink", "Drink");
                case 13: return Shared.Loc.Get("unit.prop.eat", "Eat");
                case 14: return Shared.Loc.Get("unit.prop.take_out", "Take Out");
                case 15: return Shared.Loc.Get("unit.prop.make_meat", "Make into Meat");
                case 16: return Shared.Loc.Get("unit.prop.sleep_morning", "Sleep Till Morning");
                case 17: return Shared.Loc.Get("unit.prop.sleep_night", "Sleep Till Night");
                case 18: return Shared.Loc.Get("unit.prop.rape", "Rape");
                case 19: return Shared.Loc.Get("unit.prop.sleeping_powder", "Use sleeping powder");
                case 20: return Shared.Loc.Get("unit.prop.rape_discreetly", "Rape Discreetly");
                case 21: return Shared.Loc.Get("unit.prop.peek", "Peek");
                case 22: return Shared.Loc.Get("unit.prop.enter", "Enter");
                case 23: return Shared.Loc.Get("unit.prop.roughness_2", "Roughness 2");
                case 24: return Shared.Loc.Get("unit.prop.start", "Startup");
                case 25: return Shared.Loc.Get("unit.prop.wash", "Wash");
                case 26: return Shared.Loc.Get("unit.prop.cosmetic", "Cosmetic");
                case 27: return Shared.Loc.Get("unit.prop.hair_dress", "Hair Dress");
                case 28: return Shared.Loc.Get("unit.prop.cosmetic_friend", "Cosmetic (Friend)");
                case 29: return Shared.Loc.Get("unit.prop.hair_dress_friend", "Hair Dress (Friend)");
                case 30: return Shared.Loc.Get("unit.prop.connect", "Connect");
                case 31: return Shared.Loc.Get("unit.prop.dye", "Dye");
                case 32: return Shared.Loc.Get("unit.prop.kill", "Kill");
                case 33: return Shared.Loc.Get("unit.prop.reset", "Reset");
                case 34: return Shared.Loc.Get("unit.prop.call", "Call");
                case 35: return Shared.Loc.Get("unit.prop.scale", "Scale");
                case 36: return Shared.Loc.Get("unit.prop.outfit", "Outfit");
                case 37: return Shared.Loc.Get("unit.prop.outfit_friend", "Outfit (Friend)");
                case 38: return Shared.Loc.Get("unit.prop.visit", "Visit");
                case 39: return Shared.Loc.Get("unit.prop.summon", "Summon");
                case 40: return Shared.Loc.Get("unit.prop.male_display", "Male display");
                case 41: return Shared.Loc.Get("unit.prop.edit", "Edit");
                case 42: return Shared.Loc.Get("unit.prop.give_birth", "Give Birth");
                case 43: return Shared.Loc.Get("unit.prop.rename", "Rename");
            }
            return null;
        }

        // a waiting pose, by NPCManager.OrderAnim (TextManager.orderAnimName's index)
        internal static string Pose(int id)
        {
            switch (id)
            {
                case 0: return Shared.Loc.Get("orders.pose.none", "Nothing");
                case 1: return Shared.Loc.Get("orders.pose.cheer", "Cheer");
                case 2: return Shared.Loc.Get("orders.pose.dance", "Dance");
                case 3: return Shared.Loc.Get("orders.pose.lewd_dance", "Lewd Dance");
                case 4: return Shared.Loc.Get("orders.pose.sit", "Sit");
                case 5: return Shared.Loc.Get("orders.pose.shower", "Shower");
                case 6: return Shared.Loc.Get("orders.pose.harvest", "Harvest");
                case 7: return Shared.Loc.Get("orders.pose.fan", "Fan");
                case 8: return Shared.Loc.Get("orders.pose.lie_down", "Lay Down");
                case 9: return Shared.Loc.Get("orders.pose.sleep", "Sleep");
                case 10: return Shared.Loc.Get("orders.pose.repair", "Repair");
                case 11: return Shared.Loc.Get("orders.pose.mining", "Mining");
                case 12: return Shared.Loc.Get("orders.pose.walk", "Walk");
                case 13: return Shared.Loc.Get("orders.pose.walk_backward", "Walk Backward");
                case 14: return Shared.Loc.Get("orders.pose.run", "Run");
                case 15: return Shared.Loc.Get("orders.pose.attack", "Attack");
            }
            return null;
        }

        // a skill, by its id (SkillManager.skillsText's index)
        internal static string Skill(int id)
        {
            switch (id)
            {
                case 0: return Shared.Loc.Get("skills.name.self_healing", "Self-healing");
                case 1: return Shared.Loc.Get("skills.name.stomach", "Stomach");
                case 2: return Shared.Loc.Get("skills.name.harvest", "Harvest");
                case 3: return Shared.Loc.Get("skills.name.patience", "Patience");
                case 4: return Shared.Loc.Get("skills.name.observation", "Observation");
                case 5: return Shared.Loc.Get("skills.name.persuasive", "Persuasive");
                case 6: return Shared.Loc.Get("skills.name.construction", "Construction");
                case 7: return Shared.Loc.Get("skills.name.carry", "Carry");
                case 8: return Shared.Loc.Get("skills.name.smash", "Smash");
                case 9: return Shared.Loc.Get("skills.name.grapple", "Grapple");
                case 10: return Shared.Loc.Get("skills.name.leader", "Leader");
                case 11: return Shared.Loc.Get("skills.name.swim", "Swim");
            }
            return null;
        }
    }
}
