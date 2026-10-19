using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI
{
    // Probes inside Unity's own UI pipeline, to attribute the engine-side frame
    // spike after menu opens: PerformUpdate is the whole per-frame UI rebuild
    // (layout passes + graphic mesh regeneration), LayoutRebuilder.Rebuild is one
    // layout root within it. Installed manually with try/catch — engine-internal
    // signatures must never abort PatchAll for the functional patches.
    internal static class UIProbes
    {
        private static readonly System.Diagnostics.Stopwatch TotalTimer = new System.Diagnostics.Stopwatch();
        private static readonly System.Diagnostics.Stopwatch LayoutTimer = new System.Diagnostics.Stopwatch();
        private static double _layoutMs;
        private static int _layoutRoots;
        private static double _maxLayoutMs;
        private static string _maxLayoutRoot;

        private static AccessTools.FieldRef<LayoutRebuilder, RectTransform> _toRebuild;

        internal static void Install(Harmony harmony)
        {
            try
            {
                _toRebuild = AccessTools.FieldRefAccess<LayoutRebuilder, RectTransform>("m_ToRebuild");
                harmony.Patch(AccessTools.Method(typeof(CanvasUpdateRegistry), "PerformUpdate"),
                    prefix: new HarmonyMethod(typeof(UIProbes), nameof(PerformUpdate_Prefix)),
                    postfix: new HarmonyMethod(typeof(UIProbes), nameof(PerformUpdate_Postfix)));
                harmony.Patch(AccessTools.Method(typeof(LayoutRebuilder), nameof(LayoutRebuilder.Rebuild)),
                    prefix: new HarmonyMethod(typeof(UIProbes), nameof(LayoutRebuild_Prefix)),
                    postfix: new HarmonyMethod(typeof(UIProbes), nameof(LayoutRebuild_Postfix)));
                Plugin.Log.LogDebug("UI pipeline probes installed");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"UI pipeline probes unavailable: {e.Message}");
            }
        }

        private static void PerformUpdate_Prefix()
        {
            TotalTimer.Restart();
            _layoutMs = 0;
            _layoutRoots = 0;
            _maxLayoutMs = 0;
            _maxLayoutRoot = null;
        }

        private static void PerformUpdate_Postfix()
        {
            TotalTimer.Stop();
            double totalMs = TotalTimer.Elapsed.TotalMilliseconds;
            if (totalMs > 15)
            {
                Plugin.Log.LogDebug($"UIRebuild frame {Time.frameCount}: total {totalMs:0}ms — " +
                    $"layout {_layoutMs:0}ms/{_layoutRoots} roots (max '{_maxLayoutRoot}' {_maxLayoutMs:0.0}ms), " +
                    $"graphics+rest {totalMs - _layoutMs:0}ms");
            }
        }

        private static void LayoutRebuild_Prefix()
        {
            LayoutTimer.Restart();
        }

        private static void LayoutRebuild_Postfix(LayoutRebuilder __instance)
        {
            LayoutTimer.Stop();
            double ms = LayoutTimer.Elapsed.TotalMilliseconds;
            _layoutMs += ms;
            _layoutRoots++;
            if (ms > _maxLayoutMs)
            {
                _maxLayoutMs = ms;
                RectTransform root = _toRebuild != null ? _toRebuild(__instance) : null;
                _maxLayoutRoot = root != null ? root.name : "?";
            }
        }
    }

    // Millisecond timers on the suspected closet/chest hot paths, so the BepInEx log
    // names the offender instead of us guessing from feel. Debug-level, near-zero
    // cost when the methods are fast.
    [HarmonyPatch]
    internal static class PerfProbes
    {
        private static readonly System.Diagnostics.Stopwatch AppliableTimer = new System.Diagnostics.Stopwatch();
        private static readonly System.Diagnostics.Stopwatch SubLoadTimer = new System.Diagnostics.Stopwatch();

        // apparel closet: builds the "appliable" clothing list — per owned item it does
        // Spine attachment lookups + (cold) NPC-roster equipability scans
        [HarmonyPatch(typeof(RandomCharacter), nameof(RandomCharacter.LoadAppliable))]
        [HarmonyPrefix]
        private static void LoadAppliable_Prefix()
        {
            AppliableTimer.Restart();
        }

        [HarmonyPatch(typeof(RandomCharacter), nameof(RandomCharacter.LoadAppliable))]
        [HarmonyPostfix]
        private static void LoadAppliable_Postfix()
        {
            AppliableTimer.Stop();
            if (AppliableTimer.ElapsedMilliseconds > 10)
            {
                Plugin.Log.LogDebug($"LoadAppliable took {AppliableTimer.ElapsedMilliseconds}ms");
            }
        }

        // chest/closet open: moves the container contents into the mirror UI slots
        [HarmonyPatch(typeof(InventoryManager), nameof(InventoryManager.SubInventoryLoad))]
        [HarmonyPrefix]
        private static void SubInventoryLoad_Prefix()
        {
            SubLoadTimer.Restart();
        }

        [HarmonyPatch(typeof(InventoryManager), nameof(InventoryManager.SubInventoryLoad))]
        [HarmonyPostfix]
        private static void SubInventoryLoad_Postfix()
        {
            SubLoadTimer.Stop();
            if (SubLoadTimer.ElapsedMilliseconds > 10)
            {
                Plugin.Log.LogDebug($"SubInventoryLoad took {SubLoadTimer.ElapsedMilliseconds}ms");
            }
        }
    }
}
