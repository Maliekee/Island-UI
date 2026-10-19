using System.Collections.Generic;
using IslandUI.UI;
using UnityEngine;

namespace IslandUI
{
    // TAB TARGETS WHAT IS NEAREST THE CURSOR (window-system.md 6.32, D157). A click
    // serves two purposes, targeting and moving; Tab is the targeting-only version.
    // It targets the nearest thing around the cursor, and pressed while the closest
    // thing is already the target, it targets the next closest. People and things.
    //
    // The point under the cursor is the ground the pointer's ray meets - the ray a
    // click casts. Around it a sphere finds what could be a target, the way E finds
    // the nearest thing around you (PlayerMove.Update, 3 m), and they are ranked by
    // how far each one's middle is from the cursor ON SCREEN: nearest to what you
    // see. Again, with the cursor where it was, steps to the next; moved, it looks
    // again. Nothing walks, and nothing acts: a person is picked as the roster picks
    // them, a thing in reach is opened where you stand - its dock lit, as the walk
    // that stops inside reach opens it (D154) - and anything else is held as ours
    // until you walk up, as a thing kept past vanilla's end is (D153).
    internal static partial class InventoryWindow
    {
        // how far from the cursor's ground point Tab looks - 6 m first, then 3, now 2:
        // Tab on nothing deselects, and at a wider radius the cursor had to be really
        // far from a target for that to happen
        private const float TabRadius = 2f;
        private const float TabSamePlace = 40f;   // px the cursor may drift and still mean "the next"
        private const float TabFresh = 4f;        // s after which a press looks again

        private static readonly List<object> TabCands = new List<object>();   // CommonStates, or a thing's GameObject
        private static Vector2 _tabAt;
        private static float _tabTime = -100f;
        private static int _tabIndex = -1;
        private static GameObject _tabOpen;       // a thing to open once vanilla has let go of what it held
        private static float _tabOpenUntil;
        private static string _tabSay;
        private static float _tabSayUntil;

        private static void TickTabTarget(ManagersScript mn)
        {
            if (Time.unscaledTime < _tabSayUntil)
            {
                CursorBadge.Show(_tabSay);
            }
            LandTabOpen();
            LandBodyAct();
            KeyCode key = Settings.TargetKey.Value;
            if (key == KeyCode.None || !Input.GetKeyDown(key) || !TabArmed(mn))
            {
                return;
            }
            Camera cam = Camera.main;
            if (cam != null)
            {
                TabTarget(cam, Input.mousePosition);
            }
        }

        // Normal play only: while building, flying the drone, dressing someone or
        // writing their line, Tab is vanilla's cancel (all four leave the player
        // uncontrollable or building), and a text field of ours keeps its keys.
        private static bool TabArmed(ManagersScript mn)
        {
            return mn.gameMN != null && mn.gameMN.pMove != null && mn.gameMN.pMove.controllable
                && !GameManager.nowEvent && (mn.buildMN == null || !mn.buildMN.nowBuild)
                && !FilterInput.AnyFocused;
        }

        // (A click while walking used to target like Tab - withdrawn
        // with D158: a click is the game's now, and the game reads no
        // world click while a movement key is down. Tab targets while you walk.)

        // What a collider under the pointer or in the sphere is as a target: one of
        // your people, a thing we stand in for (by the object vanilla's click holds),
        // or nothing
        private static object TargetableAt(GameObject g, PlayerMove p)
        {
            CommonStates who = PersonOf(g);
            if (who != null)
            {
                // D159: your people as the roster picks them, and anyone else standing
                // or down - an enemy, a stranger, a body; never you, nor one carried
                // off or in parts
                if (ReferenceEquals(who, p.common) || !who.gameObject.activeInHierarchy)
                {
                    return null;
                }
                return (Yours(who, p) && who.life > 0.0) || who.dead == 0 || ReachRule.Body(who) ? who : null;
            }
            GameObject go = g.name == "BrokenColl" && g.transform.parent != null ? g.transform.parent.gameObject : g;
            if (go.GetComponent<ItemInfo>() == null)
            {
                return null;
            }
            object thing = PropTargetOf(_mn, go);
            return thing != null && IsReplaceable(thing) ? go : null;
        }

        // For the patch on UIManager.SettingPanelOpen: this press is ours
        internal static bool TabTakesTheKey()
        {
            KeyCode key = Settings.TargetKey.Value;
            return key != KeyCode.None && Input.GetKeyDown(key) && _mn != null && TabArmed(_mn);
        }

