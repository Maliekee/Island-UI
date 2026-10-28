using IslandUI.UI;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI
{
    // LOW VITALS (Forgejo #86): health, food or water running low lights its own
    // bar or icon on the vitals panel, no new furniture. Each glows in its painted
    // shape, amber when low and red when critical, under the fill so the empty part
    // still reads empty. Stepping down a level - low, critical, food or water empty -
    // flashes it for a moment to be seen from the corner of the eye; then it settles
    // into a calm breath when low, a pulse when critical (health's a heartbeat), and
    // fades out once restored. Vanilla warns of nothing short of zero, and at zero
    // only by the stomach's growl for food (GameManager.FoodChange); an empty food
    // or water bar then costs health on each hunger tick and stops healing (Update).
    internal static partial class InventoryWindow
    {
        private enum Want { None, Low, Critical, Empty }

        private sealed class LowVital
        {
            public Image Aura;
            public RectTransform Body;   // what the step-down flash bumps
            public TMPro.TextMeshProUGUI Reading;   // a number warmed with the aura; null = none
            public Want Level;
            public float Since = -99f;   // when Level last rose
            public float Shown;          // the aura's presence, 0-1, eased
            public bool Heart;           // a heartbeat rather than a breath
            public bool Red;             // critical or worse when last warned, kept while it fades
        }

        private static readonly LowVital[] LowVitals = new LowVital[3];
        private static readonly Color LowAmber = new Color(1f, .58f, .12f, 1f);
        // the readings' warm tints, light enough to read on the dark bar
        private static readonly Color LowReadAmber = new Color(1f, .76f, .4f, 1f), LowReadRed = new Color(1f, .52f, .46f, 1f);
        // the flash's length; the aura's fade in and out, per second; the slack to leave a level
        private const float LowFlash = 1.8f, LowIn = 5f, LowOut = 1.5f, LowSlack = .03f;

        private static void ResetLowVitals() { System.Array.Clear(LowVitals, 0, LowVitals.Length); }

        // `fill` is the vital's fill image, placed in its rect from the top-left: the
        // aura goes under it in the same parent, reaching AuraPad texels past it.
        // The health bar is too wide to bump, so its reading bumps and warms instead.
        private static LowVital LowVitalAura(Image fill, string key, RectTransform body, TMPro.TextMeshProUGUI reading, bool heart)
        {
            Sprite aura = PlayerPanelArt.Aura(key);
            Sprite own = fill.sprite;
            var image = PlayerPanelArt.Image(fill.transform.parent, "LowAura", aura);
            image.transform.SetSiblingIndex(fill.transform.GetSiblingIndex());
            Vector2 size = fill.rectTransform.sizeDelta;
            float padX = size.x * PlayerPanelArt.AuraPad / own.rect.width, padY = size.y * PlayerPanelArt.AuraPad / own.rect.height;
            Theme.Place(image, -padX, -padY, size.x + 2f * padX, size.y + 2f * padY);
            image.gameObject.SetActive(false);
            ScaleFromMiddle(body, 1f);
            return new LowVital { Aura = image, Body = body, Reading = reading, Heart = heart };
        }

        private static void BuildLowVitals()
        {
            LowVitals[0] = LowVitalAura(_playerHpFill, "health", _playerHpText.rectTransform, _playerHpText, true);
            LowVitals[1] = LowVitalAura(_playerFood, "food", (RectTransform)_playerFood.transform.parent, null, false);
            LowVitals[2] = LowVitalAura(_playerWater, "water", (RectTransform)_playerWater.transform.parent, null, false);
        }

        // Fractions of their maximum; below zero = not known, no warning.
        private static void TickLowVitals(float health, float food, float water)
        {
            if (LowVitals[0] == null) BuildLowVitals();
            bool on = Settings.LowVitals.Value;
            float healthAt = Settings.LowHealthAt.Value, needsAt = Settings.LowNeedsAt.Value;
            PaintLowVital(LowVitals[0], on ? Judge(LowVitals[0].Level, health, healthAt, false) : Want.None);
            PaintLowVital(LowVitals[1], on ? Judge(LowVitals[1].Level, food, needsAt, true) : Want.None);
            PaintLowVital(LowVitals[2], on ? Judge(LowVitals[2].Level, water, needsAt, true) : Want.None);
        }

        // Low at or under `at`, critical at half of it, empty at nothing (food and
        // water only: then the game drains health and stops healing). A level is
        // left only `LowSlack` above its line, so regeneration ticking across it
        // does not flash it again and again.
        private static Want Judge(Want was, float f, float at, bool empties)
        {
            if (f < 0f || at <= 0f) return Want.None;
            Want now = empties && f <= 0f ? Want.Empty : f <= at * .5f ? Want.Critical : f <= at ? Want.Low : Want.None;
            if (now >= was) return now;
            float line = was == Want.Empty ? 0f : was == Want.Critical ? at * .5f : at;
            return f > line + LowSlack ? now : was;
        }

        private static void PaintLowVital(LowVital v, Want want)
        {
            float now = Time.unscaledTime;
            if (want > v.Level) v.Since = now;
            v.Level = want;
            if (want != Want.None) v.Red = want >= Want.Critical;
            v.Shown = Mathf.MoveTowards(v.Shown, want == Want.None ? 0f : 1f, Time.unscaledDeltaTime * (want == Want.None ? LowOut : LowIn));
            bool lit = v.Shown > 0f;
            if (v.Aura.gameObject.activeSelf != lit) v.Aura.gameObject.SetActive(lit);
            float since = now - v.Since, bump = 1f, warm = 0f;
            if (lit)
            {
                bool red = v.Red;
                float period = v.Heart ? (red ? .9f : 1.5f) : (red ? 1.1f : 2.6f);
                float k = v.Heart ? HeartBeat(now / period) : .5f + .5f * Mathf.Sin(now * (2f * Mathf.PI / period));
                float a = red ? Mathf.Lerp(.45f, 1f, k) : Mathf.Lerp(.4f, .8f, k);
                warm = Mathf.Lerp(.6f, 1f, k);
                // the flash: full strength, three beats of a bump, easing into the pulse
                if (since < LowFlash && want != Want.None)
                {
                    float flash = 1f - since / LowFlash;
                    float beat = Mathf.Abs(Mathf.Sin(since / LowFlash * 3f * Mathf.PI));
                    a = Mathf.Lerp(a, 1f, flash);
                    warm = Mathf.Lerp(warm, 1f, flash);
                    bump = 1f + .3f * flash * beat;
                }
                else if (red) bump = 1f + .07f * k;
                Color c = red ? ThreatRed : LowAmber;
                c.a = a * v.Shown;
                v.Aura.color = c;
            }
            v.Body.localScale = new Vector3(bump, bump, 1f);
            if (v.Reading != null) v.Reading.color = Color.Lerp(MarkerCream, v.Red ? LowReadRed : LowReadAmber, warm * v.Shown);
        }

        // lub-dub: a strong beat and a softer one close behind, at rest between
        private static float HeartBeat(float t)
        {
            t -= Mathf.Floor(t);
            return Mathf.Max(Spike(t, 0f), Spike(t, 1f), .6f * Spike(t, .28f));
        }

        private static float Spike(float t, float at)
        {
            float d = (t - at) / .08f;
            return Mathf.Exp(-d * d);
        }

        // harness: the selected character's life, food and water set outright (below
        // zero = left alone), as the game's own changes would leave them. Returns the
        // values before, for putting them back after a test.
        internal static string VitalsProbe(float life, float food, float water)
        {
            GameManager gm = _mn != null ? _mn.gameMN : null;
            int at = GameManager.selectPlayer;
            if (gm == null || gm.playerCommons == null || at < 0 || at >= gm.playerCommons.Length || gm.playerCommons[at] == null) return "no player";
            CommonStates who = gm.playerCommons[at];
            string before = who.life.ToString("0") + "," + who.food.ToString("0.##") + "," + who.water.ToString("0.##")
                + " (max " + who.maxLife.ToString("0") + "," + who.maxFood.ToString("0") + "," + who.maxWater.ToString("0") + ")";
            if (life >= 0f) who.life = System.Math.Min(life, who.maxLife);
            if (food >= 0f) who.food = Mathf.Min(food, who.maxFood);
            if (water >= 0f) who.water = Mathf.Min(water, who.maxWater);
            gm.PlayerStatesCheck();
            return before;
        }

        // the hover's colour for a reading in this vital's state
        private static string LowWord(int index, string reading)
        {
            var v = LowVitals[index];
            if (v == null || v.Level == Want.None) return reading;
            return (v.Level >= Want.Critical ? "<color=#ff6a5a>" : "<color=#ffb43c>") + reading + "</color>";
        }
    }
}
