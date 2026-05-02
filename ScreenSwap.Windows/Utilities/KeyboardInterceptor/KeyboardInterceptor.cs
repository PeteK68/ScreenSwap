using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace ScreenSwap.Windows.Utilities.KeyboardInterceptor;

/// <summary>
/// Listens keyboard globally.
/// 
/// <remarks>Uses WH_KEYBOARD_LL.</remarks>
/// </summary>
public class GlobalKeyboardHook : IDisposable
{
    private readonly Dispatcher dispatcher;

    /// <summary>
    /// Fired when any of the keys is pressed down.
    /// </summary>
    public event RawKeyEventHandler KeyDown;

    /// <summary>
    /// Fired when any of the keys is released.
    /// </summary>
    public event RawKeyEventHandler KeyUp;

    /// <summary>
    /// Hook ID
    /// </summary>
    private readonly IntPtr hookId = IntPtr.Zero;

    /// <summary>
    /// If the keyboard event has been handled
    /// </summary>
    private bool handled;

    /// <summary>
    /// Callback hook.
    /// </summary>
    /// <param name="character">Character</param>
    /// <param name="keyEvent">Keyboard event</param>
    /// <param name="vkCode">VKCode</param>
    private delegate void KeyboardCallback(InterceptKeys.KeyEvent keyEvent, int vkCode, string character);

    /// <summary>
    /// Event to be invoked each time key is pressed.
    /// </summary>
    private readonly KeyboardCallback hookedKeyboardCallback;

    /// <summary>
    /// Contains the hooked callback in runtime.
    /// </summary>
    private readonly InterceptKeys.LowLevelKeyboardProc hookedLowLevelKeyboardProc;

    /// <summary>
    /// Creates global keyboard listener.
    /// </summary>
    public GlobalKeyboardHook()
    {
        // Dispatcher thread handling the KeyDown/KeyUp events.
        dispatcher = Dispatcher.CurrentDispatcher;

        // We have to store the LowLevelKeyboardProc, so that it is not garbage collected runtime
        hookedLowLevelKeyboardProc = LowLevelKeyboardProc;

        // Set the hook
        hookId = InterceptKeys.SetHook(hookedLowLevelKeyboardProc);

        // Assign the asynchronous callback event
        hookedKeyboardCallback = new KeyboardCallback(KeyboardListener_KeyboardCallback);
    }

    /// <summary>
    /// Destroys global keyboard listener.
    /// </summary>
    ~GlobalKeyboardHook()
    {
        Dispose();
    }

