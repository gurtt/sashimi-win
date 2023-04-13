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

            // Setup UI state
            TriggerUiStateUpdate();

            Closed += (_, e) =>
            {
                e.Handled = true;
                appWindow.Hide();
            };

            [DllImport("user32.dll")]
            static extern bool SetForegroundWindow(IntPtr hWnd);
            Activated += (_, _) =>
            {
               SetForegroundWindow(hWnd);

                appWindow.Title = "Sashimi";

               // Shrink to fit content, centre position
               appWindow.Resize(new SizeInt32((int)Math.Round(Content.DesiredSize.Width),
                   (int)Math.Round(Content.DesiredSize.Height)));
               var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Nearest);
               if (displayArea is not null)
               {
                   var centredPosition = appWindow.Position;
                   centredPosition.X = (displayArea.WorkArea.Width - appWindow.Size.Width) / 2;
                   centredPosition.Y = (displayArea.WorkArea.Height - appWindow.Size.Height) / 2;
                   appWindow.Move(centredPosition);
               }
            };
        }

        #region UiUpdates

        /// <summary>
        /// Updates the sign in/out button to show current app state.
        /// </summary>
        public void TriggerUiStateUpdate() => SignInOutButton.Content = $"Sign {(App.IsSignedIn ? "Out of" : "In to")} Slack";

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

        #endregion

        #region EventHandlers

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            App.SetCustomMessage(this.MessageTextBox.Text);
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

        private void OnKeyDownHandler(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter) SaveButton_Click(sender, new RoutedEventArgs());
        }

        #endregion
    }
}
