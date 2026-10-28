using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace IslandUI
{
    // THE PLAIN TALK'S REPLY LEAVES YOU FREE (Forgejo #58 round 3). Talk -> Talk runs
    // NPCManager.NPCTalkButton -> NPCTalk(menuNPC, TalkType.Friend) (NPCManager.cs:2967),
    // which locks the player for the whole line: Controlable(false) (GameManager.cs:5520:
    // pMove.controllable off, so PlayerMove.Update returns at :171; the rigidbody
    // kinematic; the player invincible), then TalkAction's balloon dwells 5 s or until a
    // click or Space (NPCManager.cs:3203), then Controlable(true). Vanilla freezes exactly
    // so. The bubble only made it stand out: since round 2 everything up to the choice
    // leaves you free, and the reply was the one place you still stood still.
    //
    // A FRIEND TALK IS A LINE AND NOTHING ELSE. TalkAction's Friend case picks a sentence
    // by mood and love, shows it, and returns: no love change, no item, no event. So its
    // lock guards nothing, and this runs the same talk without it, the way vanilla already
    // runs the Sex invite's reply (SexButton, NPCManager.cs:5123, controlable: true). What
    // stays vanilla's: pMove.target is cleared, which ends PlayerMove.CheckNPC and closes
    // the NPC menu as before; she turns to you and holds still (ActType.Wait) while she
    // speaks; the line, its 5 s and its click/Space skip are TalkAction's own, called as is.
    // What goes with the lock: the player's forced idle pose, ResetColl and
    // Controlable(true) at the end, which would also have freed a player that something
    // else locked during the line.
    //
    // ONLY THE FRIEND TALK. Event and shop talks (CheckFriendlyNPC, PlayerMove.cs:979/983)
    // step through a scene's lines with mn.story.Next and keep their lock; a talk already
    // asked to be controlable is vanilla's to run.
    //
    // HER HOLD IS RELEASED THE WAY VANILLA RELEASES ONE IT SHARES (RecieveEmotion,
    // NPCManager.cs:6553): back to Idle only if she is still in the Wait it set. Now that
    // you can act during the line, something else may take her first - a sex scene or a
    // story holds its NPCs in Wait too (SexManager, StoryManager) - so neither a sex state
    // nor a changed act is handed back to Idle. And she says one line at a time: a second
    // Talk while she speaks closes the menu and adds no second balloon over the first.
    [HarmonyPatch]
    internal static class TalkReply
    {
        // TalkAction's longest line and a little: a talk stopped from outside (a scene
        // change) stops counting as speaking by then, instead of never
        private const float LineMax = 6f;

        private static readonly AccessTools.FieldRef<NPCManager, ManagersScript> NpcMn =
            AccessTools.FieldRefAccess<NPCManager, ManagersScript>("mn");
        private static readonly Dictionary<int, float> _speakingUntil = new Dictionary<int, float>();

        [HarmonyPatch(typeof(NPCManager), nameof(NPCManager.NPCTalk))]
        [HarmonyPrefix]
        private static bool NPCTalk_Prefix(NPCManager __instance, CommonStates common,
            NPCManager.TalkType talkType, bool controlable, ref IEnumerator __result)
        {
            if (talkType != NPCManager.TalkType.Friend || controlable)
            {
                return true;
            }
            __result = Free(__instance, common);
            return false;
        }

        private static IEnumerator Free(NPCManager npcMN, CommonStates common)
        {
            // vanilla's own guard
            if (common == null || common.nMove == null || common.life <= 0.0 || common.faint <= 0.0)
            {
                yield break;
            }
            ManagersScript mn = NpcMn(npcMN);
            mn.gameMN.pMove.target = null;
            int id = common.GetInstanceID();
            float until;
            if (_speakingUntil.TryGetValue(id, out until) && Time.time < until)
            {
                yield break;
            }
            _speakingUntil[id] = Time.time + LineMax;
            NPCMove nMove = common.nMove;
            nMove.actType = NPCMove.ActType.Wait;
            nMove.Rot(mn.gameMN.player.transform.position);
            yield return npcMN.TalkAction(common, NPCManager.TalkType.Friend);
            _speakingUntil.Remove(id);
            if (common != null && nMove != null && common.life > 0.0 && common.faint > 0.0
                && nMove.actType == NPCMove.ActType.Wait && common.sex == CommonStates.SexState.None)
            {
                nMove.actType = NPCMove.ActType.Idle;
            }
        }
    }
}
