using BepInEx.Configuration;
using HarmonyLib;
using Shared;
using UnityEngine;

namespace IslandEx
{
    // Crafting draws from your chests too, by setting. The game crafts at a bench
    // from its own strip and from the Material storages (spider, chaos) you own
    // within 5 m of it - FindCraftReference fills InventoryManager.refInventory
    // with them, and the craft check (CraftableCheckReference) and the consume
    // (CraftItem) both read that one list. A plain chest never counts, which is a
    // rule of the game, so this is Island Ex's and not Island UI's.
    //
    // A postfix appends more of your containers to the same list: one master
    // switch, Extend Supported Storages, off by default, and under it one
    // switch per family:
    //   Chests  - the inert storage: wooden chests, boxes, the mine's output
    //   Benches - another workbench's ten-slot strip
    // Never a breeding prop (crafting would eat a planter's seed or a trap's
    // bait), a rack, a closet, anyone's bag, or a treasure chest (its loot is
    // rolled on first open). The game's own terms stay: only what you own
    // (BuildManager.OwnCheck), and not a wreck.
    //
    // WHERE FROM. At a bench (craftPanelID != 0) the scan is the game's: 5 m
    // around the bench, and the game's blue link lines are drawn to what joins.
    // Without a bench the game reads no list at all, but Island UI fills it for
    // its benchless crafting and building, centred on you at the reach its
    // vicinity window gives one of your chests (3 m, or construction range when
    // larger) - so this measures the same, and a chest the vicinity window shows
    // is one a recipe can use. Island UI's own prefix runs first and clears the
    // list; this only adds.
    //
    // THE OPEN CONTAINER is left out. While the game's panel shows a container
    // its contents sit in the mirror slots (itemSlot[50+]) and its real slots are
    // empty; at a bench that container is the bench, which the game already
    // counts, and under Island UI an opened bench is counted as the focus bench.
    // Either way, listing it again would count it twice.
    [HarmonyPatch]
    internal static class ChestCrafting
    {
        private static ConfigEntry<bool> _on;
        private static ConfigEntry<bool> _chests;
        private static ConfigEntry<bool> _benches;

        private static readonly AccessTools.FieldRef<InventoryManager, ManagersScript> MnRef =
            AccessTools.FieldRefAccess<InventoryManager, ManagersScript>("mn");

        private const float BenchReach = 5f;   // FindCraftReference's own sphere
        private const float NearReach = 3f;    // Island UI's reach for a chest of yours

        internal static void Bind(ConfigFile cfg)
        {
            _on = cfg.Bind("Crafting", "Extend Supported Storages", false,
                "Crafting also uses what is in the kinds of your containers switched on below, on top of the game's material storages. At a bench: within 5 m of it. Without one (Island UI): within your reach. Off = only material storages, as in the unmodded game.");
            _chests = cfg.Bind("Crafting", "Chests And Boxes", true,
                "With Extend Supported Storages on: your wooden chests, boxes and the mine's output count.");
            _benches = cfg.Bind("Crafting", "Other Workbenches", false,
                "With Extend Supported Storages on: what lies in your other workbenches' strips counts.");
        }

        [HarmonyPatch(typeof(InventoryManager), nameof(InventoryManager.FindCraftReference))]
        [HarmonyPostfix]
        private static void FindCraftReference_Postfix(InventoryManager __instance)
        {
            if (!Plugin.Enabled.Value || !_on.Value || !(_chests.Value || _benches.Value))
            {
                return;
            }
            ManagersScript mn = MnRef(__instance);
            if (mn == null || mn.buildMN == null || mn.gameMN == null)
            {
                return;
            }
            bool bench = __instance.craftPanelID != 0;
            Transform centre = bench
                ? (__instance.tmpSubInventory != null ? __instance.tmpSubInventory.transform : null)
                : (mn.gameMN.pMove != null ? mn.gameMN.pMove.transform : null);
            if (centre == null)
            {
                return;
            }
            float reach = bench ? BenchReach : Mathf.Max(NearReach, mn.buildMN.buildableDist + 0.5f);
            InventorySlot open = __instance.subInventory != null && __instance.subInventory.activeSelf
                ? __instance.tmpSubInventory : null;
            Vector3 from = centre.position;
            Collider[] hits = Physics.OverlapSphere(from, reach, LayerMask.GetMask("BG"));
            for (int i = 0; i < hits.Length; i++)
            {
                InventorySlot chest = hits[i].GetComponent<InventorySlot>();
                if (chest == null || chest.slots == null || ReferenceEquals(chest, open)
                    || __instance.refInventory.Contains(chest) || !Wanted(chest)
                    || !mn.buildMN.OwnCheck(chest.GetComponent<ItemInfo>()))
                {
                    continue;
                }
                // without a bench, Island UI's rule: the chest's centre within reach
                if (!bench && (chest.transform.position - from).sqrMagnitude > reach * reach)
                {
                    continue;
                }
                __instance.refInventory.Add(chest);
                if (bench)
                {
                    __instance.StartCoroutine(__instance.DrawLinkLine(from, chest.transform.position));
                }
            }
        }

        // Harness hook (game.ps1 invoke IslandEx.ChestCrafting CraftDump): every
        // container within 12 m of you, what it is and whether it is wanted, and
        // what the list holds after a rescan from where you stand.
        internal static string CraftDump()
        {
            InventoryManager inv = Object.FindObjectOfType<InventoryManager>();
            ManagersScript mn = inv != null ? MnRef(inv) : null;
            if (mn == null || mn.gameMN == null || mn.gameMN.pMove == null)
            {
                return "no game";
            }
            Vector3 you = mn.gameMN.pMove.transform.position;
            var sb = new System.Text.StringBuilder();
            sb.Append("panel ").Append(inv.craftPanelID).Append(" reach ")
              .Append(Mathf.Max(NearReach, mn.buildMN.buildableDist + 0.5f).ToString("0.0")).Append('\n');
            foreach (InventorySlot c in Object.FindObjectsOfType<InventorySlot>())
            {
                float d = (c.transform.position - you).magnitude;
                if (d > 12f)
                {
                    continue;
                }
                ItemInfo info = c.GetComponent<ItemInfo>();
                sb.Append(info != null ? info.itemKey : c.name).Append(' ').Append(c.type)
                  .Append(' ').Append(ContainerKinds.EngineOf(c)).Append(" d=").Append(d.ToString("0.0"))
                  .Append(info != null && mn.buildMN.OwnCheck(info) ? " own" : " notown")
                  .Append(Wanted(c) ? " wanted" : string.Empty).Append('\n');
            }
            inv.FindCraftReference();
            sb.Append("list:");
            foreach (InventorySlot c in inv.refInventory)
            {
                ItemInfo info = c.GetComponent<ItemInfo>();
                sb.Append(' ').Append(info != null ? info.itemKey : c.name);
            }
            return sb.ToString();
        }

        private static bool Wanted(InventorySlot c)
        {
            if (ContainerKinds.IsCharacterBag(c) || ContainerKinds.IsWrecked(c))
            {
                return false;
            }
            switch (ContainerKinds.EngineOf(c))
            {
                case ContainerKinds.Engine.Inert:
                    // Material storage is the game's own, already listed; a closet
                    // holds clothes, never materials
                    return _chests.Value && c.type != InventorySlot.Type.Material && !ContainerKinds.IsCloset(c);
                case ContainerKinds.Engine.Bench:
                    return _benches.Value;
            }
            return false;
        }
    }
}
