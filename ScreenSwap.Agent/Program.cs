using ScreenSwap.Windows.Utilities.BorderDrawing;
using System;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace ScreenSwap.Agent;

[SupportedOSPlatform("windows6.1")]
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        BorderDrawer.Initialize(new WindowsFormsSynchronizationContext());

        using var context = new AgentApplicationContext();
        Application.Run(context);
    }
}
