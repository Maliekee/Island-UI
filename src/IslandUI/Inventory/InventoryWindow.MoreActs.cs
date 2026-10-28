using System.Collections.Generic;
using IslandUI.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI
{
    internal static partial class InventoryWindow
    {
        private static bool _moreActsOpen;
        private static Image _moreActsPanel;
        private static readonly List<TargetVerbs.Verb> MoreActs = new List<TargetVerbs.Verb>();
        private static readonly List<Button> MoreButtons = new List<Button>();
        private static readonly List<TextMeshProUGUI> MoreLabels = new List<TextMeshProUGUI>();

        internal static void ToggleMoreActs()
        {
            _moreActsOpen = !_moreActsOpen;
            PaintMoreActs();
        }

        private static void PaintMoreActs()
        {
            if (_moreActsPanel == null && _moreActsOpen)
            {
                _moreActsPanel = Theme.KitImage(_unit.transform, "MoreActions", "kit_plank_walnut", 18, Color.white);
                _moreActsPanel.raycastTarget = true;
                MoreButtons.Clear();
                MoreLabels.Clear();
            }
            if (_moreActsPanel == null)
                return;
            bool show = _moreActsOpen && MoreActs.Count > 0;
            _moreActsPanel.gameObject.SetActive(show);
            if (!show)
                return;
            while (MoreButtons.Count < MoreActs.Count)
            {
                int index = MoreButtons.Count;
                var face = Theme.KitImage(_moreActsPanel.transform, "Action" + index, "kit_plank_walnut", 18, Color.white);
                face.raycastTarget = true;
                var button = face.gameObject.AddComponent<Button>();
                button.targetGraphic = face;
                button.onClick.AddListener(() => { if (index < MoreActs.Count) { string name = MoreActs[index].Name; _moreActsOpen = false; PressUnitAct(_unitFor, name); PaintMoreActs(); } });
                var text = Theme.MakeKitText(face.transform, "Label", 9, Theme.KitCream, TextAlignmentOptions.MidlineLeft);
                text.raycastTarget = false;
                text.enableWordWrapping = false;
                text.overflowMode = TextOverflowModes.Ellipsis;
                MoreButtons.Add(button);
                MoreLabels.Add(text);
            }
            float height = MoreActs.Count * 20 + 8;
            Theme.Place(_moreActsPanel, 0, -height - 4, 140, height);
            for (int i = 0; i < MoreButtons.Count; i++)
            {
                bool live = i < MoreActs.Count;
                MoreButtons[i].gameObject.SetActive(live);
                if (!live)
                    continue;
                Theme.Place(MoreButtons[i].GetComponent<Image>(), 4, 4 + i * 20, 132, 19);
                Theme.Place(MoreLabels[i], 5, 0, 122, 19);
                MoreLabels[i].text = MoreActs[i].Label;
                MoreButtons[i].interactable = InReach(_unitFor) || ReachFree(MoreActs[i].Name);
            }
            _moreActsPanel.transform.SetAsLastSibling();
        }

        // For the harness: the row as the player meets it - every socket with its
        // label, then what waits in More - so two ways onto one person can be
        // compared as text.
        internal static string ActsDump()
        {
            if (_unit == null || !_unit.activeSelf)
            {
                return "unit: hidden";
            }
            var sb = new System.Text.StringBuilder();
            sb.Append(_unitName.text);
            for (int i = 0; i < ActRow.Sockets; i++)
            {
                ActRow.Slot s = _unitRow.Get(i);
                if (s != null) { sb.Append(" | ").Append(i).Append(':').Append(s.Name).Append(" \"").Append(s.Label).Append('"'); }
            }
            sb.Append(" || more:");
            for (int i = 0; i < MoreActs.Count; i++)
            {
                sb.Append(i == 0 ? " " : ", ").Append(MoreActs[i].Name).Append(" \"").Append(MoreActs[i].Label).Append('"');
            }
            return sb.ToString();
        }

        // For the harness: press the dock's socket `i` as a click on it does -
        // refused when its act is not live
        internal static string UnitActPress(int i)
        {
            if (_unit == null || !_unit.activeSelf || i < 0 || i >= ActRow.Sockets)
            {
                return "unit: hidden";
            }
            ActRow.Slot s = _unitRow.Get(i);
            if (s == null || s.Press == null)
            {
                return "empty";
            }
            if (!s.Live)
            {
                return s.Name + ": not live";
            }
            s.Press();
            return "pressed " + s.Name;
        }

        // For the harness: the other two ways onto one named person of yours - a
        // Tab pick and a roster pick (ClickPersonNamed is the world click)
        internal static string TabPickNamed(string name)
        {
            CommonStates who = FriendNamed(name);
            if (who == null) { return "nobody named " + name; }
            TargetFromTab(who);
            return TargetName();
        }

        internal static string RosterPickNamed(string name)
        {
            CommonStates who = FriendNamed(name);
            if (who == null) { return "nobody named " + name; }
            PressRosterRow(who);
            return TargetName();
        }

        private static CommonStates FriendNamed(string name)
        {
            NPCManager npcs = _mn != null ? _mn.npcMN : null;
            for (int i = 0; npcs != null && npcs.friendList != null && i < npcs.friendList.Count; i++)
            {
                if (npcs.friendList[i] != null && npcs.friendList[i].charaName == name) { return npcs.friendList[i]; }
            }
            return null;
        }

        // For the harness: open or close More, as its socket would
        internal static bool MoreActsOpen(bool open)
        {
            if (_moreActsOpen != open) { ToggleMoreActs(); }
            return _moreActsOpen && _moreActsPanel != null && _moreActsPanel.gameObject.activeSelf;
        }

        // ...and press its row by number, as a click on it would
        internal static string MoreActPress(int row)
        {
            if (row < 0 || row >= MoreActs.Count || row >= MoreButtons.Count)
            {
                return "no row " + row;
            }
            string label = MoreActs[row].Label;
            MoreButtons[row].onClick.Invoke();
            return "pressed " + label;
        }

        // HARNESS ONLY, INERT UNTIL CALLED (Forgejo #11): stands in for a mod
        // such as UnlockSS, which adds its own buttons to vanilla's Talk sub-panel
        // without registering them in npcPanelState. Two, one of each kind a mod
        // makes - a clone of vanilla's Sex button with its own TMP words, and a
        // bare Button with a legacy Text - each logging and counting its press.
        // "add", "remove", or anything else to read the press count. Found by
        // name rather than kept in a list, since a hot reload forgets a list.
        private static readonly string[] FakeNames = { "Sex(Clone)", "FakeModButton" };
        private static int _fakePresses;

        internal static string FakeForeignActs(string mode)
        {
            GameObject panel = _mn != null && _mn.uiMN != null ? _mn.uiMN.talkPanel : null;
            if (mode != "add" && mode != "remove")
            {
                return "presses " + _fakePresses;
            }
            if (panel == null)
            {
                return "no talkPanel";
            }
            int found = 0;
            for (int i = panel.transform.childCount - 1; i >= 0; i--)
            {
                GameObject child = panel.transform.GetChild(i).gameObject;
                if (System.Array.IndexOf(FakeNames, child.name) < 0) continue;
                found++;
                if (mode == "remove") Object.DestroyImmediate(child);
            }
            if (mode == "remove" || found > 0)
            {
                return mode == "remove" ? "removed " + found : "already added";
            }
            Transform sex = panel.transform.Find("Sex");
            if (sex != null)
            {
                GameObject clone = Object.Instantiate(sex.gameObject, panel.transform);
                clone.name = "Sex(Clone)";
                // its localiser would put "Sex" back on enable (not referenced, so by name)
                foreach (MonoBehaviour b in clone.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (b.GetType().Name == "LocalizeStringEvent") Object.DestroyImmediate(b);
                }
                clone.GetComponentInChildren<TextMeshProUGUI>(true).text = "[SS] test one";
                AddFakePress(clone, "[SS] test one");
            }
            var bare = new GameObject("FakeModButton", typeof(RectTransform), typeof(Image), typeof(Button));
            bare.transform.SetParent(panel.transform, false);
            var words = new GameObject("Text", typeof(RectTransform), typeof(Text));
            words.transform.SetParent(bare.transform, false);
            Text legacy = words.GetComponent<Text>();
            legacy.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            legacy.text = "[SS] test two";
            AddFakePress(bare, "[SS] test two");
            return "added";
        }

        private static void AddFakePress(GameObject go, string what)
        {
            go.SetActive(true);
            var button = go.GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent();   // a clone's vanilla wiring goes, as a mod's would
            button.onClick.AddListener(() => { _fakePresses++; Plugin.Log.LogInfo("fake foreign act pressed: " + what); });
        }
    }
}
