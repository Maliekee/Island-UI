using System;
using System.Runtime.InteropServices;
using System.Text;
using BepInEx.Configuration;

namespace IslandEx
{
    // Keep the fullscreen game window up when it loses focus.
    //
    // Switch virtual desktop, alt-tab, click another monitor: the game minimises
    // itself. Nothing in the game's code does this. Unity's Windows player does, on
    // WM_ACTIVATE deactivation, for any fullscreen mode — unless the build was made
    // with the "Visible In Background" player setting on, and this one was not. The
    // setting is baked into globalgamemanagers; there is no runtime API, launch
    // argument or registry key for it. (The game runs in FullScreenWindow, i.e.
    // borderless — a plain WS_POPUP window at monitor size, so Windows itself has
    // no reason to minimise it. Unity alone decides to.)
    //
    // The setting has exactly one consequence: one ShowWindow(hwnd, SW_MINIMIZE)
    // call from UnityPlayer.dll. So rather than patch the player settings file
    // (which a Steam update would revert), this redirects UnityPlayer.dll's import
    // of user32!ShowWindow to a managed function that swallows minimise requests
    // against the game window and passes everything else through. The Import
    // Address Table is the module's own per-DLL pointer list, so only calls from
    // UnityPlayer.dll are affected: a taskbar click or Win+D minimises through
    // DefWindowProc inside user32 and still works.
    //
    // Nothing here is version-specific — an import name is stable across Unity
    // patch releases. If UnityPlayer.dll ever stops importing ShowWindow, or the
    // module cannot be read, this logs and does nothing.
    //
    // Second, smaller thing: Win+M and Win+Down only minimise windows whose style
    // has WS_MINIMIZEBOX, and Unity's borderless window has no such bit (it has
    // no caption to draw a button on). Setting the bit is enough for Windows —
    // it checks the style, not the button. Unity rewrites the style whenever it
    // restyles the window (resolution or mode change), so the bit is re-asserted
    // on every ShowWindow it makes and on a slow tick.
    internal static class KeepFullscreen
    {
        private static ConfigEntry<bool> _enabled;
        private static ConfigEntry<bool> _minimiseShortcuts;

        internal static void Bind(ConfigFile cfg)
        {
            _enabled = cfg.Bind("Window", "KeepFullscreenOnFocusLoss", true,
                "Stops the game minimising itself when you switch desktop, Alt+Tab or click another monitor. Off = the game minimises as usual.");
            _minimiseShortcuts = cfg.Bind("Window", "MinimiseShortcuts", true,
                "Lets Win+M and Win+Down minimise the game as they do other windows. A taskbar click and Win+D work either way.");
        }

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int ShowWindowFn(IntPtr hWnd, int nCmdShow);

        // Both kept in statics: the IAT slot points at _hook's native thunk, which is
        // only valid while the delegate is alive, and _original is the real user32
        // entry point the hook forwards to.
        private static ShowWindowFn _original;
        private static ShowWindowFn _hook;
        private static IntPtr _gameWindow;
        private static int _swallowed;
        // what Uninstall puts back, and where
        private static IntPtr _slot;
        private static IntPtr _originalPtr;
        private static IntPtr _hookPtr;

        private const int SW_SHOWMINIMIZED = 2;
        private const int SW_MINIMIZE = 6;
        private const int SW_SHOWMINNOACTIVE = 7;
        private const int SW_FORCEMINIMIZE = 11;
        private const uint PAGE_READWRITE = 0x04;
        private const int GWL_STYLE = -16;
        private const long WS_MINIMIZEBOX = 0x00020000;

