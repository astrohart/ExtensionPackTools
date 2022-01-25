﻿using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ExtensionManager
{
    /// <summary>
    /// Provides definitions for Win32 API methods and constants.
    /// </summary>
    internal static class NativeMethods
    {
        /// <summary>
        /// Associates a new large or small icon with a window. The system displays the large icon in the ALT+TAB dialog box, and the small icon in the window caption.
        /// </summary>
        public const int WM_SETICON = 0x0080;

        // from winuser.h
        public const int GWL_STYLE = -16,
                      WS_DLGFRAME = 0x00400000,
                      WS_MAXIMIZEBOX = 0x10000,
                      WS_MINIMIZEBOX = 0x20000;

        internal const int GWL_EXSTYLE = -20;
        internal const int WS_EX_DLGMODALFRAME = 0x0001;
        internal const int SWP_NOSIZE = 0x0001;
        internal const int SWP_NOMOVE = 0x0002;
        internal const int SWP_NOZORDER = 0x0004;
        internal const int SWP_FRAMECHANGED = 0x0020;

        [DllImport("user32.dll")]
        static extern bool SetWindowPos(IntPtr hwnd, IntPtr hwndInsertAfter,
     int x, int y, int width, int height, uint flags);

        [DllImport("user32.dll")]
        static extern IntPtr SendMessage(IntPtr hwnd, uint msg,
            IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        internal static extern int GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll")]
        internal static extern int SetWindowLong(IntPtr hwnd, int index, int value);

        // thanks stack overflow <https://stackoverflow.com/questions/339620/how-do-i-remove-minimize-and-maximize-from-a-resizable-window-in-wpf>
        /// <summary>
        /// Removes the Maximize button from a <see cref="T:System.Windows.Window"/>'s title bar.
        /// </summary>
        internal static void HideMaximizeButton(this Window window)
        {
            if (window == null) return;

            var hwnd = new WindowInteropHelper(window).Handle;

            if (!IsWindow(hwnd)) return;

            var currentStyle = GetWindowLong(hwnd, GWL_STYLE);

            SetWindowLong(hwnd, GWL_STYLE, currentStyle & ~WS_MAXIMIZEBOX);
        }

        // thanks stack overflow <https://stackoverflow.com/questions/339620/how-do-i-remove-minimize-and-maximize-from-a-resizable-window-in-wpf>
        /// <summary>
        /// Removes the Maximize button from a <see cref="T:System.Windows.Window"/>'s title bar.
        /// </summary>
        internal static void HideMinimizeButton(this Window window)
        {
            if (window == null) return;

            var hwnd = new WindowInteropHelper(window).Handle;

            if (!IsWindow(hwnd)) return;

            var currentStyle = GetWindowLong(hwnd, GWL_STYLE);

            SetWindowLong(hwnd, GWL_STYLE, currentStyle & ~WS_MINIMIZEBOX);
        }

        /// <summary>
        /// Removes the icon from the title bar of the <see cref="T:System.Windows.Window"/> referred to by the <paramref name="window"/> parameter.
        /// </summary>
        /// <param name="window">Reference to an instance of a <see cref="T:System.Windows.Window"/> from which the icon is to be removed.</param>
        /// <remarks>Thank you <a href="https://stackoverflow.com/questions/18580430/hide-the-icon-from-a-wpf-window">Stack Overflow.</a></remarks>
        internal static void RemoveIcon(this Window window)
        {
            if (window == null) return;

            // Get this window's handle
            var hwnd = new WindowInteropHelper(window).Handle;

            // Change the extended window style to not show a window icon
            var extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_DLGMODALFRAME);

            SendMessage(hwnd, WM_SETICON, 1, IntPtr.Zero);
            SendMessage(hwnd, WM_SETICON, 0, IntPtr.Zero);

            // Update the window's non-client area to reflect the changes
            SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE |
                  SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);
        }

        // thanks stack overflow <https://stackoverflow.com/questions/339620/how-do-i-remove-minimize-and-maximize-from-a-resizable-window-in-wpf>
        /// <summary>
        /// Changes the border of the window to the style commonly utilized for dialog boxes.
        /// </summary>
        internal static void SetDialogWindowFrame(this Window window)
        {
            if (window == null) return;

            var hwnd = new WindowInteropHelper(window).Handle;

            if (!IsWindow(hwnd)) return;

            var currentStyle = GetWindowLong(hwnd, GWL_STYLE);

            SetWindowLong(hwnd, GWL_STYLE, currentStyle | WS_DLGFRAME);
        }

        // thanks stack overflow <https://stackoverflow.com/questions/339620/how-do-i-remove-minimize-and-maximize-from-a-resizable-window-in-wpf>
        /// <summary>
        /// Changes the border of the window to the style commonly utilized for dialog boxes.
        /// </summary>
        internal static void StyleWindowAsDialogBox(this Window window)
        {
            if (window == null) return;

            // Set the border of the window to be a dialog frame
            window.SetDialogWindowFrame();

            // hide the Minimize and Maximize buttons
            window.HideMaximizeButton();
            window.HideMinimizeButton();

            // Remove the icon from the title bar
            window.RemoveIcon();
        }

        /// <summary>
        /// Determines whether the specified window handle identifies an existing window.
        /// </summary>
        /// <param name="hWnd">A handle to the window to be tested.</param>
        /// <returns>If the window handle identifies an existing window, the return value is nonzero. If the window handle does not identify an existing window, the return value is zero.</returns>
        /// <remarks>A thread should not use IsWindow for a window that it did not create because the window could be destroyed after this function was called. Further, because window handles are recycled the handle could even point to a different window.</remarks>
        [DllImport("user32.dll")]
        internal static extern bool IsWindow(IntPtr hWnd);

        /// <summary>
        /// Sends the specified message to a window or windows. The SendMessage function calls the window procedure for the specified window and does not return until the window procedure has processed the message.<para>To send a message and return immediately, use the SendMessageCallback or SendNotifyMessage function. To post a message to a thread's message queue and return immediately, use the PostMessage or PostThreadMessage function.</para>
        /// </summary>
        /// <param name="hWnd">A handle to the window whose window procedure will receive the message. If this parameter is HWND_BROADCAST ((HWND)0xffff), the message is sent to all top-level windows in the system, including disabled or invisible unowned windows, overlapped windows, and pop-up windows; but the message is not sent to child windows.<para/>Message sending is subject to UIPI. The thread of a process can send messages only to message queues of threads in processes of lesser or equal integrity level.</param>
        /// <param name="Msg">The message to be sent.<para/>For lists of the system-provided messages, see System-Defined Messages.</param>
        /// <param name="wParam">Additional message-specific information.</param>
        /// <param name="lParam">Additional message-specific information.</param>
        /// <returns></returns>
        /// <returns>The return value specifies the result of the message processing; it depends on the message sent.</returns>
        /// <remarks>When a message is blocked by UIPI the last error, retrieved with GetLastError, is set to 5 (access denied).<para/>Applications that need to communicate using HWND_BROADCAST should use the RegisterWindowMessage function to obtain a unique message for inter-application communication.<para/>The system only does marshalling for system messages (those in the range 0 to (WM_USER-1)). To send other messages (those >= WM_USER) to another process, you must do custom marshalling.<para/>If the specified window was created by the calling thread, the window procedure is called immediately as a subroutine. If the specified window was created by a different thread, the system switches to that thread and calls the appropriate window procedure. Messages sent between threads are processed only when the receiving thread executes message retrieval code. The sending thread is blocked until the receiving thread processes the message. However, the sending thread will process incoming nonqueued messages while waiting for its message to be processed. To prevent this, use SendMessageTimeout with SMTO_BLOCK set. For more information on nonqueued messages, see Nonqueued Messages.<para/>An accessibility application can use SendMessage to send WM_APPCOMMAND messages to the shell to launch applications. This functionality is not guaranteed to work for other types of applications.</remarks>
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        internal static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, IntPtr lParam);
    }
}