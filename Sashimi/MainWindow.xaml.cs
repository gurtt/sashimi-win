// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Graphics;
using Windows.System;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using WinRT.Interop;
using System.Collections.ObjectModel;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.UI.Xaml.Data;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Sashimi
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public partial class MainWindow
    {

        private ContentDialog _displayedContentDialog;

        // Hold Emoji data
        private List<Emoji> AllEmoji;
        private ObservableCollection<Emoji> EmojiFiltered;
        private Emoji? SelectedEmoji;
        public MainWindow()
        {
            InitializeComponent();

            // Get window handles
            var hWnd = WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);

            // Remove window chrome
            var presenter = appWindow.Presenter as OverlappedPresenter;
            presenter!.IsMaximizable = false;
            presenter!.IsMinimizable = false;
            presenter!.IsResizable = false;
            presenter!.SetBorderAndTitleBar(true, false);

            Closed += (_, e) =>
            {
                e.Handled = true;
                appWindow.Hide();
            };

            // Show and bring to foreground on activation
            [DllImport("user32.dll")]
            static extern bool SetForegroundWindow(IntPtr hWnd);
            Activated += async (_, _) =>
            {
                
                appWindow.Title = "Sashimi";

                // Shrink to fit content, centre position
                appWindow.Resize(new SizeInt32(500,
                    650));
                var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Nearest);
                if (displayArea is not null)
                {
                    var centredPosition = appWindow.Position;
                    centredPosition.X = (displayArea.WorkArea.Width - appWindow.Size.Width) / 2;
                    centredPosition.Y = (displayArea.WorkArea.Height - appWindow.Size.Height) / 2;
                    appWindow.Move(centredPosition);
                }

                SetForegroundWindow(hWnd);

                // Setup UI state
                await TriggerUiStateUpdate();
            };
        }

        #region UiUpdates

        /// <summary>
        /// Updates the UI to reflect current app state.
        /// </summary>
        public async Task TriggerUiStateUpdate() {
            SignInOutButton.Content = $"Sign {(App.IsSignedIn ? "Out of" : "In to")} Slack";
            if (App.IsSignedIn) 
            { 
                var (message, emojiAlias) = App.GetCustomMessage();

                if (!string.IsNullOrEmpty(message))
                {
                    MessageTextBox.Text = message;
                }

                await PopulateEmojiGridView();

                if (!string.IsNullOrEmpty(emojiAlias))
                {
                    SelectedEmoji = AllEmoji.Find(e => e.Alias == emojiAlias);
                    EmojiGridView.SelectedItem = SelectedEmoji;
                    EmojiPickerButton.Content = new Image() { Source = SelectedEmoji.Bitmap, Height = 20, Width = 20 };
                }
                
            } else
            {
                EmojiGridView.ItemsSource = null;
                EmojiPickerButton.Content = new FontIcon() { FontFamily = new FontFamily("Segoe MDL2 Assets"), Glyph = "\xE76E" };
            }
        }

        /// <summary>
        /// Activates the window and then shows a <see cref="ContentDialog"/> with the supplied title and content. Dismisses an existing dialog, if any.
        /// </summary>
        public async void ShowContentDialog(string title = "", string content = "")
        {
            if (_displayedContentDialog != null) _displayedContentDialog.Hide();

            Activate();

            _displayedContentDialog = new()
            {
                Title = title,
                Content = content,
                CloseButtonText = "Ok",
                XamlRoot = Content.XamlRoot
            };
            await _displayedContentDialog.ShowAsync();
        }

        /// <summary>
        /// Populates the emoji list, creates an observable collection, and binds it to the UI.
        /// </summary>
        public async Task PopulateEmojiGridView()
        {
            // Show all custom emojis ordered by alias, then all builtins (already ordered)
            AllEmoji = (await App.GetCustomEmojis()).OrderBy(e => e.Alias).Concat(await Emoji.GetBuiltins()).ToList();
            EmojiFiltered = new(AllEmoji);

            EmojiGridView.ItemsSource = EmojiFiltered;
        }

        #endregion

        #region EventHandlers

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            App.SetCustomMessage(MessageTextBox.Text, SelectedEmoji == null ?  "" : SelectedEmoji.Alias);
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void SignInOutButton_Click(object sender, RoutedEventArgs e)
        {
            if (App.IsSignedIn)
                App.SignOut();
            else
                App.SignIn();
        }

        private void EmojiGridView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
                SelectedEmoji = (Emoji)e.AddedItems.First();
                EmojiPickerButton.Content = new Image() { Source = SelectedEmoji.Bitmap, Height = 20, Width = 20 };
            }
            
        }

        private void OnKeyDownHandler(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter) SaveButton_Click(sender, new RoutedEventArgs());
        }

        private void EmojiSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!App.IsSignedIn) return;

            List<Emoji> TempFiltered = new(AllEmoji.Where(emoji =>
            {
                string normalisedAlias = new((from c in emoji.Alias where char.IsLetterOrDigit(c) select c).ToArray());
                string normalisedSearch = new((from c in EmojiSearchTextBox.Text where char.IsLetterOrDigit(c) select c).ToArray());

                return normalisedSearch == string.Empty || normalisedAlias.Contains(normalisedSearch, StringComparison.InvariantCultureIgnoreCase);
            }
            ));

            Debug.WriteLine($"Found {TempFiltered.Count}/{AllEmoji.Count} emoji matching \"{EmojiSearchTextBox.Text}\"");

            // Remove old ones
            for (int i = EmojiFiltered.Count - 1; i >= 0; i--)
            {
                var item = EmojiFiltered[i];
                if (!TempFiltered.Contains(item))
                {
                    EmojiFiltered.Remove(item);
                }
            }

            // Add new ones in the right spot
            for (int i = 0; i < TempFiltered.Count; i++)
            {
                if (i >= EmojiFiltered.Count) 
                {
                    EmojiFiltered.Add(TempFiltered[i]);
                } else {
                    if (EmojiFiltered[i] != TempFiltered[i]) EmojiFiltered.Insert(i, TempFiltered[i]);
                };
            }
        }

        #endregion
    }
}
