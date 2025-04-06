using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics;
using Microsoft.UI;
using QuickType.Controller;
using Microsoft.UI.Windowing;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace QuickType
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class SuggestionsWindow : WinUIEx.WindowEx
    {
        public SuggestionsWindow()
        {
            this.InitializeComponent();

            IsResizable = false;
            IsShownInSwitchers = false;
            IsTitleBarVisible = false;
            IsAlwaysOnTop = false;

        }

        public void UpdateSuggestions(params List<string> suggestions)
        {
            SuggestionsStackPanel.Children.Clear();
            foreach (var suggestion in suggestions)
            {
                var textBlock = new TextBlock
                {
                    Name = $"SuggestionTextBlock{SuggestionsStackPanel.Children.Count}",
                    Text = suggestion
                };
                SuggestionsStackPanel.Children.Add(textBlock);
            }

            SuggestionsStackPanel.UpdateLayout();
        }

        public void UpdateWindowPosAndSize(CaretFinder.CaretRectangle caretRectangle)
        {
            SuggestionsStackPanel.UpdateLayout();

            var width = Math.Max(200, (int)SuggestionsStackPanel.ActualWidth);
            var height = Math.Max(50, (int)SuggestionsStackPanel.ActualHeight);

            AppWindow.Resize(new SizeInt32(width, height));

            var rect = new PointInt32()
            {
                X = (int)(caretRectangle.Left + (caretRectangle.Width / 2) - (SuggestionsStackPanel.ActualWidth / 2)),
                Y = (int)(caretRectangle.Top - SuggestionsStackPanel.ActualHeight)
            };

            AppWindow.Move(rect);
        }
    }
}
