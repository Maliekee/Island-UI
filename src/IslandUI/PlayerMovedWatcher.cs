using System;
using System.Collections.Generic;
using UnityEngine;

namespace IslandUI
{
    // Polling->edge machinery: "the player has moved". Samples the player position once
    // per frame (driven by Plugin.Update) and fires each subscription when the distance
    // from its own anchor (position at last firing) crosses its threshold. Lived in the
    // IslandEvents core mod once; this window is its only subscriber.
    internal static class PlayerMovedWatcher
    {
        private sealed class Subscription : IDisposable
        {
            internal float Threshold;
            internal Action<Vector3> Callback;
            internal Vector3 Anchor;
            internal bool HasAnchor;
            internal bool Disposed;

            public void Dispose()
            {
                Disposed = true;
            }
        }

        private static readonly List<Subscription> Subs = new List<Subscription>();

        /// <summary>
        /// Invokes <paramref name="callback"/> every time the player has moved at least
        /// <paramref name="thresholdMeters"/> from the position of the previous invocation
        /// (or from the subscription/session start). The callback receives the player's
        /// current position. Fires only while a play session is active; entering/leaving
        /// the game world resets the reference position without firing.
        /// Dispose the returned handle to unsubscribe.
        /// </summary>
        internal static IDisposable Subscribe(float thresholdMeters, Action<Vector3> callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }
            var sub = new Subscription
            {
                Threshold = Mathf.Max(0.01f, thresholdMeters),
                Callback = callback,
            };
            Subs.Add(sub);
            return sub;
        }

        internal static void Tick(Vector3 playerPos)
        {
            for (int i = Subs.Count - 1; i >= 0; i--)
            {
                Subscription sub = Subs[i];
                if (sub.Disposed)
                {
                    Subs.RemoveAt(i);
                    continue;
                }
                if (!sub.HasAnchor)
                {
                    sub.Anchor = playerPos;
                    sub.HasAnchor = true;
                    continue;
                }
                if (Vector3.Distance(playerPos, sub.Anchor) >= sub.Threshold)
                {
                    sub.Anchor = playerPos;
                    try
                    {
                        sub.Callback(playerPos);
                    }
                    catch (Exception e)
                    {
                        // one broken subscriber must not break the others
                        Plugin.Log.LogError($"PlayerMoved subscriber threw: {e}");
                    }
                }
            }
        }

        // Player left the world (menu, loading, death screen): drop anchors so the next
        // session initializes fresh instead of firing on the position jump.
        internal static void PlayerGone()
        {
            for (int i = 0; i < Subs.Count; i++)
            {
                Subs[i].HasAnchor = false;
            }
        }
    }
}