    /// <summary>
    /// Actual callback hook.
    /// 
    /// <remarks>Calls asynchronously the asyncCallback.</remarks>
    /// </summary>
    /// <param name="nCode"></param>
    /// <param name="wParam"></param>
    /// <param name="lParam"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private IntPtr LowLevelKeyboardProc(int nCode, UIntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            if (wParam.ToUInt32() is ((int)InterceptKeys.KeyEvent.WmKeyDown)
                or ((int)InterceptKeys.KeyEvent.WmKeyUp)
                or ((int)InterceptKeys.KeyEvent.WmSysKeyDown)
                or ((int)InterceptKeys.KeyEvent.WmSysKeyUp))
            {
                // Captures the character(s) pressed only on WmKeyDown
                var chars = InterceptKeys.VkCodeToString((uint)Marshal.ReadInt32(lParam),
                    wParam.ToUInt32() is ((int)InterceptKeys.KeyEvent.WmKeyDown)
                    or ((int)InterceptKeys.KeyEvent.WmSysKeyDown));
                hookedKeyboardCallback.Invoke((InterceptKeys.KeyEvent)wParam.ToUInt32(), Marshal.ReadInt32(lParam), chars);
            }
        }

        return handled ? 1 : InterceptKeys.CallNextHookEx(hookId, nCode, wParam, lParam);
    }

    /// <summary>
    /// HookCallbackAsync procedure that calls accordingly the KeyDown or KeyUp events.
    /// </summary>
    /// <param name="keyEvent">Keyboard event</param>
    /// <param name="vkCode">VKCode</param>
    /// <param name="character">Character as string.</param>
    private void KeyboardListener_KeyboardCallback(InterceptKeys.KeyEvent keyEvent, int vkCode, string character)
    {
        RawKeyEventArgs rawKeyEventArgs = new(vkCode, false, character);

        switch (keyEvent)
        {
            // KeyDown events
            case InterceptKeys.KeyEvent.WmKeyDown:
                if (KeyDown != null)
                {
                    _ = dispatcher.Invoke(new RawKeyEventHandler(KeyDown), this, rawKeyEventArgs);
                }

                break;
            case InterceptKeys.KeyEvent.WmSysKeyDown:
                if (KeyDown != null)
                {
                    rawKeyEventArgs.IsSysKey = true;
                    _ = dispatcher.Invoke(new RawKeyEventHandler(KeyDown), this, rawKeyEventArgs);
                }

                break;

            // KeyUp events
            case InterceptKeys.KeyEvent.WmKeyUp:
                if (KeyUp != null)
                {
                    _ = dispatcher.Invoke(new RawKeyEventHandler(KeyUp), this, rawKeyEventArgs);
                }

                break;
            case InterceptKeys.KeyEvent.WmSysKeyUp:
                if (KeyUp != null)
                {
                    rawKeyEventArgs.IsSysKey = true;
                    _ = dispatcher.Invoke(new RawKeyEventHandler(KeyUp), this, rawKeyEventArgs);
                }

                break;

            default:
                break;
        }

        handled = rawKeyEventArgs.Handled;
    }

    /// <summary>
    /// Disposes the hook.
    /// <remarks>This call is required as it calls the UnhookWindowsHookEx.</remarks>
    /// </summary>
    public void Dispose()
    {
        _ = InterceptKeys.UnhookWindowsHookEx(hookId);
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Raw KeyEvent arguments.
/// </summary>
/// <remarks>
/// Create raw keyevent arguments.
/// </remarks>
/// <param name="vkCode"></param>
/// <param name="isSysKey"></param>
/// <param name="Character">Character</param>
public class RawKeyEventArgs(int vkCode, bool isSysKey, string Character) : EventArgs
{
    /// <summary>
    /// VkCode of the key.
    /// </summary>
    public int VkCode { get; set; } = vkCode;

    /// <summary>
    /// Is the hitted key system key.
    /// </summary>
    public bool IsSysKey { get; set; } = isSysKey;

    /// <summary>
    /// Convert to string.
    /// </summary>
    /// <returns>Returns string representation of this key, if not possible empty string is returned.</returns>
    public override string ToString() => Character;

    /// <summary>
    /// Unicode character of key pressed.
    /// </summary>
    public string Character { get; set; } = Character;

    /// <summary>
    /// If the keystroke has been handled.
    /// </summary>
    public bool Handled { get; set; } = false;
}

/// <summary>
/// Raw keyevent handler.
/// </summary>
/// <param name="sender">sender</param>
/// <param name="args">raw keyevent arguments</param>
public delegate void RawKeyEventHandler(object sender, RawKeyEventArgs args);

/// <summary>
/// Winapi Key interception helper class.
/// </summary>
internal static partial class InterceptKeys
{
    public delegate IntPtr LowLevelKeyboardProc(int nCode, UIntPtr wParam, IntPtr lParam);
    public const int whKeyboardLl = 13;

    /// <summary>
    /// Key event
    /// </summary>
    public enum KeyEvent : int
    {
        /// <summary>
        /// Key down
        /// </summary>
        WmKeyDown = 256,

        /// <summary>
        /// Key up
        /// </summary>
        WmKeyUp = 257,

        /// <summary>
        /// System key up
        /// </summary>
        WmSysKeyUp = 261,

        /// <summary>
        /// System key down
        /// </summary>
        WmSysKeyDown = 260
    }

    public static IntPtr SetHook(LowLevelKeyboardProc proc)
    {
        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule;
        return SetWindowsHookEx(whKeyboardLl, proc, GetModuleHandle(curModule.ModuleName), 0);
    }

    [LibraryImport("user32.dll", EntryPoint = "SetWindowsHookExW", SetLastError = true)]
    public static partial IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [LibraryImport("user32.dll", EntryPoint = "UnhookWindowsHookEx", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool UnhookWindowsHookEx(IntPtr hhk);

    [LibraryImport("user32.dll", EntryPoint = "CallNextHookEx", SetLastError = true)]
    public static partial IntPtr CallNextHookEx(IntPtr hhk, int nCode, UIntPtr wParam, IntPtr lParam);

    [LibraryImport("kernel32.dll", EntryPoint = "GetModuleHandleW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    public static partial IntPtr GetModuleHandle(string lpModuleName);

    // Note: Sometimes single vkCode represents multiple chars, thus string. 
    // E.g. typing "^1" (notice that when pressing 1 the both characters appear, 
    // because of this behavior, "^" is called dead key)

    [LibraryImport("user32.dll", EntryPoint = "ToUnicodeEx")]
    private static partial int ToUnicodeEx(uint wVirtKey, uint wScanCode, [In] byte[] lpKeyState, [Out] char[] pwszBuff, int cchBuff, uint wFlags, IntPtr dwhkl);

    [LibraryImport("user32.dll", EntryPoint = "GetKeyboardState")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetKeyboardState([In, Out] byte[] lpKeyState);

    [LibraryImport("user32.dll", EntryPoint = "MapVirtualKeyExW")]
    private static partial uint MapVirtualKeyEx(uint uCode, uint uMapType, IntPtr dwhkl);

    [LibraryImport("user32.dll", EntryPoint = "GetKeyboardLayout")]
    private static partial IntPtr GetKeyboardLayout(uint dwLayout);

    [LibraryImport("User32.dll", EntryPoint = "GetForegroundWindow")]
    private static partial IntPtr GetForegroundWindow();

    [LibraryImport("User32.dll", EntryPoint = "GetWindowThreadProcessId")]
    private static partial uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [LibraryImport("user32.dll", EntryPoint = "AttachThreadInput")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AttachThreadInput(uint idAttach, uint idAttachTo, [MarshalAs(UnmanagedType.Bool)] bool fAttach);

    [LibraryImport("kernel32.dll", EntryPoint = "GetCurrentThreadId")]
    private static partial uint GetCurrentThreadId();

    private static uint lastVkCode;
    private static uint lastScanCode;
    private static byte[] lastKeyState = new byte[255];
    private static bool lastIsDead;

    /// <summary>
    /// Convert vkCode to Unicode.
    /// <remarks>isKeyDown is required for because of keyboard state inconsistencies!</remarks>
    /// </summary>
    /// <param name="vkCode">vkCode</param>
    /// <param name="isKeyDown">Is the key down event?</param>
    /// <returns>String representing single unicode character.</returns>
    public static string VkCodeToString(uint vkCode, bool isKeyDown)
    {
        var charBuff = new char[5];

        var bKeyState = new byte[255];
        bool bKeyStateStatus;
        var isDead = false;

        // Gets the current windows window handle, threadID, processID
        var currentHWnd = GetForegroundWindow();
        var currentWindowThreadID = GetWindowThreadProcessId(currentHWnd, out _);

        // This programs Thread ID
        var thisProgramThreadId = GetCurrentThreadId();

        // Attach to active thread so we can get that keyboard state
        if (AttachThreadInput(thisProgramThreadId, currentWindowThreadID, true))
        {
            // Current state of the modifiers in keyboard
            bKeyStateStatus = GetKeyboardState(bKeyState);

            // Detach
            _ = AttachThreadInput(thisProgramThreadId, currentWindowThreadID, false);
        }
        else
        {
            // Could not attach, perhaps it is this process?
            bKeyStateStatus = GetKeyboardState(bKeyState);
        }

        // On failure we return empty string.
        if (!bKeyStateStatus)
        {
            return string.Empty;
        }

        // Gets the layout of keyboard
        var hkl = GetKeyboardLayout(currentWindowThreadID);

        // Maps the virtual keycode
        var lScanCode = MapVirtualKeyEx(vkCode, 0, hkl);

        // Keyboard state goes inconsistent if this is not in place. In other words, we need to call above commands in UP events also.
        if (!isKeyDown)
        {
            return string.Empty;
        }

        // Converts the VKCode to unicode
        var relevantKeyCountInBuffer = ToUnicodeEx(vkCode, lScanCode, bKeyState, charBuff, charBuff.Length, 0, hkl);

        var ret = string.Empty;

        switch (relevantKeyCountInBuffer)
        {
            // Dead keys (^,`...)
            case -1:
                isDead = true;

                // We must clear the buffer because ToUnicodeEx messed it up, see below.
                ClearKeyboardBuffer(vkCode, lScanCode, hkl);
                break;

            case 0:
                break;

            // Single character in buffer
            case 1:
                ret = charBuff[0].ToString();
                break;

            // Two or more (only two of them is relevant)
            case 2:
            default:
                ret = new string(charBuff, 0, 2);
                break;
        }

        // We inject the last dead key back, since ToUnicodeEx removed it.
        // More about this peculiar behavior see e.g: 
        //   http://www.experts-exchange.com/Programming/System/Windows__Programming/Q_23453780.html
        //   http://blogs.msdn.com/michkap/archive/2005/01/19/355870.aspx
        //   http://blogs.msdn.com/michkap/archive/2007/10/27/5717859.aspx
        if (lastVkCode != 0 && lastIsDead)
        {
            var charTemp = new char[5];
            _ = ToUnicodeEx(lastVkCode, lastScanCode, lastKeyState, charTemp, charTemp.Length, 0, hkl);
            lastVkCode = 0;

            return ret;
        }

        // Save these
        lastScanCode = lScanCode;
        lastVkCode = vkCode;
        lastIsDead = isDead;
        lastKeyState = (byte[])bKeyState.Clone();

        return ret;
    }

    private static void ClearKeyboardBuffer(uint vk, uint sc, IntPtr hkl)
    {
        var charBuf = new char[10];

        int rc;
        do
        {
            var lpKeyStateNull = new byte[255];
            rc = ToUnicodeEx(vk, sc, lpKeyStateNull, charBuf, charBuf.Length, 0, hkl);
        } while (rc < 0);
    }
}
