using UnityEngine;

namespace IslandUI
{
    // THE TARGET'S RING ON THE GROUND: a visual indicator for the target, a little
    // circle on the ground under it.
    //
    // The marker over the head says WHO the target is; it floats, and in a crowd it
    // is not obvious whose head it is over. A ring under the feet is in the world,
    // where the target stands. It is a flat sprite on the ground plane, in the
    // marker's own relationship colour, following the target each frame the unit
    // paints its marker, and gone when the unit goes.
    internal static partial class InventoryWindow
    {
        private static GameObject _ring;
        private static SpriteRenderer _ringSprite;
        private static Sprite _ringArt;
        private const float RingLift = 0.04f;        // clear of the ground, or the two fight for the depth
        private const float PersonRadius = 0.75f;

        private static void PaintGroundRing(Transform target, Color colour, float radius)
        {
            PaintRing(ref _ring, ref _ringSprite, "IslandUI_TargetRing", target, colour, radius);
        }

        private static void HideGroundRing()
        {
            if (_ring != null && _ring.activeSelf)
            {
                _ring.SetActive(false);
            }
        }

        // One ring, whoever it marks: the target's, and the one under a person you
        // hold an item over (InventoryWindow.Drop.cs) - the same art, its own object.
        private static void PaintRing(ref GameObject ring, ref SpriteRenderer sprite, string name,
            Transform target, Color colour, float radius)
        {
            if (target == null || !Settings.TargetRing.Value)
            {
                if (ring != null && ring.activeSelf)
                {
                    ring.SetActive(false);
                }
                return;
            }
            if (ring == null)
            {
                ring = Owned.New(name);
                sprite = ring.AddComponent<SpriteRenderer>();
                sprite.sprite = RingArt();
                // under whatever stands on it: people and props draw after
                sprite.sortingOrder = -50;
                ring.transform.rotation = Quaternion.Euler(90f, 0f, 0f);   // flat on the ground
            }
            if (!ring.activeSelf)
            {
                ring.SetActive(true);
            }
            Vector3 at = target.position;
            at.y += RingLift;
            ring.transform.position = at;
            // the art is one unit across at scale 1
            float size = Mathf.Clamp(radius, 0.4f, 4f) * 2f * Settings.TargetRingScale.Value;
            ring.transform.localScale = new Vector3(size, size, 1f);
            colour.a = Mathf.Clamp01(Settings.TargetRingAlpha.Value);
            sprite.color = colour;
        }

        // a thing's footprint, from what it draws: half its wider side, a little proud
        private static float ThingRadius(Component prop)
        {
            Renderer body = prop != null ? prop.GetComponentInChildren<Renderer>() : null;
            if (body == null)
            {
                return PersonRadius;
            }
            Vector3 e = body.bounds.extents;
            return Mathf.Max(e.x, e.z) * 1.1f;
        }

        // A ring with a breath of fill, white so the renderer's colour is the colour.
        // Generated: no asset ships, as with the cursor.
        private static Sprite RingArt()
        {
            if (_ringArt != null)
            {
                return _ringArt;
            }
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            var px = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - half) * (x + 0.5f - half) + (y + 0.5f - half) * (y + 0.5f - half)) / half;
                    // the band between 0.80 and 0.96 of the radius, a pixel of softness each side
                    float band = Mathf.Clamp01((d - 0.78f) / 0.03f) * Mathf.Clamp01((0.98f - d) / 0.03f);
                    float fill = d < 0.80f ? 0.16f : 0f;
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(Mathf.Max(band, fill)) * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            _ringArt = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size, 0,
                SpriteMeshType.FullRect);
            return _ringArt;
        }

        // ---------- a level prop's reach (Forgejo #182) ----------

        // A WELL'S OR A FOOD BOX'S CIRCLE IS VANILLA'S, drawn only while the game has
        // the prop open: UseProp -> PropPanelTypeChange -> WaterBucketUse /
        // FoodStorageUse light circle 0 at the prop with its reach, and clear it
        // when the panel closes. The target outlives that - Tabbed from beyond
        // reach or while walking, or clicked and walked away from - and then the
        // circle was missing while the well was plainly the target. So while a
        // level prop is the target we draw that circle ourselves, with vanilla's
        // radius.
        //
        // Circle 0 is SHARED: build mode, an NPC's area and a scene draw it too.
        // While the prop is the target we hold it there the way vanilla's own
        // open does (it simply overwrites the circle, and so do we: a stale one is
        // often left up, an NPC's area far below in a house), except in build
        // mode, a scene or an event, which keep theirs. We clear only a circle
        // that is still ours - at our prop with our radius - so whoever took it
        // over keeps it.
        private static ItemInfo _propCircleFor;
        private static Vector3 _propCircleAt;
        private static float _propCircleRad;

        // vanilla's reach, copied as the Vitals maximums are (no field holds it):
        // WaterBucketUse 10 m for well_01, 20 for well_02, 1 for any other key;
        // FoodStorageUse 20 m for foodstorage_02, 10 for any other
        private static float PropCircleRadius(InventorySlot slot, string key)
        {
            if (slot.type == InventorySlot.Type.Water)
            {
                return key == "well_01" ? 10f : key == "well_02" ? 20f : 1f;
            }
            return key == "foodstorage_02" ? 20f : 10f;
        }

        // LateSync: after the coroutines, so vanilla's draw or clear of this
        // frame has happened and we answer it in the same frame
        private static void TickPropCircle(ManagersScript mn)
        {
            UIManager ui = mn != null ? mn.uiMN : null;
            if (ui == null || ui.circleBlue == null)
            {
                return;
            }
            GameObject circle = ui.circleBlue.transform.parent.gameObject;
            ItemInfo prop = ResolveTarget(mn) as ItemInfo;
            InventorySlot slot = prop != null ? prop.GetComponent<InventorySlot>() : null;
            bool want = IsLevelProp(slot) && mn.allSet && !GameManager.nowEvent && !InScene(mn)
                && (mn.buildMN == null || !mn.buildMN.nowBuild);
            // another target, or none: ours goes first, so the next draws this frame
            if (_propCircleFor != null && !(want && ReferenceEquals(prop, _propCircleFor)))
            {
                ClearPropCircle(mn);
            }
            if (want)
            {
                Vector3 at = prop.transform.position;
                float rad = PropCircleRadius(slot, prop.itemKey);
                // drawn only when it is not already there - vanilla's own open
                // draws the same circle, and CreatePoints is not free
                if (!circle.activeSelf || !CircleIs(ui, at, rad))
                {
                    ui.SetCircleRad(0, rad);
                    ui.SetCircle(0, true, at);
                }
                _propCircleFor = prop;
                _propCircleAt = at;
                _propCircleRad = rad;
            }
        }

        private static bool CircleIs(UIManager ui, Vector3 at, float rad)
        {
            return (ui.circleBlue.transform.parent.position - at).sqrMagnitude < 0.0001f
                && Mathf.Approximately(ui.circleBlue.xradius, rad);
        }

        // Clears circle 0 only while it is the one we drew; forgets it either way.
        internal static void ClearPropCircle(ManagersScript mn)
        {
            UIManager ui = mn != null ? mn.uiMN : null;
            if (_propCircleFor != null && ui != null && ui.circleBlue != null)
            {
                GameObject circle = ui.circleBlue.transform.parent.gameObject;
                if (circle.activeSelf && CircleIs(ui, _propCircleAt, _propCircleRad))
                {
                    ui.SetCircle(0, false);
                }
            }
            _propCircleFor = null;
        }
    }
}