        private static void TabTarget(Camera cam, Vector2 cursor)
        {
            bool same = _tabIndex >= 0 && TabCands.Count > 0
                && (cursor - _tabAt).sqrMagnitude < TabSamePlace * TabSamePlace
                && Time.unscaledTime - _tabTime < TabFresh;
            if (same)
            {
                TabCands.RemoveAll(c => c == null || (c is Object o && o == null));
                same = TabCands.Count > 0;
            }
            if (!same)
            {
                GatherTabCandidates(cam, cursor);
                _tabIndex = -1;
                _tabAt = cursor;
            }
            _tabTime = Time.unscaledTime;
            // TAB ON NOTHING LETS GO: with nothing targetable near the cursor, Tab
            // deselects. That also means that if the only target around the cursor
            // is already targeted, Tab deselects it
            if (TabCands.Count == 0)
            {
                if (!Untarget("tab on nothing"))
                {
                    Say("nothing to target here");
                }
                return;
            }
            int i = same ? (_tabIndex + 1) % TabCands.Count : 0;
            // the nearest is the target already: the next nearest
            if (ReferenceEquals(TabCands[i], _heldRef) && TabCands.Count > 1)
            {
                i = (i + 1) % TabCands.Count;
            }
            _tabIndex = i;
            if (ReferenceEquals(TabCands[i], _heldRef))
            {
                Untarget("tab on the target");   // the only one here, and yours already
                _tabIndex = -1;
                return;
            }
            TargetFromTab(TabCands[i]);
        }

        private static void GatherTabCandidates(Camera cam, Vector2 cursor)
        {
            TabCands.Clear();
            PlayerMove p = _mn.gameMN.pMove;
            Ray ray = cam.ScreenPointToRay(cursor);
            Vector3 point;
            if (Physics.Raycast(ray, out RaycastHit hit, 200f, p.movableLayer))
            {
                point = hit.point;
            }
            else
            {
                var ground = new Plane(Vector3.up, p.transform.position);
                if (!ground.Raycast(ray, out float along))
                {
                    return;
                }
                point = ray.GetPoint(along);
            }
            // BG and BGNPC are the props, NPC the people's own colliders; loot
            // (PhysicBG) and floors (Platform) are not targets
            Collider[] hits = Physics.OverlapSphere(point, TabRadius, LayerMask.GetMask("BG", "NPC", "BGNPC"));
            var seen = new HashSet<Object>();
            var scored = new List<KeyValuePair<float, object>>();
            // WHAT YOU POINT AT, NOT WHOSE MIDDLE IS NEAREST: a house's middle can
            // lie nearer the cursor than the person standing in front of it, so each
            // candidate is its drawn shape on screen - the distance from the cursor
            // to that shape (none when the cursor is on it), then, among the shapes
            // the cursor is on, the smallest first
            for (int i = 0; i < hits.Length; i++)
            {
                object pick = TargetableAt(hits[i].gameObject, p);
                if (pick == null || !seen.Add((Object)pick))
                {
                    continue;
                }
                if (!ScreenShape(cam, pick, out Rect shape))
                {
                    continue;
                }
                float dx = Mathf.Max(0f, Mathf.Max(shape.xMin - cursor.x, cursor.x - shape.xMax));
                float dy = Mathf.Max(0f, Mathf.Max(shape.yMin - cursor.y, cursor.y - shape.yMax));
                float gap = Mathf.Sqrt(dx * dx + dy * dy);
                // on it: 0 plus a hair per pixel of area, so the smaller wins; off it,
                // the distance, which is always more than any area's hair
                float score = gap > 0f ? 1f + gap : Mathf.Clamp01(shape.width * shape.height / 4e6f);
                scored.Add(new KeyValuePair<float, object>(score, pick));
            }
            scored.Sort((a, b) => a.Key.CompareTo(b.Key));
            for (int i = 0; i < scored.Count; i++)
            {
                if (scored[i].Key < float.MaxValue)
                {
                    TabCands.Add(scored[i].Value);
                }
            }
        }

