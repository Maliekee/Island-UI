using HarmonyLib;
using IslandUI.UI;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI
{
    // A LEVEL-UP SAYS SO, with a slight notification rather than staying hidden, and
    // its tag tells how many skill/attribute points were awarded rather than the level
    // reached. Vanilla's notice is an effect on the player, a sound and a log line,
    // and a fight hides all three. Here the EXP rail that filled answers:
    //   its fill shows full and shines in its colour, then falls to what carried over;
    //   beside its right end a points tag pops up with what the level-up paid -
    //   cerulean attribute points for the character level, jade skill points for the
    //   skill level - holds, drifts up and fades;
    //   a burst (one kill, several levels) adds to the tag already up, never a second;
    //   the level coin on your plate and the Player dock's matching tag bump.
    // Heard from the two LevelUP methods, which only ExpChange calls: loading a save
    // or an NPC's NPCLevelUp never passes through them, so nothing fires falsely.
    internal static partial class InventoryWindow
    {
        private sealed class LevelBurst
        {
            public int Points;
            public float At = -100f;      // the last level-up, unscaled time
            public Image Shine;           // behind the rail
            public PointsTag Tag;
            public CanvasGroup Fade;
        }

        private static readonly LevelBurst CharacterBurst = new LevelBurst();
        private static readonly LevelBurst SkillBurst = new LevelBurst();

        private const float BurstFull = 0.25f;     // the fill shows full
        private const float BurstShine = 1.1f;     // the rail's shine, falling off
        private const float BurstHold = 2.2f;      // the tag, before it fades
        private const float BurstFade = 0.6f;
        private const float BurstPop = 0.3f;       // a bump, out and back
        private const float BurstTagH = 9f;
        private const float BurstDrift = 4f;       // how far the tag rises over its life

        internal static void OnCharacterLevelUp(CommonStates who)
        {
            var gm = _mn != null ? _mn.gameMN : null;
            var players = gm != null ? gm.playerCommons : null;
            int selected = GameManager.selectPlayer;
            if (players == null || selected < 0 || selected >= players.Length || players[selected] != who)
            {
                return;
            }
            Burst(CharacterBurst, who.level / 10 + 1);
        }

        internal static void OnSkillLevelUp(SkillManager sk)
        {
            Burst(SkillBurst, sk.skillLevel / 10 + 1);
        }

        // what LevelUP just paid, by vanilla's own sum (level / 10 + 1, the new level)
        private static void Burst(LevelBurst b, int points)
        {
            float now = Time.unscaledTime;
            b.Points = now - b.At < BurstHold + BurstFade ? b.Points + points : points;
            b.At = now;
        }

        private static void ClearBurstVisuals()
        {
            foreach (var b in new[] { CharacterBurst, SkillBurst })
            {
                b.Shine = null;
                b.Tag = null;
                b.Fade = null;
            }
        }

        // 1 at rest; out and back over BurstPop after the level-up
        private static float PopScale(LevelBurst b, float amount)
        {
            float since = Time.unscaledTime - b.At;
            return since >= 0f && since < BurstPop ? 1f + amount * Mathf.Sin(since / BurstPop * Mathf.PI) : 1f;
        }

        // Theme.Place pins a rect by its top-left; a bump grows from the middle
        private static void ScaleFromMiddle(RectTransform r, float scale)
        {
            var middle = new Vector2(.5f, .5f);
            Vector2 size = r.sizeDelta;
            r.anchoredPosition += Vector2.Scale(middle - r.pivot, size);
            r.pivot = middle;
            r.localScale = new Vector3(scale, scale, 1f);
        }

        // The rail's part: full for a moment, and a shine that falls off
        private static void BurstRail(LevelBurst b, ref float fraction, ref Color color, float width)
        {
            float since = Time.unscaledTime - b.At;
            if (since >= 0f && since < BurstFull)
            {
                fraction = 1f;
            }
            float shine = since >= 0f && since < BurstShine ? 1f - since / BurstShine : 0f;
            if (b.Shine != null)
            {
                bool on = shine > 0f;
                if (b.Shine.gameObject.activeSelf != on) { b.Shine.gameObject.SetActive(on); }
                if (on)
                {
                    Theme.Place(b.Shine, -1.5f, -.5f, width + 3f, PlayerRailH + 1f);
                    b.Shine.color = new Color(color.r, color.g, color.b, .45f * shine * shine);
                }
            }
            color = Color.Lerp(color, Color.white, .6f * shine * shine);
        }

        // The tags stand right of the rails' right end, the skill level's low and
        // the character level's above it, each in its own place whether or not the
        // other is up. `parent` is the lower rail's box, so the tags hide with the rails.
        private static void PlaceLevelTags(RectTransform parent)
        {
            PlaceLevelTag(CharacterBurst, parent, PointsTag.Kind.Attribute, BurstTagH * .5f + BurstTagH + 1.5f);
            PlaceLevelTag(SkillBurst, parent, PointsTag.Kind.Skill, BurstTagH * .5f);
        }

        private static void PlaceLevelTag(LevelBurst b, RectTransform parent, PointsTag.Kind kind, float middle)
        {
            float since = Time.unscaledTime - b.At;
            bool up = since >= 0f && since < BurstHold + BurstFade;
            if (b.Tag == null)
            {
                if (!up) { return; }
                b.Tag = PointsTag.Create(parent, kind == PointsTag.Kind.Skill ? "SkillLevelUp" : "CharacterLevelUp");
                b.Fade = b.Tag.Rect.gameObject.AddComponent<CanvasGroup>();
                b.Fade.interactable = b.Fade.blocksRaycasts = false;
            }
            if (!up)
            {
                b.Tag.Hide();
                return;
            }
            b.Tag.Show(kind, "+" + b.Points, BurstTagH);
            var r = b.Tag.Rect;
            r.anchorMin = r.anchorMax = Vector2.zero;
            r.pivot = new Vector2(.5f, .5f);
            float life = Mathf.Clamp01(since / (BurstHold + BurstFade));
            r.anchoredPosition = new Vector2(parent.sizeDelta.x + 3f + b.Tag.Width * .5f, middle + BurstDrift * life);
            float scale = PopScale(b, .45f);
            r.localScale = new Vector3(scale, scale, 1f);
            b.Fade.alpha = since < BurstHold ? 1f : 1f - (since - BurstHold) / BurstFade;
        }

        // for the harness: raise both as a level-up would, without touching the save
        internal static string LevelUpProbe(int characterPoints, int skillPoints)
        {
            if (characterPoints > 0) { Burst(CharacterBurst, characterPoints); }
            if (skillPoints > 0) { Burst(SkillBurst, skillPoints); }
            return "character +" + CharacterBurst.Points + ", skill +" + SkillBurst.Points;
        }
    }

    // The two level-ups, heard where they happen (InventoryWindow.LevelUp.cs).
    // The bare [HarmonyPatch] is load-bearing: the census skips a class without one.
    [HarmonyPatch]
    internal static class LevelUpPatches
    {
        [HarmonyPatch(typeof(CommonStates), nameof(CommonStates.LevelUP))]
        [HarmonyPostfix]
        private static void CommonStatesLevelUP_Postfix(CommonStates __instance)
        {
            InventoryWindow.OnCharacterLevelUp(__instance);
        }

        [HarmonyPatch(typeof(SkillManager), nameof(SkillManager.LevelUP))]
        [HarmonyPostfix]
        private static void SkillManagerLevelUP_Postfix(SkillManager __instance)
        {
            InventoryWindow.OnSkillLevelUp(__instance);
        }
    }
}
