using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace ScreenSwap.Windows.Utilities.VirtualDesktop;

[ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("a5cd92ff-29be-454c-8d04-d82879fb3f1b")]
[SuppressMessage("Interoperability", "SYSLIB1096:Convert to 'GeneratedComInterface'", Justification = "Suppressing warning for COM interface.")]
public interface IVirtualDesktopManager
{
    [PreserveSig]
    int IsWindowOnCurrentVirtualDesktop(IntPtr topLevelWindow, out int onCurrentDesktop);

    [PreserveSig]
    int GetWindowDesktopId(IntPtr topLevelWindow, out Guid currentDesktop);

    [PreserveSig]
    int MoveWindowToDesktop(IntPtr topLevelWindow, Guid currentDesktop);
}

[ComImport, Guid("aa509086-5ca9-4c25-8f95-589d3c07b48a")]
public class CVirtualDesktopManager { }
