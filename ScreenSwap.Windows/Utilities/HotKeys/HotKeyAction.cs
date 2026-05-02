using System;

namespace ScreenSwap.Windows.Utilities.HotKeys;

public class HotKeyAction : HotKey
{
    public Action<HotKeyAction> OnHotKeyActivated { get; set; }
    public Action<HotKeyAction> OnHotKeyRepeated { get; set; }
    public Action<HotKeyAction> OnHotKeyTerminated { get; set; }
}