        internal static void Install()
        {
            try
            {
                IntPtr module = GetModuleHandle("UnityPlayer.dll");
                if (module == IntPtr.Zero)
                {
                    Plugin.Log.LogWarning("KeepFullscreen: UnityPlayer.dll not found in the process; leaving Unity's minimise-on-focus-loss alone.");
                    return;
                }
                IntPtr slot = FindImportSlot(module, "user32.dll", "ShowWindow");
                if (slot == IntPtr.Zero)
                {
                    Plugin.Log.LogWarning("KeepFullscreen: UnityPlayer.dll does not import user32!ShowWindow by name; leaving Unity's minimise-on-focus-loss alone.");
                    return;
                }

                IntPtr originalPtr = Marshal.ReadIntPtr(slot);
                _original = Marshal.GetDelegateForFunctionPointer<ShowWindowFn>(originalPtr);
                _hook = ShowWindowHook;
                IntPtr hookPtr = Marshal.GetFunctionPointerForDelegate(_hook);

                if (!VirtualProtect(slot, (UIntPtr)IntPtr.Size, PAGE_READWRITE, out uint previous))
                {
                    Plugin.Log.LogWarning($"KeepFullscreen: VirtualProtect failed (error {Marshal.GetLastWin32Error()}); leaving Unity's minimise-on-focus-loss alone.");
                    return;
                }
                Marshal.WriteIntPtr(slot, hookPtr);
                VirtualProtect(slot, (UIntPtr)IntPtr.Size, previous, out _);
                _slot = slot;
                _originalPtr = originalPtr;
                _hookPtr = hookPtr;

                Plugin.Log.LogInfo($"KeepFullscreen: UnityPlayer.dll's ShowWindow import redirected (slot 0x{slot.ToInt64():X}, was 0x{originalPtr.ToInt64():X}).");
                EnsureMinimiseBox(FindGameWindow());
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"KeepFullscreen: could not redirect ShowWindow; leaving Unity's minimise-on-focus-loss alone. {e}");
            }
        }

