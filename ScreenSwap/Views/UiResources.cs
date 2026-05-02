using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System.Runtime.Versioning;
using Windows.UI;

namespace ScreenSwap.Views;

[SupportedOSPlatform("windows10.0.19041.0")]
internal static class UiResources
{
    public static Brush Brush(string key, Color fallback)
        => Application.Current.Resources.TryGetValue(key, out var value) && value is Brush brush
            ? brush
            : new SolidColorBrush(fallback);

    public static Style Style(string key)
        => Application.Current.Resources.TryGetValue(key, out var value) && value is Style style
            ? style
            : null;

    public static Brush TextPrimaryBrushFor(FrameworkElement element)
        => ThemeBrushFor(
            element,
            "TextFillColorPrimaryBrush",
            element.ActualTheme == ElementTheme.Dark ? Colors.White : Color.FromArgb(255, 32, 32, 32));

    public static Brush TextSecondaryBrushFor(FrameworkElement element)
        => ThemeBrushFor(
            element,
            "TextFillColorSecondaryBrush",
            element.ActualTheme == ElementTheme.Dark ? Color.FromArgb(255, 200, 200, 200) : Color.FromArgb(255, 96, 96, 96));

    public static Brush CardBackgroundBrushFor(FrameworkElement element)
        => ThemeBrushFor(
            element,
            "CardBackgroundFillColorDefaultBrush",
            element.ActualTheme == ElementTheme.Dark ? Color.FromArgb(255, 48, 48, 48) : Colors.White);

    public static Brush CardStrokeBrushFor(FrameworkElement element)
        => ThemeBrushFor(
            element,
            "CardStrokeColorDefaultBrush",
            element.ActualTheme == ElementTheme.Dark ? Color.FromArgb(255, 48, 48, 48) : Color.FromArgb(255, 210, 218, 225));

    public static Brush ThemeBrushFor(FrameworkElement element, string key, Color fallback)
        => TryGetThemeBrush(Application.Current.Resources, key, ThemeKeyFor(element), out var brush)
            ? brush
            : new SolidColorBrush(fallback);

    private static string ThemeKeyFor(FrameworkElement element)
        => (element.RequestedTheme == ElementTheme.Default ? element.ActualTheme : element.RequestedTheme) == ElementTheme.Dark
            ? "Dark"
            : "Light";

    private static bool TryGetThemeBrush(ResourceDictionary dictionary, string key, string themeKey, out Brush brush)
    {
        if (dictionary.ThemeDictionaries.TryGetValue(themeKey, out var themeDictionaryValue)
            && themeDictionaryValue is ResourceDictionary themeDictionary
            && themeDictionary.TryGetValue(key, out var themedValue)
            && themedValue is Brush themedBrush)
        {
            brush = themedBrush;
            return true;
        }

        foreach (var mergedDictionary in dictionary.MergedDictionaries)
        {
            if (TryGetThemeBrush(mergedDictionary, key, themeKey, out brush))
                return true;
        }

        brush = null;
        return false;
    }

    public static Brush AccentBrush => Brush("AccentFillColorDefaultBrush", Color.FromArgb(255, 0, 120, 212));
    public static Brush AccentPressedBrush => Brush("AccentFillColorTertiaryBrush", Color.FromArgb(255, 0, 92, 158));
    public static Brush CardBackgroundBrush => Brush("CardBackgroundFillColorDefaultBrush", Colors.White);
    public static Brush CardStrokeBrush => Brush("CardStrokeColorDefaultBrush", Color.FromArgb(255, 210, 218, 225));
    public static Brush TextPrimaryBrush => Brush("TextFillColorPrimaryBrush", Color.FromArgb(255, 32, 32, 32));
    public static Brush TextSecondaryBrush => Brush("TextFillColorSecondaryBrush", Color.FromArgb(255, 96, 96, 96));
    public static Brush DangerBrush => Brush("SystemFillColorCriticalBrush", Color.FromArgb(255, 196, 43, 28));
}
