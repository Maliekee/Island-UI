using System.Collections.Generic;
using IslandUI.UI;
using UnityEngine;

namespace IslandUI
{
    // THE TARGET IS OURS (window-system.md 6.31, D139/D140). It used to be whatever
    // vanilla's three variables said - pMove.searchNPC, gameMN.menuNPC,
    // buildMN.tmpProp - read afresh every time anything asked. Vanilla ends only
    // what its own openers began (CheckNPC's lifetime loop), so every path of ours
    // that set a variable without that loop - the roster pick, the Follow/Leave keep,
    // the sheet's doors - left it set for good; and since UseProp never clears
    // menuNPC and the old resolve asked menuNPC first, a lingering person outranked
    // the chest you had just opened: the target got stuck while you interacted with
    // other items.
    //
    // Now one record says what the target is, and every surface reads it through
    // ResolveTarget as before. Once a tick it is RECONCILED with vanilla:
    //   - whichever of vanilla's three was newly set is a begin, and a begin ends
    //     the last target - the last interaction wins (D140);
    //   - the adopted variable cleared is an end (vanilla closed it, or a close of
    //     ours did);
    //   - a target of ours ends by our rules (6.32, D146): Escape, the unit's
    //     close, another target, another area - never on distance, and not when
    //     you move or click the ground.
    // THE TARGET OUTLIVES THE CLICK (6.32, D153: the click change worked too
    // hard against vanilla): a click walks and acts as the game
    // does, but a person clicked is the target from the click, and when the
    // game's menu lifetime ends because you moved, the menu stays - ours now.
    // Each begin and end is logged with its reason (TargetLog, for the harness).
    internal static partial class InventoryWindow
    {
        // Person: someone Tab holds whom the game holds by no variable of its own -
        // an enemy, a stranger, a body (D159). Ours alone, like a thing we keep.
        private enum Held { None, Search, Menu, Prop, Person }
        private enum Begun { World, Roster }

        private static Held _heldVar;
        private static object _heldRef;        // the reference vanilla's variable held when adopted
        private static Begun _heldHow;
        private static CommonStates _adoptRosterFor;   // armed by a pick of ours just before it drives vanilla
        private static float _adoptRosterUntil;
        private static string _adoptWhy;       // what armed it, for the log ("pick", "click")
        private static string _endWhy;         // a close of ours, naming itself for the log
        private static CommonStates _seenSearch, _seenMenu;
        private static GameObject _seenProp;
        private static int _reconciledFrame = -1;
        private static readonly List<string> TargetLines = new List<string>();

        // A thing Tab is opening in reach (OpenFromTab): when vanilla's UseProp sets
        // it, it is adopted as ours, not as a click (D158)
        private static GameObject _adoptPropFor;
        private static float _adoptPropUntil;

        // FORGOTTEN in another area (D146): a door, a dungeon's stairs, a switch of
        // character all move you further in one frame than any walk does.
        private const float AreaJump = 12f;
        private static Vector3 _lastHere;
        private static bool _lastHereSet;

        // What the target IS, for every surface: the held person, or what the held
        // prop resolves to (its chest, or itself - PropTargetOf).
        private static object ResolveTarget(ManagersScript mn)
        {
            switch (_heldVar)
            {
                case Held.Search:
                case Held.Menu:
                case Held.Person:
                    CommonStates who = _heldRef as CommonStates;
                    return who != null ? who : null;
                case Held.Prop:
                    return PropTargetOf(mn, _heldRef as GameObject);
                default:
                    return null;
            }
        }

