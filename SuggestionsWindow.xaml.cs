using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using QuickType.Controller;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Windows.Graphics;
using Windows.Win32;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace QuickType
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class SuggestionsWindow : WinUIEx.WindowEx
    {
        private const int PADDING_X = 20;
        private const int PADDING_Y = 10;

        public SuggestionsWindow()
        {
            this.InitializeComponent();

            IsResizable = false;
            IsShownInSwitchers = false;
            IsTitleBarVisible = false;
            IsAlwaysOnTop = true;
            AppWindow.TitleBar.BackgroundColor = Colors.Transparent;
        }

        public void UpdateSuggestions(params List<string> suggestions)
        {
            SuggestionsStackPanel.Children.Clear();

            if (suggestions == null || suggestions.Count == 0)
            {
                AppWindow.Hide();
                return;
            }


            foreach (var suggestion in suggestions)
            {
                var completeButton = new Button
                {
                    Name = $"SuggestionButton{SuggestionsStackPanel.Children.Count}",
                    Content = suggestion,
                    Margin = new Thickness(5),
                    Padding = new Thickness(3),
                    FontSize = 14,
                    Background = new SolidColorBrush(Colors.Transparent),
                    BorderBrush = new SolidColorBrush(Colors.Transparent),
                    Foreground = new SolidColorBrush(Colors.White)
                };
                completeButton.Click += (s, e) =>
                {
                    AppWindow.Hide();
                    InputSimulator.SimulateInputString(suggestion.RemoveFirst(MainWindow.CurrentBuffer));
                };
                SuggestionsStackPanel.Children.Add(completeButton);
            }

            SuggestionsStackPanel.UpdateLayout();
        }

        public void UpdateWindowPosAndSize(CaretFinder.CaretRectangle caretRectangle)
        {
            SuggestionsStackPanel.UpdateLayout();

            var width = (int)SuggestionsStackPanel.ActualWidth + PADDING_X;
            var height = (int)SuggestionsStackPanel.ActualHeight + PADDING_Y;

            AppWindow.Resize(new SizeInt32(width, height));

            var hMonitor = PInvoke.MonitorFromPoint(
                new System.Drawing.Point((int)caretRectangle.Left, (int)caretRectangle.Top),
                Windows.Win32.Graphics.Gdi.MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);

            var monitorInfo = new Windows.Win32.Graphics.Gdi.MONITORINFO();
            monitorInfo.cbSize = (uint)Marshal.SizeOf(monitorInfo);
            PInvoke.GetMonitorInfo(hMonitor, ref monitorInfo);

            var workArea = monitorInfo.rcWork;

            var x = Math.Max(workArea.left, Math.Min((int)caretRectangle.Left - (width / 2) + (int)(caretRectangle.Width / 2), workArea.right - width));

            var y = (int)caretRectangle.Top - height - 2;

            if (y < workArea.top)
            {
                y = (int)caretRectangle.Bottom + 2;
            }

            AppWindow.Move(new PointInt32(x, y));
        }

    }
}
