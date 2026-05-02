using System;
using System.Drawing;

namespace ScreenSwap.Windows;

public class ScreenInfo : IEquatable<ScreenInfo>
{
    public string Name { get; set; }
    public Rectangle Bounds { get; set; }
    public Rectangle WorkingArea { get; set; }
    public bool IsPrimary { get; set; }

    public bool Equals(ScreenInfo other) => other is not null && Name == other.Name;

    public override bool Equals(object obj) => Equals(obj as ScreenInfo);

    public override int GetHashCode() => Name?.GetHashCode() ?? 0;
}
