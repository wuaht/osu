// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Win32;
using osu.Framework.Logging;

namespace osu.Desktop.Windows
{
    /// <summary>
    /// Moves the game window to another Windows virtual desktop, so an automated instance stays out of the user's way.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static class VirtualDesktopMover
    {
        private const string desktops_key = @"Software\Microsoft\Windows\CurrentVersion\Explorer\VirtualDesktops";

        private const int dwmwa_cloak = 13;
        private const int type_e_elementnotfound = unchecked((int)0x8002802B);

        /// <summary>
        /// Starts moving the game window to the last virtual desktop that isn't the current one.
        /// Call while the window is still hidden: it gets cloaked so it never shows up on the current desktop.
        /// </summary>
        /// <returns>The window to pass to <see cref="TryMove"/>, or <see cref="IntPtr.Zero"/> if it doesn't exist.</returns>
        public static IntPtr Begin()
        {
            IntPtr hwnd = findGameWindow();
            if (hwnd != IntPtr.Zero)
                setCloaked(hwnd, true);
            return hwnd;
        }

        /// <summary>
        /// Moves the window started with <see cref="Begin"/> and uncloaks it.
        /// </summary>
        /// <param name="hwnd">The window returned by <see cref="Begin"/>.</param>
        /// <param name="giveUp">Uncloak even if the window isn't shown yet.</param>
        /// <returns>False if the window isn't shown yet (virtual desktops only track shown windows), so the caller should try again.</returns>
        public static bool TryMove(IntPtr hwnd, bool giveUp)
        {
            try
            {
                var manager = (IVirtualDesktopManager)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("aa509086-5ca9-4c25-8f95-589d3c07b48a"))!)!;

                int result = manager.GetWindowDesktopId(hwnd, out Guid current);

                if (result == type_e_elementnotfound && !giveUp)
                    return false;

                Marshal.ThrowExceptionForHR(result);

                // the documented API can't list desktops, but explorer keeps their ids here (16 bytes each).
                byte[]? ids = Registry.CurrentUser.OpenSubKey(desktops_key)?.GetValue("VirtualDesktopIDs") as byte[];
                Guid target = ids?.Chunk(16).Where(c => c.Length == 16).Select(c => new Guid(c)).LastOrDefault(g => g != current) ?? Guid.Empty;

                if (target == Guid.Empty)
                    Logger.Log("Only one virtual desktop exists, so the window stays on the current one. Create a second desktop (Win+Ctrl+D) to run in the background.", level: LogLevel.Important);
                else
                {
                    manager.MoveWindowToDesktop(hwnd, ref target);
                    Logger.Log($"Moved window to virtual desktop {target}.");
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, $"Failed to move window to another virtual desktop: {e.GetType().Name}: {e.Message}");
            }

            setCloaked(hwnd, false);
            return true;
        }

        private static void setCloaked(IntPtr hwnd, bool cloaked)
        {
            int value = cloaked ? 1 : 0;
            DwmSetWindowAttribute(hwnd, dwmwa_cloak, ref value, sizeof(int));
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        /// <summary>
        /// Finds the game window, including while it is still hidden (unlike Process.MainWindowHandle).
        /// </summary>
        private static IntPtr findGameWindow()
        {
            uint processId = (uint)Environment.ProcessId;
            IntPtr found = IntPtr.Zero;
            var className = new StringBuilder(64);

            EnumWindows((hwnd, _) =>
            {
                GetWindowThreadProcessId(hwnd, out uint windowProcessId);
                if (windowProcessId != processId)
                    return true;

                className.Clear();
                GetClassName(hwnd, className, className.Capacity);
                if (className.ToString() != "SDL_app")
                    return true;

                found = hwnd;
                return false;
            }, IntPtr.Zero);

            return found;
        }

        private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hwnd, StringBuilder className, int maxCount);

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("a5cd92ff-29be-454c-8d04-d82879fb3f1b")]
        private interface IVirtualDesktopManager
        {
            [PreserveSig]
            int IsWindowOnCurrentVirtualDesktop(IntPtr topLevelWindow, out bool onCurrentDesktop);

            [PreserveSig]
            int GetWindowDesktopId(IntPtr topLevelWindow, out Guid desktopId);

            void MoveWindowToDesktop(IntPtr topLevelWindow, ref Guid desktopId);
        }
    }
}
