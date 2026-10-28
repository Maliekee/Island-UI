using System.Collections.Generic;
using System.Text.RegularExpressions;
using HarmonyLib;
using IslandUI.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI
{
    // THE GAME LOG, AS A FEED (make_log_feed_ideas.py, design C). Vanilla's log
    // (UIManager.GoLogText) is a strip per line at the top left: cut off at a
    // fixed width, kept for a hundred lines, never fading, and reaching in under
    // the combat plaque. Its lines are drawn here instead, in the left column
    // under the counts: cream on a dark band that fades out to the right, a wood
    // tick at its edge, wrapped, names set bold (a dark name colour lifted until
    // it reads), a repeat folded into a square "×N" chip, four at most, each gone
    // after a few seconds. The console keeps every line (DevConsole's GoLogText
    // postfix), so the screen needs only the news of the moment.
    //
    // Vanilla's panel still fills, veiled with its +/- button (TickHud's hidden
    // row), so its rect still bounds the markers' top (PinCeiling, ArcSafe).
    //
    // UNDER THE WINDOWS: the feed stands just after vanilla's log among
    // MainCanvas's children, first of the stack KeepMarkerOverHud keeps, and
    // every window comes later - an open window on the left covers it.
    internal static partial class InventoryWindow
    {
        private const int FeedMax = 4;
        private const float FeedLife = 9f, FeedFadeFrom = 6f, FeedFadeIn = .15f;
        private const float FeedFont = 7.2f, FeedWidth = 175f, FeedLeft = 4.4f, FeedGap = 1.2f;
        private const float FeedPadX = 3.8f, FeedPadY = 1f, FeedTail = 28f, FeedTickW = .9f;
        private const float FeedChipGap = 3f, FeedChipH = 6.8f, FeedChipFont = 5.4f;
        private static readonly Color FeedBand = new Color(22 / 255f, 16 / 255f, 10 / 255f, .66f);
        private static readonly Color FeedTick = new Color(150 / 255f, 104 / 255f, 58 / 255f, 1f);
        private static readonly Color FeedShadow = new Color(0f, 0f, 0f, .8f);
        private static readonly Color FeedChip = Theme.ParseColor("#E8BA60", Color.yellow);
        private static readonly Color FeedChipRim = Theme.ParseColor("#765024", Color.black);
        private static readonly Color FeedChipInk = Theme.ParseColor("#261A10", Color.black);
        // a name as the game colours it: <color=#RRGGBB[AA]>Name</color>
        private static readonly Regex FeedName = new Regex("<color=#([0-9A-Fa-f]{6})(?:[0-9A-Fa-f]{2})?>(.*?)</color>");
        private static readonly Regex FeedColourTags = new Regex("</?color[^>]*>");
        private static readonly Vector3[] FeedCorners = new Vector3[4];

        private sealed class FeedLine
        {
            public string Raw;
            public int Count = 1;
            public float Age, Shown, Y;
            public bool Placed, Dirty = true;
            public RectTransform Root;
            public CanvasGroup Group;
            public Image Band, Tail, Chip, ChipRim;
            public TextMeshProUGUI Text, Shadow, ChipText;
        }

        private static readonly List<FeedLine> FeedLines = new List<FeedLine>();
        private static GameObject _logFeed;
        private static Sprite _feedFade;

        // a line the game logs; one already up comes down to the newest place
        // with its count raised, so a burst of the same news is one entry
        internal static void OnGameLog(string log)
        {
            if (string.IsNullOrEmpty(log)) { return; }
            for (int i = FeedLines.Count - 1; i >= 0; i--)
            {
                if (FeedLines[i].Raw != log) { continue; }
                FeedLine same = FeedLines[i];
                FeedLines.RemoveAt(i);
                same.Count++;
                same.Age = 0f;
                same.Dirty = true;
                FeedLines.Add(same);
                return;
            }
            while (FeedLines.Count >= FeedMax) { DropFeedLine(0); }
            FeedLines.Add(new FeedLine { Raw = log });
        }

        internal static void TickLogFeed(ManagersScript mn)
        {
            if (mn == null || !mn.allSet || mn.uiMN == null || mn.uiMN.logPanel == null) { return; }
            _mn = mn;
            // game time, but not sped up: a line is read at the speed you read
            float dt = Time.timeScale > 0f ? Time.unscaledDeltaTime : 0f;
            for (int i = FeedLines.Count - 1; i >= 0; i--)
            {
                FeedLine line = FeedLines[i];
                line.Age += dt;
                line.Shown += dt;
                if (line.Age >= FeedLife) { DropFeedLine(i); }
            }
            bool show = FeedLines.Count > 0;
            if (_logFeed == null && (!show || !EnsureLogFeed(mn))) { return; }
            if (_logFeed.activeSelf != show) { _logFeed.SetActive(show); }
            if (!show) { return; }
            KeepMarkerOverHud();
            var root = (RectTransform)_logFeed.transform;
            float y = FeedTop(root);
            float ease = 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime);
            foreach (FeedLine line in FeedLines)
            {
                if (line.Root == null) { BuildFeedLine(line, root); }
                if (line.Dirty) { LayFeedLine(line); }
                line.Y = line.Placed ? Mathf.Lerp(line.Y, y, ease) : y;
                line.Placed = true;
                var at = new Vector2(FeedLeft, -Mathf.Round(line.Y * 4f) / 4f);
                if (line.Root.anchoredPosition != at) { line.Root.anchoredPosition = at; }
                float alpha = Mathf.Clamp01(line.Shown / FeedFadeIn)
                    * (1f - Mathf.Clamp01((line.Age - FeedFadeFrom) / (FeedLife - FeedFadeFrom)));
                if (!Mathf.Approximately(line.Group.alpha, alpha)) { line.Group.alpha = alpha; }
                y += line.Root.sizeDelta.y + FeedGap;
            }
        }

        internal static void ResetLogFeed()
        {
            FeedLines.Clear();
            if (_logFeed != null) { Object.Destroy(_logFeed); }
            _logFeed = null;
        }

        private static void DropFeedLine(int i)
        {
            FeedLine line = FeedLines[i];
            if (line.Root != null) { Object.Destroy(line.Root.gameObject); }
            FeedLines.RemoveAt(i);
        }

        private static bool EnsureLogFeed(ManagersScript mn)
        {
            Transform log = mn.uiMN.logPanel.transform.parent;
            if (log == null || log.parent == null) { return false; }
            _logFeed = Owned.New("IslandUI_LogFeed");
            _logFeed.transform.SetParent(log.parent, false);
            var r = (RectTransform)_logFeed.transform;
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = r.offsetMax = Vector2.zero;
            CanvasIsolation.Isolate(_logFeed);
            // a feed, not a panel: clicks go through it to the world
            var raycaster = _logFeed.GetComponent<GraphicRaycaster>();
            if (raycaster != null) { raycaster.enabled = false; }
            return true;
        }

        // the top of the feed, down from the canvas's top: under the lowest of
        // the counts and the buff chips beside them, which grow a row as buffs come
        private static float FeedTop(RectTransform root)
        {
            Transform main = root.parent;
            float low = float.PositiveInfinity;
            FeedLow(main.Find("followCapaText"), root, ref low);
            FeedLow(main.Find("carryCapaText"), root, ref low);
            Transform buffs = main.Find("DebuffPanel");
            if (buffs != null)
            {
                for (int i = 0; i < buffs.childCount; i++) { FeedLow(buffs.GetChild(i), root, ref low); }
            }
            return float.IsPositiveInfinity(low) ? 96f : root.rect.yMax - low + 3f;
        }

        private static void FeedLow(Transform t, RectTransform root, ref float low)
        {
            var r = t as RectTransform;
            if (r == null || !r.gameObject.activeInHierarchy) { return; }
            r.GetWorldCorners(FeedCorners);   // 0 = bottom-left
            low = Mathf.Min(low, root.InverseTransformPoint(FeedCorners[0]).y);
        }

        private static void BuildFeedLine(FeedLine line, RectTransform root)
        {
            var go = Owned.New("Line");
            go.transform.SetParent(root, false);
            line.Root = (RectTransform)go.transform;
            line.Root.anchorMin = line.Root.anchorMax = line.Root.pivot = new Vector2(0f, 1f);
            line.Group = go.AddComponent<CanvasGroup>();
            line.Group.blocksRaycasts = false;
            line.Group.interactable = false;
            line.Group.alpha = 0f;
            line.Band = FeedImage(line.Root, "Band", FeedBand, null);
            line.Tail = FeedImage(line.Root, "Tail", FeedBand, FeedFade());
            FeedImage(line.Root, "Tick", FeedTick, null);
            line.Shadow = FeedText(line.Root, "Shadow", FeedFont, FeedShadow);
            line.Text = FeedText(line.Root, "Text", FeedFont, MarkerCream);
            line.ChipRim = FeedImage(line.Root, "ChipRim", FeedChipRim, null);
            line.Chip = FeedImage(line.ChipRim.transform, "Chip", FeedChip, null);
            line.ChipText = FeedText(line.Chip.transform, "Count", FeedChipFont, FeedChipInk);
            line.ChipText.alignment = TextAlignmentOptions.Midline;
            line.ChipText.enableWordWrapping = false;
            SetMarkerFont(line.ChipText, true);
        }

        // the words, the band behind them sized to them, and the chip after the last word
        private static void LayFeedLine(FeedLine line)
        {
            line.Dirty = false;
            string text = FeedName.Replace(line.Raw, m => "<color=#" + Lifted(m.Groups[1].Value) + "><b>" + m.Groups[2].Value + "</b></color>");
            line.Text.text = text;
            line.Shadow.text = FeedColourTags.Replace(text, "");
            float h = Mathf.Ceil(line.Text.GetPreferredValues(text, FeedWidth, 0f).y);
            FeedPlace(line.Text.rectTransform, FeedPadX, FeedPadY, FeedWidth, h);
            FeedPlace(line.Shadow.rectTransform, FeedPadX + .35f, FeedPadY + .5f, FeedWidth, h);
            line.Text.ForceMeshUpdate();
            float width = Mathf.Min(FeedWidth, line.Text.textBounds.size.x);
            float end = FeedPadX + width;
            bool chip = line.Count > 1;
            line.ChipRim.gameObject.SetActive(chip);
            if (chip)
            {
                line.ChipText.text = "×" + line.Count;
                float cw = Mathf.Ceil(line.ChipText.GetPreferredValues(line.ChipText.text).x) + 3.6f;
                // after the last character, on the last line's middle
                TMP_TextInfo info = line.Text.textInfo;
                float x = end, mid = FeedPadY + h / 2f;
                if (info.characterCount > 0 && info.lineCount > 0)
                {
                    TMP_LineInfo last = info.lineInfo[info.lineCount - 1];
                    TMP_CharacterInfo c = info.characterInfo[last.lastVisibleCharacterIndex];
                    x = FeedPadX + (c.topRight.x - line.Text.rectTransform.rect.xMin);
                    mid = FeedPadY + (line.Text.rectTransform.rect.yMax - (last.ascender + last.descender) / 2f);
                }
                if (x + FeedChipGap + cw > FeedPadX + FeedWidth + 12f) { x = FeedPadX + FeedWidth + 12f - FeedChipGap - cw; }
                FeedPlace(line.ChipRim.rectTransform, x + FeedChipGap, mid - FeedChipH / 2f, cw, FeedChipH);
                FeedPlace(line.Chip.rectTransform, .6f, .6f, cw - 1.2f, FeedChipH - 1.2f);
                FeedPlace(line.ChipText.rectTransform, 0f, 0f, cw - 1.2f, FeedChipH - 1.2f);
                end = Mathf.Max(end, x + FeedChipGap + cw);
            }
            float height = h + FeedPadY * 2f;
            float solid = end + FeedPadX;
            line.Root.sizeDelta = new Vector2(solid + FeedTail, height);
            FeedPlace(line.Band.rectTransform, 0f, 0f, solid, height);
            FeedPlace(line.Tail.rectTransform, solid, 0f, FeedTail, height);
            FeedPlace((RectTransform)line.Root.Find("Tick"), 0f, 0f, FeedTickW, height);
        }

        // a name colour too dark for the band, taken toward white until it reads
        private static string Lifted(string hex)
        {
            Color c = Theme.ParseColor("#" + hex, Color.white);
            float lum = c.r * .3f + c.g * .59f + c.b * .11f;
            if (lum >= .75f) { return hex; }
            float t = (.75f - lum) / (1f - lum);
            return ColorUtility.ToHtmlStringRGB(Color.Lerp(c, Color.white, t));
        }

        private static Image FeedImage(Transform parent, string name, Color color, Sprite sprite)
        {
            var go = Owned.New(name);
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.raycastTarget = false;
            image.sprite = sprite;
            image.color = color;
            var r = image.rectTransform;
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 1f);
            return image;
        }

        private static TextMeshProUGUI FeedText(Transform parent, string name, float size, Color color)
        {
            var text = Theme.MakeKitText(parent, name, size, color, TextAlignmentOptions.TopLeft);
            SetMarkerFont(text, false);
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Overflow;
            text.richText = true;
            text.raycastTarget = false;
            var r = text.rectTransform;
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 1f);
            return text;
        }

        // x right and y down from the parent's top-left
        private static void FeedPlace(RectTransform r, float x, float y, float w, float h)
        {
            r.anchoredPosition = new Vector2(x, -y);
            r.sizeDelta = new Vector2(w, h);
        }

        // the band's tail: opaque at its left, gone at its right, eased
        private static Sprite FeedFade()
        {
            if (_feedFade != null) { return _feedFade; }
            var tex = new Texture2D(32, 1, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "IslandUI_LogFeedFade",
            };
            for (int x = 0; x < 32; x++)
            {
                float t = 1f - (x + .5f) / 32f;
                tex.SetPixel(x, 0, new Color(1f, 1f, 1f, t * t * (3f - 2f * t)));
            }
            tex.Apply();
            _feedFade = Sprite.Create(tex, new Rect(0, 0, 32, 1), new Vector2(.5f, .5f), 100f);
            return _feedFade;
        }
    }

    // The game's log line, heard where it is made (InventoryWindow.LogFeed.cs).
    // The bare [HarmonyPatch] is load-bearing: the census skips a class without one.
    [HarmonyPatch]
    internal static class LogFeedPatches
    {
        [HarmonyPatch(typeof(UIManager), nameof(UIManager.GoLogText))]
        [HarmonyPostfix]
        private static void GoLogText_Postfix(string log)
        {
            InventoryWindow.OnGameLog(log);
        }
    }
}
