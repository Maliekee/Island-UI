using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI
{
    // WHAT YOU CAN DO TO A TARGET, READ FROM VANILLA RATHER THAN RE-DERIVED
    // (window-system.md §6.24, D65 "postfix, do not predict").
    //
    // The census in §6.23 is full of index and ID tables - npcPanelState[12] means
    // Craft, npcIDs 18/19/71/90/91/113/116 are crafters, 111 trades, charaType 100
    // is hostile. Copying any of that into our code buys silent breakage on every
    // patch: an inserted button or one new shopkeeper changes what an index means,
    // and we would draw the WRONG verb rather than crash. So we copy none of it.
    //
    // Vanilla computes the set across three sites and leaves the answer in the
    // scene, as activeSelf on the children of NPCPanel. activeSelf is the object's
    // OWN flag, so the panel can stay hidden and still be read - we never have to
    // show vanilla's panel to ask it what it would have shown.
    //
    // The glyph is keyed on the object's NAME, never its index: names survive a
    // reorder, and a name we do not know is detectable, so a verb added by a patch
    // appears wearing its own label instead of vanishing.
    internal static class TargetVerbs
    {
        internal sealed class Verb
        {
            public string Name;        // the GameObject's name: "follow", "carry", "talkedit"
            public string Label;       // its own TMP text, vanilla's words in vanilla's language
            public Button Button;      // pressed through, so vanilla's own handler runs
            public bool Deep;          // it opens a sub-panel rather than acting (talk, position)
            public Sprite Icon;        // vanilla's own art, for the buttons that carry no words
        }

        private static readonly List<Verb> Live = new List<Verb>();
        private static CommonStates _read;

        // The three verbs no code path in the build ever lights (§6.23): recruit,
        // banish, and the whole order panel. They are excluded because VANILLA
        // never lights them, not by this list - if a patch revives one it comes
        // back on its own. The list is only here to explain their absence.
        // employ · away · order

        // WHAT IS NOT AN ACT. A DROP list, not a keep list, because D65 cuts both
        // ways: a verb we have never heard of is an act until we learn otherwise,
        // so it turns up wearing its own label rather than vanishing.
        //
        // "status" is the only member today. It is a link to the very window it
        // would be drawn on - press it and you arrive where you already are.
        //
        // "position" (Move) and "follow"/"goodbye" have gone to Orders, which is
        // where D81 always sent them - they are STANDING settings, not things you
        // do to someone. They stayed here only until Orders could hold them:
        // Orders now draws the place, the two radii, the pose grid and the follow
        // toggle, so there is nowhere left that they can only be reached from here.
        //
        // "talkedit" joins them, which is where the destination
        // map sent it: authoring what someone will SAY is a standing setting about
        // them, not an act performed on them. It is the last of the four verbs
        // that map moved, and Orders draws it under Communication - beside the
        // three flags that decide what they may do, since what they may say is the
        // same kind of fact.
        private static bool Elsewhere(string name)
        {
            return name == "status" || name == "position"
                || name == "follow" || name == "goodbye" || name == "talkedit";
        }

        // A verb that opens a panel of its own rather than doing something. Named,
        // not indexed, for the same reason as everything else here.
        private static bool IsDeep(string name)
        {
            return name == "talk" || name == "position" || name == "order";
        }

        internal static IList<Verb> Current { get { return Live; } }
        internal static CommonStates ReadFor { get { return _read; } }

        // Ask vanilla what this target offers, then read the answer.
        //
        // NPCPanelStateChange is the side-effect-free half: with statusChange false
        // it only lights buttons (the overload exists because CheckFriendlyNPC
        // needed exactly that). NPCMenuOpen adds the npcID-driven ones - craft,
        // vacuum, the shops - but it also CALLS CraftOpen, so it cannot be invoked
        // to ask a question. What that costs is written below.
        // Orders still has to PRESS the verbs it took, so it needs a read that
        // keeps them. Same answer from vanilla, one filter switched off.
        private static bool _keepElsewhere;

        internal static void ReadIncludingElsewhere(ManagersScript mn, CommonStates who)
        {
            _keepElsewhere = true;
            try { Read(mn, who); }
            finally { _keepElsewhere = false; }
        }

        internal static void Read(ManagersScript mn, CommonStates who)
        {
            Live.Clear();
            _read = who;
            if (mn == null || mn.uiMN == null || who == null
                || mn.uiMN.npcPanel == null || mn.uiMN.npcPanelState == null)
            {
                return;
            }
            // WHERE THIS IS INCOMPLETE, AND WHY IT IS STILL THE RIGHT SHAPE:
            // NPCPanelStateChange lights the verbs that depend on the PERSON -
            // move, talk, status, follow/leave, birth, carry, sex. The ones that
            // depend on npcID - craft, vacuum, trade, the token shops - are set by
            // NPCMenuOpen, which we may not call. So when vanilla's own panel is
            // already up for this target (a world click), we read whatever it has
            // lit and get all of them; when it is not (a roster pick), we ask and
            // get the person half. Either way we re-derive NOTHING.
            bool ownPanelUp = mn.uiMN.npcPanel.activeSelf
                && mn.gameMN != null && ReferenceEquals(mn.gameMN.menuNPC, who);
            if (!ownPanelUp)
            {
                try { mn.uiMN.NPCPanelStateChange(who, statusChange: false); }
                catch (System.Exception e) { Plugin.Log.LogWarning("verb read: " + e.Message); return; }
            }
            GameObject[] set = mn.uiMN.npcPanelState;
            for (int i = 0; i < set.Length; i++)
            {
                GameObject go = set[i];
                if (go == null || !go.activeSelf || !Reachable(go, mn.uiMN.npcPanel))
                {
                    continue;
                }
                if (!_keepElsewhere && Elsewhere(go.name))
                {
                    continue;
                }
                Live.Add(new Verb
                {
                    Name = go.name,
                    Label = LabelOf(go),
                    Button = go.GetComponent<Button>(),
                    Deep = IsDeep(go.name),
                    Icon = IconOf(go.transform),
                });
            }
            ReadSearchPanel(mn, who);
        }

        // A BODY'S ACTS ARE ON A DIFFERENT PANEL. NPCPanel answers for
        // someone standing up; a corpse, a downed animal and a knocked-out hostile
        // are routed to SearchNPC instead, and their two acts live on
        // npcSearchPanel as DissectButton and CarryButton - toggled by SetActive
        // exactly like npcPanelState's, so they are read the same way and by NAME.
        //
        // Unlike NPCPanelStateChange there is nothing safe to CALL here: PlayerMove
        // lights these inside the SearchNPC coroutine, as part of the interaction.
        // So this reads what is already there, and if the panel is not up for this
        // body there is nothing to say - the same rule PropVerbs works under.
        private static readonly string[] SearchNames = { "CarryButton", "DissectButton" };

        private static void ReadSearchPanel(ManagersScript mn, CommonStates who)
        {
            if (mn.uiMN.npcSearchPanel == null || mn.gameMN == null || mn.gameMN.pMove == null
                || !ReferenceEquals(mn.gameMN.pMove.searchNPC, who))
            {
                return;
            }
            for (int i = 0; i < SearchNames.Length; i++)
            {
                Transform t = mn.uiMN.npcSearchPanel.transform.Find(SearchNames[i]);
                if (t == null || !t.gameObject.activeSelf)
                {
                    continue;
                }
                Live.Add(new Verb
                {
                    Name = SearchNames[i],
                    Label = LabelOf(t.gameObject),
                    Button = t.GetComponent<Button>(),
                    Deep = false,
                    Icon = IconOf(t),
                });
            }
        }

        // The button's own art, for the ones that carry no words. A CHILD Image
        // wins over the button's own, which is usually the frame.
        private static Sprite IconOf(Transform t)
        {
            Image[] all = t.GetComponentsInChildren<Image>(true);
            Sprite own = null;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].sprite == null)
                {
                    continue;
                }
                if (all[i].transform == t)
                {
                    own = all[i].sprite;
                    continue;
                }
                return all[i].sprite;
            }
            return own;
        }

        // ACTIVESELF IS NOT ENOUGH ON ITS OWN, and this is the trap in the trick.
        //
        // We read activeSelf deliberately, so that vanilla's panel can stay hidden
        // and still be asked what it would have shown. But activeSelf is the
        // object's OWN flag, so a button sitting inside a CLOSED SUB-PANEL reads
        // true as well - and npcPanelState holds several of those: the Talk
        // sub-panel's own buttons among them.
        //
        // The result was a verb on our sheet that vanilla's panel does not offer
        // (a Japanese label beside Talk and Carry, absent from
        // the vanilla prompt). Its label being untranslated was the second symptom
        // of the same cause: LocalizeStringEvent runs when an object is ENABLED, so
        // one that has never been shown still carries its authoring-language
        // string. A raw label is evidence the object was never on screen.
        //
        // So the test is the whole chain: this object and every ancestor up to the
        // panel must be active. The panel ITSELF is deliberately not tested - that
        // is the part we are allowed to have hidden.
        private static bool Reachable(GameObject go, GameObject panel)
        {
            Transform stop = panel != null ? panel.transform : null;
            for (Transform t = go.transform.parent; t != null && t != stop; t = t.parent)
            {
                if (!t.gameObject.activeSelf)
                {
                    return false;
                }
            }
            return true;
        }

        // The verb's own words, so a name we have no glyph for can still be drawn.
        // LocalizeStringEvent has already run on these, so this is the player's
        // language without us knowing anything about languages.
        private static string LabelOf(GameObject go)
        {
            var text = go.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
            return text != null && !string.IsNullOrEmpty(text.text) ? text.text : go.name;
        }

        // "CarryButton" is an object name, not a caption. A verb whose label is
        // just its own name has no words of its own, and must be drawn as its icon
        // - which is the mistake the placement acts made visible first.
        internal static bool Wordless(Verb v)
        {
            return v != null && v.Label == v.Name;
        }

        // Press it through vanilla's own button, so its handler runs and anything
        // it opens opens the way it always did.
        internal static bool Press(Verb verb)
        {
            if (verb == null || verb.Button == null || !verb.Button.interactable)
            {
                return false;
            }
            verb.Button.onClick.Invoke();
            return true;
        }

        // For the harness: what did vanilla say about this target?
        internal static string Dump(ManagersScript mn, CommonStates who)
        {
            Read(mn, who);
            var sb = new System.Text.StringBuilder();
            sb.Append(who != null ? who.charaName : "null").Append(": ").Append(Live.Count).Append(" verbs");
            for (int i = 0; i < Live.Count; i++)
            {
                sb.Append(" | ").Append(Live[i].Name).Append(" \"").Append(Live[i].Label).Append('"');
                if (Live[i].Deep) { sb.Append(" (deep)"); }
                if (Live[i].Button == null) { sb.Append(" [no button]"); }
            }
            return sb.ToString();
        }
    }
}
