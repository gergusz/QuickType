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
                TextInputLabel.Text = $"{TextInputLabel.Text[..^1]}";
            } else
            {
                TextInputLabel.Text = $"{TextInputLabel.Text + str}";
            }

            List<Word> wordlist = App.Current.Language.SearchByPrefix(TextInputLabel.Text.ToLower());

            StringBuilder sb = new();
            if (wordlist.Count < 1) return;
            wordlist[..Math.Min(wordlist.Count, 10)].ForEach(word => sb.AppendLine(word.word));

            SuggestionsTextBlock.Text = sb.ToString();

        }


    }
}
