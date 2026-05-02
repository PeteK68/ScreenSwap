using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using ScreenSwap.Windows.Utilities.HotKeys;

namespace ScreenSwap.Agent.HotKeys;

[SupportedOSPlatform("windows6.1")]
internal sealed partial class LowLevelKeyboardHook : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;

    private readonly Action<VirtualKey> keyDown;
    private readonly Action<VirtualKey> keyUp;
    private readonly LowLevelKeyboardProc proc;
    private readonly IntPtr hookId;

    public LowLevelKeyboardHook(Action<VirtualKey> keyDown, Action<VirtualKey> keyUp)
    {
        this.keyDown = keyDown;
        this.keyUp = keyUp;
        proc = HookCallback;

        using var currentProcess = Process.GetCurrentProcess();
        using var currentModule = currentProcess.MainModule;
        hookId = SetWindowsHookEx(WhKeyboardLl, proc, GetModuleHandle(currentModule!.ModuleName), 0);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private IntPtr HookCallback(int nCode, UIntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var message = wParam.ToUInt32();
            var key = (VirtualKey)Marshal.ReadInt32(lParam);

            if (message is WmKeyDown or WmSysKeyDown)
                keyDown(key);
            else if (message is WmKeyUp or WmSysKeyUp)
                keyUp(key);
        }

        return CallNextHookEx(hookId, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        _ = UnhookWindowsHookEx(hookId);
    }

    private delegate IntPtr LowLevelKeyboardProc(int nCode, UIntPtr wParam, IntPtr lParam);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowsHookExW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [LibraryImport("user32.dll", EntryPoint = "UnhookWindowsHookEx", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnhookWindowsHookEx(IntPtr hhk);

    [LibraryImport("user32.dll", EntryPoint = "CallNextHookEx", SetLastError = true)]
    private static partial IntPtr CallNextHookEx(IntPtr hhk, int nCode, UIntPtr wParam, IntPtr lParam);

    [LibraryImport("kernel32.dll", EntryPoint = "GetModuleHandleW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial IntPtr GetModuleHandle(string lpModuleName);
}