        // Once a frame, from the Tick and again from LateSync (vanilla opens a prop in
        // PlayerMove's Update, which can run after ours - the flash LateSync exists for).
        private static void ReconcileTarget(ManagersScript mn)
        {
            if (mn == null || mn.gameMN == null)
            {
                return;
            }
            bool late = _reconciledFrame == Time.frameCount;
            _reconciledFrame = Time.frameCount;
            GameManager game = mn.gameMN;
            PlayerMove p = game.pMove;
            CommonStates search = p != null ? p.searchNPC : null;
            CommonStates menu = game.menuNPC;
            GameObject prop = mn.buildMN != null ? mn.buildMN.tmpProp : null;
            bool newSearch = search != null && !ReferenceEquals(search, _seenSearch);
            bool newMenu = menu != null && !ReferenceEquals(menu, _seenMenu);
            bool newProp = prop != null && !ReferenceEquals(prop, _seenProp);
            _seenSearch = search;
            _seenMenu = menu;
            _seenProp = prop;

            if (!late && p != null)
            {
                Vector3 here = p.transform.position;
                bool jumped = _lastHereSet && (here - _lastHere).sqrMagnitude > AreaJump * AreaJump;
                _lastHere = here;
                _lastHereSet = true;
                if (jumped && _heldVar != Held.None)
                {
                    Untarget("another area");
                    return;
                }
            }

            // a body Tab holds, searched by an act of its dock (PressBodyAct): the
            // game's search is the act's instrument, not a new target (D159)
            if (_heldVar == Held.Person && (ReferenceEquals(search, _heldRef) || ReferenceEquals(menu, _heldRef)))
            {
                newSearch = newSearch && !ReferenceEquals(search, _heldRef);
                newMenu = newMenu && !ReferenceEquals(menu, _heldRef);
            }
            // a begin: the newest of vanilla's three, in the old order when two land
            // in one tick (a search sets menuNPC to the same body)
            if (newSearch)
            {
                Adopt(Held.Search, search, Begun.World, "body search");
            }
            else if (newMenu && !(_heldVar == Held.Search && ReferenceEquals(_heldRef, menu)))
            {
                // vanilla's own opener (CheckNPC) holds nowAction for the menu's
                // life; a menu with nothing holding it is ours to end, whoever set it
                bool picked = ReferenceEquals(menu, _adoptRosterFor) && Time.unscaledTime <= _adoptRosterUntil;
                bool ours = picked || !game.nowAction;
                Adopt(Held.Menu, menu, ours ? Begun.Roster : Begun.World,
                    picked ? (_adoptWhy ?? "pick") : ours ? "set, unheld" : "world click");
            }
            else if (newProp && _heldVar == Held.Prop && ReferenceEquals(prop, _heldRef))
            {
                // vanilla opened the thing we hold again (a click on it, or an act
                // of the dock re-opening it): the same target, held as it was - a
                // thing you Tabbed and then clicked stays yours (D158)
            }
            else if (newProp)
            {
                // a thing Tab opened in reach is ours, the rest are the game's (D158)
                bool tab = ReferenceEquals(prop, _adoptPropFor) && Time.unscaledTime <= _adoptPropUntil;
                Adopt(Held.Prop, prop, tab ? Begun.Roster : Begun.World, tab ? "tab" : "prop");
            }

            if (_heldVar == Held.None)
            {
                return;
            }
            // the adopted variable no longer holds it: ended - unless a Follow or
            // Leave is putting it back (RestoreKeptTarget), which is not an end
            // A THING WE KEPT is held by us, not by tmpProp (D153 for things): it
            // ends only by a close of ours, another target, another area, or gone
            bool keptThing = _heldVar == Held.Prop && _heldHow == Begun.Roster;
            bool holds = _heldVar == Held.Person
                || (_heldVar == Held.Search ? ReferenceEquals(search, _heldRef)
                : _heldVar == Held.Menu ? ReferenceEquals(menu, _heldRef)
                : keptThing || ReferenceEquals(prop, _heldRef));
            Object alive = _heldRef as Object;
            if (alive == null)
            {
                Drop("gone");
                return;
            }
            // CARRIED IS GONE FROM THE WORLD: a targeted NPC you pick up (carry)
            // left its indicator and dock visible. The game makes them an item in your bag and
            // marks them dead == 5 - as it does a prisoner and a body set on a
            // stand - however the carry began: our dock's Carry, which already ended
            // the target (D159), vanilla's search panel, or its menu. Held that
            // way, they are no target at all.
            if (_heldRef is CommonStates carried && carried.dead == 5)
            {
                Drop("carried");
                return;
            }
            // A HARVESTED THING IS GONE: a harvested resource disappears as the
            // target. The game never destroys a
            // resource: GameManager.Harvest at life 0 runs BGDestroy, which turns its
            // collider off, plays the fall and then only SetActive(false)s it - so the
            // gone check above never fires. Its life reaching 0 is the end, the moment
            // it starts to fall; inactive covers any other way a thing is put away.
            if (_heldVar == Held.Prop && HarvestedOrPutAway(alive))
            {
                Drop("harvested");
                return;
            }
            if ((keptThing || _heldVar == Held.Person) && _endWhy != null)
            {
                Drop(_endWhy);
                return;
            }
            if (!holds)
            {
                if (_keepWho != null && ReferenceEquals(_keepWho, _heldRef))
                {
                    return;
                }
                // A CLICKED thing goes when vanilla lets go of it - you moved, or
                // pressed the ground - as the game's own does (D158). The keep that
                // stood here (D153 for things) is Tab's alone now, and a Tab target
                // is held by us, not by tmpProp (keptThing above).
                Drop(_endWhy ?? "vanilla closed it");
                return;
            }
            // A person vanilla stopped holding without letting go - its loop gone,
            // the variable still set - would be stuck for good under vanilla's
            // rules. A click is the game's (D158), so it ends the game's way: the
            // menu closed, as its loop would have. (It was taken over as ours here.)
            if (_heldHow == Begun.World && _heldVar != Held.Prop && !game.nowAction && !late && p != null)
            {
                EndHeld(mn, "vanilla let go");
                return;
            }
            // A target of ours - Tab, a roster pick - ends only by our rules (D146):
            // Escape, the unit's close, the pick pressed again, another target,
            // another area - never on distance, and not when you move, swing, guard
            // or click the ground - a deselect there feels wrong.
        }

