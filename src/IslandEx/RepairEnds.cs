using System.Collections;
using BepInEx.Configuration;
using HarmonyLib;

namespace IslandEx
{
    // A finished repair lets go of the hammer pose.
    //
    // PlayerMove.Repair, the coroutine behind the build panel's Repair button,
    // starts the "A_work_repair" loop on the player's skeleton. When the repair is
    // over - done, cancelled with a right click, or the wreck gone - it means to
    // put the player back at rest, but it does so only when the current animation
    // is "A_attack_tool", a name this coroutine never sets. So the repair loop is
    // never replaced: the player is free again (toolAction and nowAction are
    // cleared) and keeps hammering the air until they walk. The game's other work
    // actions end the way this one evidently meant to: Guard, for one, checks the
    // pose it set and then sets GetMoveAnim(0), the idle (or swimming idle).
    //
    // So Repair's coroutine is wrapped: once it has run to its end, and only if
    // the repair pose is still the current one and the player is alive, the idle
    // is set exactly as Guard does. Nothing else of the repair changes - its time,
    // its water, the hammer put away, the progress bar. Off, the coroutine is
    // handed on unwrapped.
    [HarmonyPatch]
    internal static class RepairEnds
    {
        private const string RepairAnim = "A_work_repair";

        private static ConfigEntry<bool> _enabled;

        internal static void Bind(ConfigFile cfg)
        {
            _enabled = cfg.Bind("Fixes", "Repair Ends Cleanly", true,
                "When a repair is over, the player stands at rest again. Off = the player keeps the repair pose until they move, as in the unmodded game. Applies live.");
        }

        private static bool On
        {
            get { return _enabled != null && _enabled.Value && Plugin.Enabled.Value; }
        }

        [HarmonyPatch(typeof(PlayerMove), nameof(PlayerMove.Repair))]
        [HarmonyPostfix]
        private static void Wrap(PlayerMove __instance, ref IEnumerator __result)
        {
            if (On && __result != null)
            {
                __result = RunThenRest(__instance, __result);
            }
        }

        // Every step of the game's coroutine is passed through as it is; the rest
        // runs in the same frame as its last step.
        private static IEnumerator RunThenRest(PlayerMove move, IEnumerator repair)
        {
            while (repair.MoveNext())
            {
                yield return repair.Current;
            }
            if (!On || move == null)
            {
                yield break;
            }
            CommonStates common = move.common;
            if (common != null && common.dead == 0 && common.anim != null
                && common.anim.AnimationName == RepairAnim)
            {
                common.anim.state.SetAnimation(0, move.GetMoveAnim(0), true);
            }
        }
    }
}
