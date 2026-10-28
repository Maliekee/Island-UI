using System;
using System.Collections.Generic;
using TMPro;

namespace IslandUI.UI
{
    // WHEN THE LANGUAGE CHANGES, THE OPEN WINDOWS FOLLOW (Forgejo #19). The game's own
    // dropdown, the override or the harness moves Loc.Generation; the frame it moves,
    // every window re-renders in the new language without being reopened.
    //
    // Most words are set at render and come back with it. A word set once, when its
    // window was built (a caption under a tile, a field's placeholder), is registered
    // here with the words it says, and lettered again.
    internal static class LocLabels
    {
        private struct Label
        {
            public TMP_Text Text;
            public Func<string> Words;
            public float Fit;      // > 0: the size it is drawn at, shrunk to its box in a longer language
        }

        private static readonly List<Label> Labels = new List<Label>();
        private static int _generation = -1;

        internal static void Set(TMP_Text text, Func<string> words)
        {
            Set(text, words, 0f);
        }

        // ...and fitted to its box (Theme.FitLanguage) each time it is lettered: a label
        // built in English and switched later is fitted in the new language, not left at
        // the English's decision
        internal static void Set(TMP_Text text, Func<string> words, float fit)
        {
            if (text == null || words == null)
            {
                return;
            }
            text.text = words();
            if (fit > 0f)
            {
                Theme.FitLanguage(text, fit);
            }
            Labels.Add(new Label { Text = text, Words = words, Fit = fit });
        }

        // every frame from Plugin.Update
        internal static void Tick()
        {
            int generation = Shared.Loc.Generation;
            if (generation == _generation)
            {
                return;
            }
            bool first = _generation < 0;
            _generation = generation;
            if (first)
            {
                return;
            }
            for (int i = Labels.Count - 1; i >= 0; i--)
            {
                // a label whose window was destroyed (a scene load) drops out
                if (Labels[i].Text == null)
                {
                    Labels.RemoveAt(i);
                    continue;
                }
                Labels[i].Text.text = Labels[i].Words();
                if (Labels[i].Fit > 0f)
                {
                    Theme.FitLanguage(Labels[i].Text, Labels[i].Fit);
                }
            }
            Changed();
        }

        private static void Changed()
        {
            Try(InventoryWindow.LanguageChanged);
            Try(ItemCard.LanguageChanged);
            Try(BrowserSearch.LanguageChanged);
            Try(SettingsWindow.LanguageChanged);
        }

        private static void Try(Action step)
        {
            try
            {
                step();
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Language change: " + step.Method.Name + " threw " + e.Message);
            }
        }
    }
}
