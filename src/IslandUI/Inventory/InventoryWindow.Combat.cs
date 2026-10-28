using System.Collections.Generic;
using IslandUI.UI;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI
{
    // THE FIGHT ON SCREEN (Forgejo #63): the combat list (CombatList) drawn. The
    // plaque at the top centre shows its first (Anime.cs); the first few - the
    // cap, Settings.CombatMarkers - carry a light marker over their heads: the
    // world marker's plate cut down to its two tracks, health and faint, with no
    // card, no words. The one you target wears its own marker instead. The rest
    // are counted on the plaque ("+3 in combat"). The markers are a pool, kept
    // and repainted, and a bar is only laid out again when its value moves.
    // The pool also lends one to whoever the pointer is on (HoverMarked), and to
    // your people in the fight (Forgejo #98), who wear it with a jade bar.
    internal static partial class InventoryWindow
    {
        private sealed class LightMarker
        {
            internal RectTransform Root;
            internal Image Glow, Plate, Health, HealthFill, Faint, FaintFill;
            internal SheetRow HealthRuler, StunRuler;   // the target marker's rulers (PaintMarkerRuler)
            internal bool OnYou;
            internal CommonStates Who;
            internal Renderer Body;
            internal float Hp, Stun;
            internal float Rise = -1f;      // its head over its feet, as drawn (PaintLightMarker)
            internal float Peak, PeakAt;    // the highest of the last RiseHold seconds, and when
            internal string Fill;           // the health fill's piece: the stance's colour (Stance.HealthFill)
            internal RimArrow Rim;          // the world marker's arrow and distance, made when first needed
            internal Vector2 Push;
            internal int FarMetres = -1;
            internal string FarWords;
            // this frame's place (PaintLightMarker), before it stands there
            internal Vector2 Local, Wanted;
            internal Rect Bounds;
            internal string Far;
            internal float FarW;
            internal int Edge;
        }

        private static GameObject _combatLayer;
        private static readonly List<LightMarker> LightMarkers = new List<LightMarker>();
        private static readonly Vector3[] CombatCorners = new Vector3[4];

        // in the world marker's units, drawn at its scale (MarkerScale)
        private const float LightW = 40f, LightH = 13f, LightFrame = .45f, LightPad = 2.6f;
        private const float LightHpY = 2.8f, LightHpH = 4.2f, LightFaintY = 7.6f, LightFaintH = 2.6f;
        private const float LightLift = 2.2f;   // over the feet, for a body with no renderer
        private const float RiseHold = 1.5f;    // s the body's highest is kept before it may settle
        private const float RiseFall = .3f;     // m/s it then settles
        private const float RiseClimb = 1.5f;   // m/s the marker glides to it

        internal static void TickCombatMarkers(ManagersScript mn)
        {
            long heap = CombatList.Heap();
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            PaintCombatMarkers(mn);
            CombatList.MeasureDraw(System.Diagnostics.Stopwatch.GetTimestamp() - started);
            CombatList.Counted(heap);
        }

        private static void PaintCombatMarkers(ManagersScript mn)
        {
            if (mn == null || !mn.allSet || mn.uiMN == null || mn.gameMN == null) { return; }
            _mn = mn;
            Stance.Tick(mn);
            int cap = Settings.CombatMarkersCap;
            // ALWAYS ON lifts the count for its side: all of them carry one
            int enemyCap = Settings.AlwaysEnemyMarkers.Value ? int.MaxValue : cap;
            int friendCap = Settings.AlwaysFriendMarkers.Value ? int.MaxValue : cap;
            Camera camera = Camera.main;
            // the one whose own marker is up: the unit's target, while it shows
            CommonStates target = _worldMarker != null && _worldMarker.gameObject.activeInHierarchy ? _unitFor : null;
            CommonStates hover = HoverMarked(mn, target);
            ScanAlways(mn.gameMN.pMove != null ? mn.gameMN.pMove.common : null);
            bool show = (enemyCap > 0 && CombatList.Count > 0 || friendCap > 0 && CombatList.FriendCount > 0 || Always.Count > 0 || hover != null)
                && camera != null && !InScene(mn)
                && !MenuOverMarker() && EnsureCombatLayer(mn);
            if (_combatLayer != null && _combatLayer.activeSelf != show) { _combatLayer.SetActive(show); }
            RectTransform parent = show ? (RectTransform)_combatLayer.transform : null;
            Camera uiCam = null;
            float floor = 0f, ceiling = 0f;
            Rect plaque = Rect.zero;
            bool plaqueUp = false;
            if (show)
            {
                KeepMarkerOverHud();
                var canvas = parent.parent.GetComponentInParent<Canvas>();
                uiCam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
                floor = Mathf.Max(parent.rect.yMin + 51f, HudTop(parent) + 6f);
                ceiling = PinCeiling(parent);
                plaqueUp = PlaqueRect(parent, out plaque);
            }
            int slots = 0, used = 0;
            for (int i = 0; i < CombatList.Count; i++)
            {
                CombatList.Entry e = CombatList.At(i);
                // NO LINGER IN THE WORLD: the list keeps the dead and the departed a
                // moment (CombatList.Linger) for the plaque, whose bar is seen to
                // reach 0; their light marker goes at once, and its slot with it
                if (Gone(e)) { e.Marked = false; continue; }
                bool slot = slots < enemyCap;
                if (slot) { slots++; }
                bool targeted = target != null && ReferenceEquals(e.Who, target);
                e.Marked = slot || targeted;
                if (slot && !targeted && show)
                {
                    PaintLightMarker(LightMarkerAt(used++), e.Who, e.OnYou, true, camera, parent, uiCam, floor, ceiling);
                }
                if (slot && ReferenceEquals(e.Who, hover)) { hover = null; }   // it has its marker
            }
            // YOUR PEOPLE IN THE FIGHT (Forgejo #98): after the enemies, on a count
            // of their own as long as theirs, so the enemies keep every slot the
            // setting gives them and one of yours never takes one. The list keeps
            // it off when the setting is (CombatList.TickFriends).
            int friends = 0;
            for (int i = 0; i < CombatList.FriendCount; i++)
            {
                CombatList.Entry e = CombatList.FriendAt(i);
                if (Gone(e)) { e.Marked = false; continue; }
                // struck by you since, it wears the enemies' marker
                bool slot = friends < friendCap && CombatList.Of(e.Who) == null;
                if (slot) { friends++; }
                bool targeted = target != null && ReferenceEquals(e.Who, target);
                e.Marked = slot || targeted;
                if (slot && !targeted && show)
                {
                    PaintLightMarker(LightMarkerAt(used++), e.Who, false, true, camera, parent, uiCam, floor, ceiling);
                }
                if (slot && ReferenceEquals(e.Who, hover)) { hover = null; }
            }
            // ALWAYS ON (Settings.AlwaysFriendMarkers, AlwaysEnemyMarkers): the rest
            // of that side on the screen, out of a fight or not marked in it. Only on
            // the screen - every one of them waiting on the rim was a fence of arrows
            if (show)
            {
                for (int i = 0; i < Always.Count; i++)
                {
                    CommonStates c = Always[i];
                    if (!AlwaysShown(c, target, camera)) { continue; }
                    PaintLightMarker(LightMarkerAt(used++), c, false, false, camera, parent, uiCam, floor, ceiling);
                    if (ReferenceEquals(c, hover)) { hover = null; }
                }
            }
            _hoverShown = show ? hover : null;
            if (hover != null && show)
            {
                CombatList.Entry fighting = CombatList.Of(hover);
                PaintLightMarker(LightMarkerAt(used++), hover, fighting != null && fighting.OnYou, false, camera, parent, uiCam, floor, ceiling);
            }
            if (used > 0)
            {
                for (int edge = RimBottom; edge <= RimRight; edge++) { SpreadOnRim(used, edge); }
                StackOverCrowd(used);
                for (int i = 0; i < used; i++) { FinishLightMarker(LightMarkers[i], parent, floor, plaque, plaqueUp); }
            }
            for (int i = used; i < LightMarkers.Count; i++)
            {
                LightMarker m = LightMarkers[i];
                if (m.Root.gameObject.activeSelf) { m.Root.gameObject.SetActive(false); }
                m.Who = null;
            }
            // the target's own marker, and its arrow on the rim, glow as these do
            CombatList.Entry own = CombatList.Of(target);
            bool threat = own != null && own.OnYou;
            if (_markerRim != null) { _markerRim.Threat = threat; }
            PaintMarkerThreat(threat);
        }

        // ---------- always on ----------

        // Everyone of yours, every hostile and every neutral one in the scene, by the settings,
        // read twice a second from the people counted in as they woke
        // (ScenePeople): the engine's search of the scene was a 24 ms hitch each
        // time (Forgejo #154). Each frame takes the ones standing on the screen
        // (AlwaysShown).
        private const float AlwaysScanEvery = .5f;
        private static readonly List<CommonStates> Always = new List<CommonStates>();
        private static readonly List<CommonStates> AlwaysPeople = new List<CommonStates>();
        private static float _alwaysAt = float.NegativeInfinity;

        private static void ScanAlways(CommonStates player)
        {
            bool friends = Settings.AlwaysFriendMarkers.Value, enemies = Settings.AlwaysEnemyMarkers.Value, neutral = Settings.AlwaysNeutralMarkers.Value;
            if (!friends && !enemies && !neutral)
            {
                Always.Clear();
                _alwaysAt = float.NegativeInfinity;
                return;
            }
            float now = Time.unscaledTime;
            if (now - _alwaysAt < AlwaysScanEvery) { return; }
            _alwaysAt = now;
            Always.Clear();
            foreach (CommonStates c in ScenePeople.Live(AlwaysPeople))
            {
                if (ReferenceEquals(c, player)) { continue; }
                // each stance by its own setting (Stance, target-matrix D4)
                StanceKind s = Stance.Living(c);
                if (s == StanceKind.Yours ? friends : s == StanceKind.Hostile ? enemies : neutral) { Always.Add(c); }
            }
        }

        // harness: who stands on the screen, what the game makes of them, and
        // whether the always-on pass marks them
        internal static string AlwaysProbe()
        {
            var sb = new System.Text.StringBuilder();
            Camera camera = Camera.main;
            CommonStates player = _mn != null && _mn.gameMN != null && _mn.gameMN.pMove != null ? _mn.gameMN.pMove.common : null;
            int yours = 0;
            for (int i = 0; i < Always.Count; i++) { if (Stance.Living(Always[i]) == StanceKind.Yours) { yours++; } }
            sb.Append("always ").Append(Always.Count).Append(", yours ").Append(yours).Append(" (friends ").Append(Settings.AlwaysFriendMarkers.Value)
                .Append(", enemies ").Append(Settings.AlwaysEnemyMarkers.Value).Append(", neutral ").Append(Settings.AlwaysNeutralMarkers.Value).Append(")");
            if (camera == null) { return sb.ToString(); }
            CommonStates target = _worldMarker != null && _worldMarker.gameObject.activeInHierarchy ? _unitFor : null;
            foreach (CommonStates c in ScenePeople.Live(new List<CommonStates>()))
            {
                if (ReferenceEquals(c, player)) { continue; }
                Vector3 at = camera.WorldToViewportPoint(c.transform.position);
                if (at.z <= 0f || at.x < 0f || at.x > 1f || at.y < 0f || at.y > 1f) { continue; }
                NPCMove m = c.nMove;
                sb.Append("\n").Append(c.charaName).Append(" type ").Append(c.charaType)
                    .Append(" ").Append(m != null ? m.npcType + "/" + m.actType + " patrol " + m.patrolState : "-")
                    .Append(m != null && m.tmpEnemy != null ? " on " + m.tmpEnemy.name : "")
                    .Append(m != null ? " order " + m.orderType + " sub " + m.subType + (m.movable ? "" : " UNMOVABLE")
                        + " sees " + m.searchAngle.ToString("0") + "deg/" + m.searchRadius.ToString("0") + "m" : "")
                    .Append(" group ").Append(c.groupID)
                    .Append(" employ ").Append(c.employ).Append(" life ").Append(c.life.ToString("0")).Append("/").Append(c.maxLife.ToString("0"))
                    .Append(" ").Append(Stance.Of(c)).Append(" dead ").Append(c.dead)
                    .Append(Always.Contains(c) ? " ALWAYS" : "")
                    .Append(Always.Contains(c) && !AlwaysShown(c, target, camera) ? " (not shown)" : "");
            }
            return sb.Append("\n").Append(Stance.Probe()).ToString();
        }

        // standing, on the screen, and wearing no marker yet: not the target, nor
        // one the fight's passes marked. The hover's rule for who has a health to
        // show (HoverMarked)
        private static bool AlwaysShown(CommonStates c, CommonStates target, Camera camera)
        {
            if (c == null || !c.gameObject.activeInHierarchy || c.maxLife <= 0 || c.life <= 0 || c.dead == 1
                || ReferenceEquals(c, target))
            {
                return false;
            }
            CombatList.Entry e = CombatList.Of(c) ?? CombatList.FriendOf(c);
            if (e != null && e.Marked) { return false; }
            Vector3 at = camera.WorldToViewportPoint(c.transform.position);
            return at.z > 0f && at.x >= 0f && at.x <= 1f && at.y >= 0f && at.y <= 1f;
        }

        // ---------- the one under the pointer ----------

        // HEALTH ON HOVER (Forgejo #91, Settings.HoverHealth): whoever the
        // pointer is on wears a light marker while it stays there - a villager, one
        // of yours, an enemy out of a fight - the same plate, bars and rulers, so
        // the Observation skill keeps the same numbers back. Not one who already
        // wears a marker: the target (`target`), or a fight's marked enemy or one
        // of yours, which the list's passes hand back (PaintCombatMarkers); a fight's unmarked one
        // gets it, the "+N" on the plaque unchanged. Nor you, nor a body - but one
        // carried or held captive (dead 5) is alive, and has a health to show.
        //
        // WHO IS UNDER THE POINTER is vanilla's own ray, the one that names what
        // the cursor is on (PlayerMove.Update, then CursorName): read, not cast
        // again. It runs every frame you can move, and a miss leaves no collider;
        // while you cannot move it stops and holds its last hit, so then nobody.
        private static readonly HarmonyLib.AccessTools.FieldRef<PlayerMove, RaycastHit> CursorHit =
            HarmonyLib.AccessTools.FieldRefAccess<PlayerMove, RaycastHit>("hit");
        private static Collider _hoverCollider;
        private static CommonStates _hoverWho;
        private static CommonStates _hoverShown;   // this frame's, for the harness (HarnessHover)

        private static CommonStates HoverMarked(ManagersScript mn, CommonStates target)
        {
            PlayerMove p = mn.gameMN.pMove;
            if (!Settings.HoverHealth.Value || p == null || !p.controllable || mn.gameMN.OnUI) { return null; }
            Collider on = CursorHit(p).collider;
            if (!ReferenceEquals(on, _hoverCollider))
            {
                _hoverCollider = on;
                _hoverWho = on != null ? on.GetComponentInParent<CommonStates>() : null;
            }
            CommonStates who = _hoverWho;
            if (who == null || who.maxLife <= 0 || who.life <= 0 || who.dead == 1 || ReferenceEquals(who, p.common) || ReferenceEquals(who, target))
            {
                return null;
            }
            return who;
        }

        // ---------- the threat: on you, a red pulse ----------

        // AN ENEMY ON YOU GLOWS RED, PULSING (Forgejo #63's starting choice): one
        // in battler - the ones that have seen you or are after you - on its
        // light marker, its own marker when you target it, its arrow on the rim
        // and the plaque when it is the fight's first. One pulse for them all,
        // on the real clock, so a paused game still shows which. A soft band
        // round the plate, under it: the plate stays the plate.
        private static readonly Color ThreatRed = new Color(1f, .16f, .1f, 1f);
        private const float ThreatPeriod = 1.1f, ThreatLow = .3f, ThreatHigh = 1f;
        private const float LightGlow = 5f, ArrowGlow = 8f, MarkerGlow = 6f, PlaqueGlow = 7f;
        private const int ThreatEdge = 24;   // the glow sprite's falloff, in its pixels (100 a unit, as the canvas counts)
        private static Sprite _threatSprite;
        private static RectTransform _markerThreat;
        private static Image _markerThreatGlow;

        private static float ThreatAlpha()
        {
            float k = .5f + .5f * Mathf.Sin(Time.unscaledTime * (2f * Mathf.PI / ThreatPeriod));
            return Mathf.Lerp(ThreatLow, ThreatHigh, k);
        }

        private static Image ThreatGlow(Transform parent, string name)
        {
            var go = Owned.New(name);
            go.transform.SetParent(parent, false);
            var glow = go.AddComponent<Image>();
            glow.raycastTarget = false;
            glow.sprite = ThreatSprite();
            glow.type = Image.Type.Sliced;
            glow.fillCenter = true;
            glow.color = ThreatRed;
            go.SetActive(false);
            return glow;
        }

        // on or off at this frame's pulse, round a rect of its parent's (Theme.Place's
        // top-left units), reaching `band` past it
        private static void PaintThreat(Image glow, bool on, float x, float y, float w, float h, float band)
        {
            if (glow.gameObject.activeSelf != on) { glow.gameObject.SetActive(on); }
            if (!on) { return; }
            glow.pixelsPerUnitMultiplier = ThreatEdge / band;
            Theme.Place(glow, x - band, y - band, w + 2f * band, h + 2f * band);
            Color c = ThreatRed;
            c.a = ThreatAlpha();
            glow.color = c;
        }

        // ...round the arrow on the rim, turned with it (PlaceRimArrow)
        private static void PaintRimThreat(RimArrow rim, RectTransform arrow)
        {
            if (rim.Glow == null) { return; }
            if (rim.Glow.gameObject.activeSelf != rim.Threat) { rim.Glow.gameObject.SetActive(rim.Threat); }
            if (!rim.Threat) { return; }
            RectTransform g = rim.Glow.rectTransform;
            g.anchorMin = g.anchorMax = g.pivot = new Vector2(.5f, .5f);
            // a small core and a wide band: a round blob under the triangle, where
            // its own box behind it read as a red square
            g.sizeDelta = new Vector2(ArrowW * .3f + 2f * ArrowGlow, ArrowH * .3f + 2f * ArrowGlow);
            g.position = arrow.position;
            g.localRotation = arrow.localRotation;
            rim.Glow.pixelsPerUnitMultiplier = ThreatEdge / ArrowGlow;
            Color c = ThreatRed;
            c.a = ThreatAlpha();
            rim.Glow.color = c;
        }

        // ...round the target's own marker: on a layer just under it, standing
        // where it stands, as the gift outline does (PaintMarkerOffer) - in the
        // marker itself it would have to be reordered under the plate each paint
        private static void PaintMarkerThreat(bool on)
        {
            if (_worldMarker == null || _markerPlate == null) { return; }
            if (_markerThreat == null || _markerThreatGlow == null)
            {
                if (!on) { return; }
                var go = Owned.New("IslandUI_TargetThreat");
                go.transform.SetParent(_worldMarker.transform.parent, false);
                _markerThreat = (RectTransform)go.transform;
                _markerThreatGlow = ThreatGlow(_markerThreat, "Glow");
                _markerThreatGlow.gameObject.SetActive(true);
            }
            on = on && _worldMarker.gameObject.activeSelf;
            if (_markerThreat.gameObject.activeSelf != on) { _markerThreat.gameObject.SetActive(on); }
            if (!on) { return; }
            RectTransform m = _worldMarker.rectTransform;
            _markerThreat.anchorMin = m.anchorMin;
            _markerThreat.anchorMax = m.anchorMax;
            _markerThreat.pivot = m.pivot;
            _markerThreat.sizeDelta = m.sizeDelta;
            _markerThreat.localScale = m.localScale;
            _markerThreat.position = m.position;
            Transform t = _markerThreat, at = _worldMarker.transform;
            int want = t.GetSiblingIndex() < at.GetSiblingIndex() ? at.GetSiblingIndex() - 1 : at.GetSiblingIndex();
            if (t.GetSiblingIndex() != want) { t.SetSiblingIndex(want); }
            // the plate's frame is the marker's silhouette, footer and all
            RectTransform plate = _markerPlate.rectTransform;
            PaintThreat(_markerThreatGlow, true, plate.anchoredPosition.x, -plate.anchoredPosition.y,
                plate.sizeDelta.x, plate.sizeDelta.y, MarkerGlow);
        }

        // white, solid inside a box and fading out over its border: sliced, it
        // stretches round any rect with the same soft edge
        private static Sprite ThreatSprite()
        {
            if (_threatSprite != null) { return _threatSprite; }
            const int size = ThreatEdge * 2 + 4;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.hideFlags = HideFlags.HideAndDontSave;
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(ThreatEdge - (x + .5f), (x + .5f) - (size - ThreatEdge), 0f);
                    float dy = Mathf.Max(ThreatEdge - (y + .5f), (y + .5f) - (size - ThreatEdge), 0f);
                    float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / ThreatEdge);
                    float a = Mathf.Pow(1f - d, 1.6f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            _threatSprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(.5f, .5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(ThreatEdge, ThreatEdge, ThreatEdge, ThreatEdge));
            _threatSprite.hideFlags = HideFlags.HideAndDontSave;
            return _threatSprite;
        }

        // everyone else fighting you, marked or not: five on you read +4. Not the
        // ones only your people fight - they are not against you
        private static int CombatMore()
        {
            int n = 0;
            for (int i = 1; i < CombatList.Count; i++)
            {
                CombatList.Entry e = CombatList.At(i);
                if (e.Mine && !Gone(e)) { n++; }   // the lingering dead are no one to count
            }
            return n;
        }

        // the layer stands in the world stack, under the target's marker
        // (KeepMarkerOverHud), on the canvas the unit and the action bar share
        private static bool EnsureCombatLayer(ManagersScript mn)
        {
            if (_combatLayer != null) { return true; }
            Transform parent = mn.uiMN.itemDescPanel != null ? mn.uiMN.itemDescPanel.transform.parent : null;
            if (parent == null) { return false; }
            _combatLayer = Owned.New("IslandUI_CombatMarkers");
            _combatLayer.transform.SetParent(parent, false);
            var r = (RectTransform)_combatLayer.transform;
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = r.offsetMax = Vector2.zero;
            CanvasIsolation.Isolate(_combatLayer);
            LightMarkers.Clear();
            return true;
        }

        private static void ResetCombatMarkers()
        {
            _combatLayer = null;
            LightMarkers.Clear();
            _markerThreat = null;
            _markerThreatGlow = null;
        }

        // THE RIM'S TOP, for a marker waiting there: under the log and the clock,
        // as the link arcs' edge tags stand (ArcSafe). A pin along the top or
        // down the left side sat over the clock; one under the plaque crowded
        // the next. A marker over a head on the screen still goes where the
        // head is, over the HUD if it must, as the world marker does.
        private static float PinCeiling(RectTransform parent)
        {
            float top = parent.rect.yMax - 6f;
            RectTransform log = _mn.uiMN.logPanel != null ? _mn.uiMN.logPanel.transform.parent as RectTransform : null;
            if (log != null && log.gameObject.activeInHierarchy)
            {
                log.GetWorldCorners(CombatCorners);   // 0 = bottom-left
                float under = parent.InverseTransformPoint(CombatCorners[0]).y - 6f;
                if (under - parent.rect.yMin > 120f) { top = Mathf.Min(top, under); }
            }
            return top;
        }

        // the plaque and its "+N" plate, in the layer's space
        private static bool PlaqueRect(RectTransform parent, out Rect rect)
        {
            rect = Rect.zero;
            if (_animeEnemy == null || !_animeEnemy.gameObject.activeInHierarchy) { return false; }
            _animeEnemy.rectTransform.GetWorldCorners(CombatCorners);
            Vector3 low = parent.InverseTransformPoint(CombatCorners[0]), high = parent.InverseTransformPoint(CombatCorners[2]);
            if (_animeEnemyMore != null && _animeEnemyMore.gameObject.activeSelf)
            {
                _animeEnemyMore.rectTransform.GetWorldCorners(CombatCorners);
                low = Vector3.Min(low, parent.InverseTransformPoint(CombatCorners[0]));
            }
            rect = Rect.MinMaxRect(low.x, low.y, high.x, high.y);
            return true;
        }

        private static LightMarker LightMarkerAt(int i)
        {
            while (LightMarkers.Count <= i) { LightMarkers.Add(BuildLightMarker(LightMarkers.Count)); }
            return LightMarkers[i];
        }

        private static LightMarker BuildLightMarker(int n)
        {
            var m = new LightMarker { Hp = -1f, Stun = -1f };
            var go = Owned.New("CombatMarker" + n);
            go.transform.SetParent(_combatLayer.transform, false);
            m.Root = (RectTransform)go.transform;
            m.Root.anchorMin = m.Root.anchorMax = new Vector2(.5f, .5f);
            m.Root.pivot = new Vector2(.5f, 0f);
            m.Root.sizeDelta = new Vector2(LightW, LightH);
            m.Root.localScale = Vector3.one * MarkerScale;
            m.Glow = ThreatGlow(m.Root, "Threat");
            m.Plate = AnimePieces.Image(m.Root, "Plate", "frame_plate");
            AnimePieces.Sliced(m.Plate, "frame_plate", MarkerFrameBorder, LightFrame);
            Theme.Place(m.Plate, 0f, 0f, LightW, LightH);
            m.Health = LightTrack(m.Root, "Health", "combat_hp_fill", LightHpY, LightHpH, out m.HealthFill);
            m.Faint = LightTrack(m.Root, "Faint", "combat_faint_fill", LightFaintY, LightFaintH, out m.FaintFill);
            m.HealthRuler = new SheetRow { Go = m.Health.gameObject };
            m.StunRuler = new SheetRow { Go = m.Faint.gameObject };
            return m;
        }

        // the world marker's track and fill (MarkerTrack, PaintMarkerBar), laid
        // out once: only the fill's length moves
        private static Image LightTrack(Transform parent, string name, string fill, float y, float h, out Image bar)
        {
            Image track = AnimePieces.Image(parent, name, "track");
            AnimePieces.Sliced(track, "track", new Vector4(16, 0, 16, 0));
            track.pixelsPerUnitMultiplier = 48f / h;
            Theme.Place(track, LightPad, y, LightW - 2f * LightPad, h);
            bar = AnimePieces.Image(track.transform, "Fill", fill);
            AnimePieces.Sliced(bar, fill, new Vector4(20, 0, 20, 0));
            bar.pixelsPerUnitMultiplier = 32f / (h - 1.2f);
            return track;
        }

        private static void LightFill(Image bar, float f, float h)
        {
            bar.gameObject.SetActive(f > 0f);
            float inner = h - 1.2f;
            Theme.Place(bar, .6f, .6f, Mathf.Max(inner, (LightW - 2f * LightPad - 1.2f) * f), inner);
        }

        // "12 m", across the ground (FarWords), and only written when the metres change
        private static string LightFar(LightMarker m, Vector3 feet)
        {
            GameObject player = _mn.gameMN.player;
            if (player == null) { return null; }
            Vector3 d = feet - player.transform.position;
            d.y = 0f;
            int metres = Mathf.RoundToInt(d.magnitude);
            if (metres != m.FarMetres || m.FarWords == null)
            {
                m.FarMetres = metres;
                m.FarWords = Shared.Loc.Get("marker.metres", "{0} m", metres);
            }
            return m.FarWords;
        }

        // where it would stand this frame, and its bars; `rim` waits on the rim
        // for one out of sight, where the hover's - its body under the pointer,
        // a giant's head over the screen's top - only stays inside the edge
        private static bool Gone(CombatList.Entry e) => !e.Alive || !e.Who.gameObject.activeInHierarchy;

        private static void PaintLightMarker(LightMarker m, CommonStates who, bool onYou, bool rim, Camera camera, RectTransform parent,
            Camera uiCam, float floor, float ceiling)
        {
            if (!ReferenceEquals(m.Who, who))
            {
                m.Who = who;
                m.Body = who.GetComponentInChildren<Renderer>();
                m.Hp = m.Stun = -1f;
                m.Rise = -1f;
            }
            // THE HEALTH FILL IN THEIR STANCE'S COLOUR (target-matrix D4): jade
            // yours (Forgejo #98), red a hostile that came for you, amber the
            // rest - each the red fill's own shading (make_anime_hud.py hue_fill),
            // on a fight's marker or the hover's alike. The stun bar and the frame
            // stay: the colour is the one thing that differs.
            string fill = Stance.HealthFill(who);
            if (fill != m.Fill || m.HealthFill.sprite == null)
            {
                m.Fill = fill;
                m.HealthFill.sprite = AnimePieces.Get(fill, new Vector4(20, 0, 20, 0));
            }
            if (!m.Root.gameObject.activeSelf) { m.Root.gameObject.SetActive(true); }
            // JUST OVER THE HEAD. The world marker stands at least 3 m up
            // (PaintWorldMarker); there, close in, an enemy a few metres off had
            // its marker pinned at the screen's top, and in a crowd it was not
            // plain whose a marker was. A body is a figure turned to the camera,
            // so its head is straight up the screen from its feet, along the
            // camera's up: 1 m up the world, off the side of a wide view, drifted
            // out from the head. Its top is where the renderer's box tops out.
            // HELD AT ITS HIGHEST: a Spine body's box is its pose this frame, and a
            // bee's wings beat it 1.19-1.31 m several times a second - the marker
            // flickered with them. The highest of the last RiseHold seconds is
            // kept (Peak), settling back slowly (RiseFall) only once no beat has
            // reached it for that long; the marker glides to it (RiseClimb), so a
            // beat, a bob or a lunge never shakes it
            Vector3 feet = who.transform.position;
            Vector3 up = camera.transform.up;
            float raw = m.Body != null ? m.Body.bounds.max.y - feet.y + .05f : LightLift;
            float now = Time.unscaledTime, dt = Time.unscaledDeltaTime;
            if (m.Rise < 0f) { m.Rise = m.Peak = raw; m.PeakAt = now; }
            if (raw >= m.Peak) { m.Peak = raw; m.PeakAt = now; }
            else if (now - m.PeakAt > RiseHold) { m.Peak = Mathf.MoveTowards(m.Peak, raw, RiseFall * dt); }
            m.Rise = Mathf.MoveTowards(m.Rise, m.Peak, RiseClimb * dt);
            float rise = m.Rise;
            Vector3 head = feet + up * (rise / Mathf.Max(.2f, up.y));
            Vector3 screen = camera.WorldToScreenPoint(head);
            // out of sight by its head: one standing close below you, its feet
            // under the screen's edge, is still in sight, and was pinned there
            bool off = OffScreen(camera, head, ref screen);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, uiCam, out Vector2 local);
            // OFF THE SCREEN it waits on the rim and points to them, with how far,
            // as the world marker does (PlaceWorldMarker): vanilla says nothing
            // of an attacker out of sight
            m.Far = off && rim ? LightFar(m, feet) : null;
            off = m.Far != null;
            m.FarW = 0f;
            if (off)
            {
                if (m.Rim == null || !m.Rim.Built) { m.Rim = BuildRimArrow(m.Root); }
                m.FarW = RimFarWidth(m.Rim, m.Far) * MarkerScale;
                EmblemOf(who, out _, out Color colour);
                m.Rim.Fill.color = colour;
            }
            float roomX = off ? ArrowGap + Mathf.Max(ArrowH * MarkerScale, m.FarW) + 3f : 0f;
            float roomY = off ? ArrowGap + Mathf.Max(ArrowH, FarH) * MarkerScale + 3f : 0f;
            m.Wanted = local;
            float w = LightW * MarkerScale, h = LightH * MarkerScale;
            Rect r = parent.rect;
            m.Bounds = Rect.MinMaxRect(r.xMin + w * .5f + 6f + roomX, floor + roomY,
                r.xMax - w * .5f - 6f - roomX, (off ? ceiling : r.yMax - 6f) - h - roomY);
            local.x = Mathf.Clamp(local.x, m.Bounds.xMin, m.Bounds.xMax);
            local.y = Mathf.Clamp(local.y, m.Bounds.yMin, m.Bounds.yMax);
            m.Local = local;
            // which side of the rim it waits on: where the line to them leaves
            m.Edge = RimNone;
            if (off)
            {
                Vector2 d = m.Wanted - m.Bounds.center;
                bool sides = Mathf.Abs(d.x) * m.Bounds.height > Mathf.Abs(d.y) * m.Bounds.width;
                m.Edge = sides ? (d.x < 0f ? RimLeft : RimRight) : (d.y < 0f ? RimBottom : RimTop);
            }
            float hp = who.maxLife > 0 ? Mathf.Clamp01((float)(who.life / who.maxLife)) : 0f;
            bool hpMoved = hp != m.Hp;
            if (hpMoved) { m.Hp = hp; LightFill(m.HealthFill, hp, LightHpH); }
            float stun = who.maxFaint > 0 ? Mathf.Clamp01((float)(who.faint / who.maxFaint)) : 0f;
            bool stunMoved = stun != m.Stun;
            if (stunMoved) { m.Stun = stun; LightFill(m.FaintFill, stun, LightFaintH); }
            // THE POOLS COUNTED (Forgejo #88): the target marker's ruler, thin
            // hundreds and heavy thousands, so a giant's bar reads denser than a
            // mouse's. Its marks count the pools, so they are the numbers the
            // Observation skill keeps from a stranger, as on the target's marker.
            float trackW = LightW - 2f * LightPad;
            bool placed = PaintMarkerRuler(m.HealthRuler, Observe.Sees(who, Observe.Life) ? (float)who.maxLife : 0f, trackW, LightHpH);
            if (placed || hpMoved) { ShadeMarkerRuler(m.HealthRuler, hp); }
            placed = PaintMarkerRuler(m.StunRuler, Observe.Sees(who, Observe.Faint) ? (float)who.maxFaint : 0f, trackW, LightFaintH);
            if (placed || stunMoved) { ShadeMarkerRuler(m.StunRuler, stun); }
            m.OnYou = onYou;
            PaintThreat(m.Glow, m.OnYou, 0f, 0f, LightW, LightH, LightGlow);
            if (m.Rim != null) { m.Rim.Threat = m.OnYou; }
        }

        // ...and stands there, the arrow beside it
        private static void FinishLightMarker(LightMarker m, RectTransform parent, float floor, Rect plaque, bool plaqueUp)
        {
            float w = LightW * MarkerScale, h = LightH * MarkerScale;
            Vector2 local = m.Local;
            // never over the plaque: under it, as the world marker steps
            if (plaqueUp && local.x + w * .5f > plaque.xMin && local.x - w * .5f < plaque.xMax && local.y + h > plaque.yMin - 4f)
            {
                local.y = Mathf.Max(floor, plaque.yMin - h - 5f - (m.Far != null ? ArrowGap + FarH * MarkerScale : 0f));
            }
            // where it stood, so the arrow placed in the screen's space lands
            // where the marker then stands
            m.Root.localPosition = new Vector3(local.x + m.Push.x, local.y + m.Push.y, 0f);
            Vector2 push = Vector2.zero;
            if (m.Rim != null && m.Rim.Built)
            {
                PlaceRimArrow(m.Rim, parent, m.Far, m.FarW, new Vector2(local.x, local.y + h * .5f),
                    new Vector2(w * .5f, h * .5f), m.Wanted, floor, out push);
            }
            m.Push = push;
            m.Root.localPosition = new Vector3(local.x + push.x, local.y + push.y, 0f);
        }

        // MARKERS WAITING ON ONE SIDE OF THE RIM stand apart along it: a crowd
        // off the screen stacked its markers into one, the distances over each
        // other. In their order along the side, each steps clear of the one
        // before, and back from the side's end if that runs them off it.
        private const int RimNone = 0, RimBottom = 1, RimTop = 2, RimLeft = 3, RimRight = 4;
        private static readonly List<LightMarker> OnRim = new List<LightMarker>();

        private static void SpreadOnRim(int used, int edge)
        {
            OnRim.Clear();
            bool across = edge == RimBottom || edge == RimTop;
            for (int i = 0; i < used; i++)
            {
                LightMarker m = LightMarkers[i];
                if (m.Edge != edge) { continue; }
                // in order along the side: an insertion, there are a handful
                int at = OnRim.Count;
                while (at > 0 && Along(OnRim[at - 1], across) > Along(m, across)) { at--; }
                OnRim.Insert(at, m);
            }
            if (OnRim.Count < 2) { return; }
            for (int i = 1; i < OnRim.Count; i++)
            {
                float least = Along(OnRim[i - 1], across) + Gap(OnRim[i - 1], OnRim[i], across);
                if (Along(OnRim[i], across) < least) { SetAlong(OnRim[i], across, least); }
            }
            for (int i = OnRim.Count - 1; i >= 0; i--)
            {
                LightMarker m = OnRim[i];
                float most = across ? m.Bounds.xMax : m.Bounds.yMax;
                if (i < OnRim.Count - 1) { most = Mathf.Min(most, Along(OnRim[i + 1], across) - Gap(m, OnRim[i + 1], across)); }
                if (Along(m, across) > most) { SetAlong(m, across, most); }
            }
        }

        // MARKERS OVER ONE CROWD STACK: enemies on one spot drew their markers
        // as one. In the list's order, one that would mostly cover a marker
        // before it steps up over it, so the first stands nearest the heads.
        private static void StackOverCrowd(int used)
        {
            float w = LightW * MarkerScale, h = LightH * MarkerScale;
            for (int i = 1; i < used; i++)
            {
                LightMarker m = LightMarkers[i];
                if (m.Edge != RimNone) { continue; }
                for (int pass = 0; pass < i; pass++)
                {
                    bool moved = false;
                    for (int j = 0; j < i; j++)
                    {
                        LightMarker o = LightMarkers[j];
                        if (o.Edge != RimNone || Mathf.Abs(o.Local.x - m.Local.x) >= w * .8f
                            || Mathf.Abs(o.Local.y - m.Local.y) >= h + 1f) { continue; }
                        m.Local.y = o.Local.y + h + 1f;
                        moved = true;
                    }
                    if (!moved) { break; }
                }
            }
        }

        private static float Along(LightMarker m, bool across) => across ? m.Local.x : m.Local.y;

        private static void SetAlong(LightMarker m, bool across, float v)
        {
            if (across) { m.Local.x = v; } else { m.Local.y = v; }
        }

        // the room two neighbours on the rim need between their middles: along
        // the top or bottom the marker or its arrow and distance side by side,
        // whichever is wider; down a side the marker or the arrow over its
        // distance, whichever is taller
        private static float Gap(LightMarker a, LightMarker b, bool across)
        {
            // with room over: each pair leans toward its own enemy, so two
            // neighbours' pairs stand a little apart from their markers
            const float clear = 9f;
            if (!across)
            {
                return Mathf.Max(LightH * MarkerScale, (ArrowW + FarH) * MarkerScale + 1f) + clear;
            }
            float spanA = Mathf.Max(LightW * MarkerScale, ArrowW * MarkerScale + a.FarW + 2f);
            float spanB = Mathf.Max(LightW * MarkerScale, ArrowW * MarkerScale + b.FarW + 2f);
            return (spanA + spanB) * .5f + clear;
        }

        // For the harness, which cannot fight: you step up to the biggest patrol,
        // some metres north of it so the fight stands below you on the screen
        // (or south, east or west: "16 east"), and everyone of every patrol
        // within 30 m takes you as their target, as their sight check does on
        // seeing you (NPCMove.Targeting). A debug patrol spawns 40 m out, past
        // the 20 m at which a chase gives up.
        internal static string HarnessAmbush(string how)
        {
            // "16", or "16 east": how far, and on which side of them you stand
            string[] words = (how ?? "").Split(' ');
            float metres = words.Length > 0 && float.TryParse(words[0], out float m) ? m : 7f;
            Vector3 side = words.Length < 2 ? Vector3.forward
                : words[1] == "south" ? Vector3.back : words[1] == "east" ? Vector3.right : words[1] == "west" ? Vector3.left : Vector3.forward;
            GameManager gm = _mn != null ? _mn.gameMN : null;
            NPCManager npcs = _mn != null ? _mn.npcMN : null;
            PlayerMove p = gm != null ? gm.pMove : null;
            if (p == null || npcs == null || npcs.patrolList == null) { return "no game"; }
            NPCManager.PatrolNPC best = null;
            float bestD = float.MaxValue;
            Vector3 middle = Vector3.zero;
            foreach (NPCManager.PatrolNPC patrol in npcs.patrolList)
            {
                if (patrol == null || patrol.patroller == null) { continue; }
                Vector3 sum = Vector3.zero;
                int alive = 0;
                foreach (CommonStates who in patrol.patroller)
                {
                    if (who == null || who.dead != 0 || !who.gameObject.activeInHierarchy) { continue; }
                    sum += who.transform.position;
                    alive++;
                }
                if (alive == 0) { continue; }
                sum /= alive;
                // the biggest, and of those the nearest
                float d = (sum - p.transform.position).sqrMagnitude - alive * 1e6f;
                if (d < bestD) { bestD = d; best = patrol; middle = sum; }
            }
            if (best == null) { return "no patrol"; }
            p.transform.position = middle + side * metres;
            _lastHere = p.transform.position;
            int on = 0, near = 0;
            foreach (NPCManager.PatrolNPC patrol in npcs.patrolList)
            {
                if (patrol == null || patrol.patroller == null) { continue; }
                foreach (CommonStates who in patrol.patroller)
                {
                    if (who == null || who.dead != 0 || who.nMove == null
                        || (who.transform.position - p.transform.position).sqrMagnitude > 900f) { continue; }
                    near++;
                    if (who.nMove.Targeting(gm.player)) { on++; }
                }
            }
            return on + " of " + near + " on you";
        }

        // For the harness, the rulers' test (Forgejo #88): the combat list's pools
        // set in memory, one word an entry in its order - "4000" fills a pool of
        // 4000, "1500/4000" leaves it at 1500, and a second after a semicolon is the
        // faint's: "300;150 2600/4000;900/2500" (the harness splits on commas). Never saved.
        // Words past the enemies go to your people in the fight, in theirs (Forgejo #98).
        internal static string HarnessPools(string spec)
        {
            string[] words = (spec ?? "").Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            var said = new System.Text.StringBuilder();
            for (int i = 0; i < words.Length && i < CombatList.Count + CombatList.FriendCount; i++)
            {
                CommonStates who = i < CombatList.Count ? CombatList.At(i).Who : CombatList.FriendAt(i - CombatList.Count).Who;
                if (who == null) { continue; }
                string[] pools = words[i].Split(';');
                if (HarnessPool(pools[0], out float life, out float maxLife)) { who.maxLife = maxLife; who.life = life; }
                if (pools.Length > 1 && HarnessPool(pools[1], out float faint, out float maxFaint)) { who.maxFaint = maxFaint; who.faint = faint; }
                said.Append(who.charaName).Append(' ').Append(who.life).Append('/').Append(who.maxLife)
                    .Append(',').Append(who.faint).Append('/').Append(who.maxFaint).Append("; ");
            }
            return said.Length > 0 ? said.ToString() : "nobody in the fight";
        }

        private static bool HarnessPool(string word, out float now, out float max)
        {
            string[] parts = word.Split('/');
            now = max = 0f;
            if (!float.TryParse(parts[parts.Length - 1], out max) || max <= 0f) { return false; }
            now = parts.Length > 1 && float.TryParse(parts[0], out float n) ? Mathf.Clamp(n, 0f, max) : max;
            return true;
        }

        // For the harness, the friendly markers' test (Forgejo #98): the nearest of
        // yours of that name follows you, as the unit's Follow tile asks
        // (NPCManager.NPCFollow), and stands beside you; a second word sets the
        // order it fights by ("Selsia Defence"), in memory. "" names who follows now.
        internal static string HarnessFollow(string spec)
        {
            string[] words = (spec ?? "").Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            string name = words.Length > 0 ? words[0] : null;
            NPCManager npcs = _mn != null ? _mn.npcMN : null;
            PlayerMove p = _mn != null && _mn.gameMN != null ? _mn.gameMN.pMove : null;
            if (npcs == null || p == null) { return "no game"; }
            if (!string.IsNullOrWhiteSpace(name))
            {
                CommonStates near = null;
                foreach (CommonStates c in npcs.friendList)
                {
                    if (c != null && c.charaName == name && c.dead == 0 && (near == null
                        || (c.transform.position - p.transform.position).sqrMagnitude < (near.transform.position - p.transform.position).sqrMagnitude))
                    {
                        near = c;
                    }
                }
                if (near == null) { return "none of yours named " + name; }
                if (near.nMove == null || near.nMove.npcType != NPCMove.NPCType.Follow) { npcs.NPCFollow(near); }
                if (words.Length > 1 && near.nMove != null && System.Enum.TryParse(words[1], true, out NPCMove.OrderType order))
                {
                    near.nMove.orderType = order;
                }
                near.transform.position = p.transform.position + Vector3.right * 1.5f;
            }
            var said = new System.Text.StringBuilder("following:");
            foreach (CommonStates c in npcs.follower)
            {
                if (c == null) { continue; }
                NPCMove m = c.nMove;
                said.Append(' ').Append(c.charaName).Append(" age=").Append(c.age).Append(" life=").Append(c.life).Append('/').Append(c.maxLife)
                    .Append(' ').Append(m != null ? m.npcType + "/" + m.actType + "/" + m.orderType : "-")
                    .Append(" on=").Append(m != null && m.tmpEnemy != null ? m.tmpEnemy.name : "-")
                    .Append(" d=").Append(Vector3.Distance(c.transform.position, p.transform.position).ToString("0.0")).Append(';');
            }
            return said.ToString();
        }

        // For the harness, the hover's test (Forgejo #91): who vanilla's ray is on
        // and who of them wears the hover's marker, then the people on the screen
        // within 30 m with the screen pixel of their body's middle, for a pointer
        // to be put on (at that pixel over the display scale).
        // A name steps you 3 m south of the nearest of that name first.
        internal static string HarnessHover(string standBy)
        {
            PlayerMove p = _mn != null && _mn.gameMN != null ? _mn.gameMN.pMove : null;
            Camera camera = Camera.main;
            if (p == null || camera == null) { return "no game"; }
            if (!string.IsNullOrWhiteSpace(standBy))
            {
                CommonStates near = null;
                foreach (CommonStates c in Object.FindObjectsOfType<CommonStates>())
                {
                    if (c != null && c.charaName == standBy && c.life > 0 && !ReferenceEquals(c, p.common) && (near == null
                        || (c.transform.position - p.transform.position).sqrMagnitude < (near.transform.position - p.transform.position).sqrMagnitude))
                    {
                        near = c;
                    }
                }
                if (near == null) { return "no " + standBy; }
                p.transform.position = near.transform.position + Vector3.back * 3f;
                _lastHere = p.transform.position;
                return "by " + standBy;
            }
            CommonStates target = _worldMarker != null && _worldMarker.gameObject.activeInHierarchy ? _unitFor : null;
            Collider on = CursorHit(p).collider;
            var said = new System.Text.StringBuilder();
            said.Append("ray=").Append(on != null ? on.name : "-").Append(" hover=").Append(_hoverShown != null ? _hoverShown.charaName : "-")
                .Append(" target=").Append(target != null ? target.charaName : "-").Append(" | ");
            foreach (CommonStates c in Object.FindObjectsOfType<CommonStates>())
            {
                if (c == null || ReferenceEquals(c, p.common) || (c.transform.position - p.transform.position).sqrMagnitude > 900f) { continue; }
                Renderer body = c.GetComponentInChildren<Renderer>();
                Vector3 middle = body != null ? body.bounds.center : c.transform.position + Vector3.up;
                Vector3 s = camera.WorldToScreenPoint(middle);
                if (s.z <= 0f || s.x < 0f || s.y < 0f || s.x > Screen.width || s.y > Screen.height) { continue; }
                CombatList.Entry e = CombatList.Of(c);
                said.Append(c.charaName).Append(" age=").Append(c.age).Append(" npc=").Append(c.npcID)
                    .Append(" type=").Append(c.nMove != null ? c.nMove.npcType.ToString() : "-")
                    .Append(" life=").Append(c.life).Append('/').Append(c.maxLife).Append(" dead=").Append(c.dead)
                    .Append(e == null ? "" : e.Marked ? " fight:marked" : " fight")
                    .Append(" at ").Append(Mathf.RoundToInt(s.x)).Append(',').Append(Mathf.RoundToInt(Screen.height - s.y)).Append(" | ");
            }
            return said.ToString();
        }
    }
}
