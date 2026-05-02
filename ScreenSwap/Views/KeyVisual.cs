using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.Runtime.Versioning;
using System.Windows.Input;
using Windows.System;
using HotKeyVirtualKey = ScreenSwap.Windows.Utilities.HotKeys.VirtualKey;
using WinUiRectangle = Microsoft.UI.Xaml.Shapes.Rectangle;

namespace ScreenSwap.Views;

[SupportedOSPlatform("windows10.0.19041.0")]
public sealed partial class KeyVisual : UserControl
{
    private readonly Border keyBorder = new();
    private string currentText = string.Empty;

    public KeyVisual()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
        keyBorder.Background = UiResources.AccentBrush;
        keyBorder.BorderBrush = UiResources.AccentPressedBrush;
        keyBorder.BorderThickness = new Thickness(1);
        keyBorder.CornerRadius = new CornerRadius(5);
        keyBorder.HorizontalAlignment = HorizontalAlignment.Stretch;
        keyBorder.VerticalAlignment = VerticalAlignment.Stretch;
        Content = keyBorder;
        Loaded += (_, _) => ApplyTheme();
        ActualThemeChanged += (_, _) => ApplyTheme();
    }

    private void ApplyTheme()
    {
        keyBorder.Background = UiResources.ThemeBrushFor(this, "KeyVisualBackgroundBrush", ColorHelper.FromArgb(255, 0, 120, 212));
        if (!string.IsNullOrEmpty(currentText))
            SetKey(currentText);
    }

    public void SetKey(string text)
    {
        currentText = text;

        var h = Height > 0 ? Height : 32;
        var pad = h * 0.27;
        // Scale min-width by label length so text always fits comfortably
        MinWidth = text.Length switch
        {
            1     => h * 1.1,
            2 or 3 => h * 1.2,
            4     => h * 1.4,
            5     => h * 1.7,
            _     => h * 2.0,
        };
        keyBorder.Padding = new Thickness(pad, 0, pad, 0);

        var foreground = UiResources.ThemeBrushFor(this, "KeyVisualForegroundBrush", Colors.White);
        var fontSize = text.Length == 1 ? h * 0.42 : h * 0.33;
        keyBorder.Child = text switch
        {
            "Win" => CreateWindowsIcon(foreground, fontSize),
            "Shift" => new Viewbox
            {
                Height = fontSize,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Stretch = Stretch.Uniform,
                Child = new FontIcon
                {
                    Glyph = "",
                    FontSize = fontSize,
                    FontFamily = new FontFamily("Segoe Fluent Icons"),
                    Foreground = foreground,
                }
            },
            _ => new Viewbox
            {
                Height = fontSize,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Stretch = Stretch.Uniform,
                Child = new TextBlock
                {
                    Text = text,
                    FontSize = fontSize,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = foreground,
                }
            }
        };
    }

    private static Viewbox CreateWindowsIcon(Brush foreground, double size)
    {
        var canvas = new Canvas { Width = 13, Height = 13 };
        canvas.Children.Add(CreateLogoTile(0, 0, foreground));
        canvas.Children.Add(CreateLogoTile(7, 0, foreground));
        canvas.Children.Add(CreateLogoTile(0, 7, foreground));
        canvas.Children.Add(CreateLogoTile(7, 7, foreground));

        return new Viewbox
        {
            Height = size,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Stretch = Stretch.Uniform,
            Child = canvas,
        };
    }

    private static WinUiRectangle CreateLogoTile(double left, double top, Brush foreground)
    {
        var rect = new WinUiRectangle { Width = 6, Height = 6, Fill = foreground };
        Canvas.SetLeft(rect, left);
        Canvas.SetTop(rect, top);
        return rect;
    }

    public static string[] GetShortcutParts(VirtualKey key, ModifierKeys modifiers)
    {
        var parts = new System.Collections.Generic.List<string>();

        if ((modifiers & ModifierKeys.Control) != 0) parts.Add("Ctrl");
        if ((modifiers & ModifierKeys.Alt)     != 0) parts.Add("Alt");
        if ((modifiers & ModifierKeys.Shift)   != 0) parts.Add("Shift");
        if ((modifiers & ModifierKeys.Windows) != 0) parts.Add("Win");

        var hkKey = (HotKeyVirtualKey)(int)key;
        if (key != VirtualKey.None && !Windows.Utilities.HotKeys.HotKey.KeysToModifers.ContainsKey(hkKey))
            parts.Add(new Windows.Utilities.HotKeys.HotKey { Key = hkKey }.ToStringKey());

        return [.. parts];
    }
}