        // The game ends a person's menu the moment you move, swing or guard
        // (PlayerMove.CheckNPC's loop: !wasd && !toolAction && !guard), or when its
        // target changes - a click elsewhere, the ground included, since a press
        // there empties pMove.target. A CLICKED person ends there, as in the game
        // (D158). One you Tabbed or picked - ours - outlives it: you clicked them
        // to walk up, and the loop that came with the click is not what holds them.
        // So its NPCMenuClose is refused when it is that end, for one of your people
        // we hold as ours, and the menu stays. A close of ours (_endWhy), a click on
        // another person, and vanilla's other callers, which leave the target as it
        // was (a fire, a scene, the friends list), close as before.
        internal static bool KeepPastVanillaEnd()
        {
            if (_mn == null || _mn.gameMN == null || _endWhy != null || _heldVar != Held.Menu
                || _heldHow != Begun.Roster || !_mn.gameMN.nowAction)
            {
                return false;
            }
            CommonStates who = _heldRef as CommonStates;
            PlayerMove p = _mn.gameMN.pMove;
            if (who == null || p == null || !ReferenceEquals(_mn.gameMN.menuNPC, who) || !who.Employed())
            {
                return false;
            }
            bool moved = p.wasd || p.toolAction || p.guard;
            // the ground (no target) or a THING (a stone, a chest) is not another
            // target: the person stays until a thing of ours begins (Adopt lets
            // them go); another person clicked takes over, and closes this menu
            bool ground = p.target == null;
            bool thing = !ground && PersonOf(p.target) == null;
            if (!moved && !ground && !thing)
            {
                return false;
            }
            Note("kept " + NameOf(who) + " past vanilla's end ("
                + (moved ? "you moved" : ground ? "its target let go" : "a click on a thing") + ")");
            return true;
        }

