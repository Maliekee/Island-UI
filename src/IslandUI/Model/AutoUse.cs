using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace IslandUI
{
    // AUTO-USAGE (Forgejo #119): an act that can also run by itself. A right-click
    // on its key in the dock (or a window's act row) switches it on for THAT
    // building; the key wears the auto mark while it is on, and the player then
    // does the act unasked whenever they are near the building and need it.
    //
    // The mechanism is the act's, not the building's: an act opts in by appearing
    // in Kinds below - the InventorySlot.Type it is offered on, the state vanilla's
    // PropAction switches on, what "in need" means and what one use does. The well's
    // Drink and the food box's Eat are the first two; another act opts in with one
    // more entry, and the key, its mark, its tip and the save come with it.
    //
    // VANILLA. Nobody auto-uses for the player: Drink and Eat are props' panel acts
    // (UIManager.PropPanelTypeChange: Type.Water -> state 3, Type.FoodStorage ->
    // state 4), done on the press by UIManager.PropAction. What vanilla does run by
    // itself is the VILLAGERS': on each breed tick (GameManager.BreedCheck) a food
    // store feeds and a well waters every active friend inside its circle to 100
    // (FoodStorageCheck, WaterStorageCheck) - 10 m for the _01 kinds, 20 m for _02,
    // the circle the well draws when you use it. The player's auto-use does NOT take
    // that circle: it works from where a press on the act works (ReachRule.Gap),
    // so it saves the click and never the walk (the ask: within the player's reach,
    // not the building's).
    //
    // NOT EVERY FRAME. The switched-on buildings are a short list of live
    // references. Once a second, and only while the player is below a kind's need
    // line, that list - and nothing else in the scene - is walked for the nearest
    // building of the kind in range with stock in it. A player who is not hungry or
    // thirsty costs two comparisons a second.
    //
    // KEPT IN THE SAVE (SaveBlock): each switch as the building's buildID - the
    // game's own per-building counter, saved with the building (BuildManager.AddBuild,
    // SaveManager BuildSave) - plus its item key as the check. Off by default: it is
    // the player's opt-in, not vanilla's behaviour.
    internal static class AutoUse
    {
        // Below this share of the bar the player is in need; one use then fills it,
        // as a press of the act does.
        internal const float NeedBelow = 0.7f;
        private const float Every = 1f;

        private sealed class Kind
        {
            public InventorySlot.Type Type;
            public int State;                                   // propActionState: what PropAction switches on
            public string Name;                                 // for the log and the harness
            public string Icon;                                 // the piece that flies from the station to you (UseFlight)
            public Func<string> OffTip;                         // the key's tip line, switched off
            public Func<string, string> OnTip;                  // ...and on, with the radius in metres
            public Func<CommonStates, float> Fill;              // 0..1 of the bar this act refills
            public Func<ManagersScript, CommonStates, InventorySlot, bool> Use;
        }

        private static readonly Kind[] Kinds =
        {
            new Kind
            {
                Type = InventorySlot.Type.Water, State = 3, Name = "drink", Icon = "st_water",
                OffTip = () => Shared.Loc.Get("auto.water.off", "Right-click: drink here by yourself when thirsty"),
                OnTip = m => Shared.Loc.Get("auto.water.on", "Auto: you drink here when thirsty within {0} m. Right-click to stop", m),
                Fill = who => who.maxWater > 0f ? who.water / who.maxWater : 1f,
                Use = Drink,
            },
            new Kind
            {
                Type = InventorySlot.Type.FoodStorage, State = 4, Name = "eat", Icon = "st_food",
                OffTip = () => Shared.Loc.Get("auto.food.off", "Right-click: eat here by yourself when hungry"),
                OnTip = m => Shared.Loc.Get("auto.food.on", "Auto: you eat here when hungry within {0} m. Right-click to stop", m),
                Fill = who => who.maxFood > 0f ? who.food / who.maxFood : 1f,
                Use = Eat,
            },
        };

        private static readonly List<ItemInfo> On = new List<ItemInfo>();
        private static float _next;

        private static Kind KindOf(ItemInfo prop, int state)
        {
            InventorySlot store = prop != null ? prop.GetComponent<InventorySlot>() : null;
            if (store == null || store.slots == null || store.slots.Length == 0)
            {
                return null;
            }
            for (int i = 0; i < Kinds.Length; i++)
            {
                if (Kinds[i].Type == store.type && (state < 0 || Kinds[i].State == state))
                {
                    return Kinds[i];
                }
            }
            return null;
        }

        // The piece a use of this act flies to the player (UseFlight), or null when
        // the act is not one of the kinds
        internal static string IconFor(ItemInfo prop, int state)
        {
            Kind kind = KindOf(prop, state);
            return kind != null ? kind.Icon : null;
        }

        internal static bool IsOn(ItemInfo prop)
        {
            return prop != null && On.Contains(prop);
        }

        internal static bool Toggle(ItemInfo prop)
        {
            if (prop == null || KindOf(prop, -1) == null)
            {
                return false;
            }
            bool on = !On.Remove(prop);
            if (on)
            {
                On.Add(prop);
                _next = 0f;   // in need and in range: the first use is now, not a second on
            }
            Plugin.Log.LogInfo($"Auto-use {(on ? "on" : "off")}: {prop.itemKey} #{prop.buildID}");
            return on;
        }

        // The act's switch for the act row, or null when this act has no auto-usage.
        internal static ActRow.IAuto SwitchFor(ItemInfo prop, int state)
        {
            Kind kind = KindOf(prop, state);
            return kind != null ? new Switch(prop, kind) : null;
        }

        private sealed class Switch : ActRow.IAuto
        {
            private readonly ItemInfo _prop;
            private readonly Kind _kind;

            internal Switch(ItemInfo prop, Kind kind)
            {
                _prop = prop;
                _kind = kind;
            }

            public bool On { get { return IsOn(_prop); } }

            public void Toggle()
            {
                AutoUse.Toggle(_prop);
            }

            public string Tip
            {
                get
                {
                    return On ? _kind.OnTip(Reach(_prop).ToString("0", CultureInfo.InvariantCulture)) : _kind.OffTip();
                }
            }
        }

        // The reach a press on the act works from: yours by ReachRule.OfProp (3 m, or
        // the construction range when it is longer), for the tip's "within N m"
        private static float Reach(ItemInfo prop)
        {
            float reach = ReachRule.OfProp(Mn(), prop);
            return reach > 0f ? reach : ReachRule.Touch;
        }

        // ---------- the tick ----------

        internal static void Tick(ManagersScript mn)
        {
            if (On.Count == 0 || Time.time < _next)
            {
                return;
            }
            _next = Time.time + Every;
            if (mn == null || !mn.allSet || mn.gameMN == null || mn.gameMN.pMove == null
                || !mn.gameMN.pMove.controllable || GameManager.nowEvent || Time.timeScale <= 0f)
            {
                return;
            }
            CommonStates you = mn.gameMN.playerCommons != null && GameManager.selectPlayer < mn.gameMN.playerCommons.Length
                ? mn.gameMN.playerCommons[GameManager.selectPlayer] : null;
            if (you == null || you.life <= 0.0 || you.dead != 0)
            {
                return;
            }
            for (int k = 0; k < Kinds.Length; k++)
            {
                Kind kind = Kinds[k];
                if (kind.Fill(you) >= NeedBelow)
                {
                    continue;
                }
                InventorySlot best = Nearest(mn, kind, you.transform.position);
                if (best != null && kind.Use(mn, you, best))
                {
                    Plugin.Log.LogInfo($"Auto-use: {kind.Name} from {best.GetComponent<ItemInfo>().itemKey}");
                    UseFlight.Launch(mn, best, kind.Icon);
                }
            }
        }

        private static InventorySlot Nearest(ManagersScript mn, Kind kind, Vector3 you)
        {
            InventorySlot best = null;
            float bestD = float.MaxValue;
            for (int i = On.Count - 1; i >= 0; i--)
            {
                ItemInfo prop = On[i];
                if (prop == null)
                {
                    On.RemoveAt(i);   // removed or destroyed: its switch goes with it
                    continue;
                }
                if (!prop.gameObject.activeInHierarchy)
                {
                    continue;         // another stage's
                }
                InventorySlot store = prop.GetComponent<InventorySlot>();
                if (store == null || store.type != kind.Type || store.slots == null || store.slots.Length == 0
                    || store.slots[0] == null || store.slots[0].itemLife <= 0f)
                {
                    continue;
                }
                float d = Vector3.Distance(you, prop.transform.position);
                if (ReachRule.Gap(mn, prop, false) <= 0f && d < bestD)
                {
                    best = store;
                    bestD = d;
                }
            }
            return best;
        }

        // UIManager.PropAction, state 3 - the same moves, the same sound
        private static bool Drink(ManagersScript mn, CommonStates who, InventorySlot well)
        {
            ItemSlot store = well != null ? well.slots[0] : null;
            if (store == null || store.itemLife <= 0f)
            {
                return false;
            }
            if (store.itemLife >= who.maxWater - who.water)
            {
                float value = 100f - who.water;
                mn.gameMN.WaterChange(who, 100f);
                mn.gameMN.PlayerStatesCheck();
                mn.itemMN.CollectWater(store, value);
            }
            else
            {
                mn.gameMN.WaterChange(who, store.itemLife);
                mn.gameMN.PlayerStatesCheck();
                mn.itemMN.CollectWater(store, 100f);
            }
            mn.sound.GoSound(10, who.transform.position);
            return true;
        }

        // UIManager.PropAction, state 4
        private static bool Eat(ManagersScript mn, CommonStates who, InventorySlot box)
        {
            ItemSlot store = box != null ? box.slots[0] : null;
            if (store == null || store.itemLife <= 0f || who.food >= 100f)
            {
                return false;
            }
            if (store.itemLife - (100f - who.food) >= 0f)
            {
                store.itemLife -= 100f - who.food;
                mn.gameMN.FoodChange(who, 100f);
            }
            else
            {
                mn.gameMN.FoodChange(who, store.itemLife);
                store.itemLife = 0f;
            }
            mn.gameMN.PlayerStatesCheck();
            mn.sound.GoSound(9, who.transform.position);
            return true;
        }

        // ---------- the save ----------

        internal static void WriteTo(StringBuilder body)
        {
            var seen = new HashSet<int>();
            foreach (ItemInfo prop in On)
            {
                if (prop != null && prop.buildID >= 0 && seen.Add(prop.buildID))
                {
                    body.Append("    <Auto b=\"").Append(prop.buildID.ToString(CultureInfo.InvariantCulture))
                        .Append("\" key=\"").Append(System.Security.SecurityElement.Escape(prop.itemKey ?? "")).Append("\" />\n");
                }
            }
        }

        internal static bool Restore(ManagersScript mn, int buildID, string key, bool dryRun)
        {
            if (mn == null || mn.buildMN == null || mn.buildMN.build == null || buildID < 0 || string.IsNullOrEmpty(key))
            {
                return false;
            }
            List<ItemInfo> all = mn.buildMN.build;
            for (int i = 0; i < all.Count; i++)
            {
                ItemInfo prop = all[i];
                if (prop != null && prop.buildID == buildID && prop.itemKey == key && KindOf(prop, -1) != null)
                {
                    if (!dryRun && !On.Contains(prop))
                    {
                        On.Add(prop);
                    }
                    return true;
                }
            }
            return false;
        }

        // the switches are this scene's buildings; a load puts the save's back (SaveBlock)
        internal static void ResetForSceneLoad()
        {
            On.Clear();
            _next = 0f;
        }

        // ---------- across a hot reload ----------

        private const string HandoverKey = "IslandUI.AutoUse.handover";

        internal static void HandOver()
        {
            On.RemoveAll(p => p == null);
            AppDomain.CurrentDomain.SetData(HandoverKey, On.ToArray());
        }

        internal static void TakeOver()
        {
            ItemInfo[] kept = AppDomain.CurrentDomain.GetData(HandoverKey) as ItemInfo[];
            AppDomain.CurrentDomain.SetData(HandoverKey, null);
            if (kept == null)
            {
                return;
            }
            foreach (ItemInfo prop in kept)
            {
                if (prop != null && !On.Contains(prop))
                {
                    On.Add(prop);
                }
            }
            Plugin.Log.LogInfo($"Auto-use: reload kept {On.Count} switch(es)");
        }

        private static ManagersScript Mn()
        {
            GameObject managers = GameObject.Find("Managers");
            return managers != null ? managers.GetComponent<ManagersScript>() : null;
        }

        // For the harness: the player's two bars set outright, to put them in need.
        internal static string SetBars(float water, float food)
        {
            ManagersScript mn = Mn();
            CommonStates you = mn != null && mn.gameMN != null ? mn.gameMN.playerCommons[GameManager.selectPlayer] : null;
            if (you == null)
            {
                return "no player";
            }
            you.water = water;
            you.food = food;
            mn.gameMN.PlayerStatesCheck();
            return Dump();
        }

        // For the harness: where each building of a kind is on screen, in pixels from
        // the top left, and its buildID - to point at it with real input.
        internal static string HarnessWhere(string key)
        {
            Camera cam = Camera.main;
            var sb = new StringBuilder();
            foreach (ItemInfo info in UnityEngine.Object.FindObjectsOfType<ItemInfo>())
            {
                if (cam == null || info == null || info.itemKey != key || !info.gameObject.activeInHierarchy)
                {
                    continue;
                }
                Vector3 s = cam.WorldToScreenPoint(info.transform.position);
                sb.Append('#').Append(info.buildID).Append(" at ").Append(s.x.ToString("0")).Append(',')
                    .Append((Screen.height - s.y).ToString("0")).Append(IsOn(info) ? " auto" : "").Append(" | ");
            }
            return sb.ToString();
        }

        // For the harness: the switches, the bars, and what the tick would see now.
        internal static string Dump()
        {
            ManagersScript mn = Mn();
            var sb = new StringBuilder();
            CommonStates you = mn != null && mn.gameMN != null ? mn.gameMN.playerCommons[GameManager.selectPlayer] : null;
            if (you != null)
            {
                sb.Append("water ").Append(you.water.ToString("0.#")).Append(" food ").Append(you.food.ToString("0.#")).Append(" | ");
            }
            sb.Append(On.Count).Append(" on");
            foreach (ItemInfo prop in On)
            {
                if (prop == null) { sb.Append(" | (gone)"); continue; }
                InventorySlot store = prop.GetComponent<InventorySlot>();
                sb.Append(" | ").Append(prop.itemKey).Append(" #").Append(prop.buildID)
                    .Append(" stock ").Append(store != null && store.slots.Length > 0 ? store.slots[0].itemLife.ToString("0") : "?")
                    .Append(" r ").Append(Reach(prop).ToString("0.#"));
                if (mn != null && mn.gameMN != null && mn.gameMN.pMove != null)
                {
                    sb.Append(" d ").Append(Vector3.Distance(mn.gameMN.pMove.transform.position, prop.transform.position).ToString("0.0"));
                }
            }
            return sb.ToString();
        }
    }
}
