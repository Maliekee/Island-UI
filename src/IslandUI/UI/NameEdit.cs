using System;
using UnityEngine;

namespace IslandUI.UI
{
    // RENAMING ON THE SHEET, IN PLACE. Vanilla renames from its status window: a
    // click on the name opens UIManager.nameInput, Return writes charaName, the
    // name tag over the head and the friends list (UIManager.NameInput). Our sheet
    // replaced that window, so the name is the door now - and it is edited where
    // it is printed: a click puts a text cursor at the end of the name, the keys
    // type and Backspace deletes, Return or a click away keeps it, Escape keeps
    // the old name. No field is laid over it.
    //
    // It reads the keyboard itself - Input.inputString for the characters, which
    // FilterInput's capture leaves alone, and FilterInput.RawKeyDown for Escape -
    // and FilterInput counts it as typing, so the game's hotkeys stay quiet. A
    // borrowed TMP field was tried first and lost its focus the frame after it
    // took it.
    internal static class NameEdit
    {
        private const int Limit = 24;
        private static Action<string> _commit;
        private static string _text = string.Empty;
        private static Func<Vector2, bool> _over;   // is a screen point on the name being edited
        private static int _openedFrame;

        internal static bool Open { get; private set; }
        internal static bool IsFocused => Open;
        internal static string Text => _text;

        // the cursor's blink: on for half of every second, and on while typing
        private static float _typedAt;
        internal static bool CaretShown => Time.unscaledTime - _typedAt < 0.5f || Mathf.Repeat(Time.unscaledTime, 1f) < 0.5f;

        // Starts an edit of `name`; `commit` gets the new name once one is given.
        // `over` says whether a click lands on the name - a click anywhere else
        // keeps what was typed, as leaving a field does.
        internal static void Begin(string name, Func<Vector2, bool> over, Action<string> commit)
        {
            _text = name ?? string.Empty;
            _commit = commit;
            _over = over;
            _openedFrame = Time.frameCount;
            _typedAt = Time.unscaledTime;
            Open = true;
        }

        // the sheet that held it went away: drop the edit, keep the old name
        internal static void Cancel()
        {
            Open = false;
            _commit = null;
            _over = null;
        }

        // Once a frame, before anything else reads the mouse or the keys. True
        // while an edit is open (or closed this frame), so the caller can repaint.
        internal static bool Tick()
        {
            if (!Open)
            {
                return false;
            }
            if (Time.frameCount == _openedFrame)
            {
                return true;   // the click that opened it is not also the click that closes it
            }
            if (FilterInput.RawKeyDown(KeyCode.Escape))
            {
                Cancel();
                return true;
            }
            if (Input.GetMouseButtonDown(0) && (_over == null || !_over(Input.mousePosition)))
            {
                Finish();
                return true;
            }
            string typed = Input.inputString;
            for (int i = 0; typed != null && i < typed.Length; i++)
            {
                char c = typed[i];
                if (c == '\b')
                {
                    if (_text.Length > 0) { _text = _text.Substring(0, _text.Length - 1); }
                }
                else if (c == '\n' || c == '\r')
                {
                    Finish();
                    return true;
                }
                else if (!char.IsControl(c) && _text.Length < Limit)
                {
                    _text += c;
                }
                _typedAt = Time.unscaledTime;
            }
            return true;
        }

        // keep it - unless it is empty, which is no name: the old one stays
        private static void Finish()
        {
            Action<string> commit = _commit;
            string name = _text.Trim();
            Cancel();
            if (commit != null && name.Length > 0)
            {
                commit(name);
            }
        }
    }
}
