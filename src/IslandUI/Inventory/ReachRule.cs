using UnityEngine;

namespace IslandUI
{
    // REACH IS ONE RULE SYSTEM (window-system.md 6.32, D145 and D149). Every
    // surface that asks "can I handle this from here" asks it here - the vicinity
    // window, quick-stack, bench-less crafting, the recipes of benches and
    // crafters nearby, a person's unit and bag, the roster's "In reach", an item
    // dropped on someone - so no two of them can disagree about the same chest.
    //
    // The numbers are the game's own, not ours:
    //   5 m  a bench links your special crafting storages (Type.Material)
    //        within 5 m of itself (InventoryManager.FindCraftReference);
    //   3 m  E looks for the nearest interactable within 3 m (PlayerMove.Update);
    //   construction range - vanilla's Ctrl reached your own props from
    //        buildableDist + 0.5, which the construction skill grows
    //        (SkillManager: buildableDist = 2 + 2 x level). Ctrl+click is
    //        gone (D158); the range is Tab's now, and alone it gates the
    //        building's own acts (LivePlacement).
    // Two vicinities: the special crafting chests keep 5 m, "normal" chests and
    // NPCs are reduced to 3 m - and the construction skill stretches your own
    // props. People take
    // 3 m and nothing else: vanilla's Ctrl range is one modifier for props and
    // people alike, and the follow distance is a slider, not a rule.
    //
    // NO EXIT (D149): a thing is live exactly within its reach, and flickers
    // when you - or a person - shift across the edge. A 2 m "stays live until"
    // buffer was built and taken out the same day: the flicker is preferred, for
    // its simplicity. So this is a pure function of distance, with no state.
    internal static class ReachRule
    {
        internal const float Near = 3f;
        internal const float Workshop = 5f;

        internal static float Construction(ManagersScript mn)
        {
            return mn != null && mn.buildMN != null ? mn.buildMN.buildableDist + 0.5f : 0f;
        }

        // How far one of YOUR containers reaches; 0 for one that is not yours -
        // a stranger's things are touch, vanilla's walk, and never pooled.
        internal static float Of(ManagersScript mn, InventorySlot container)
        {
            return container != null ? OfProp(mn, container.GetComponent<ItemInfo>()) : 0f;
        }

        // ...and any prop of yours, a container or not (a bed, a bench, a well).
        internal static float OfProp(ManagersScript mn, ItemInfo prop)
        {
            if (prop == null || mn == null || mn.buildMN == null || !mn.buildMN.OwnCheck(prop))
            {
                return 0f;
            }
            InventorySlot container = prop.GetComponent<InventorySlot>();
            float own = container != null && container.type == InventorySlot.Type.Material ? Workshop : Near;
            return Mathf.Max(own, Construction(mn));
        }

        // The sphere a scan must cover to see everything of yours that may be live.
        internal static float Widest(ManagersScript mn)
        {
            return Mathf.Max(Workshop, Construction(mn));
        }

        internal static bool Live(ManagersScript mn, InventorySlot container)
        {
            return mn != null && mn.gameMN != null && mn.gameMN.pMove != null
                && Live(container, mn.gameMN.pMove.transform.position, Of(mn, container));
        }

        // A thing you are dealing with: yours by its kind's reach; a stranger's by
        // touch, since vanilla opened it by walking you into it. Touch is measured
        // from the thing's EDGE, not its centre: vanilla's touch is collider
        // contact, which puts the centres 0.8-1.5 m apart by the chest's size, so
        // one centre number is loose for a small chest and tight for a big one.
        // Just 1 m, for non-owned chests.
        // It was 2 m centre to centre before, which read as looting from afar.
        internal const float Touch = 1f;

        internal static bool LiveProp(ManagersScript mn, ItemInfo prop)
        {
            if (prop == null || mn == null || mn.gameMN == null || mn.gameMN.pMove == null)
            {
                return false;
            }
            float reach = OfProp(mn, prop);
            Vector3 you = mn.gameMN.pMove.transform.position;
            return reach > 0f ? Live(prop, you, reach) : WithinOfEdge(prop, you, Touch);
        }

        // Distance to the nearest point of the thing's solid colliders' bounds.
        // Bounds, not Collider.ClosestPoint: that refuses a non-convex mesh, and a
        // box around a chest is the chest. Triggers are left out where there is a
        // solid collider - an interaction trigger can reach well past the thing -
        // and a thing with no collider at all falls back to its centre.
        private static readonly System.Collections.Generic.List<Collider> EdgeBuffer
            = new System.Collections.Generic.List<Collider>();

        internal static bool WithinOfEdge(Component thing, Vector3 from, float reach, bool triggersToo = false)
        {
            if (thing == null)
            {
                return false;
            }
            thing.GetComponentsInChildren(false, EdgeBuffer);
            float best = float.MaxValue;
            float bestTrigger = float.MaxValue;
            for (int i = 0; i < EdgeBuffer.Count; i++)
            {
                Collider c = EdgeBuffer[i];
                if (c == null || !c.enabled)
                {
                    continue;
                }
                float d = (c.bounds.ClosestPoint(from) - from).sqrMagnitude;
                if (c.isTrigger)
                {
                    bestTrigger = Mathf.Min(bestTrigger, d);
                }
                else
                {
                    best = Mathf.Min(best, d);
                }
            }
            EdgeBuffer.Clear();
            if (best == float.MaxValue || triggersToo)
            {
                best = Mathf.Min(best, bestTrigger);
            }
            return best == float.MaxValue ? Live(thing, from, reach) : best <= reach * reach;
        }

