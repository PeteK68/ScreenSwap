using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;

namespace ScreenSwap.Windows.Utilities.ScreenManager;

public partial class ScreenManager
{
    private const uint EnumCurrentSettings = 0xFFFFFFFF;
    private const uint DisplayDeviceActive = 0x1;
    private const uint DisplayDevicePrimaryDevice = 0x4;

    // DEVMODEW — only the fields needed to determine panel position and size.
    // dmPositionX/Y (offsets 76/80) are desktop (logical) coordinates, matching the
    // layout shown in Windows Settings regardless of per-monitor DPI awareness.
    // dmPelsWidth/Height (offsets 172/176) are physical pixel dimensions for
    // proportional sizing.
    [StructLayout(LayoutKind.Explicit)]
    private struct DevMode
    {
        [FieldOffset(68)] public ushort dmSize;
        [FieldOffset(76)] public int dmPositionX;
        [FieldOffset(80)] public int dmPositionY;
        [FieldOffset(172)] public uint dmPelsWidth;
        [FieldOffset(176)] public uint dmPelsHeight;
        [FieldOffset(219)] private readonly byte pad; // ensures sizeof == 220 (full DEVMODEW)
    }

    private unsafe struct DisplayDevice
    {
        public uint cb;
        public fixed char deviceName[32];
        public fixed char deviceString[128];
        public uint stateFlags;
        public fixed char deviceId[128];
        public fixed char deviceKey[128];
    }

    [LibraryImport("user32.dll", EntryPoint = "EnumDisplayDevicesW", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EnumDisplayDevices(string lpDevice, uint iDevNum, ref DisplayDevice lpDisplayDevice, uint dwFlags);

    [LibraryImport("user32.dll", EntryPoint = "EnumDisplaySettingsW", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EnumDisplaySettings(string lpszDeviceName, uint iModeNum, ref DevMode lpDevMode);

    /// <summary>
    /// Returns display information using desktop (logical) coordinates for position and
    /// physical pixel dimensions for size, matching the layout in Windows Settings.
    /// </summary>
    public static unsafe IEnumerable<ScreenInfo> GetScreensForPanel()
    {
        List<ScreenInfo> result = [];
        var dd = new DisplayDevice { cb = (uint)sizeof(DisplayDevice) };
        uint i = 0;

        while (EnumDisplayDevices(null, i++, ref dd, 0))
        {
            if ((dd.stateFlags & DisplayDeviceActive) != 0)
            {
                var dm = new DevMode { dmSize = (ushort)sizeof(DevMode) };
                var name = new string(dd.deviceName);

                if (EnumDisplaySettings(name, EnumCurrentSettings, ref dm))
                {
                    result.Add(new ScreenInfo
                    {
                        Name = name,
                        Bounds = new Rectangle(dm.dmPositionX, dm.dmPositionY, (int)dm.dmPelsWidth, (int)dm.dmPelsHeight),
                        IsPrimary = (dd.stateFlags & DisplayDevicePrimaryDevice) != 0
                    });
                }
            }

            dd = new DisplayDevice { cb = (uint)sizeof(DisplayDevice) };
        }

        return result;
    }
}
