using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI
{
    // Feature: visible section headers in the innate/aggregated crafting menu, grouped
    // by the [Crafting] GroupRecipesBy dimension (SourceBench or ItemType).
    //
    // Mechanism: the grid is a FixedColumnCount GridLayoutGroup laying out ACTIVE
    // children in sibling order. After CraftOpen fills the buttons, we insert pooled
    // filler cells to pad each group to a full row, then a header row (one cell whose
    // child label overflows across the full grid width). Inactive recipe buttons
    // (locked recipes) occupy no cells and are skipped. The grid's ContentSizeFitter
    // and scrollbar adapt to the added rows automatically.
    [HarmonyPatch(typeof(InventoryManager))]
    internal static class Sections
    {
        private static readonly AccessTools.FieldRef<InventoryManager, ManagersScript> MnRef =
            AccessTools.FieldRefAccess<InventoryManager, ManagersScript>("mn");

        private static readonly List<GameObject> Pool = new List<GameObject>();
        private static readonly Dictionary<int, string> PanelLabelCache = new Dictionary<int, string>();
        private static int _poolUsed;
        private static TMP_FontAsset _font;
        private static float _fontSize;

        [HarmonyPatch(nameof(InventoryManager.CraftOpen))]
        [HarmonyPostfix]
        private static void CraftOpen_Postfix(InventoryManager __instance, int id)
        {
            ManagersScript mn0 = MnRef(__instance);
            if (CraftLazyFill.ShouldSkip(mn0, id))
            {
                return; // hidden open: nothing was rebuilt, keep existing inserts
            }
            if (CraftLazyFill.UnchangedOpen)
            {
                return; // identical list: existing headers/fillers are already right
            }
            if (BrowserShell.HandledThisOpen)
            {
                return; // custom view renders its own section headers
            }
            ResetInserts();
            if (id != 0 || Settings.GroupRecipesBy.Value == Settings.RecipeGrouping.None)
            {
                return;
            }
            ManagersScript mn = mn0;
            GridLayoutGroup grid = CraftPanelLayout.FindGrid(mn);
            if (grid == null)
            {
                return;
            }
            int columns = Mathf.Max(1, grid.constraintCount);
            CraftInfo[] recipes = mn.craftMN.craftData[0].craftInfo;

            int cell = 0;
            string currentGroup = null;
            for (int i = 0; i < recipes.Length && 100 + i < __instance.itemSlot.Length; i++)
            {
                ItemSlot button = __instance.itemSlot[100 + i];
                if (recipes[i] == null || !button.gameObject.activeSelf)
                {
                    continue;
                }
                string group = GroupLabel(mn, recipes[i]);
                if (group != currentGroup)
                {
                    currentGroup = group;
                    int insertAt = button.transform.GetSiblingIndex();
                    while (cell % columns != 0)
                    {
                        InsertCell(grid, insertAt, null);
                        insertAt++;
                        cell++;
                    }
                    InsertCell(grid, insertAt, group);
                    insertAt++;
                    cell++;
                    while (cell % columns != 0)
                    {
                        InsertCell(grid, insertAt, null);
                        insertAt++;
                        cell++;
                    }
                }
                cell++;
            }
        }

        // third-round diagnostic: sample the LIVE layout state once per second while
        // the menu is actually open and laid out (the open-time dump ran during game
        // load with the panel inactive — positions were prefab defaults)
        private static float _nextSample;

        internal static void SampleLiveLayout(ManagersScript mn)
        {
            if (mn == null || mn.uiMN == null || mn.uiMN.craftPanel == null
                || !mn.uiMN.craftPanel.activeSelf || Time.unscaledTime < _nextSample)
            {
                return;
            }
            _nextSample = Time.unscaledTime + 1f;
            GridLayoutGroup grid = CraftPanelLayout.FindGrid(mn);
            if (grid == null)
            {
                return;
            }
            Transform mask = mn.uiMN.craftPanel.transform.Find("mask");
            RectTransform panelRect = mn.uiMN.craftPanel.GetComponent<RectTransform>();
            Plugin.Log.LogInfo($"LiveLayout: constraint={grid.constraint}/{grid.constraintCount}, startAxis={grid.startAxis}, " +
                $"enabled={grid.enabled}, gridRect={grid.GetComponent<RectTransform>().rect.size}, " +
                $"maskRect={(mask != null ? mask.GetComponent<RectTransform>().rect.size.ToString() : "?")}, " +
                $"panelRect={panelRect.rect.size}");
            InventoryManager inv = GameObject.Find("InventoryManager")?.GetComponent<InventoryManager>();
            if (inv == null)
            {
                return;
            }
            int logged = 0;
            for (int i = 0; i < 40 && logged < 8 && 100 + i < inv.itemSlot.Length; i++)
            {
                if (!inv.itemSlot[100 + i].gameObject.activeSelf)
                {
                    continue;
                }
                RectTransform bt = inv.itemSlot[100 + i].GetComponent<RectTransform>();
                Plugin.Log.LogInfo($"LiveLayout: slot_{100 + i} pos={bt.anchoredPosition}");
                logged++;
            }
        }

        internal static string GroupLabel(ManagersScript mn, CraftInfo recipe)
        {
            if (Settings.GroupRecipesBy.Value == Settings.RecipeGrouping.ItemType)
            {
                ItemData item = mn.itemMN.FindItem(recipe.name);
                return item != null ? item.itemType.ToString() : "Other";
            }
            // SourceBench
            if (!AggregatedCrafting.SourceOf.TryGetValue(recipe, out int panelID))
            {
                panelID = 0;
            }
            if (PanelLabelCache.TryGetValue(panelID, out string cached))
            {
                return cached;
            }
            string label = BrowserSearch.HandLabel;   // was "Basic"; the search answers to both
            if (panelID != 0)
            {
                if (AggregatedCrafting.NpcOfPanel.TryGetValue(panelID, out int npcID))
                {
                    // NPC crafter section carries the character's name
                    StatsData stats = mn.npcMN != null ? mn.npcMN.GetStatsData(npcID) : null;
                    label = stats != null ? stats.GetLocalizedCharaName() : $"NPC {npcID}";
                }
                else
                {
                    string benchKey = AggregatedCrafting.BenchKeyOf(panelID);
                    ItemData bench = benchKey != null ? mn.itemMN.FindItem(benchKey) : null;
                    label = bench != null ? bench.GetItemName() : $"Bench {panelID}";
                }
            }
            PanelLabelCache[panelID] = label;
            return label;
        }

        // ---------- pooled filler/header cells ----------

        private static void ResetInserts()
        {
            // loading another save reloads the scene and destroys pooled cells (they are
            // parented to the old grid) — drop dead entries or every CraftOpen throws
            Pool.RemoveAll(cell => cell == null);
            for (int i = 0; i < Pool.Count; i++)
            {
                Pool[i].SetActive(false);
                Pool[i].transform.SetAsLastSibling();
            }
            _poolUsed = 0;
            PanelLabelCache.Clear();
        }

        private static void InsertCell(GridLayoutGroup grid, int siblingIndex, string headerText)
        {
            GameObject cell;
            if (_poolUsed < Pool.Count)
            {
                cell = Pool[_poolUsed];
            }
            else
            {
                cell = Owned.New("CraftSectionCell");
                cell.transform.SetParent(grid.transform, false);
                GameObject labelGo = Owned.New("Label");
                labelGo.transform.SetParent(cell.transform, false);
                TextMeshProUGUI text = labelGo.AddComponent<TextMeshProUGUI>();
                text.raycastTarget = false;
                Pool.Add(cell);
            }
            _poolUsed++;

            TextMeshProUGUI label = cell.GetComponentInChildren<TextMeshProUGUI>(true);
            if (headerText != null)
            {
                EnsureFont(grid);
                float width = grid.constraintCount * (grid.cellSize.x + grid.spacing.x) - grid.spacing.x;
                RectTransform labelRect = label.GetComponent<RectTransform>();
                labelRect.anchorMin = new Vector2(0f, 0f);
                labelRect.anchorMax = new Vector2(0f, 1f);
                labelRect.pivot = new Vector2(0f, 0.5f);
                labelRect.anchoredPosition = Vector2.zero;
                labelRect.sizeDelta = new Vector2(width, 0f);
                if (_font != null)
                {
                    label.font = _font;
                }
                label.fontSize = _fontSize;
                // This is the VANILLA grid path — a bench panel, or CustomGrid
                // switched off — and it sits on the same craftPanel, so it inherits
                // the wood field (PanelFill is a child of the panel, not of our
                // root, and stays on when our grid stands down). A bench is the most
                // literally making-surface there is, so that is D46 working rather
                // than leaking. The label follows the surface for the same reason;
                // the hardcoded gold here was the rule's only exception on the day
                // the rule was written.
                label.color = UI.Theme.CraftSurface.Text;
                label.alignment = TextAlignmentOptions.MidlineLeft;
                label.enableWordWrapping = false;
                label.text = headerText;
                label.gameObject.SetActive(true);
            }
            else
            {
                label.gameObject.SetActive(false);
            }
            cell.SetActive(true);
            cell.transform.SetSiblingIndex(siblingIndex);
        }

        private static void EnsureFont(GridLayoutGroup grid)
        {
            if (_font != null)
            {
                return;
            }
            TextMeshProUGUI reference = grid.GetComponentInParent<Canvas>()?.GetComponentInChildren<TextMeshProUGUI>();
            if (reference != null)
            {
                _font = reference.font;
                _fontSize = 10f;
            }
        }
    }
}