        // The screen rectangle a candidate is drawn in: a person's body (the Spine
        // mesh vanilla outlines, CommonStates.anim), a thing's renderers together.
        private static bool ScreenShape(Camera cam, object pick, out Rect shape)
        {
            shape = default(Rect);
            Bounds b;
            if (pick is CommonStates who)
            {
                Renderer body = who.anim != null ? who.anim.GetComponent<Renderer>() : who.GetComponentInChildren<Renderer>();
                if (body == null)
                {
                    return false;
                }
                b = body.bounds;
            }
            else
            {
                Renderer[] parts = ((GameObject)pick).GetComponentsInChildren<Renderer>();
                bool any = false;
                b = default(Bounds);
                for (int i = 0; i < parts.Length; i++)
                {
                    if (parts[i] == null || !parts[i].enabled)
                    {
                        continue;
                    }
                    if (!any) { b = parts[i].bounds; any = true; }
                    else { b.Encapsulate(parts[i].bounds); }
                }
                if (!any)
                {
                    return false;
                }
            }
            float xMin = float.MaxValue, yMin = float.MaxValue, xMax = float.MinValue, yMax = float.MinValue;
            Vector3 c = b.center, e = b.extents;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = c + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z);
                Vector3 s = cam.WorldToScreenPoint(corner);
                if (s.z <= 0f)
                {
                    return false;
                }
                xMin = Mathf.Min(xMin, s.x); xMax = Mathf.Max(xMax, s.x);
                yMin = Mathf.Min(yMin, s.y); yMax = Mathf.Max(yMax, s.y);
            }
            shape = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
            return true;
        }

        private static void TargetFromTab(object pick)
        {
            PlayerMove p = _mn.gameMN.pMove;
            if (pick is CommonStates who)
            {
                if (Yours(who, p) && who.life > 0.0)
                {
                    _adoptWhy = "tab";
                    PressRosterRow(who);
                    return;
                }
                // D159: an enemy, a stranger or a body is held as ours, the game not
                // asked - nothing of theirs is the game's to hold until an act of the
                // dock asks it (a body's search)
                Adopt(Held.Person, who, Begun.Roster, "tab");
                if (_mn.gameMN.nowAction)
                {
                    p.target = null;   // vanilla lets go of what it held, as a click elsewhere makes it
                }
                return;
            }
            var go = pick as GameObject;
            ItemInfo info = go != null ? go.GetComponent<ItemInfo>() : null;
            if (info == null)
            {
                return;
            }
            ItemData data = _mn.itemMN != null ? _mn.itemMN.FindItem(info.itemKey) : null;
            // a machine too: its window works only while vanilla has it open
            InventorySlot machine = go.GetComponent<InventorySlot>();
            bool rack = machine != null && ContainerPolicy.IsViewedRack(machine);
            // ...standing still: UseProp's loop holds while no movement key is down,
            // and it is checked before the first yield - opened while you walk, a
            // chest is opened and let go inside the one call, and nothing is left
            // to target
            if ((OpensFromReach(data) || rack) && ReachRule.LiveProp(_mn, info) && !p.wasd)
            {
                // in reach: vanilla opens it where you stand, once it has let go of
                // whatever it holds (its loop would close this on the way out)
                if (_mn.gameMN.nowAction || p.searchNPC != null)
                {
                    p.target = null;
                    _tabOpen = go;
                    _tabOpenUntil = Time.unscaledTime + 1.5f;
                    return;
                }
                OpenFromTab(go, data);
                return;
            }
            // beyond reach, or a thing whose use is not opened from reach (a bed):
            // held as ours, the game not asked, until you walk up and click or press
            Adopt(Held.Prop, go, Begun.Roster, "tab");
            if (_mn.gameMN.nowAction)
            {
                p.target = null;   // vanilla lets go of what it held, the way a click elsewhere makes it
            }
        }

        private static void OpenFromTab(GameObject go, ItemData data)
        {
            PlayerMove p = _mn.gameMN.pMove;
            // ours when it lands, not a click's: it stays when vanilla lets go (D158)
            _adoptPropFor = go;
            _adoptPropUntil = Time.unscaledTime + 1f;
            p.target = go;
            p.StartCoroutine(p.UseProp(data));
            // its acts are lit NOW - UseProp's opening runs before its first yield -
            // and walking, its loop lets go on the next frame, before the dock's
            // quarter-second read would see them: remember them while they are there
            ItemInfo info = go.GetComponent<ItemInfo>();
            if (info != null)
            {
                RememberThingActs(info, _mn.buildMN != null && _mn.buildMN.OwnCheck(info));
            }
        }

        private static void LandTabOpen()
        {
            if (_tabOpen == null)
            {
                return;
            }
            if (Time.unscaledTime > _tabOpenUntil || _mn == null || _mn.gameMN == null)
            {
                _tabOpen = null;
                return;
            }
            if (_mn.gameMN.nowAction || (_mn.gameMN.pMove != null && _mn.gameMN.pMove.searchNPC != null))
            {
                return;
            }
            GameObject go = _tabOpen;
            _tabOpen = null;
            ItemInfo info = go.GetComponent<ItemInfo>();
            ItemData data = info != null && _mn.itemMN != null ? _mn.itemMN.FindItem(info.itemKey) : null;
            if (data == null)
            {
                return;
            }
            if (_mn.gameMN.pMove.wasd)
            {
                Adopt(Held.Prop, go, Begun.Roster, "tab");   // walking: vanilla would let it go at once
                return;
            }
            OpenFromTab(go, data);
        }

        private static void Say(string words)
        {
            _tabSay = words;
            _tabSayUntil = Time.unscaledTime + 1.2f;
        }

        // For the harness (D159): Tab onto the nearest "body" or "enemy" (anyone
        // standing who is not yours), as if it were the one nearest the cursor
        internal static string TabPickNearest(string kind)
        {
            PlayerMove p = _mn != null && _mn.gameMN != null ? _mn.gameMN.pMove : null;
            if (p == null)
            {
                return "no player";
            }
            CommonStates best = null;
            float bestD = float.MaxValue;
            foreach (CommonStates c in Object.FindObjectsOfType<CommonStates>())
            {
                if (c == null || ReferenceEquals(c, p.common) || !c.gameObject.activeInHierarchy)
                {
                    continue;
                }
                bool body = ReachRule.Body(c) && !(c.Employed() && c.life > 0.0);
                bool enemy = c.dead == 0 && !Yours(c, p);
                if (kind == "body" ? !body : !enemy)
                {
                    continue;
                }
                float d = (c.transform.position - p.transform.position).sqrMagnitude;
                if (d < bestD)
                {
                    bestD = d;
                    best = c;
                }
            }
            if (best == null)
            {
                return "no " + kind;
            }
            TargetFromTab(best);
            return (string.IsNullOrEmpty(best.charaName) ? "someone" : best.charaName) + " at "
                + Mathf.Sqrt(bestD).ToString("0.0") + " m, live " + ReachRule.Live(_mn, best);
        }

        // For the harness: Tab onto the nearest container of a kind ("Water" a well,
        // "FoodStorage" a food box), near or far, and what its dock then offers.
        internal static string TabPickStore(string type)
        {
            PlayerMove p = _mn != null && _mn.gameMN != null ? _mn.gameMN.pMove : null;
            if (p == null)
            {
                return "no player";
            }
            InventorySlot best = null;
            float bestD = float.MaxValue;
            foreach (InventorySlot s in Object.FindObjectsOfType<InventorySlot>())
            {
                if (s == null || !s.gameObject.activeInHierarchy || s.type.ToString() != type
                    || s.GetComponent<ItemInfo>() == null)
                {
                    continue;
                }
                float d = (s.transform.position - p.transform.position).sqrMagnitude;
                if (d < bestD)
                {
                    bestD = d;
                    best = s;
                }
            }
            if (best == null)
            {
                return "no " + type;
            }
            TargetFromTab(best.gameObject);
            return best.name + " at " + Mathf.Sqrt(bestD).ToString("0.0") + " m, live "
                + ReachRule.LiveProp(_mn, best.GetComponent<ItemInfo>());
        }

        // For the harness: Tab at the cursor as it stands, and what it came to.
        internal static string TabTargetNow()
        {
            Camera cam = Camera.main;
            if (cam == null || _mn == null || _mn.gameMN == null || _mn.gameMN.pMove == null)
            {
                return "no camera or player";
            }
            TabTarget(cam, Input.mousePosition);
            var sb = new System.Text.StringBuilder();
            sb.Append(TabCands.Count).Append(" near the cursor:");
            for (int i = 0; i < TabCands.Count && i < 6; i++)
            {
                sb.Append(i == _tabIndex ? " [" : " ").Append(NameOf(TabCands[i])).Append(i == _tabIndex ? "]" : "");
            }
            return sb.ToString();
        }
    }
}