        // Hot reload: hand the import back, or the next generation reads OUR thunk as
        // "the original" and hooks the hook - one more dead assembly in the chain per
        // reload. Only if the slot still holds our pointer; anything else in there is
        // somebody else's redirect and not ours to overwrite. The minimise box stays
        // set: the next generation asserts it again anyway.
        internal static void Uninstall()
        {
            if (_slot == IntPtr.Zero) return;
            try
            {
                if (Marshal.ReadIntPtr(_slot) != _hookPtr)
                {
                    Plugin.Log.LogWarning("KeepFullscreen: the ShowWindow import no longer points at this hook; leaving it.");
                    return;
                }
                if (!VirtualProtect(_slot, (UIntPtr)IntPtr.Size, PAGE_READWRITE, out uint previous))
                {
                    Plugin.Log.LogWarning($"KeepFullscreen: VirtualProtect failed on uninstall (error {Marshal.GetLastWin32Error()}).");
                    return;
                }
                Marshal.WriteIntPtr(_slot, _originalPtr);
                VirtualProtect(_slot, (UIntPtr)IntPtr.Size, previous, out _);
                _slot = IntPtr.Zero;
                Plugin.Log.LogInfo("KeepFullscreen: ShowWindow import restored.");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"KeepFullscreen: could not restore ShowWindow. {e}");
            }
        }

        // Every ~2 s at 60 fps. Unity restyles the window rarely (settings menu) and
        // usually calls ShowWindow right after, so this is a backstop, not the path.
        private static int _tick;

        internal static void Tick()
        {
            if (_hook == null || ++_tick < 120) return;
            _tick = 0;
            EnsureMinimiseBox(_gameWindow != IntPtr.Zero ? _gameWindow : FindGameWindow());
        }

        private static int ShowWindowHook(IntPtr hWnd, int nCmdShow)
        {
            bool game = IsGameWindow(hWnd);
            if (game) EnsureMinimiseBox(hWnd);
            if (_enabled.Value && Plugin.Enabled.Value && IsMinimise(nCmdShow) && game)
            {
                _swallowed++;
                if (_swallowed == 1)
                {
                    Plugin.Log.LogInfo($"KeepFullscreen: swallowed UnityPlayer's minimise of the game window (nCmdShow={nCmdShow}).");
                }
                else
                {
                    Plugin.Log.LogDebug($"KeepFullscreen: swallowed minimise #{_swallowed} (nCmdShow={nCmdShow}).");
                }
                return 1; // "was previously visible" — it still is
            }
            return _original(hWnd, nCmdShow);
        }

        private static bool IsMinimise(int nCmdShow)
        {
            return nCmdShow == SW_MINIMIZE || nCmdShow == SW_SHOWMINIMIZED
                || nCmdShow == SW_SHOWMINNOACTIVE || nCmdShow == SW_FORCEMINIMIZE;
        }

        private static void EnsureMinimiseBox(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return;
            long style = GetStyle(hWnd);
            bool has = (style & WS_MINIMIZEBOX) != 0;
            bool want = _minimiseShortcuts.Value && Plugin.Enabled.Value;
            if (has == want) return;
            SetStyle(hWnd, want ? style | WS_MINIMIZEBOX : style & ~WS_MINIMIZEBOX);
            Plugin.Log.LogDebug($"KeepFullscreen: WS_MINIMIZEBOX {(want ? "set" : "cleared")} on the game window.");
        }

        private static IntPtr FindGameWindow()
        {
            uint pid = GetCurrentProcessId();
            IntPtr found = IntPtr.Zero;
            EnumWindows((h, _) =>
            {
                GetWindowThreadProcessId(h, out uint owner);
                if (owner != pid || !IsGameWindow(h)) return true;
                found = h;
                return false;
            }, IntPtr.Zero);
            return found;
        }

        private static bool IsGameWindow(IntPtr hWnd)
        {
            if (hWnd == _gameWindow) return true;
            var cls = new StringBuilder(64);
            if (GetClassName(hWnd, cls, cls.Capacity) == 0) return false;
            if (cls.ToString() != "UnityWndClass") return false;
            _gameWindow = hWnd;
            return true;
        }

        // Walks the loaded module's import directory to the IAT slot holding the
        // resolved address of `function` from `dll`. Returns zero if absent.
        private static IntPtr FindImportSlot(IntPtr module, string dll, string function)
        {
            int e_lfanew = Marshal.ReadInt32(module, 0x3C);
            IntPtr nt = module + e_lfanew;
            if (Marshal.ReadInt32(nt) != 0x00004550) return IntPtr.Zero; // "PE\0\0"
            IntPtr optional = nt + 0x18;
            ushort magic = (ushort)Marshal.ReadInt16(optional);
            int dataDirectories = magic == 0x20B ? 0x70 : 0x60;          // PE32+ : PE32
            int importRva = Marshal.ReadInt32(optional + dataDirectories + 8); // [1] = imports
            if (importRva == 0) return IntPtr.Zero;

            for (IntPtr desc = module + importRva; ; desc += 20)       // IMAGE_IMPORT_DESCRIPTOR
            {
                int lookupRva = Marshal.ReadInt32(desc, 0);              // OriginalFirstThunk
                int nameRva = Marshal.ReadInt32(desc, 12);
                int iatRva = Marshal.ReadInt32(desc, 16);                // FirstThunk
                if (nameRva == 0) return IntPtr.Zero;
                if (!string.Equals(Marshal.PtrToStringAnsi(module + nameRva), dll, StringComparison.OrdinalIgnoreCase)) continue;

                IntPtr lookup = module + (lookupRva != 0 ? lookupRva : iatRva);
                for (int i = 0; ; i++)
                {
                    long entry;
                    bool byOrdinal;
                    if (IntPtr.Size == 8)
                    {
                        entry = Marshal.ReadInt64(lookup, i * 8);
                        byOrdinal = entry < 0;
                    }
                    else
                    {
                        entry = (uint)Marshal.ReadInt32(lookup, i * 4);
                        byOrdinal = (entry & 0x80000000L) != 0;
                    }
                    if (entry == 0) break;
                    if (byOrdinal) continue;
                    string name = Marshal.PtrToStringAnsi(module + (int)entry + 2); // IMAGE_IMPORT_BY_NAME: hint, name
                    if (name == function) return module + iatRva + i * IntPtr.Size;
                }
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string moduleName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool VirtualProtect(IntPtr address, UIntPtr size, uint newProtect, out uint oldProtect);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentProcessId();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder className, int maxCount);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        // GetWindowLongPtrW is only an export on 64-bit user32; 32-bit has the plain one.
        private static long GetStyle(IntPtr hWnd) =>
            IntPtr.Size == 8 ? GetWindowLongPtr(hWnd, GWL_STYLE).ToInt64() : (uint)GetWindowLong(hWnd, GWL_STYLE);

        private static void SetStyle(IntPtr hWnd, long style)
        {
            if (IntPtr.Size == 8) SetWindowLongPtr(hWnd, GWL_STYLE, new IntPtr(style));
            else SetWindowLong(hWnd, GWL_STYLE, unchecked((int)(uint)style));
        }

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
        private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int index);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int index, IntPtr value);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
        private static extern int GetWindowLong(IntPtr hWnd, int index);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
        private static extern int SetWindowLong(IntPtr hWnd, int index, int value);
    }
}
