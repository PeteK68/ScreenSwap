using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using ScreenSwap.Windows;
using ScreenSwap.Windows.Utilities.ScreenManager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using Windows.Foundation;
using DrawingRectangle = System.Drawing.Rectangle;

namespace ScreenSwap.Views;

[SupportedOSPlatform("windows10.0.19041.0")]
public sealed partial class MonitorPicker : Canvas
{
    public static readonly DependencyProperty MainMonitorNameProperty = DependencyProperty.Register(nameof(MainMonitorName), typeof(string), typeof(MonitorPicker), new PropertyMetadata(string.Empty, OnLayoutPropertyChanged));

    private readonly List<(ScreenInfo screen, Rect rect)> hitTargets = [];

    public event EventHandler<string> MonitorSelected;

    public string MainMonitorName
    {
        get => (string)GetValue(MainMonitorNameProperty);
        set => SetValue(MainMonitorNameProperty, value);
    }

    public MonitorPicker()
    {
        SizeChanged += (_, _) => Refresh();
        ActualThemeChanged += (_, _) => Refresh();
        Tapped += (_, e) =>
        {
            var point = e.GetPosition(this);
            var (screen, rect) = hitTargets.FirstOrDefault(target => target.rect.Contains(point));
            if (screen is not null)
                MonitorSelected?.Invoke(this, screen.Name);
        };
    }

    public void Refresh()
    {
        Children.Clear();
        hitTargets.Clear();

        var screens = GetScreens().OrderBy(s => s.Bounds.Left)
                                  .ThenBy(s => s.Bounds.Top)
                                  .ToList();

        if (screens.Count == 0 || ActualWidth <= 0 || ActualHeight <= 0)
            return;

        var left = screens.Min(s => s.Bounds.Left);
        var top = screens.Min(s => s.Bounds.Top);
        var width = screens.Max(s => s.Bounds.Right) - left;
        var height = screens.Max(s => s.Bounds.Bottom) - top;

        const double padding = 14;
        var scale = Math.Min((ActualWidth - (padding * 2)) / width, (ActualHeight - (padding * 2)) / height);
        var offsetX = (ActualWidth - (width * scale)) / 2;
        var offsetY = (ActualHeight - (height * scale)) / 2;
        var effectiveMain = !string.IsNullOrEmpty(MainMonitorName) ? MainMonitorName : screens.FirstOrDefault(s => s.IsPrimary)?.Name;

        foreach (var screen in screens)
        {
            var rect = new Rect(
                ((screen.Bounds.Left - left) * scale) + offsetX,
                ((screen.Bounds.Top - top) * scale) + offsetY,
                Math.Max(screen.Bounds.Width * scale, 42),
                Math.Max(screen.Bounds.Height * scale, 28));
            var isMain = screen.Name == effectiveMain;
            hitTargets.Add((screen, rect));

            var border = new Border
            {
                Width = rect.Width,
                Height = rect.Height,
                BorderThickness = new Thickness(isMain ? 3 : 1),
                CornerRadius = new CornerRadius(6),
                Background = isMain
                    ? UiResources.ThemeBrushFor(this, "MonitorPrimaryBackgroundBrush", Microsoft.UI.ColorHelper.FromArgb(255, 0, 120, 212))
                    : UiResources.ThemeBrushFor(this, "MonitorSecondaryBackgroundBrush", Microsoft.UI.Colors.White),
                BorderBrush = isMain
                    ? UiResources.ThemeBrushFor(this, "MonitorPrimaryBorderBrush", Microsoft.UI.ColorHelper.FromArgb(255, 0, 120, 212))
                    : UiResources.ThemeBrushFor(this, "MonitorSecondaryBorderBrush", Microsoft.UI.ColorHelper.FromArgb(255, 203, 211, 221)),
                Child = new TextBlock
                {
                    Text = GetDisplayNumber(screen.Name).ToString(),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = isMain
                        ? UiResources.ThemeBrushFor(this, "MonitorPrimaryForegroundBrush", Microsoft.UI.Colors.White)
                        : UiResources.ThemeBrushFor(this, "MonitorSecondaryForegroundBrush", Microsoft.UI.ColorHelper.FromArgb(255, 17, 24, 39)),
                    FontWeight = isMain ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal
                }
            };
            SetLeft(border, rect.Left);
            SetTop(border, rect.Top);
            Children.Add(border);
        }
    }

    private static IEnumerable<ScreenInfo> GetScreens()
    {
        if (!global::Windows.ApplicationModel.DesignMode.DesignModeEnabled)
            return ScreenManager.GetScreensForPanel();

        return
        [
            new() { Name = @"\\.\DISPLAY3", Bounds = new DrawingRectangle(-1920, 0, 1920, 1080), WorkingArea = new DrawingRectangle(-1920, 0, 1920, 1040) },
            new() { Name = @"\\.\DISPLAY1", Bounds = new DrawingRectangle(0, 0, 1920, 1080), WorkingArea = new DrawingRectangle(0, 0, 1920, 1040), IsPrimary = true },
            new() { Name = @"\\.\DISPLAY2", Bounds = new DrawingRectangle(1920, 0, 1920, 1080), WorkingArea = new DrawingRectangle(1920, 0, 1920, 1040) }
        ];
    }

    private static void OnLayoutPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((MonitorPicker)d).Refresh();

    private static int GetDisplayNumber(string name)
    {
        var end = name.Length;
        var start = end;

        while (start > 0 && char.IsDigit(name[start - 1]))
            start--;

        return start < end && int.TryParse(name[start..end], out var num) ? num : 0;
    }
}
