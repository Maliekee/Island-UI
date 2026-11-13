using System;

namespace IslandUI
{
    // WHAT MAN AND YONA MAY BE TOLD (Forgejo #246). The game gives the player
    // character you are not playing no stance, job, permissions or Call
    // (InventoryWindow.IsPlayerCharacter); Island Ex can allow each of the four,
    // since that changes the game's rules. This only reads its switches, through
    // Island Ex's plugin instance (Plugin.PartnerTakes, found by name), with no
    // reference to its assembly: Island Ex missing, or a build without the method,
    // reads as no, which is exactly the game's rule.
    internal static class PartnerSwitches
    {
        internal const string Job = "job";
        internal const string Stance = "stance";
        internal const string Permissions = "permissions";
        internal const string Call = "call";

        private const string ExGuid = "madisland.islandex";

        private static bool _resolved;
        private static Func<string, bool> _takes;

        // whether `who` may be given this kind of order: anyone who isn't a player
        // character, and Man or Yona while Island Ex allows it. Never the friend
        // you play right now (the beta): an order to yourself is no order.
        internal static bool Allows(CommonStates who, string order)
        {
            if (who != null && who.npcID > 1 && InventoryWindow.IsPlayerCharacter(who))
            {
                return false;
            }
            return !InventoryWindow.IsPlayerCharacter(who) || IslandExAllows(order);
        }

        // the reverse, for the many places that ask "is this refused"
        internal static bool Refuses(CommonStates who, string order)
        {
            return !Allows(who, order);
        }

        internal static bool IslandExAllows(string order)
        {
            Func<string, bool> takes = Resolve();
            if (takes == null)
            {
                return false;
            }
            try
            {
                return takes(order);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Partner orders: Island Ex's switch could not be read - " + e.Message);
                _takes = null;   // resolved, and off from here on
                return false;
            }
        }

        // Found once Island Ex is loaded, then kept: its methods do not change
        // while the game runs. Until it is found (it may load after Island UI),
        // each ask looks again, which is one dictionary lookup.
        private static Func<string, bool> Resolve()
        {
            if (_resolved)
            {
                return _takes;
            }
            BepInEx.PluginInfo info;
            if (!BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue(ExGuid, out info) || info.Instance == null)
            {
                return null;
            }
            _resolved = true;
            try
            {
                System.Reflection.MethodInfo m = info.Instance.GetType().GetMethod("PartnerTakes",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                    null, new[] { typeof(string) }, null);
                if (m != null && m.ReturnType == typeof(bool))
                {
                    _takes = (Func<string, bool>)Delegate.CreateDelegate(typeof(Func<string, bool>), m);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Partner orders: Island Ex's switch not found - " + e.Message);
                _takes = null;
            }
            return _takes;
        }
    }
}
