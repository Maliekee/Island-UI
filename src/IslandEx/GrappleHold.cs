using System;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace IslandEx
{
    // Hold the button instead of spamming it, in the grapple struggle.
    //
    // The struggle is one loop, in SexManager.ManRapes (SexManager.cs:574). It
    // drains the target's faint with `StunDamage(..., grapple: true)` and reaches
    // it two ways:
    //
    //   :589  a click            -> one hit, every press
    //   :579  the button HELD    -> one hit every `autoTime` seconds
    //
    // So holding is already in the game. What makes it useless is that `autoTime`
    // is the reward curve for skill 9 (:536): it starts at 100f — one hit every
    // hundred seconds, which is "never" — and only becomes usable as the skill
    // levels, 0.5 / 0.4 / 0.3 / 0.2 / 0.1. Early on there is no auto-repeat worth
    // the name, and spamming is the only way to make progress. That is the finger
    // strain: not a missing feature, a feature switched off until you grind for it.
    //
    // This opens that one knob and touches nothing else. Damage per hit, the timer,
    // the animation, the failure branch and clicking itself are all untouched — a
    // held button simply repeats at a usable rate. Taking the MINIMUM means a
    // player who HAS the skill never gets slowed down by us.
    //
    // `autoTime` and `tmpAutoTime` live across a `yield`, so the C# compiler hoists
    // them out of the coroutine into fields on its generated state-machine class —
    // which is what makes them reachable at all. They are found by name at runtime
    // rather than by index, and if the game is rebuilt and they move, this logs and
    // does nothing rather than guessing. Patched by hand after PatchAll for the
    // same reason: an iterator target that fails to resolve must not take the
    // FX filter down with it.
    internal static class GrappleHold
    {
        private static ConfigEntry<bool> _enabled;
        private static ConfigEntry<float> _interval;

        internal static void Bind(ConfigFile cfg)
        {
            _enabled = cfg.Bind("Grapple", "HoldToStruggle", true,
                "Holding the attack button in a grapple keeps hitting, so you need not click repeatedly. Off = the game's rule, where holding only works at high skill.");
            _interval = cfg.Bind("Grapple", "HoldInterval", 0.1f,
                "Seconds between hits while the button is held. 0.1 is ten a second, the game's own rate at full skill. Never slower than your skill already allows.");
        }

        // Cached per resolved state-machine type. Reflection runs once per struggle
        // frame, which is a handful of frames inside one scene.
        private static FieldInfo _autoTime;
        private static FieldInfo _tmpAutoTime;
        private static bool _warned;

        internal static void Apply(Harmony harmony)
        {
            try
            {
                MethodBase target = AccessTools.EnumeratorMoveNext(
                    AccessTools.Method(typeof(SexManager), nameof(SexManager.ManRapes)));
                if (target == null)
                {
                    Plugin.Log.LogWarning("GrappleHold: ManRapes iterator not found; hold-to-struggle is off");
                    return;
                }
                // the hoisted locals are named <autoTime>5__N — the angle brackets
                // are what keep "<autoTime>" from also matching "<tmpAutoTime>"
                foreach (FieldInfo f in AccessTools.GetDeclaredFields(target.DeclaringType))
                {
                    if (f.FieldType != typeof(float))
                    {
                        continue;
                    }
                    if (f.Name.Contains("<autoTime>"))
                    {
                        _autoTime = f;
                    }
                    else if (f.Name.Contains("<tmpAutoTime>"))
                    {
                        _tmpAutoTime = f;
                    }
                }
                if (_autoTime == null || _tmpAutoTime == null)
                {
                    Plugin.Log.LogWarning("GrappleHold: struggle timing fields not found "
                        + $"(autoTime={_autoTime != null}, tmpAutoTime={_tmpAutoTime != null}); hold-to-struggle is off");
                    return;
                }
                harmony.Patch(target, postfix: new HarmonyMethod(
                    AccessTools.Method(typeof(GrappleHold), nameof(Postfix))));
                Plugin.Log.LogInfo("GrappleHold: hold-to-struggle armed "
                    + $"({_autoTime.Name}, {_tmpAutoTime.Name})");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"GrappleHold: not armed — {e.Message}");
            }
        }

        // Runs at each yield of the struggle loop, so the value is re-asserted every
        // frame rather than set once: the loop reloads `tmpAutoTime` from `autoTime`
        // after each hit, and re-asserting is how anything that writes them back
        // gets overwritten rather than fought.
        internal static void Postfix(object __instance)
        {
            try
            {
                if (__instance == null || _enabled == null || !_enabled.Value || !Plugin.Enabled.Value)
                {
                    return;
                }
                float interval = Mathf.Clamp(_interval.Value, 0.02f, 2f);
                if ((float)_autoTime.GetValue(__instance) > interval)
                {
                    _autoTime.SetValue(__instance, interval);
                }
                // also clamp the countdown in flight, or the FIRST hit still waits
                // out whatever the skill curve had already loaded into it
                if ((float)_tmpAutoTime.GetValue(__instance) > interval)
                {
                    _tmpAutoTime.SetValue(__instance, interval);
                }
            }
            catch (Exception e)
            {
                if (!_warned)
                {
                    _warned = true;   // once: this runs every frame of the struggle
                    Plugin.Log.LogWarning($"GrappleHold: {e.Message}");
                }
            }
        }
    }
}
