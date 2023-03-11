// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using Microsoft.UI.Windowing;
using Microsoft.UI;
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
using Windows.UI.Core;
using Windows.System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Windows.UI.WindowManagement;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Sashimi
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();

            var presenter = GetAppWindowAndPresenter();
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.IsResizable = false;
            presenter.SetBorderAndTitleBar(true, false);

            NotifyAuthStatusChanged();

            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WindowId myWndId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            var _apw = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(myWndId);

            Closed += (object sender, WindowEventArgs e) =>
            {
                e.Handled = true;
                _apw.Hide();
            };
        }

        private OverlappedPresenter GetAppWindowAndPresenter()
        {
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WindowId myWndId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            var _apw = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(myWndId);
            _apw.Resize(new SizeInt32(500, 245));
            DisplayArea displayArea = DisplayArea.GetFromWindowId(myWndId, DisplayAreaFallback.Nearest);
            if (displayArea is not null)
            {
                var CentredPosition = _apw.Position;
                CentredPosition.X = ((displayArea.WorkArea.Width - _apw.Size.Width) / 2);
                CentredPosition.Y = ((displayArea.WorkArea.Height - _apw.Size.Height) / 2);
                _apw.Move(CentredPosition);
            }

            return _apw.Presenter as OverlappedPresenter;
        }

        public void NotifyAuthStatusChanged() => signInOutButton.Content = $"Sign {(App.IsSignedIn ? "Out of" : "In to")} Slack";

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            App.SetPreferencesForMessage(this.messageTextBox.Text);
            Close();
        }

        private async void CancelButton_Click(object sender, RoutedEventArgs e)
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
            if (e.Key == VirtualKey.Enter) SaveButton_Click(sender, new());
        }
    }
}
