using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI
{
    // WHAT YOU CAN DO TO A PROP, READ FROM VANILLA (window-system.md §6.24 D82/D85,
    // target-matrix.md G6/G7). The prop half of what TargetVerbs does for a person.
    //
    // Vanilla's answer lives in UIManager.propActionText[]: PropPanelTypeChange
    // lights the entries this object offers and PropPanelStateChange writes each
    // one's label and its action id into propActionState[]. We read that and
    // nothing else.
    //
    // WE MUST NEVER CALL PropPanelTypeChange. TargetVerbs may ask its question,
    // because NPCPanelStateChange only lights buttons; this one does not have that
    // property. It starts coroutines (AreaPreview for the animal remover and the
    // libido pot), it can open the melody editor, it resets propActProgress, and
    // it ends by calling .Select() on the first live entry - which takes the
    // controller focus D72 says is not ours to move. So the rule here is stricter
    // than "postfix, do not predict": we read whatever vanilla last lit, and if it
    // has lit nothing we show nothing. That is the whole of D85 - do not suppress,
    // do not eat its input, and do not poke it either.
    //
    // The KEY is the action id, not the button index. propActionState[i] is what
    // PropAction switches on, so it is the semantic name of the act; the index is
    // just which of the seven slots vanilla happened to use, and a patch that
    // reorders them would silently change what a glyph means.
    internal static class PropVerbs
    {
        internal sealed class Act
        {
            public int State;          // propActionState[i]: what PropAction switches on
            public string Name;        // for a placement act, vanilla's button name - the glyph key
            public string Label;       // vanilla's own words, already localised
            public Button Button;      // pressed through, so its handler runs
            public Sprite Icon;        // vanilla's own art, for the acts drawn as icons
        }

        private static readonly List<Act> Live = new List<Act>();
        private static ItemInfo _read;

        internal static IList<Act> Current { get { return Live; } }
        internal static ItemInfo ReadFor { get { return _read; } }

        // Read the answer vanilla left in the scene. activeSelf is the object's own
        // flag, so the panel can be hidden and still be read - the same trick the
        // NPC verbs use, and the reason neither reader has to show a vanilla panel
        // to ask it a question.
        internal static void Read(ManagersScript mn, ItemInfo prop)
        {
            Live.Clear();
            _read = prop;
            if (mn == null || mn.uiMN == null || prop == null
                || mn.uiMN.propActionText == null || mn.uiMN.propActionState == null)
            {
                return;
            }
            // The panel's entries describe whatever vanilla last opened it for. If
            // that is not this prop, the answer is not about this prop, so there is
            // no answer - better silent than wrong.
            if (!ShowingFor(mn, prop))
            {
                return;
            }
            // UseProp deliberately skips PropPanelTypeChange for a wreck, so
            // its old child buttons can still describe the previous object.
            // A wreck has Repair on the placement rail and no body actions.
            if (mn.uiMN.propPanel == null || !mn.uiMN.propPanel.activeSelf
                || (prop.lifeMax > 0f && prop.life <= 0f && prop.GetComponent<DefenceInfo>() != null))
            {
                return;
            }
            TMPro.TextMeshProUGUI[] set = mn.uiMN.propActionText;
            for (int i = 0; i < set.Length && i < mn.uiMN.propActionState.Length; i++)
            {
                TMPro.TextMeshProUGUI text = set[i];
                if (text == null || text.transform.parent == null
                    || !text.transform.parent.gameObject.activeSelf)
                {
                    continue;
                }
                Live.Add(new Act
                {
                    State = mn.uiMN.propActionState[i],
                    Label = string.IsNullOrEmpty(text.text) ? "Act" : text.text,
                    Button = text.transform.parent.GetComponent<Button>(),
                });
            }
        }

        // Whose acts are on the panel right now. buildMN.tmpProp is what UseProp
        // set when it opened, and inventory.tmpSubInventory is the container half
        // of the same answer; either identifies the object.
        private static bool ShowingFor(ManagersScript mn, ItemInfo prop)
        {
            if (mn.buildMN != null && mn.buildMN.tmpProp != null
                && ReferenceEquals(mn.buildMN.tmpProp.GetComponent<ItemInfo>(), prop))
            {
                return true;
            }
            return mn.inventory != null && mn.inventory.tmpSubInventory != null
                && ReferenceEquals(mn.inventory.tmpSubInventory.GetComponent<ItemInfo>(), prop);
        }


        // THE PLACEMENT ACTS: Move, Remove and Repair, off buildPanel
        // (target-matrix.md G6). They are a different KIND from the acts above -
        // about the object as something you BUILT rather than something you are
        // dealing with - which is why they get the glyph row on the top rail and
        // the rest get the sheet.
        //
        // They are also the one act group a glyph row suits: always these three,
        // uniform across every prop, predictable. Everything that made a general
        // act row impossible to size is absent here.
        //
        // Read by NAME off vanilla's own panel, the same rule as everywhere else -
        // UseProp lights Move and Remove normally and swaps to Repair alone when
        // the thing is broken, so reading activeSelf gets that swap for free.
        private static readonly string[] PlacementNames = { "MoveButton", "RemoveButton", "RepairButton" };
        private static readonly List<Act> LivePlacement = new List<Act>();

        internal static IList<Act> Placement { get { return LivePlacement; } }

        internal static void ReadPlacement(ManagersScript mn)
        {
            LivePlacement.Clear();
            if (mn == null || mn.uiMN == null || mn.uiMN.buildPanel == null)
            {
                return;
            }
            for (int i = 0; i < PlacementNames.Length; i++)
            {
                Transform t = mn.uiMN.buildPanel.transform.Find(PlacementNames[i]);
                if (t == null || !t.gameObject.activeSelf)
                {
                    continue;
                }
                // These three carry NO text - vanilla draws them as icons alone,
                // bottom left of the HUD, and the blue four-way arrow and the red
                // barred circle are what a player already reads them by. So take
                // vanilla's own sprite rather than a kit mark of ours: it is the
                // art the player has already learned, and it costs us no glyph to
                // keep in step.
                //
                // Label is kept as a fallback for a build where they gain text, but
                // today it is the OBJECT NAME - so it must never be drawn. That is
                // exactly what the first attempt did: two buttons reading
                // "MoveButton" and "RemoveButton".
                var text = t.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
                LivePlacement.Add(new Act
                {
                    State = -1 - i,                      // not a propActionState; negative marks it
                    Name = PlacementNames[i],
                    Label = text != null && !string.IsNullOrEmpty(text.text) ? text.text : PlacementNames[i],
                    Button = t.GetComponent<Button>(),
                    Icon = IconOf(t),
                });
            }
        }

        // A placement act's art by its button's name, whether or not vanilla has
        // lit the panel - the dock draws Move and Remove for a thing of yours
        // before vanilla has opened it (D156)
        internal static Sprite PlacementIcon(ManagersScript mn, string name)
        {
            Transform t = mn != null && mn.uiMN != null && mn.uiMN.buildPanel != null
                ? mn.uiMN.buildPanel.transform.Find(name) : null;
            return t != null ? IconOf(t) : null;
        }

        // The button's own art. Its own Image may be the plate rather than the
        // mark, so a CHILD Image wins where there is one - which is how vanilla
        // builds these: a frame with the glyph laid on it.
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
                    own = all[i].sprite;      // the frame: the fallback
                    continue;
                }
                return all[i].sprite;         // a child: the mark itself
            }
            return own;
        }

        // Pressed THROUGH vanilla's own button, so its handler runs and whatever it
        // opens opens the way it always did (D65).
        internal static bool Press(Act act)
        {
            if (act == null || act.Button == null || !act.Button.interactable)
            {
                return false;
            }
            act.Button.onClick.Invoke();
            return true;
        }

        // For the harness: what does vanilla say this prop offers?
        internal static string Dump(ManagersScript mn, ItemInfo prop)
        {
            Read(mn, prop);
            var sb = new System.Text.StringBuilder();
            sb.Append(prop != null ? prop.itemKey : "null").Append(": ").Append(Live.Count).Append(" acts");
            for (int i = 0; i < Live.Count; i++)
            {
                sb.Append(" | state ").Append(Live[i].State).Append(" \"").Append(Live[i].Label).Append('"');
                if (Live[i].Button == null) { sb.Append(" [no button]"); }
            }
            return sb.ToString();
        }
    }
}
