using System;
using HarmonyLib;

namespace IslandUI
{
    // WHO "YOU" ARE: THE CHARACTER BEING PLAYED, NOT ALWAYS THE HERO. The beta
    // (26642668) lets you take control of a friend - Reika, Nami, Takumi - and does
    // it by rebinding GameManager.player and pMove to her (GameManager.cs:6738,
    // CommitControlledCharacter :7971). The hero is parked as an NPC meanwhile, and
    // playerCommons[selectPlayer] still names the hero: it no longer means "you".
    // The game's own HUD reads CurrentPlayer (player.GetComponent<CommonStates>()),
    // so this does too.
    //
    // TWO GAME BUILDS: CurrentPlayer's body is plain enough to repeat, so Of works on
    // both. IsControlledNpc is the beta's (it also leaves free mode out); on the
    // public build there is no such method and nobody but a hero is ever played.
    internal static class PlayedAs
    {
        // the character you play right now: the hero, or the friend you took over
        internal static CommonStates Of(GameManager gm)
        {
            if (gm == null || gm.player == null)
            {
                return null;
            }
            return gm.player.GetComponent<CommonStates>();
        }

        internal static bool Is(GameManager gm, CommonStates who)
        {
            return who != null && ReferenceEquals(who, Of(gm));
        }

        private static Func<GameManager, CommonStates, bool> _controlled;
        private static bool _looked;

        // a friend you are playing in place of the hero (the beta only)
        internal static bool IsControlledNpc(GameManager gm, CommonStates who)
        {
            if (gm == null || who == null)
            {
                return false;
            }
            if (!_looked)
            {
                _looked = true;
                System.Reflection.MethodInfo m = AccessTools.Method(typeof(GameManager), "IsControlledNpc", new[] { typeof(CommonStates) });
                if (m != null)
                {
                    _controlled = (Func<GameManager, CommonStates, bool>)Delegate.CreateDelegate(typeof(Func<GameManager, CommonStates, bool>), m);
                }
            }
            return _controlled != null && _controlled(gm, who);
        }

        // you play a friend right now
        internal static bool PlayingFriend(GameManager gm)
        {
            return IsControlledNpc(gm, Of(gm));
        }
    }
}
