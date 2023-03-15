// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using System;
using System.Runtime.InteropServices;
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

        private ContentDialog displayedContentDialog;
        public MainWindow()
        {
            InitializeComponent();

            var presenter = GetAppWindowAndPresenter();
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.IsResizable = false;
            presenter.SetBorderAndTitleBar(true, false);

            NotifyAuthStatusChanged();

            var hWnd = WindowNative.GetWindowHandle(this);
            var myWndId = Win32Interop.GetWindowIdFromWindow(hWnd);
            var apw = AppWindow.GetFromWindowId(myWndId);

            Closed += (_, e) =>
            {
                e.Handled = true;
                apw.Hide();
            };

            [DllImport("user32.dll")]
            static extern bool SetForegroundWindow(IntPtr hWnd);

            Activated += (_, _) =>
            {
               SetForegroundWindow(hWnd);
            };
        }

        private OverlappedPresenter GetAppWindowAndPresenter()
        {
            var hWnd = WindowNative.GetWindowHandle(this);
            var myWndId = Win32Interop.GetWindowIdFromWindow(hWnd);
            var apw = AppWindow.GetFromWindowId(myWndId);
            apw.Resize(new SizeInt32(500, 245));
            var displayArea = DisplayArea.GetFromWindowId(myWndId, DisplayAreaFallback.Nearest);
            if (displayArea is not null)
            {
                var centredPosition = apw.Position;
                centredPosition.X = (displayArea.WorkArea.Width - apw.Size.Width) / 2;
                centredPosition.Y = (displayArea.WorkArea.Height - apw.Size.Height) / 2;
                apw.Move(centredPosition);
            }

            return apw.Presenter as OverlappedPresenter;
        }

        public void NotifyAuthStatusChanged() => SignInOutButton.Content = $"Sign {(App.IsSignedIn ? "Out of" : "In to")} Slack";

        public async void ShowSignedInViaClipboardMessage()
        {
            if (displayedContentDialog != null) displayedContentDialog.Hide();

            displayedContentDialog = new()
            {
                Title = "Signed in to Slack",
                Content = "We got the token you copied to the clipboard.",
                CloseButtonText = "Ok",
                XamlRoot = Content.XamlRoot
            };
            await displayedContentDialog.ShowAsync();
        }

        public async void ShowAuthErrorMessage()
        {
            if (displayedContentDialog != null) displayedContentDialog.Hide();

            displayedContentDialog = new()
            {
                Title = "Can't connect to Slack",
                Content = "You need to sign in again.",
                CloseButtonText = "Ok",
                XamlRoot = Content.XamlRoot
            };
            await displayedContentDialog.ShowAsync();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            App.SetPreferencesForMessage(this.MessageTextBox.Text);
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void SignInOutButton_Click(object sender, RoutedEventArgs? e)
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
    }
}
