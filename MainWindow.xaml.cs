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
using QuickType.Model;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace QuickType
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        public static string CurrentBuffer { get; set; }

        public MainWindow()
        {
            this.InitializeComponent();
            KeyboardCapturer.KeyboardEvent += KeyboardCapturer_KeyboardEvent;
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
            if (str == "\b")
            {
                if (CurrentBuffer.Length <= 1)
                {
                    CurrentBuffer = "";
                }
                else
                {
                    CurrentBuffer = CurrentBuffer[..^1];
                }
            }
            else if (str == "\r" || str == " ")
            {
                CurrentBuffer = "";
            }
            else
            {
                CurrentBuffer += str;
            }

            TextInputLabel.Text = $"Current buffer: {CurrentBuffer}";

            if (CurrentBuffer.Length > 2 && !string.IsNullOrWhiteSpace(CurrentBuffer))
            {
                GetSuggestions();
            } else
            {
                SuggestionsTextBlock.Text = "Too few chars (<2), or just whitespace!";
            }
    
            FindCaret();

        }

        private void GetSuggestions()
        {
            List<Word> wordlist = App.Current.Language.SearchByPrefix(CurrentBuffer.ToLower());

            StringBuilder sb = new();
            if (wordlist.Count < 1)
            {
                SuggestionsTextBlock.Text = "This word is not recognised! Something went wrong?";
                return;
            }
            wordlist[..Math.Min(wordlist.Count, 10)].ForEach(word => sb.AppendLine($"{word.word} ({word.frequency})"));

            SuggestionsTextBlock.Text = sb.ToString();
        }

        private void FindCaret()
        {
            CaretFinder.CaretRectangle? caretRectangle = CaretFinder.GetCaretPos();

            if (caretRectangle is not null)
            {
                CaretPosition.Text = caretRectangle.ToString();
            }
            else
            {
                CaretPosition.Text = "Caret not found!";
            }
        }
    }
}
