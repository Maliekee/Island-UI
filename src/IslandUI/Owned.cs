using System.Collections.Generic;
using UnityEngine;

namespace IslandUI
{
    // Every GameObject this mod creates goes through here, so the mod can find its own
    // objects again. The one consumer is Plugin.Teardown, which runs when the plugin
    // component is destroyed EXPLICITLY - the test harness's hot reload
    // (docs/test-harness.md §7) - and never at game exit. In a normal session this is a
    // list that grows and is pruned on scene loads; nothing reads it.
    //
    // Children die with their parents, so tracking every object rather than only the
    // roots costs a reference each and spares every site the question of which it is.
    internal static class Owned
    {
        private static readonly List<GameObject> Objects = new List<GameObject>();

        internal static int Count => Objects.Count;

        /// <summary><c>new GameObject(name, typeof(RectTransform))</c>, tracked.</summary>
        internal static GameObject New(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            Objects.Add(go);
            return go;
        }

        /// <summary><c>Object.Instantiate</c>, tracked. Same overloads as the sites use.</summary>
        internal static T Instantiate<T>(T original) where T : Object
            => Track(Object.Instantiate(original));

        internal static T Instantiate<T>(T original, Transform parent) where T : Object
            => Track(Object.Instantiate(original, parent));

        internal static T Instantiate<T>(T original, Transform parent, bool worldPositionStays) where T : Object
            => Track(Object.Instantiate(original, parent, worldPositionStays));

        private static T Track<T>(T clone) where T : Object
        {
            GameObject go = clone as GameObject ?? (clone as Component)?.gameObject;
            if (go != null)
            {
                Objects.Add(go);
            }
            return clone;
        }

        // A scene load destroyed most of these; drop the corpses so the list does not
        // grow for the life of the session.
        internal static void Prune()
        {
            Objects.RemoveAll(go => go == null);
        }

        // Destroy everything still alive. Deferred (Object.Destroy), so the caller lets
        // a frame pass before anything expects them gone.
        internal static int DestroyAll()
        {
            int n = 0;
            for (int i = 0; i < Objects.Count; i++)
            {
                if (Objects[i] != null)
                {
                    Object.Destroy(Objects[i]);
                    n++;
                }
            }
            Objects.Clear();
            return n;
        }
    }
}
