using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Text;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI.UI
{
    // THE DROP-DOWN CONSOLE, the Counter-Strike way: the key left of 1 lowers a sheet over the top half of the
    // screen - a scrolling log, a prompt with history and completion - and every
    // line typed into it goes to vanilla's own DebugTool.DebugCodeEnter, which
    // splits it and runs its own switch. Nothing about a command is ours: the list
    // the prompt completes from is read out of that switch at runtime, so a game
    // update brings its new commands with it.
    //
    // WHILE IT IS DOWN IT OWNS THE KEYBOARD: an open console absorbs every key
    // press. It joins FilterInput's capture: every
    // Input.GetKey* read in the process - vanilla's and ours - answers
    // "not pressed", PlayerMove.NowInput says typing (which stops the movement
    // axes), and vanilla's hotkey guard sees nameInput. Typing reaches the field
    // through the event queue, which none of that touches. The console's own keys
    // - Escape, the toggle, the arrows, Tab and the page keys - read past it.
    //
    // What a command says back is caught where the game says it: its log lines
    // (UIManager.GoLogText), its centre cautions (EventManager.GoCaution/St), and
    // Unity's log for a moment after each line. A thrown exception - a missing
    // argument, a word where a number goes - is caught and said in words; vanilla
    // has no default case, so an unknown command is ours to report.
    [HarmonyPatch]
    internal static class DevConsole
    {
        private const float Pad = 6f;
        private const float PromptH = 13f;
        private const float HintH = 10f;
        private const float LogFont = 7f;
        private const float SlideSeconds = 0.12f;
        private const int MaxLines = 500;
        private const int MaxHistory = 200;

        // The game blends in LINEAR space, where a dark sheet at 0.94 still shows
        // the world at about a quarter of its brightness; 0.99 reads as the
        // faint see-through a console wants.
        private static readonly Color Back = new Color(0.085f, 0.06f, 0.045f, 0.99f);
        private static readonly Color Well = new Color(0f, 0f, 0f, 0.35f);
        private const string EchoHex = "#E8B86A";
        private const string ErrorHex = "#FF6B5A";
        private const string DimHex = "#A8977D";
        private const string CautionHex = "#F0C674";

        private static ManagersScript _mn;
        private static GameObject _root;
        private static RectTransform _sheet;
        private static RectTransform _viewport;
        private static TextMeshProUGUI _log;
        private static TMP_InputField _field;
        private static TextMeshProUGUI _hint;
        private static bool _open;
        private static float _slide;
        private static int _grace;
        private static float _scroll;
        private static bool _logDirty;
        private static int _caretToEnd;
        private static bool _settingText;
        private static string _tabBase;
        private static int _tabIndex;
        private static int _historyAt = -1;
        private static string _draft = "";
        private static float _captureUntil;
        private static bool _hooked;
        private static bool _historyLoaded;
        private static string[] _commands;

        private static readonly List<string> Lines = new List<string>();
        private static readonly List<string> History = new List<string>();

        internal static bool IsOpen => _open;

        // Open, or closing and still inside the grace frames: the key that closed
        // it must not reach vanilla on the frame after (Escape would pause).
        internal static bool Swallowing => _open || _grace > 0;

        // ---------- the toggle: the physical key left of 1 ----------

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll")]
        private static extern uint MapVirtualKey(uint uCode, uint uMapType);

        // Scan code 0x29 is the key under Escape on every layout - ½ on a Danish
        // board, ` on a US one, ^ on a German one. Unity names it after what the
        // layout's virtual key means in US terms, so no KeyCode is right for
        // everyone; the scan code is.
        private const uint ScanLeftOfOne = 0x29;
        private const uint MapScanToVk = 1;
        private static bool _toggleWasDown;
        private static bool _nativeFailed;

        private static int ToggleVk()
        {
            if (_nativeFailed)
            {
                return 0;
            }
            try { return (int)MapVirtualKey(ScanLeftOfOne, MapScanToVk); }
            catch (Exception) { _nativeFailed = true; return 0; }
        }

        private static bool ToggleHeld()
        {
            KeyCode key = Settings.ConsoleKey.Value;
            if (key != KeyCode.None)
            {
                return FilterInput.RawKey(key);
            }
            int vk = ToggleVk();
            if (vk == 0)
            {
                return FilterInput.RawKey(KeyCode.BackQuote);
            }
            try { return (GetAsyncKeyState(vk) & 0x8000) != 0; }
            catch (Exception) { _nativeFailed = true; return false; }
        }

        private static bool TogglePressed()
        {
            bool down = ToggleHeld() && Application.isFocused;
            bool pressed = down && !_toggleWasDown;
            _toggleWasDown = down;
            return pressed;
        }

        // ---------- the tick ----------

        internal static void Tick(ManagersScript mn)
        {
            _mn = mn;
            if (!_open && _grace > 0)
            {
                _grace--;
            }
            bool ready = mn != null && mn.allSet && mn.uiMN != null;
            bool toggle = TogglePressed();
            if (!_open)
            {
                if (toggle && ready && !SomeoneElseTyping(mn))
                {
                    Open(mn);
                }
                Animate();
                return;
            }
            if (!ready || _root == null)
            {
                Close();
                return;
            }
            if (toggle || FilterInput.RawKeyDown(KeyCode.Escape))
            {
                Close();
                Animate();
                return;
            }
            ReadKeys();
            if (_field != null && !_field.isFocused)
            {
                _field.ActivateInputField();
                _field.Select();
            }
            if (_caretToEnd > 0 && _field != null)
            {
                _field.MoveTextEnd(false);
                _caretToEnd--;
            }
            Animate();
            if (_logDirty)
            {
                RebuildLog();
            }
            PlaceLog();
        }

        // A vanilla text entry or one of our filter fields has the keyboard: the
        // key left of 1 is a character there, not a door.
        private static bool SomeoneElseTyping(ManagersScript mn)
        {
            UIManager ui = mn.uiMN;
            if (FilterInput.AnyFocused)
            {
                return true;
            }
            if (ui.mainInputField != null && ui.mainInputField.gameObject.activeSelf)
            {
                return true;
            }
            if (ui.talkEditPanel != null && ui.talkEditPanel.activeSelf)
            {
                return true;
            }
            // nameInput is also OUR capture flag (FilterInput), so it only counts
            // as vanilla's rename while nothing of ours is holding it
            return ui.nameInput != null && ui.nameInput.enabled && !Swallowing;
        }

        private static void Open(ManagersScript mn)
        {
            if (!Build(mn))
            {
                return;
            }
            _open = true;
            _grace = 0;
            _historyAt = -1;
            _scroll = 0f;
            if (!_root.activeSelf)
            {
                _root.SetActive(true);
            }
            _logDirty = true;
            UpdateHint();
            _field.ActivateInputField();
            _field.Select();
        }

        private static void Close()
        {
            if (!_open)
            {
                return;
            }
            _open = false;
            _grace = 3;
            if (_field != null)
            {
                if (_field.isFocused)
                {
                    _field.DeactivateInputField();
                }
                var es = UnityEngine.EventSystems.EventSystem.current;
                if (es != null && es.currentSelectedGameObject == _field.gameObject)
                {
                    es.SetSelectedGameObject(null);
                }
            }
        }

        private static void Animate()
        {
            if (_root == null)
            {
                return;
            }
            float target = _open ? 1f : 0f;
            if (!Mathf.Approximately(_slide, target))
            {
                _slide = Mathf.MoveTowards(_slide, target, Time.unscaledDeltaTime / SlideSeconds);
            }
            if (_sheet != null)
            {
                float e = _slide * _slide * (3f - 2f * _slide);
                float h = _sheet.rect.height;
                _sheet.anchoredPosition = new Vector2(0f, (1f - e) * (h + 2f));
            }
            if (!_open && _slide <= 0f && _root.activeSelf)
            {
                _root.SetActive(false);
            }
        }

        // ---------- the console's own keys ----------

        private static void ReadKeys()
        {
            if (FilterInput.RawKeyDown(KeyCode.UpArrow))
            {
                if (History.Count > 0)
                {
                    if (_historyAt < 0)
                    {
                        _draft = _field.text;
                        _historyAt = History.Count - 1;
                    }
                    else
                    {
                        _historyAt = Mathf.Max(0, _historyAt - 1);
                    }
                    SetText(History[_historyAt]);
                }
            }
            else if (FilterInput.RawKeyDown(KeyCode.DownArrow))
            {
                if (_historyAt >= 0)
                {
                    _historyAt++;
                    if (_historyAt >= History.Count)
                    {
                        _historyAt = -1;
                        SetText(_draft);
                    }
                    else
                    {
                        SetText(History[_historyAt]);
                    }
                }
            }
            if (FilterInput.RawKeyDown(KeyCode.Tab))
            {
                Complete();
            }
            float page = Mathf.Max(LogFont * 1.2f, (_viewport != null ? _viewport.rect.height : 100f) * 0.8f);
            if (FilterInput.RawKeyDown(KeyCode.PageUp))
            {
                _scroll += page;
            }
            if (FilterInput.RawKeyDown(KeyCode.PageDown))
            {
                _scroll -= page;
            }
            float wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) > 0.01f && _sheet != null
                && RectTransformUtility.RectangleContainsScreenPoint(_sheet, Input.mousePosition, null))
            {
                _scroll += wheel * LogFont * 3.6f;
            }
        }

        private static void SetText(string text)
        {
            _settingText = true;
            try { _field.text = text ?? ""; }
            finally { _settingText = false; }
            _caretToEnd = 2;   // TMP moves the caret for the arrow a frame after us
            UpdateHint();
        }

        private static void OnChanged(string value)
        {
            if (!_settingText)
            {
                _tabBase = null;
                _historyAt = -1;
            }
            UpdateHint();
        }

        // THE KEY THAT OPENS IT IS NOT A CHARACTER, nor are Tab and Enter: TMP
        // would type ½ into the prompt on the frame it drops, and a tab or a
        // newline into a one-line command.
        private static char Validate(string text, int pos, char ch)
        {
            if (ch == '\t' || ch == '\n' || ch == '\r' || ToggleHeld())
            {
                return '\0';
            }
            return ch;
        }

        private static void OnSubmit(string text)
        {
            Run(text);
            SetText("");
            _historyAt = -1;
            _draft = "";
            _scroll = 0f;
        }

        // ---------- commands ----------

        // What vanilla's switch answers to, read out of DebugCodeEnter's own IL:
        // every case label is a string it loads. Two of the loads are whole lines
        // ("/wp takumi2") and some repeat a label; a set without spaces is the list.
        private static string[] Commands()
        {
            if (_commands != null)
            {
                return _commands;
            }
            var set = new SortedSet<string>(StringComparer.Ordinal);
            try
            {
                var method = AccessTools.Method(typeof(DebugTool), nameof(DebugTool.DebugCodeEnter));
                foreach (CodeInstruction ci in PatchProcessor.GetOriginalInstructions(method))
                {
                    if (ci.opcode == OpCodes.Ldstr && ci.operand is string s
                        && s.Length > 1 && s[0] == '/' && s.IndexOf(' ') < 0)
                    {
                        set.Add(s);
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Console: reading DebugCodeEnter's commands failed: " + e.Message);
            }
            _commands = new string[set.Count];
            set.CopyTo(_commands);
            Plugin.Log.LogInfo("Console: " + _commands.Length + " commands read from DebugTool.DebugCodeEnter");
            return _commands;
        }

        private static List<string> Matching(string prefix)
        {
            var hits = new List<string>();
            string[] all = Commands();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    hits.Add(all[i]);
                }
            }
            return hits;
        }

        // Tab: one match completes it and a space; several complete to what they
        // share, then each further Tab steps through them.
        private static void Complete()
        {
            string text = _field.text ?? "";
            if (text.IndexOf(' ') >= 0)
            {
                return;   // only the command word completes; arguments are free
            }
            if (_tabBase == null)
            {
                _tabBase = text.Length == 0 || text[0] == '/' ? text : "/" + text;
                _tabIndex = -1;
                List<string> first = Matching(_tabBase);
                if (first.Count == 1)
                {
                    SetText(first[0] + " ");
                    _tabBase = null;
                    return;
                }
                string shared = SharedPrefix(first);
                if (shared.Length > _tabBase.Length)
                {
                    SetText(shared);
                    _tabBase = shared;
                    return;
                }
            }
            List<string> hits = Matching(_tabBase);
            if (hits.Count == 0)
            {
                return;
            }
            _tabIndex = (_tabIndex + 1) % hits.Count;
            SetText(hits[_tabIndex]);
        }

        private static string SharedPrefix(List<string> words)
        {
            if (words.Count == 0)
            {
                return "";
            }
            string p = words[0];
            for (int i = 1; i < words.Count && p.Length > 0; i++)
            {
                int n = 0;
                while (n < p.Length && n < words[i].Length
                    && char.ToLowerInvariant(p[n]) == char.ToLowerInvariant(words[i][n]))
                {
                    n++;
                }
                p = p.Substring(0, n);
            }
            return p;
        }

        // The line under the prompt: what the word being typed could become.
        private static void UpdateHint()
        {
            if (_hint == null)
            {
                return;
            }
            if (_field != null && _field.placeholder is TMP_Text ph)
            {
                ph.text = Shared.Loc.Get("console.placeholder", "type a command");
            }
            string text = _field != null ? _field.text ?? "" : "";
            if (text.Trim().Length == 0)
            {
                // the keys and the command are code, the words around them the language's
                _hint.text = string.Join(" " + Shared.Loc.Separator + " ",
                    Shared.Loc.Get("console.hint.complete", "{0} completes", "Tab"),
                    Shared.Loc.Get("console.hint.history", "{0} history", "↑ ↓"),
                    Shared.Loc.Get("console.hint.scroll", "{0} scroll", "PgUp PgDn"),
                    Shared.Loc.Get("console.hint.close", "{0} closes", "Esc"),
                    Shared.Loc.Get("console.hint.help", "{0} lists every command", "help"));
                return;
            }
            string head = text.TrimStart();
            int sp = head.IndexOf(' ');
            if (sp >= 0)
            {
                string cmd = head.Substring(0, sp);
                if (cmd.Length > 0 && cmd[0] != '/')
                {
                    cmd = "/" + cmd;
                }
                _hint.text = Array.IndexOf(Commands(), cmd) >= 0 || IsOwn(cmd.TrimStart('/'))
                    ? cmd : "<color=" + ErrorHex + ">" + Shared.Loc.Get("console.unknown", "unknown command {0}", Escape(cmd)) + "</color>";
                return;
            }
            if (IsOwn(head))
            {
                _hint.text = head;
                return;
            }
            List<string> hits = Matching(head[0] == '/' ? head : "/" + head);
            if (hits.Count == 0)
            {
                _hint.text = "<color=" + ErrorHex + ">" + Shared.Loc.Get("console.no_match", "no command starts with {0}", Escape(head))
                    + "</color>";
                return;
            }
            var sb = new StringBuilder();
            int shown = Mathf.Min(hits.Count, 12);
            for (int i = 0; i < shown; i++)
            {
                if (i > 0)
                {
                    sb.Append("   ");
                }
                sb.Append(hits[i]);
            }
            if (hits.Count > shown)
            {
                sb.Append("   (+").Append(hits.Count - shown).Append(')');
            }
            _hint.text = sb.ToString();
        }

        private static bool IsOwn(string word)
        {
            return word == "help" || word == "clear" || word == "cls";
        }

        // One line: collapsed to single spaces, since vanilla splits on each space
        // and a double one would shift every argument after it.
        internal static void Run(string raw)
        {
            string[] parts = (raw ?? "").Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return;
            }
            string line = string.Join(" ", parts);
            Print("<color=" + EchoHex + ">> " + Escape(line) + "</color>");
            Remember(line);
            string head = parts[0];
            if (IsOwn(head))
            {
                if (head == "help")
                {
                    PrintCommands();
                }
                else
                {
                    Lines.Clear();
                    _logDirty = true;
                }
                return;
            }
            if (head[0] != '/')
            {
                head = "/" + head;
                parts[0] = head;
                line = string.Join(" ", parts);
            }
            string[] known = Commands();
            if (known.Length > 0 && Array.IndexOf(known, head) < 0)
            {
                Print("<color=" + ErrorHex + ">" + Shared.Loc.Get("console.unknown", "unknown command {0}", Escape(head)) + "</color>  "
                    + Shared.Loc.Get("console.unknown.help", "({0} lists them)", "help"));
                return;
            }
            DebugTool tool = Tool();
            if (tool == null)
            {
                Print("<color=" + ErrorHex + ">" + Shared.Loc.Get("console.no_handler", "the game's command handler is not in this scene")
                    + "</color>");
                return;
            }
            _captureUntil = Time.unscaledTime + 1.5f;
            try
            {
                tool.DebugCodeEnter(line);
            }
            catch (IndexOutOfRangeException)
            {
                Print("<color=" + ErrorHex + ">" + Shared.Loc.Get("console.missing_argument", "{0}: an argument is missing", Escape(head))
                    + "</color>");
            }
            catch (FormatException)
            {
                Print("<color=" + ErrorHex + ">" + Shared.Loc.Get("console.number_argument", "{0}: an argument should be a number", Escape(head))
                    + "</color>");
            }
            catch (Exception e)
            {
                Print("<color=" + ErrorHex + ">" + Escape(head) + ": " + e.GetType().Name + " - " + Escape(e.Message) + "</color>");
                Plugin.Log.LogWarning("Console: " + line + " threw " + e);
            }
        }

        private static void PrintCommands()
        {
            string[] all = Commands();
            var sb = new StringBuilder();
            sb.Append("<color=").Append(DimHex).Append('>')
                .Append(Shared.Loc.Plural("console.commands", "{0} command, from the game's own list (arguments are the game's):",
                    "{0} commands, from the game's own list (arguments are the game's):", all.Length))
                .Append("</color>");
            Print(sb.ToString());
            sb.Length = 0;
            // columns by position, not by padding: the face is proportional
            const int perLine = 7;
            for (int i = 0; i < all.Length; i++)
            {
                int col = i % perLine;
                sb.Append("<pos=").Append((col * 100f / perLine).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture))
                    .Append("%>").Append(all[i]);
                if (col == perLine - 1)
                {
                    Print(sb.ToString());
                    sb.Length = 0;
                }
            }
            if (sb.Length > 0)
            {
                Print(sb.ToString());
            }
            Print("<color=" + DimHex + ">" + Shared.Loc.Get("console.own", "and the console's own: {0}", "help, clear") + "</color>");
        }

        private static DebugTool _tool;

        private static DebugTool Tool()
        {
            if (_tool == null)
            {
                GameObject go = GameObject.Find("DebugTool");
                _tool = go != null ? go.GetComponent<DebugTool>() : UnityEngine.Object.FindObjectOfType<DebugTool>();
            }
            return _tool;
        }

        // ---------- history, kept across sessions ----------

        private static string HistoryPath =>
            Path.Combine(BepInEx.Paths.ConfigPath, "IslandUI.console-history.txt");

        private static void LoadHistory()
        {
            if (_historyLoaded)
            {
                return;
            }
            _historyLoaded = true;
            try
            {
                if (!File.Exists(HistoryPath))
                {
                    return;
                }
                string[] lines = File.ReadAllLines(HistoryPath);
                int from = Mathf.Max(0, lines.Length - MaxHistory);
                for (int i = from; i < lines.Length; i++)
                {
                    if (lines[i].Trim().Length > 0)
                    {
                        History.Add(lines[i].Trim());
                    }
                }
                if (lines.Length > MaxHistory * 2)
                {
                    File.WriteAllLines(HistoryPath, History.ToArray());
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Console: history not read: " + e.Message);
            }
        }

        private static void Remember(string line)
        {
            if (History.Count > 0 && History[History.Count - 1] == line)
            {
                return;
            }
            History.Add(line);
            if (History.Count > MaxHistory)
            {
                History.RemoveAt(0);
            }
            try { File.AppendAllText(HistoryPath, line + Environment.NewLine); }
            catch (Exception) { }
        }

        // ---------- the log ----------

        internal static void Print(string richLine)
        {
            Lines.Add(richLine ?? "");
            if (Lines.Count > MaxLines)
            {
                Lines.RemoveRange(0, Lines.Count - MaxLines);
            }
            _logDirty = true;
        }

        // TMP reads no HTML entities; a run it must not parse is <noparse>
        private static string Escape(string s)
        {
            return string.IsNullOrEmpty(s) ? "" : "<noparse>" + s.Replace("</noparse>", "") + "</noparse>";
        }

        private static void RebuildLog()
        {
            _logDirty = false;
            if (_log == null)
            {
                return;
            }
            var sb = new StringBuilder();
            for (int i = 0; i < Lines.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append('\n');
                }
                sb.Append(Lines[i]);
            }
            _log.text = sb.ToString();
            _log.ForceMeshUpdate();
        }

        // The text grows up from the viewport's floor; scrolling slides it down to
        // show what is above. At the floor it follows new lines.
        private static void PlaceLog()
        {
            if (_log == null || _viewport == null)
            {
                return;
            }
            float viewH = _viewport.rect.height;
            float textH = Mathf.Max(viewH, _log.preferredHeight + 2f);
            _scroll = Mathf.Clamp(_scroll, 0f, Mathf.Max(0f, textH - viewH));
            RectTransform r = _log.rectTransform;
            r.sizeDelta = new Vector2(0f, textH);
            r.anchoredPosition = new Vector2(0f, -_scroll);
        }

        private static void OnUnityLog(string condition, string stack, LogType type)
        {
            bool fresh = Time.unscaledTime <= _captureUntil;
            if (!fresh && type != LogType.Exception)
            {
                return;
            }
            string hex = type == LogType.Error || type == LogType.Exception || type == LogType.Assert ? ErrorHex
                : type == LogType.Warning ? CautionHex : DimHex;
            Print("<color=" + hex + ">" + Escape(condition) + "</color>");
        }

        // the game's own log line, the one it prints at the top left
        [HarmonyPatch(typeof(UIManager), nameof(UIManager.GoLogText))]
        [HarmonyPostfix]
        private static void GoLogText_Postfix(string log)
        {
            if (!string.IsNullOrEmpty(log))
            {
                Print(log);
            }
        }

        // ...and its centre cautions ("Not enough space", "position not found")
        [HarmonyPatch(typeof(EventManager), nameof(EventManager.GoCautionSt))]
        [HarmonyPrefix]
        private static void GoCautionSt_Prefix(string message)
        {
            if (!string.IsNullOrEmpty(message))
            {
                Print("<color=" + CautionHex + ">" + Escape(message) + "</color>");
            }
        }

        [HarmonyPatch(typeof(EventManager), nameof(EventManager.GoCaution))]
        [HarmonyPrefix]
        private static void GoCaution_Prefix(int id)
        {
            string[] all = _mn != null && _mn.textMN != null ? _mn.textMN.cautions : null;
            if (all != null && id >= 0 && id < all.Length && !string.IsNullOrEmpty(all[id]))
            {
                Print("<color=" + CautionHex + ">" + Escape(all[id]) + "</color>");
            }
        }

        // ---------- building ----------

        private static bool Build(ManagersScript mn)
        {
            if (_root != null)
            {
                return true;
            }
            if (mn == null || mn.uiMN == null || mn.uiMN.mainInputField == null || mn.uiMN.itemDescPanel == null)
            {
                return false;
            }
            if (!_hooked)
            {
                Application.logMessageReceived += OnUnityLog;
                _hooked = true;
            }
            LoadHistory();
            Canvas rc = mn.uiMN.itemDescPanel.GetComponentInParent<Canvas>();
            Transform parent = rc != null ? rc.rootCanvas.transform : mn.uiMN.itemDescPanel.transform.parent;
            _root = Owned.New("IslandUI_Console");
            _root.transform.SetParent(parent, false);
            Stretch((RectTransform)_root.transform);
            // over everything the game draws, and a raycast target of its own so
            // the pointer over it is "on UI" and the world ignores the wheel
            var canvas = _root.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 30000;
            _root.AddComponent<GraphicRaycaster>();

            var sheetGo = Owned.New("Sheet");
            sheetGo.transform.SetParent(_root.transform, false);
            _sheet = (RectTransform)sheetGo.transform;
            _sheet.anchorMin = new Vector2(0f, 0.5f);
            _sheet.anchorMax = new Vector2(1f, 1f);
            _sheet.pivot = new Vector2(0.5f, 1f);
            _sheet.offsetMin = Vector2.zero;
            _sheet.offsetMax = Vector2.zero;
            var back = sheetGo.AddComponent<Image>();
            back.color = Back;
            back.raycastTarget = true;

            // the lower edge, a cream hairline, where the sheet meets the world
            var edgeGo = Owned.New("Edge");
            edgeGo.transform.SetParent(_sheet, false);
            var edge = edgeGo.AddComponent<Image>();
            edge.color = new Color(Theme.KitCream.r, Theme.KitCream.g, Theme.KitCream.b, 0.55f);
            edge.raycastTarget = false;
            var er = (RectTransform)edgeGo.transform;
            er.anchorMin = new Vector2(0f, 0f);
            er.anchorMax = new Vector2(1f, 0f);
            er.pivot = new Vector2(0.5f, 0f);
            er.offsetMin = new Vector2(0f, 0f);
            er.offsetMax = new Vector2(0f, 0.8f);

            // the log, in a masked viewport above the prompt
            var viewGo = Owned.New("Log");
            viewGo.transform.SetParent(_sheet, false);
            _viewport = (RectTransform)viewGo.transform;
            _viewport.anchorMin = Vector2.zero;
            _viewport.anchorMax = Vector2.one;
            _viewport.offsetMin = new Vector2(Pad, Pad + HintH + PromptH + 3f);
            _viewport.offsetMax = new Vector2(-Pad, -Pad);
            viewGo.AddComponent<RectMask2D>();
            _log = Theme.MakeKitText(_viewport, "Text", LogFont, Theme.KitCream, TextAlignmentOptions.BottomLeft);
            _log.richText = true;
            _log.enableWordWrapping = true;
            _log.overflowMode = TextOverflowModes.Overflow;
            RectTransform lr = _log.rectTransform;
            lr.anchorMin = new Vector2(0f, 0f);
            lr.anchorMax = new Vector2(1f, 0f);
            lr.pivot = new Vector2(0.5f, 0f);

            // the prompt: a well, the caret mark, the field
            var wellGo = Owned.New("Prompt");
            wellGo.transform.SetParent(_sheet, false);
            var well = wellGo.AddComponent<Image>();
            well.sprite = Theme.RoundedFill;
            well.type = Image.Type.Sliced;
            well.color = Well;
            well.raycastTarget = true;
            var wr = (RectTransform)wellGo.transform;
            wr.anchorMin = new Vector2(0f, 0f);
            wr.anchorMax = new Vector2(1f, 0f);
            wr.pivot = new Vector2(0.5f, 0f);
            wr.offsetMin = new Vector2(Pad, Pad + HintH);
            wr.offsetMax = new Vector2(-Pad, Pad + HintH + PromptH);
            TextMeshProUGUI mark = Theme.MakeKitText(wellGo.transform, "Mark", Theme.FontBody,
                Theme.ParseColor(EchoHex, Theme.KitCream), TextAlignmentOptions.MidlineLeft, bold: true);
            mark.text = ">";
            var mr = mark.rectTransform;
            mr.anchorMin = new Vector2(0f, 0f);
            mr.anchorMax = new Vector2(0f, 1f);
            mr.pivot = new Vector2(0f, 0.5f);
            mr.offsetMin = new Vector2(3f, 0f);
            mr.offsetMax = new Vector2(10f, 0f);
            if (!BuildField(mn, wellGo.transform))
            {
                UnityEngine.Object.Destroy(_root);
                _root = null;
                return false;
            }

            _hint = Theme.MakeKitText(_sheet, "Hint", Theme.FontCaption,
                Theme.ParseColor(DimHex, Theme.KitCream), TextAlignmentOptions.MidlineLeft);
            _hint.richText = true;
            _hint.enableWordWrapping = false;
            // Overflow, not Ellipsis: the face's line is taller than the row, and
            // an ellipsis box drops a first line that does not fit - all of it
            _hint.overflowMode = TextOverflowModes.Overflow;
            var hr = _hint.rectTransform;
            hr.anchorMin = new Vector2(0f, 0f);
            hr.anchorMax = new Vector2(1f, 0f);
            hr.pivot = new Vector2(0.5f, 0f);
            hr.offsetMin = new Vector2(Pad + 3f, Pad * 0.5f);
            hr.offsetMax = new Vector2(-Pad, Pad * 0.5f + HintH);

            _slide = 0f;
            Animate();
            if (Lines.Count == 0)
            {
                Print("<color=" + DimHex + ">" + Shared.Loc.Plural("console.welcome",
                    "The game's command line. {0} command; type {1} for the list.",
                    "The game's command line. {0} commands; type {1} for the list.", Commands().Length, "help") + "</color>");
            }
            Plugin.Log.LogInfo("Console built");
            return true;
        }

        // A clone of vanilla's dialog input, as the filter fields are made
        // (FilterField.Build carries the reasons for every line of this).
        private static bool BuildField(ManagersScript mn, Transform parent)
        {
            GameObject go = Owned.Instantiate(mn.uiMN.mainInputField.gameObject, parent);
            go.name = "Field";
            _field = go.GetComponent<TMP_InputField>();
            if (_field == null)
            {
                return false;
            }
            foreach (Behaviour behaviour in go.GetComponentsInChildren<Behaviour>(true))
            {
                if (behaviour is ILayoutController || behaviour is Animator)
                {
                    behaviour.enabled = false;
                }
            }
            foreach (Button button in go.GetComponentsInChildren<Button>(true))
            {
                UnityEngine.Object.Destroy(button.gameObject);
            }
            _field.lineType = TMP_InputField.LineType.SingleLine;
            _field.contentType = TMP_InputField.ContentType.Standard;
            _field.richText = false;
            _field.transition = Selectable.Transition.None;
            _field.onValueChanged = new TMP_InputField.OnChangeEvent();
            _field.onEndEdit = new TMP_InputField.SubmitEvent();
            _field.onSubmit = new TMP_InputField.SubmitEvent();
            _field.onSelect = new TMP_InputField.SelectionEvent();
            _field.onDeselect = new TMP_InputField.SelectionEvent();
            _field.text = "";
            _field.interactable = true;
            _field.readOnly = false;
            _field.characterLimit = 0;
            var background = go.GetComponent<Image>();
            if (background != null)
            {
                background.color = new Color(0f, 0f, 0f, 0f);
                background.raycastTarget = false;
            }
            if (_field.textComponent != null)
            {
                _field.textComponent.enableAutoSizing = false;
                _field.textComponent.fontSize = Theme.FontBody;
                _field.textComponent.alignment = TextAlignmentOptions.MidlineLeft;
                _field.textComponent.margin = Vector4.zero;
                _field.textComponent.enableWordWrapping = false;
                _field.textComponent.color = Theme.KitCream;
                Theme.FlattenType(_field.textComponent);
            }
            if (_field.placeholder is TMP_Text ph)
            {
                ph.text = Shared.Loc.Get("console.placeholder", "type a command");
                ph.enableAutoSizing = false;
                ph.fontSize = Theme.FontBody;
                ph.alignment = TextAlignmentOptions.MidlineLeft;
                ph.margin = Vector4.zero;
                ph.enableWordWrapping = false;
                ph.color = Theme.ParseColor(DimHex, Theme.KitCream);
                Theme.FlattenType(ph);
            }
            _field.customCaretColor = true;
            _field.caretColor = Theme.KitCream;
            var fr = (RectTransform)go.transform;
            fr.anchorMin = Vector2.zero;
            fr.anchorMax = Vector2.one;
            fr.offsetMin = new Vector2(11f, 0f);
            fr.offsetMax = new Vector2(-4f, 0f);
            if (_field.textViewport != null)
            {
                Stretch(_field.textViewport);
            }
            if (_field.textComponent != null)
            {
                Stretch(_field.textComponent.rectTransform);
            }
            if (_field.placeholder != null)
            {
                Stretch(_field.placeholder.rectTransform);
            }
            if (!go.activeSelf)
            {
                go.SetActive(true);
            }
            if (_field.placeholder != null && !_field.placeholder.gameObject.activeSelf)
            {
                _field.placeholder.gameObject.SetActive(true);
            }
            _field.onValidateInput = Validate;
            _field.onValueChanged.AddListener(OnChanged);
            _field.onSubmit.AddListener(OnSubmit);
            return true;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        // ---------- lifecycle ----------

        internal static void ResetForSceneLoad()
        {
            _open = false;
            _grace = 0;
            _root = null;
            _sheet = null;
            _viewport = null;
            _log = null;
            _field = null;
            _hint = null;
            _tool = null;
            _slide = 0f;
        }

        // hot reload: nothing of ours stays subscribed to anything that outlives us
        internal static void Teardown()
        {
            if (_hooked)
            {
                Application.logMessageReceived -= OnUnityLog;
                _hooked = false;
            }
            _open = false;
            _grace = 0;
        }

        // For the harness: the console as a key would drive it, and its log's tail
        internal static bool HarnessToggle()
        {
            if (_open)
            {
                Close();
            }
            else if (_mn != null)
            {
                Open(_mn);
            }
            return _open;
        }

        internal static string HarnessRun(string line)
        {
            int before = Lines.Count;
            Run(line);
            var sb = new StringBuilder();
            for (int i = Mathf.Max(0, before); i < Lines.Count; i++)
            {
                sb.Append(Lines[i]).Append('\n');
            }
            return sb.ToString();
        }

        internal static string HarnessTail(int n)
        {
            var sb = new StringBuilder();
            for (int i = Mathf.Max(0, Lines.Count - n); i < Lines.Count; i++)
            {
                sb.Append(Lines[i]).Append('\n');
            }
            return sb.ToString();
        }
    }
}
