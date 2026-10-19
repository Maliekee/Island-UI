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
            if (target == null || !Settings.TargetRing.Value)
            {
                HideGroundRing();
                return;
            }
            if (_ring == null)
            {
                _ring = Owned.New("IslandUI_TargetRing");
                _ringSprite = _ring.AddComponent<SpriteRenderer>();
                _ringSprite.sprite = RingArt();
                // under whatever stands on it: people and props draw after
                _ringSprite.sortingOrder = -50;
                _ring.transform.rotation = Quaternion.Euler(90f, 0f, 0f);   // flat on the ground
            }
            if (!_ring.activeSelf)
            {
                _ring.SetActive(true);
            }
            Vector3 at = target.position;
            at.y += RingLift;
            _ring.transform.position = at;
            // the art is one unit across at scale 1
            float size = Mathf.Clamp(radius, 0.4f, 4f) * 2f * Settings.TargetRingScale.Value;
            _ring.transform.localScale = new Vector3(size, size, 1f);
            colour.a = Mathf.Clamp01(Settings.TargetRingAlpha.Value);
            _ringSprite.color = colour;
        }

        private static void HideGroundRing()
        {
            if (_ring != null && _ring.activeSelf)
            {
                _ring.SetActive(false);
            }
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
    }
}