        // A CLICK IS THE GAME'S (D158): a clicked target behaves as it does in
        // vanilla - you literally walk to it - and drops on movement like vanilla
        // as well. What stood here - the target begun at
        // the click, the walk that stopped inside reach and acted there (D153,
        // D154), Ctrl+click as "target from where I stand" - is gone: a click walks
        // you to contact and the game opens what it opens. Tab is the targeting
        // without walking.

        // What Tab may open from reach rather than hold until you walk up: a
        // container, a level, a plain prop's panel - never a bed, a play prop, an
        // interactive one or a stove
        private static bool OpensFromReach(ItemData data)
        {
            return data != null && data.itemType == ItemData.ItemType.Prop
                && (data.subType == ItemData.SubType.Chest || data.subType == ItemData.SubType.Stock
                    || data.subType == ItemData.SubType.None);
        }

        // one of your people, as a click reaches them: not an enemy of the moment
        private static bool Yours(CommonStates who, PlayerMove p)
        {
            return who != null && !ReferenceEquals(who, p.common) && who.Employed() && who.debuff.discontent != 4;
        }

        // D150: Escape ends the target, after a dialog of ours (which keeps the key)
        // and before vanilla pauses. Asked from GameManager.PauseGame.
        internal static bool EscapeUntargets()
        {
            if (!Input.GetKeyDown(KeyCode.Escape) || SplitDialog.IsOpen || FilterInput.AnyFocused
                || DevConsole.IsOpen || SettingsWindow.IsOpen)
            {
                return false;
            }
            return Untarget("escape");
        }

        // The whole close - the unit's own (CloseFocus) - named for the log.
        internal static bool Untarget(string why)
        {
            if (_heldVar == Held.None || _mn == null)
            {
                return false;
            }
            CloseFocus(_mn);
            NoteEnd(why);
            return true;
        }

        // The person a world collider belongs to: vanilla's click lands on the
        // character's interaction collider (layer 14), a child of the character
        // (PlayerMove.InteractNPC).
        private static CommonStates PersonOf(GameObject g)
        {
            if (g == null || g.layer != 14 || g.transform.parent == null)
            {
                return null;
            }
            return g.transform.parent.GetComponent<CommonStates>();
        }

        private static void Adopt(Held var, object what, Begun how, string why)
        {
            // A lingering pick of ours gives way SOFTLY. Vanilla's own closer would
            // also drop the new interaction's target (NPCMenuClose -> SubInventory-
            // Visible -> pMove.target = null), ending the chest you just opened.
            if (_heldVar == Held.Menu && _heldHow == Begun.Roster && var != Held.Menu
                && _mn != null && _mn.gameMN != null && ReferenceEquals(_mn.gameMN.menuNPC, _heldRef))
            {
                ReleasePerson(_mn);
            }
            if (_heldVar != Held.None && !ReferenceEquals(_heldRef, what))
            {
                Note("end " + NameOf(_heldRef) + " (" + why + " of " + NameOf(what) + ")");
            }
            _heldVar = var;
            _heldRef = what;
            _heldHow = how;
            _adoptRosterFor = null;
            _adoptPropFor = null;
            _adoptWhy = null;
            _endWhy = null;
            Note("begin " + NameOf(what) + " (" + why + ", " + (how == Begun.Roster ? "ours" : "vanilla's") + ")");
            _forceRefresh = true;
            _nextRefresh = 0f;
        }

