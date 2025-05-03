using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using QuickType.Model.IPC;
using QuickType.Model.Trie;
using QuickType.Model;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics;
using Windows.Win32;
using Windows.Win32.UI.WindowsAndMessaging;
using WinUIEx;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace QuickType.WinUI;
/// <summary>
/// An empty window that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class SuggestionsWindow : WindowEx
{
    private const int PADDING_X = 15;
    private const int PADDING_Y = 10;

    public SuggestionsWindow()
    {
        this.InitializeComponent();
        IsResizable = false;
        IsShownInSwitchers = false;
        IsTitleBarVisible = false;
        IsAlwaysOnTop = true;
        AppWindow.TitleBar.BackgroundColor = Colors.Transparent;
        AppWindow.Closing += AppWindow_Closing;
    }

    private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        args.Cancel = true;
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        PInvoke.ShowWindow((Windows.Win32.Foundation.HWND)hwnd, SHOW_WINDOW_CMD.SW_HIDE);
    }

    private void UpdateSuggestions(params List<Word> suggestions)
    {
        SuggestionsStackPanel.Children.Clear();

        foreach (var suggestion in suggestions)
        {
            var completeButton = new Button
            {
                Name = $"SuggestionButton{SuggestionsStackPanel.Children.Count}",
                Content = $"{suggestion.word}",
                Margin = new Thickness(5),
                Padding = new Thickness(3),
                FontSize = 14,
                Background = new SolidColorBrush(Colors.Transparent),
                BorderBrush = new SolidColorBrush(Colors.Transparent),
                Foreground = new SolidColorBrush(Colors.White)
            };
            completeButton.Click += async (s, e) =>
            {
                await App.Current.SendSelectionMessageAsync((int)char.GetNumericValue(completeButton.Name[^1]));

                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                PInvoke.ShowWindow((Windows.Win32.Foundation.HWND)hwnd, SHOW_WINDOW_CMD.SW_HIDE);
            };
            SuggestionsStackPanel.Children.Add(completeButton);
        }

        SuggestionsStackPanel.UpdateLayout();
    }

    private void UpdateWindowPosAndSize(CaretRectangle caretRectangle)
    {
        SuggestionsStackPanel.UpdateLayout();

        var width = (int)SuggestionsStackPanel.Children.Sum(x => x.ActualSize.X) +
                        (SuggestionsStackPanel.Children.Count - 1) * PADDING_X;
        var height = (int)SuggestionsStackPanel.ActualHeight + PADDING_Y;

        AppWindow.Resize(new SizeInt32(width, height));

        var hMonitor = PInvoke.MonitorFromPoint(
            new System.Drawing.Point((int)caretRectangle.Left, (int)caretRectangle.Top),
            Windows.Win32.Graphics.Gdi.MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);

        var monitorInfo = new Windows.Win32.Graphics.Gdi.MONITORINFO();
        monitorInfo.cbSize = (uint)Marshal.SizeOf(monitorInfo);
        PInvoke.GetMonitorInfo(hMonitor, ref monitorInfo);

        var workArea = monitorInfo.rcWork;

        var x = Math.Max(workArea.left,
            Math.Min((int)caretRectangle.Left - (width / 2) + (int)(caretRectangle.Width / 2),
                workArea.right - width));

        var y = (int)caretRectangle.Top - height - 2;

        if (y < workArea.top)
        {
            y = (int)caretRectangle.Bottom + 2;
        }

        AppWindow.Move(new PointInt32(x, y));

    }

    public void HandleSuggestionMessage(SuggestionMessage suggestionMessage)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        
        if (suggestionMessage.Suggestions.Count == 0 ||
            suggestionMessage.CaretPosition is { Bottom: 0, Height: 0, Left: 0, Right: 0, Top: 0, Width: 0 } or null)
        {
            PInvoke.ShowWindow((Windows.Win32.Foundation.HWND)hwnd, SHOW_WINDOW_CMD.SW_HIDE);
            return;
        }

        PInvoke.ShowWindow((Windows.Win32.Foundation.HWND)hwnd, SHOW_WINDOW_CMD.SW_SHOWNOACTIVATE);

        UpdateSuggestions(suggestionMessage.Suggestions);
        UpdateWindowPosAndSize(suggestionMessage.CaretPosition.Value);

    }
}
