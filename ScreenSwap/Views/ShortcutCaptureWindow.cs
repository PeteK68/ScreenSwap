using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using ScreenSwap.Windows.Utilities.HotKeys;
using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using System.Windows.Input;
using HotKeyVirtualKey = ScreenSwap.Windows.Utilities.HotKeys.VirtualKey;
using VirtualKey = Windows.System.VirtualKey;

namespace ScreenSwap.Views;

[SupportedOSPlatform("windows10.0.19041.0")]
public sealed partial class ShortcutCaptureWindow
{
    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;

    private readonly ContentDialog dialog;
    private readonly Border capturePanel = new();
    private readonly StackPanel keyPanel = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Spacing = 12 };
    private readonly TextBlock message = new() { HorizontalAlignment = HorizontalAlignment.Center, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
    private readonly LowLevelKeyboardProc keyboardProc;
    private readonly HotKey original;
    private readonly HotKey[] conflictingHotKeys;
    private HotKey current;
    private ModifierKeys activeModifiers;
    private IntPtr keyboardHook;
    private IntPtr ownerWindowHandle;
    private bool locked;

    public ShortcutCaptureWindow(string title, HotKey hotKey, params HotKey[] conflictingHotKeys)
    {
        original = new HotKey { Key = hotKey.Key, Modifiers = hotKey.Modifiers };
        current = new HotKey { Key = hotKey.Key, Modifiers = hotKey.Modifiers };
        this.conflictingHotKeys = conflictingHotKeys;
        keyboardProc = KeyboardHookCallback;

        dialog = new ContentDialog
        {
            Title = title,
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            Content = BuildContent(),
            MinWidth = 0,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            FullSizeDesired = false
        };

        dialog.PrimaryButtonClick += Dialog_PrimaryButtonClick;
        dialog.KeyDown += OnKeyDown;
        dialog.KeyUp += OnKeyUp;
        dialog.ProcessKeyboardAccelerators += OnProcessKeyboardAccelerators;
    }

    public async Task<HotKey> ShowAsync(XamlRoot xamlRoot, IntPtr ownerHandle)
    {
        dialog.XamlRoot = xamlRoot;
        ownerWindowHandle = ownerHandle;
        dialog.RequestedTheme = xamlRoot.Content is FrameworkElement root ? root.ActualTheme : ElementTheme.Default;
        ApplyTheme();
        Render();
        UpdateDialogLayout(xamlRoot);

        xamlRoot.Changed += OnXamlRootChanged;
        InstallKeyboardHook();
        try
        {
            var result = await dialog.ShowAsync();
            return result == ContentDialogResult.Primary ? current : null;
        }
        finally
        {
            UninstallKeyboardHook();
            xamlRoot.Changed -= OnXamlRootChanged;
        }

        void OnXamlRootChanged(XamlRoot sender, XamlRootChangedEventArgs args) => UpdateDialogLayout(sender);
    }

    private void UpdateDialogLayout(XamlRoot xamlRoot)
    {
        var width = Math.Clamp(xamlRoot.Size.Width - 96, 400, 560);
        dialog.Width = width;
        dialog.MaxWidth = width;
        dialog.Margin = new Thickness(Math.Max(0, (xamlRoot.Size.Width - width) / 2), 0, 0, 0);
        dialog.Resources["ContentDialogMinWidth"] = 0d;
        dialog.Resources["ContentDialogMaxWidth"] = width;
    }

    private StackPanel BuildContent()
    {
        var root = new StackPanel { Spacing = 16, HorizontalAlignment = HorizontalAlignment.Stretch };
        root.Children.Add(new TextBlock
        {
            Text = "A shortcut should start with Windows key, Ctrl, Alt or Shift.",
            FontSize = 14,
            TextWrapping = TextWrapping.Wrap,
        });

        capturePanel.CornerRadius = new CornerRadius(10);
        capturePanel.Padding = new Thickness(16);
        capturePanel.MinHeight = 96;
        capturePanel.HorizontalAlignment = HorizontalAlignment.Stretch;
        capturePanel.Child = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 10,
            Children =
            {
                keyPanel,
                message
            }
        };
        root.Children.Add(capturePanel);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Spacing = 24 };
        actions.Children.Add(CreateActionButton("", "Reset", (_, _) =>
        {
            current = new HotKey { Key = original.Key, Modifiers = original.Modifiers };
            locked = false;
            Render();
        }));

        actions.Children.Add(CreateActionButton("", "Clear", (_, _) =>
        {
            current = new HotKey();
            locked = false;
            Render();
        }));

        root.Children.Add(actions);

        return root;
    }

    private void ApplyTheme() => capturePanel.Background = UiResources.CardBackgroundBrushFor(dialog);

    private void Dialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var isCleared = current.Key == HotKeyVirtualKey.None && current.Modifiers == ModifierKeys.None;
        if ((current.IsValid && GlobalHotkeyConflictManager.Check(current, conflictingHotKeys) == HotKeyConflict.None) || isCleared)
            return;

        args.Cancel = true;
        if (!current.IsValid)
            ShowError("Press a shortcut with at least one modifier and one non-modifier key.");
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        e.Handled = true;
        if (keyboardHook != IntPtr.Zero)
            return;

        UpdateModifierState(e.OriginalKey, isDown: true);
        CaptureKey(e.OriginalKey);
    }

    private void OnKeyUp(object sender, KeyRoutedEventArgs e)
    {
        e.Handled = true;
        if (keyboardHook != IntPtr.Zero)
            return;

        UpdateModifierState(e.OriginalKey, isDown: false);
        if (CurrentModifiers() == ModifierKeys.None)
            locked = false;
    }

    private void OnProcessKeyboardAccelerators(UIElement sender, ProcessKeyboardAcceleratorEventArgs args)
    {
        args.Handled = true;
        if (keyboardHook != IntPtr.Zero)
            return;

        CaptureKey(args.Key);
    }

    private IntPtr KeyboardHookCallback(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code < 0)
            return CallNextHookEx(keyboardHook, code, wParam, lParam);

        if (!IsOwnerWindowForeground())
            return CallNextHookEx(keyboardHook, code, wParam, lParam);

        var messageId = wParam.ToInt32();
        var keyboardInfo = Marshal.PtrToStructure<KbdLlHookStruct>(lParam);
        var key = (VirtualKey)keyboardInfo.VirtualKeyCode;

        if (messageId is WmKeyDown or WmSysKeyDown)
        {
            UpdateModifierState(key, isDown: true);
            CaptureKey(key);
            return 1;
        }

        if (messageId is WmKeyUp or WmSysKeyUp)
        {
            UpdateModifierState(key, isDown: false);
            if (CurrentModifiers() == ModifierKeys.None)
                locked = false;

            return 1;
        }

        return CallNextHookEx(keyboardHook, code, wParam, lParam);
    }

    private bool IsOwnerWindowForeground()
        => ownerWindowHandle != IntPtr.Zero && GetForegroundWindow() == ownerWindowHandle;

    private void CaptureKey(VirtualKey key)
    {
        var modifiers = CurrentModifiers();

        // If modifiers are still held and this is a new non-modifier key, allow recapture
        if (locked && ModifierFor(key) == ModifierKeys.None && modifiers != ModifierKeys.None)
            locked = false;

        if (locked)
            return;

        current = new HotKey { Key = (HotKeyVirtualKey)(int)key, Modifiers = modifiers };
        locked = current.IsValid;

        Render();
    }

    private void Render()
    {
        keyPanel.Children.Clear();

        var parts = KeyVisual.GetShortcutParts((VirtualKey)(int)current.Key, current.Modifiers);

        if (parts.Length == 0)
        {
            keyPanel.Children.Add(new TextBlock
            {
                Text = "...",
                FontSize = 28,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = UiResources.ThemeBrushFor(dialog, "TextFillColorSecondaryBrush", Microsoft.UI.ColorHelper.FromArgb(255, 160, 160, 160)),
            });
        }
        else
        {
            foreach (var part in parts)
            {
                var visual = new KeyVisual
                {
                    Height = 52,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                visual.SetKey(part);
                keyPanel.Children.Add(visual);
            }
        }

        var conflict = GlobalHotkeyConflictManager.Check(current, conflictingHotKeys);
        if (conflict == HotKeyConflict.ScreenSwapShortcut)
            ShowError("This shortcut is already assigned in ScreenSwap.");
        else if (conflict == HotKeyConflict.UnavailableGlobalShortcut)
            ShowError("This shortcut is already in use by Windows or another app.");
        else if (current.IsValid)
            ShowInfo("Shortcut ready.");
        else if (current.Key == HotKeyVirtualKey.None && current.Modifiers == ModifierKeys.None)
            ShowInfo("Shortcut cleared.");
        else if (current.Modifiers != ModifierKeys.None)
            ShowError("Press one non-modifier key to complete the shortcut.");
        else
            ShowInfo("Press a shortcut.");
    }

    private static Button CreateActionButton(string icon, string text, RoutedEventHandler click)
    {
        var button = new Button
        {
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(6, 4, 6, 4),
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                Children =
                {
                    new FontIcon { Glyph = icon, FontFamily = new FontFamily("Segoe Fluent Icons"), FontSize = 14 },
                    new TextBlock { Text = text, FontSize = 14 }
                }
            }
        };

        button.Loaded += (_, _) => ApplyActionButtonTheme(button);
        button.ActualThemeChanged += (_, _) => ApplyActionButtonTheme(button);
        button.Click += click;
        return button;
    }

    private static void ApplyActionButtonTheme(Button button) => button.Foreground = UiResources.ThemeBrushFor(button, "AccentFillColorDefaultBrush", Microsoft.UI.ColorHelper.FromArgb(255, 0, 120, 212));

    private void ShowInfo(string text)
    {
        message.Text = text;
        message.Foreground = UiResources.ThemeBrushFor(dialog, "AccentFillColorDefaultBrush", Microsoft.UI.ColorHelper.FromArgb(255, 0, 120, 212));
    }

    private void ShowError(string text)
    {
        message.Text = text;
        message.Foreground = UiResources.ThemeBrushFor(dialog, "SystemFillColorCriticalBrush", Microsoft.UI.ColorHelper.FromArgb(255, 196, 43, 28));
    }

    private ModifierKeys CurrentModifiers() => activeModifiers | GetCurrentThreadModifiers();

    private void UpdateModifierState(VirtualKey key, bool isDown)
    {
        var modifier = ModifierFor(key);
        if (modifier == ModifierKeys.None)
            return;

        if (isDown)
            activeModifiers |= modifier;
        else
            activeModifiers &= ~modifier;
    }

    private static ModifierKeys ModifierFor(VirtualKey key) => key switch
    {
        VirtualKey.Control or VirtualKey.LeftControl or VirtualKey.RightControl => ModifierKeys.Control,
        VirtualKey.Menu    or VirtualKey.LeftMenu    or VirtualKey.RightMenu    => ModifierKeys.Alt,
        VirtualKey.Shift   or VirtualKey.LeftShift   or VirtualKey.RightShift   => ModifierKeys.Shift,
        VirtualKey.LeftWindows or VirtualKey.RightWindows                        => ModifierKeys.Windows,
        _ => ModifierKeys.None
    };

    private static ModifierKeys GetCurrentThreadModifiers()
    {
        var modifiers = ModifierKeys.None;

        if (IsDown(VirtualKey.Control) || IsDown(VirtualKey.LeftControl) || IsDown(VirtualKey.RightControl)) modifiers |= ModifierKeys.Control;
        if (IsDown(VirtualKey.Menu)    || IsDown(VirtualKey.LeftMenu)    || IsDown(VirtualKey.RightMenu))    modifiers |= ModifierKeys.Alt;
        if (IsDown(VirtualKey.Shift)   || IsDown(VirtualKey.LeftShift)   || IsDown(VirtualKey.RightShift))   modifiers |= ModifierKeys.Shift;
        if (IsDown(VirtualKey.LeftWindows) || IsDown(VirtualKey.RightWindows))                                modifiers |= ModifierKeys.Windows;

        return modifiers;
    }

    private static bool IsDown(VirtualKey key)
        => (InputKeyboardSource.GetKeyStateForCurrentThread(key) & global::Windows.UI.Core.CoreVirtualKeyStates.Down) != 0;

    private void InstallKeyboardHook()
    {
        if (keyboardHook != IntPtr.Zero)
            return;

        keyboardHook = SetWindowsHookEx(WhKeyboardLl, keyboardProc, IntPtr.Zero, 0);
    }

    private void UninstallKeyboardHook()
    {
        if (keyboardHook == IntPtr.Zero)
            return;

        _ = UnhookWindowsHookEx(keyboardHook);
        keyboardHook = IntPtr.Zero;
    }

    private delegate IntPtr LowLevelKeyboardProc(int code, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct KbdLlHookStruct
    {
        public uint VirtualKeyCode;
        public uint ScanCode;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [LibraryImport("user32.dll", EntryPoint = "SetWindowsHookExW", SetLastError = true)]
    private static partial IntPtr SetWindowsHookEx(int hookId, LowLevelKeyboardProc callback, IntPtr moduleHandle, uint threadId);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnhookWindowsHookEx(IntPtr hookHandle);

    [LibraryImport("user32.dll")]
    private static partial IntPtr CallNextHookEx(IntPtr hookHandle, int code, IntPtr wParam, IntPtr lParam);

    [LibraryImport("user32.dll")]
    private static partial IntPtr GetForegroundWindow();
}
