using BepInEx.Configuration;
using UnityEngine;

namespace IslandEx
{
    // The mouse wheel pulls the camera further back than vanilla lets it.
    //
    // The camera is CameraRig (a FollowTarget on the player) / Rot / Zoom / Main
    // Camera. The wheel moves Zoom along its z, clamped to FollowTarget.moveMin.z
    // and moveMax.z: -25 at the farthest, -2.5 at the nearest. So the far end is
    // the rig's own moveMin.z times the setting.
    //
    // Pulling back alone is not enough: the camera draws most layers only to 50
    // (CullDistance, its per-layer distances) and nothing past its far plane, 150.
    // From further back the ground and everyone on it would end in a ring round
    // the player. So both stretch with how far back the camera stands now, by
    // its distance over vanilla's farthest - in close, the camera draws exactly
    // what vanilla does. Turned down, or Island Ex off, a camera standing past the
    // limit is brought back to it.
    internal static class ZoomOut
    {
        private static ConfigEntry<bool> _override;
        private static ConfigEntry<float> _reach;
        private static FollowTarget _rig;
        private static Camera _camera;
        private static CullDistance _cull;
        private static float _vanillaMin;      // the rig's moveMin.z before ours
        private static float[] _vanillaCull;   // the camera's layer distances before ours
        // vanilla's far plane, CameraManager.ResetCameraNearFar's default. Never read
        // off the camera: while a save loads the game holds it at 0.2, and a value
        // read then, re-asserted, left the island undrawn
        private const float VanillaFar = 150f;
        private static float _stretch = 1f;    // what the camera draws to now, over vanilla
        private static int _tick;
        private static bool _touched;          // this rig's limit or zoom was moved by us

        internal static void Bind(ConfigFile cfg)
        {
            _override = cfg.Bind("Camera", "Camera Override", false,
                "Let Island Ex change the camera's limits (Zoom Out below). Off = the game's own camera, cutscenes included. On, a cutscene that stands the camera further back than the zoom limit can come out too close. Applies live.");
            _reach = cfg.Bind("Camera", "Zoom Out", 1f, new ConfigDescription(
                "With Camera Override on: how far the mouse wheel pulls the camera back, as a multiple of the game's own farthest, 1-4. The camera draws as far as it stands back, so a far zoom costs frames. 1 = as in the unmodded game. Applies live.",
                new AcceptableValueRange<float>(1f, 4f)));
        }

        internal static void Tick()
        {
            if (++_tick < 10)
            {
                return;
            }
            _tick = 0;
            if (_rig == null)
            {
                if (!Find())
                {
                    return;
                }
            }
            // the drone (ItemManager.Drone) takes the rig and doubles the layer
            // distances itself, restoring them from CullDistance when it lands:
            // while the rig follows anything but you, it is the drone's camera
            if (_rig.target != null && !_rig.target.CompareTag("Player"))
            {
                return;
            }
            // THE CAMERA OVERRIDE IS OPT-IN, off by default. A cutscene places the
            // camera itself (CameraManager.CamZoomFocus, CameraZoomLayout; one
            // stands it at -35, past the wheel's own -25), and a limit held here
            // pulls such a shot back in. Off, the camera is the game's throughout.
            float reach = Plugin.Enabled.Value && _override.Value ? Mathf.Clamp(_reach.Value, 1f, 4f) : 1f;
            // off it writes nothing (off means inert): only a rig this has pulled
            // back is brought back, once, and then left alone again
            if (reach <= 1f && !_touched)
            {
                return;
            }
            _touched = true;
            float min = _vanillaMin * reach;
            Vector3 limit = _rig.moveMin;
            if (!Mathf.Approximately(limit.z, min))
            {
                _rig.moveMin = new Vector3(limit.x, limit.y, min);
            }
            Transform zoom = _rig.zoom.transform;
            Vector3 at = zoom.localPosition;
            if (at.z < min)
            {
                zoom.localPosition = new Vector3(at.x, at.y, min);
                at.z = min;
            }
            Stretch(Mathf.Max(1f, at.z / _vanillaMin));
            if (reach <= 1f && _stretch <= 1f)
            {
                _touched = false;   // back to vanilla's: nothing more to undo
            }
        }

        // a new scene, or the first tick: the rig and its camera, read as vanilla left them
        private static bool Find()
        {
            _rig = null;
            _camera = null;
            _stretch = 1f;
            _touched = false;   // a new scene's rig is as vanilla made it
            foreach (FollowTarget f in Object.FindObjectsOfType<FollowTarget>())
            {
                Camera camera = f.zoom != null ? f.zoom.GetComponentInChildren<Camera>() : null;
                if (camera == null || f.moveMin.z >= 0f)
                {
                    continue;
                }
                _rig = f;
                _camera = camera;
                _cull = camera.GetComponent<CullDistance>();
                _vanillaMin = f.moveMin.z;
                _vanillaCull = _cull != null && _cull.cullDistance != null ? (float[])_cull.cullDistance.Clone() : null;
                return true;
            }
            return false;
        }

        // draw to `k` times vanilla's distances; written only when it moves, and
        // into CullDistance's own array too, which re-applies it on every OnEnable
        private static void Stretch(float k)
        {
            // the far plane is the game's while the camera stands within vanilla's
            // reach - it shrinks it while a save loads, and resets it to 150
            // itself (CameraManager.ResetCameraNearFar) - so it is only ever
            // raised, past vanilla's, and only while pulled back past the limit
            if (k > 1f && _camera.farClipPlane < VanillaFar * k - 0.5f && _camera.farClipPlane >= VanillaFar - 0.5f)
            {
                _camera.farClipPlane = VanillaFar * k;
            }
            if (Mathf.Abs(k - _stretch) < 0.02f)
            {
                return;
            }
            if (k <= 1f && _stretch > 1f && _camera.farClipPlane > VanillaFar)
            {
                _camera.farClipPlane = VanillaFar;   // back within reach: vanilla's again
            }
            _stretch = k;
            if (_vanillaCull == null)
            {
                return;
            }
            var cull = new float[_vanillaCull.Length];
            for (int i = 0; i < cull.Length; i++)
            {
                cull[i] = _vanillaCull[i] * k;   // 0 stays 0: that layer goes to the far plane
            }
            _cull.cullDistance = cull;
            _camera.layerCullDistances = cull;
        }
    }
}
