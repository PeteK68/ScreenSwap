using System.Collections.Generic;
using System.Text;
using System.Windows.Input;

namespace ScreenSwap.Windows.Utilities.HotKeys;

public class HotKey
{
    public VirtualKey Key { get; set; }
    public ModifierKeys Modifiers { get; set; }

    public int Id => (int)Key + ((int)Modifiers * 0x10000);
    public uint VirtualKeyCode => (uint)Key;

    public static Dictionary<VirtualKey, ModifierKeys> KeysToModifers { get; } = new()
    {
        { VirtualKey.LeftWindows,  ModifierKeys.Windows },
        { VirtualKey.RightWindows, ModifierKeys.Windows },
        { VirtualKey.LeftMenu,     ModifierKeys.Alt     },
        { VirtualKey.RightMenu,    ModifierKeys.Alt     },
        { VirtualKey.LeftControl,  ModifierKeys.Control },
        { VirtualKey.RightControl, ModifierKeys.Control },
        { VirtualKey.LeftShift,    ModifierKeys.Shift   },
        { VirtualKey.RightShift,   ModifierKeys.Shift   },
    };

    public bool IsValid => !KeysToModifers.ContainsKey(Key) && Key != VirtualKey.None && Modifiers != ModifierKeys.None;

    public override string ToString() => ToStringModifiers() + ToStringKey();

    private static readonly Dictionary<VirtualKey, string> friendlyKeyNames = new()
    {
        { VirtualKey.Back,           "Bksp"    },
        { VirtualKey.Tab,            "Tab"     },
        { VirtualKey.Return,         "Enter"   },
        { VirtualKey.Escape,         "Esc"     },
        { VirtualKey.Space,          "Space"   },
        { VirtualKey.PageUp,         "PgUp"    },
        { VirtualKey.PageDown,       "PgDn"    },
        { VirtualKey.End,            "End"     },
        { VirtualKey.Home,           "Home"    },
        { VirtualKey.Left,           "←"       },
        { VirtualKey.Up,             "↑"       },
        { VirtualKey.Right,          "→"       },
        { VirtualKey.Down,           "↓"       },
        { VirtualKey.Insert,         "Ins"     },
        { VirtualKey.Delete,         "Del"     },
        { VirtualKey.Number0,        "0"       },
        { VirtualKey.Number1,        "1"       },
        { VirtualKey.Number2,        "2"       },
        { VirtualKey.Number3,        "3"       },
        { VirtualKey.Number4,        "4"       },
        { VirtualKey.Number5,        "5"       },
        { VirtualKey.Number6,        "6"       },
        { VirtualKey.Number7,        "7"       },
        { VirtualKey.Number8,        "8"       },
        { VirtualKey.Number9,        "9"       },
        { VirtualKey.NumberPad0,     "Num 0"   },
        { VirtualKey.NumberPad1,     "Num 1"   },
        { VirtualKey.NumberPad2,     "Num 2"   },
        { VirtualKey.NumberPad3,     "Num 3"   },
        { VirtualKey.NumberPad4,     "Num 4"   },
        { VirtualKey.NumberPad5,     "Num 5"   },
        { VirtualKey.NumberPad6,     "Num 6"   },
        { VirtualKey.NumberPad7,     "Num 7"   },
        { VirtualKey.NumberPad8,     "Num 8"   },
        { VirtualKey.NumberPad9,     "Num 9"   },
        { VirtualKey.Multiply,       "Num *"   },
        { VirtualKey.Add,            "Num +"   },
        { VirtualKey.Subtract,       "Num -"   },
        { VirtualKey.Decimal,        "Num ."   },
        { VirtualKey.Divide,         "Num /"   },
        { VirtualKey.F1,             "F1"      },
        { VirtualKey.F2,             "F2"      },
        { VirtualKey.F3,             "F3"      },
        { VirtualKey.F4,             "F4"      },
        { VirtualKey.F5,             "F5"      },
        { VirtualKey.F6,             "F6"      },
        { VirtualKey.F7,             "F7"      },
        { VirtualKey.F8,             "F8"      },
        { VirtualKey.F9,             "F9"      },
        { VirtualKey.F10,            "F10"     },
        { VirtualKey.F11,            "F11"     },
        { VirtualKey.F12,            "F12"     },
        { (VirtualKey)0xAD,          "Mute"    },
        { (VirtualKey)0xAE,          "Vol -"   },
        { (VirtualKey)0xAF,          "Vol +"   },
        { (VirtualKey)0xB0,          "Next"    },
        { (VirtualKey)0xB1,          "Prev"    },
        { (VirtualKey)0xB2,          "Stop"    },
        { (VirtualKey)0xB3,          "Play"    },
        { (VirtualKey)0x2C,          "PrtScn"  },
        { VirtualKey.Semicolon,      ";"       },
        { VirtualKey.Equal,          "="       },
        { VirtualKey.Comma,          ","       },
        { VirtualKey.Minus,          "-"       },
        { VirtualKey.Period,         "."       },
        { VirtualKey.Slash,          "/"       },
        { VirtualKey.GraveAccent,    "`"       },
        { VirtualKey.OpenBracket,    "["       },
        { VirtualKey.BackSlash,      "\\"      },
        { VirtualKey.CloseBracket,   "]"       },
        { VirtualKey.Quote,          "'"       },
    };

    public string ToStringKey()
    {
        if (Key == VirtualKey.None) return string.Empty;
        if (friendlyKeyNames.TryGetValue(Key, out var friendly)) return friendly;
        return Key.ToString();
    }

    public string ToStringModifiers()
    {
        var sb = new StringBuilder();
        AppendModifier(sb, ModifierKeys.Control, "Ctrl");
        AppendModifier(sb, ModifierKeys.Alt,     "Alt");
        AppendModifier(sb, ModifierKeys.Shift,   "Shift");
        AppendModifier(sb, ModifierKeys.Windows, "Win");
        return sb.ToString();
    }

    private void AppendModifier(StringBuilder sb, ModifierKeys flag, string label)
    {
        if ((Modifiers & flag) != 0)
            sb.Append(label).Append(" + ");
    }
}
