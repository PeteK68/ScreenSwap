using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace ScreenSwap.Windows.Utilities.VirtualDesktop;

[SupportedOSPlatform("windows6.1")]
public class VirtualDesktopManager : IDisposable
{
    private CVirtualDesktopManager cmanager;
    private IVirtualDesktopManager manager;

    public VirtualDesktopManager()
    {
        cmanager = new CVirtualDesktopManager();
        manager = (IVirtualDesktopManager)cmanager;
    }

    public void Dispose()
    {
        _ = Marshal.ReleaseComObject(cmanager);
        cmanager = null;
        manager = null;
        GC.SuppressFinalize(this);
    }

    public bool IsWindowOnCurrentVirtualDesktop(IntPtr topLevelWindow)
    {
        int result;
        int hr;

        try
        {
            if ((hr = manager.IsWindowOnCurrentVirtualDesktop(topLevelWindow, out result)) != 0)
            {
                Marshal.ThrowExceptionForHR(hr);
            }
        }
        catch (Exception)
        {
            return true;
        }

        return result != 0;
    }

    public Guid GetWindowDesktopId(IntPtr topLevelWindow)
    {
        int hr;

        if ((hr = manager.GetWindowDesktopId(topLevelWindow, out var result)) != 0)
        {
            Marshal.ThrowExceptionForHR(hr);
        }

        return result;
    }

    public void MoveWindowToDesktop(IntPtr topLevelWindow, Guid currentDesktop)
    {
        int hr;

        if ((hr = manager.MoveWindowToDesktop(topLevelWindow, currentDesktop)) != 0)
        {
            Marshal.ThrowExceptionForHR(hr);
        }
    }
}

