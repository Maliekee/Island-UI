using BepInEx.Configuration;
using UnityEngine;

namespace IslandEx
{
    // No rain inside a house: entering a building, a stone house say, should stop
    // the rain, and vanilla lets it go on.
    //
    // The weather is one object, "Weather" (GameManager.weatherGrp, a FollowPosition
    // that keeps it on the player), with the rain, the blood rain, leaves and cloud
    // under it. Vanilla takes it away only on another STAGE - StageChange switches
    // it off in a cave and back on at the island. A house is not a stage: its
    // interior stands on the island's stage, and entering one only sets
    // GameManager.tmpHouse to the house's id (DoorScript.EnterDoor; 0 again through
    // the in_door). Vanilla reads tmpHouse for footstep sounds and never for the
    // weather, so the rain follows you in.
    //
    // So while tmpHouse is not 0 the Weather object is switched off, and on again
    // when it is. Only its own active flag, which vanilla never touches - vanilla
    // toggles the FollowPosition and the children - so a change of weather while you
    // are inside lands as usual and shows when you step out. The weather itself
    // (weatherState) is untouched: a fire still will not light outdoors in the rain,
    // the blood rain's red tint on the camera stays, and the crops are watered.
    internal static class DryIndoors
    {
        private static ConfigEntry<bool> _enabled;
        private static GameManager _game;
        private static GameObject _hidden;   // the Weather object we switched off
        private static int _tick;

        internal static void Bind(ConfigFile cfg)
        {
            _enabled = cfg.Bind("Weather", "Dry Indoors", true,
                "No rain falls inside a house; outside it still rains. Off = the rain follows you indoors. Applies live.");
        }

        internal static void Tick()
        {
            if (++_tick < 10)
            {
                return;
            }
            _tick = 0;
            if (_game == null)
            {
                _game = Object.FindObjectOfType<GameManager>();
                _hidden = null;   // a new scene's weather, never switched off by us
                if (_game == null)
                {
                    return;
                }
            }
            FollowPosition grp = _game.weatherGrp;
            GameObject weather = grp != null ? grp.gameObject : null;
            bool dry = _enabled.Value && Plugin.Enabled.Value && weather != null
                && _game.tmpStage == 0 && _game.tmpHouse != 0;
            if (dry && weather.activeSelf)
            {
                weather.SetActive(false);
                _hidden = weather;
            }
            else if (!dry && _hidden != null)
            {
                _hidden.SetActive(true);
                _hidden = null;
            }
        }
    }
}