        // a thing harvested to nothing (its ItemInfo's life spent) or put away by the
        // game (inactive: how BGDestroy ends a felled tree or a broken rock).
        //
        // A BROKEN BUILDING IS NOT HARVESTED: a broken workbench, and any destroyed
        // building, must stay targetable for its repair.
        // A building a raid wrecks reads the same - life 0 of a max - but it stays up,
        // active, waiting for its Repair: vanilla's own test for that is a DefenceInfo
        // at life 0 (PlayerMove.UseProp, the build panel's RepairButton), and so is
        // ours (Thing.cs, the wreck). Except a Resource-type DefenceInfo: its Down()
        // runs BGDestroy, the tree's fall - that one is harvested like a tree.
        private static bool HarvestedOrPutAway(Object thing)
        {
            GameObject go = thing as GameObject;
            if (go == null && thing is Component c)
            {
                go = c.gameObject;
            }
            if (go == null)
            {
                return false;
            }
            if (!go.activeInHierarchy)
            {
                return true;
            }
            ItemInfo info = go.GetComponent<ItemInfo>();
            if (info == null || info.lifeMax <= 0f || info.life > 0f)
            {
                return false;
            }
            DefenceInfo wreck = go.GetComponent<DefenceInfo>();
            return wreck == null || wreck.defenceType == DefenceInfo.DefenceType.Resource;
        }

        // the record goes; vanilla has already let go (or was never holding it)
        private static void Drop(string why)
        {
            Note("end " + NameOf(_heldRef) + " (" + why + ")");
            _heldVar = Held.None;
            _heldRef = null;
            _endWhy = null;
            _forceRefresh = true;
            _nextRefresh = 0f;
        }

        // Ending a target of OURS: vanilla is let go too, the way it lets go of a menu.
        private static void EndHeld(ManagersScript mn, string why)
        {
            _endWhy = why;
            if (_heldVar == Held.Search
                || (_heldVar == Held.Person && mn.gameMN != null && mn.gameMN.pMove != null
                    && ReferenceEquals(mn.gameMN.pMove.searchNPC, _heldRef)))
            {
                CloseFocus(mn);   // the search's own variables, as the chip ends it
            }
            else if (_heldVar == Held.Menu && mn.gameMN != null && ReferenceEquals(mn.gameMN.menuNPC, _heldRef)
                && mn.uiMN != null)
            {
                mn.uiMN.NPCMenuClose();
            }
            _seenMenu = mn.gameMN != null ? mn.gameMN.menuNPC : null;
            _seenSearch = mn.gameMN != null && mn.gameMN.pMove != null ? mn.gameMN.pMove.searchNPC : null;
            Drop(why);
        }

        // A MENU NOBODY HOLDS. Vanilla's person menu shows only while
        // our target is that person, and the unit veils it then (VeilVanilla); once
        // the target has moved on and vanilla's own lifetime is over - CheckNPC's
        // loop gone, nowAction false - nothing else closes it, and it stood
        // unveiled: Move · Talk · Status · Follow over the player and no unit
        // after talking to an NPC and clicking around. One way in is a race: a
        // thing clicked while a person's menu is still vanilla's. If CheckNPC's
        // loop ends before our reconcile adopts the thing, its close is refused as
        // "a click on a thing" (D153); the thing is then adopted while the person
        // is still vanilla's, and Adopt releases only a person of ours. Whatever
        // the way in, a menu for someone who is not the target, with no vanilla
        // loop holding it, is released softly - the new interaction's sub-inventory
        // and pMove.target stay.
        private static void ReleaseOrphanMenu(ManagersScript mn)
        {
            if (!Settings.TheUnit.Value || mn == null || mn.uiMN == null || mn.gameMN == null
                || mn.uiMN.npcPanel == null || !mn.uiMN.npcPanel.activeSelf
                || mn.gameMN.nowAction || GameManager.nowEvent)
            {
                return;
            }
            CommonStates menu = mn.gameMN.menuNPC;
            bool held = menu != null && (_heldVar == Held.Menu || _heldVar == Held.Search)
                && ReferenceEquals(_heldRef, menu);
            if (held || (menu != null && ReferenceEquals(_keepWho, menu)))
            {
                return;
            }
            Note("released vanilla's menu" + (menu != null ? " for " + NameOf(menu) : "") + " (not the target)");
            ReleasePerson(mn);
            _seenMenu = null;
        }

