using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using QuickType.Controller;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Core;
using Microsoft.UI.Windowing;
using Windows.Win32;
using Windows.Win32.UI.WindowsAndMessaging;
using QuickType.Model.Trie;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace QuickType
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        public static string CurrentBuffer { get; set; } = "";

        public MainWindow()
        {
            this.InitializeComponent();
            KeyboardCapturer.KeyboardEvent += KeyboardCapturer_KeyboardEvent;
            this.Closed += Current_Closed;
        }

        private void Current_Closed(object sender, WindowEventArgs args)
        {
            App.Current.SuggestionsWindow?.Close();
        }

        private void KeyboardCapturer_KeyboardEvent(string str)
        {
            if (DispatcherQueue.HasThreadAccess)
            {
                EditTextInputLabelText(str);
            }
            else
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    EditTextInputLabelText(str);
                });
            }
        }

        private void EditTextInputLabelText(string str)
        {
            switch (str)
            {
                case "\b" when CurrentBuffer.Length <= 1:
                    CurrentBuffer = "";
                    break;
                case "\b":
                    CurrentBuffer = CurrentBuffer[..^1];
                    break;
                case "\r":
                case " ":
                case "\n":
                    CurrentBuffer = "";
                    break;
                default:
                    CurrentBuffer += str;
                    break;
            }

            TextInputLabel.Text = $"Current buffer: {CurrentBuffer}";

            if (CurrentBuffer.Length > 2 && !string.IsNullOrWhiteSpace(CurrentBuffer))
            {
                GetSuggestions();
            } 
            else
            {
                SuggestionsTextBlock.Text = "Too few chars (<2), or just whitespace!";
            }
    
            FindCaret();

        }

        private void GetSuggestions(int amount = 5)
        {

            List<Word> wordlist = App.Current.Language.SearchByPrefix(CurrentBuffer.ToLower(), amount);

            StringBuilder sb = new();
            if (wordlist.Count < 1)
            {
                SuggestionsTextBlock.Text = "This word is not recognised! Something went wrong?";
                return;
            }
            wordlist[..Math.Min(wordlist.Count, amount)].ForEach(word => sb.AppendLine($"{word.word} ({word.frequency})"));

            SuggestionsTextBlock.Text = sb.ToString();

            var topSuggestions = wordlist[..Math.Min(wordlist.Count, amount)].Select(word => word.word).ToList();
            
            var caretRectangle = CaretFinder.GetCaretPos();

            if (caretRectangle is not null) 
            {
                try
                {
                    App.Current.SuggestionsWindow ??= new();

                    App.Current.SuggestionsWindow.UpdateSuggestions(topSuggestions);

                    App.Current.SuggestionsWindow.UpdateWindowPosAndSize(caretRectangle.Value);
                    
                    var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.Current.SuggestionsWindow);
                    
                    PInvoke.ShowWindow((Windows.Win32.Foundation.HWND)hwnd, SHOW_WINDOW_CMD.SW_SHOWNOACTIVATE);
                    
                    PInvoke.SetWindowPos(
                        (Windows.Win32.Foundation.HWND)hwnd,
                        new Windows.Win32.Foundation.HWND(new IntPtr(-1)),
                        0, 0, 0, 0,
                        SET_WINDOW_POS_FLAGS.SWP_NOMOVE | 
                        SET_WINDOW_POS_FLAGS.SWP_NOSIZE | 
                        SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE
                    );
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error showing suggestions window: {ex.Message}");
                }
            }
        }

        private void FindCaret()
        {
            var caretRectangle = CaretFinder.GetCaretPos();
            CaretPosition.Text = caretRectangle is not null ? caretRectangle.ToString() : "Caret not found!";
        }
    }
}