        // ...and how far that edge is, in metres (the centre's distance with no collider)
        internal static float EdgeDistance(Component thing, Vector3 from, bool triggersToo = false)
        {
            float lo = 0f, hi = (thing.transform.position - from).magnitude;
            if (!WithinOfEdge(thing, from, hi, triggersToo))
            {
                return hi;
            }
            for (int i = 0; i < 12; i++)
            {
                float mid = (lo + hi) * 0.5f;
                if (WithinOfEdge(thing, from, mid, triggersToo)) { hi = mid; } else { lo = mid; }
            }
            return hi;
        }

        // HOW MUCH CLOSER THE RULE WANTS YOU, in metres - the dock's tip for an act
        // it has dimmed, shown on hover. 0 or less: in reach.
        internal static float Gap(ManagersScript mn, CommonStates who)
        {
            if (who == null || mn == null || mn.gameMN == null || mn.gameMN.pMove == null)
            {
                return 0f;
            }
            Vector3 you = mn.gameMN.pMove.transform.position;
            if (Body(who))
            {
                return ReferenceEquals(mn.gameMN.pMove.searchNPC, who) ? 0f
                    : EdgeDistance(who, you, triggersToo: true) - BodyTouch;
            }
            return (who.transform.position - you).magnitude - Near;
        }

        // ...for a thing: its building acts by build range, the rest by OfProp
        // (yours) or by touch at its edge (a stranger's)
        internal static float Gap(ManagersScript mn, ItemInfo prop, bool placement)
        {
            if (prop == null || mn == null || mn.gameMN == null || mn.gameMN.pMove == null)
            {
                return 0f;
            }
            Vector3 you = mn.gameMN.pMove.transform.position;
            float centre = (prop.transform.position - you).magnitude;
            if (placement)
            {
                return centre - Construction(mn);
            }
            float reach = OfProp(mn, prop);
            return reach > 0f ? centre - reach : EdgeDistance(prop, you) - Touch;
        }

        // For the harness and the calibration round: how far you are
        // from a thing, centre and edge, and what the rule says.
        internal static string Report(ManagersScript mn, ItemInfo prop)
        {
            if (prop == null || mn == null || mn.gameMN == null || mn.gameMN.pMove == null)
            {
                return "no player or no prop";
            }
            Vector3 you = mn.gameMN.pMove.transform.position;
            float centre = (prop.transform.position - you).magnitude;
            float edge = 0f;
            while (edge < centre && !WithinOfEdge(prop, you, edge + 0.05f))
            {
                edge += 0.05f;
            }
            return prop.itemKey + ": centre " + centre.ToString("0.00") + " m, edge ~" + edge.ToString("0.00")
                + " m, reach " + OfProp(mn, prop).ToString("0.0") + ", live " + LiveProp(mn, prop);
        }

        // THE BUILDING'S OWN ACTS - Move, Remove, Repair - are the construction
        // skill's - everything can still be selected with Tab, but moving and
        // removing become available only in the allowed range: live within
        // build range alone, the range the game builds in and its Ctrl reached from
        // before D158 took Ctrl+click out. Tab targets the thing from anywhere; the
        // rest of it answers OfProp.
        internal static bool LivePlacement(ManagersScript mn, ItemInfo prop)
        {
            return prop != null && mn != null && mn.gameMN != null && mn.gameMN.pMove != null
                && mn.buildMN != null && mn.buildMN.OwnCheck(prop)
                && Live(prop, mn.gameMN.pMove.transform.position, Construction(mn));
        }

        internal static bool Live(ManagersScript mn, CommonStates who)
        {
            if (mn == null || mn.gameMN == null || mn.gameMN.pMove == null)
            {
                return false;
            }
            Vector3 you = mn.gameMN.pMove.transform.position;
            // A BODY IS TOUCH (D159: dissecting or carrying is restricted to being
            // close, about one metre): its carry, its dissect and its pockets from
            // near its edge - lying down it is two metres long, so its centre says
            // little about the end you stand at. The GAME'S OWN SEARCH is in reach
            // by definition: a click walked you into its collider and opened it, and
            // the 1 m from the solid colliders said "out of reach" over a body you
            // were looting - so vanilla's behaviour is kept: the search counts, and
            // the edge is every collider's, the click's trigger included, at 1.5 m.
            if (Body(who))
            {
                return ReferenceEquals(mn.gameMN.pMove.searchNPC, who)
                    || WithinOfEdge(who, you, BodyTouch, triggersToo: true);
            }
            return Live(who, you, Near);
        }

        internal const float BodyTouch = 1.5f;

        // Down on the ground - dead, or knocked out with life left (dead == 1); not
        // one carried (5) or already in parts (100)
        internal static bool Body(CommonStates who)
        {
            return who != null && who.dead == 1;
        }

        internal static bool Live(Component thing, Vector3 from, float reach)
        {
            return thing != null && reach > 0f
                && (thing.transform.position - from).sqrMagnitude <= reach * reach;
        }
    }
}