        // For the harness: the state the race leaves - vanilla's menu up for this
        // person, seen already (so the reconcile does not adopt it), with no
        // target of ours on them. The next reconcile must release it.
        internal static string OrphanMenuFor(string name)
        {
            NPCManager npcs = _mn != null ? _mn.npcMN : null;
            for (int i = 0; npcs != null && npcs.friendList != null && i < npcs.friendList.Count; i++)
            {
                CommonStates who = npcs.friendList[i];
                if (who != null && who.charaName == name)
                {
                    if (_heldVar != Held.None && ReferenceEquals(_heldRef, who))
                    {
                        Untarget("harness");
                    }
                    _mn.uiMN.NPCMenuOpen(who);
                    _seenMenu = who;
                    return name + " menu up: " + _mn.uiMN.npcPanel.activeSelf;
                }
            }
            return "nobody named " + name;
        }

        // For the harness: is vanilla's person menu showing, and for whom
        internal static string VanillaMenu()
        {
            if (_mn == null || _mn.uiMN == null || _mn.uiMN.npcPanel == null)
            {
                return "no panel";
            }
            CanvasGroup veil = _mn.uiMN.npcPanel.GetComponent<CanvasGroup>();
            return (_mn.uiMN.npcPanel.activeSelf ? "up" : "down")
                + (veil != null && veil.alpha < 0.5f ? ", veiled" : "")
                + ", menuNPC " + NameOf(_mn.gameMN.menuNPC);
        }

        // NPCMenuClose without its first line: the menu, the glow and the subject go,
        // and the sub-inventory and pMove.target - which a new interaction owns - stay.
        private static void ReleasePerson(ManagersScript mn)
        {
            UIManager ui = mn.uiMN;
            if (ui == null)
            {
                return;
            }
            ui.OutGlow(null, false);
            mn.gameMN.menuNPC = null;
            mn.inventory.EquipSlotLoad();
            if (ui.npcPanel != null)
            {
                ui.npcPanel.SetActive(false);
            }
            ui.NPCMenuReset();
            ui.StatusChange(null);
        }

        // A pick of ours is about to drive vanilla for this person: adopt it as ours
        // (roster rules), not as a world click (vanilla's lifetime).
        private static void ArmPick(CommonStates who)
        {
            _adoptRosterFor = who;
            _adoptRosterUntil = Time.unscaledTime + 1f;
        }

        // a close of ours names itself before vanilla's variables clear
        private static void NoteEnd(string why)
        {
            if (_heldVar != Held.None)
            {
                _endWhy = why;
            }
        }

        private static string NameOf(object what)
        {
            if (what is CommonStates c)
            {
                return c != null ? (string.IsNullOrEmpty(c.charaName) ? "someone" : c.charaName) : "someone gone";
            }
            if (what is GameObject g)
            {
                return g != null ? g.name : "a thing gone";
            }
            return what != null ? what.ToString() : "nothing";
        }

        private static void Note(string line)
        {
            string stamped = Time.unscaledTime.ToString("0.0") + "s " + line;
            TargetLines.Add(stamped);
            if (TargetLines.Count > 40)
            {
                TargetLines.RemoveAt(0);
            }
            Plugin.Log.LogInfo("Target: " + line);
        }

        // For the harness: vanilla's own let-go, as a press on the ground makes it -
        // pMove.target emptied, so UseProp's and CheckNPC's loops end their way
        internal static void LetGoLikeTheGround()
        {
            if (_mn != null && _mn.gameMN != null && _mn.gameMN.pMove != null)
            {
                _mn.gameMN.pMove.target = null;
            }
        }

        // For the harness and the lifecycle scenario: the target now, and the last
        // begins and ends with their reasons.
        internal static string TargetLog()
        {
            string now = _heldVar == Held.None ? "none"
                : NameOf(_heldRef) + " [" + _heldVar + ", " + (_heldHow == Begun.Roster ? "ours" : "vanilla's") + "]";
            return "target: " + now + "\n" + string.Join("\n", TargetLines.ToArray());
        }
    }
}
