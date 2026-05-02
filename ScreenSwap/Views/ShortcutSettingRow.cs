using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Runtime.Versioning;
using System.Windows.Input;
using Windows.System;

namespace ScreenSwap.Views;

[SupportedOSPlatform("windows10.0.19041.0")]
public sealed partial class ShortcutSettingRow : ContentControl
{
    private readonly Border rowBorder = new();
    private readonly StackPanel keyPanel = new() { Orientation = Orientation.Horizontal, Spacing = 5, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
    private readonly Button assignButton = new() { Content = "+  Assign shortcut", VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
    private readonly FontIcon editIcon = new() { Glyph = "\uE70F", FontFamily = new FontFamily("Segoe Fluent Icons"), FontSize = 12 };
    private readonly Button editButton;
    private FontIcon rowIcon;
    private TextBlock titleBlock;
    private TextBlock descriptionBlock;

    public event EventHandler EditRequested;

    public string Title { get; set; }
    public string Description { get; set; }
    public ShortcutKind IconKind { get; set; } = ShortcutKind.Window;

    public ShortcutSettingRow()
    {
        editButton = new Button
        {
            Width = 14,
            Height = 14,
            Padding = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Center,
            Background = new SolidColorBrush(Colors.Transparent),
            BorderThickness = new Thickness(0),
            Content = editIcon
        };

        HorizontalAlignment = HorizontalAlignment.Stretch;
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        Loaded += (_, _) => Build();
        ActualThemeChanged += (_, _) => ApplyTheme();
    }

    public void SetShortcut(VirtualKey key, ModifierKeys modifiers)
    {
        keyPanel.Children.Clear();
        var parts = KeyVisual.GetShortcutParts(key, modifiers);
        var hasShortcut = parts.Length > 0;
        keyPanel.Visibility = hasShortcut ? Visibility.Visible : Visibility.Collapsed;
        assignButton.Visibility = hasShortcut ? Visibility.Collapsed : Visibility.Visible;
        editButton.Visibility = hasShortcut ? Visibility.Visible : Visibility.Collapsed;

        foreach (var part in parts)
        {
            var visual = new KeyVisual { Height = 40 };
            visual.SetKey(part);
            keyPanel.Children.Add(visual);
        }

        keyPanel.Children.Add(editButton);
    }

    private void Build()
    {
        if (Content is not null)
            return;

        assignButton.Click += (_, _) => EditRequested?.Invoke(this, EventArgs.Empty);
        editButton.Click += (_, _) => EditRequested?.Invoke(this, EventArgs.Empty);

        rowBorder.BorderThickness = new Thickness(1);
        rowBorder.CornerRadius = new CornerRadius(8);
        rowBorder.Padding = new Thickness(16, 12, 16, 12);
        rowBorder.MinHeight = 70;
        rowBorder.HorizontalAlignment = HorizontalAlignment.Stretch;
        rowBorder.Child = new Grid
        {
            ColumnSpacing = 12,
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto },
            },
            Children =
            {
                CreateIcon(),
                CreateTextBlock(),
                CreateShortcutHost(),
            }
        };

        ApplyTheme();
        Content = rowBorder;
    }

    private void ApplyTheme()
    {
        rowBorder.Background = UiResources.CardBackgroundBrushFor(this);
        rowBorder.BorderBrush = UiResources.CardStrokeBrushFor(this);
        editIcon.Foreground = UiResources.TextPrimaryBrushFor(this);
        _ = (rowIcon?.Foreground = UiResources.TextPrimaryBrushFor(this));
        _ = (titleBlock?.Foreground = UiResources.TextPrimaryBrushFor(this));
        _ = (descriptionBlock?.Foreground = UiResources.TextSecondaryBrushFor(this));
    }

    private Viewbox CreateIcon()
    {
        var glyph = IconKind == ShortcutKind.Window ? "\uEB3B" : "\uE7F4";
        rowIcon = new FontIcon
        {
            Glyph = glyph,
            FontFamily = new FontFamily("Segoe Fluent Icons"),
            FontSize = 22,
            Width = 32,
            Height = 32
        };

        return new Viewbox { Width = 32, Height = 32, Child = rowIcon, VerticalAlignment = VerticalAlignment.Center };
    }

    private StackPanel CreateTextBlock()
    {
        var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        titleBlock = new TextBlock { Text = Title, Style = UiResources.Style("BodyStrongTextBlockStyle"), FontSize = 16 };
        descriptionBlock = new TextBlock { Text = Description, Margin = new Thickness(0, 3, 0, 0), TextWrapping = TextWrapping.Wrap };
        stack.Children.Add(titleBlock);
        stack.Children.Add(descriptionBlock);
        Grid.SetColumn(stack, 1);

        return stack;
    }

    private Grid CreateShortcutHost()
    {
        var grid = new Grid { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, };

        grid.Children.Add(keyPanel);
        grid.Children.Add(assignButton);

        Grid.SetColumn(grid, 2);

        return grid;
    }

    public enum ShortcutKind
    {
        Monitor,
        Window,
    }
}
