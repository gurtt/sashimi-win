// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Microsoft.Windows.AppLifecycle;
using static Sashimi.SlackClient;
using LaunchActivatedEventArgs = Microsoft.UI.Xaml.LaunchActivatedEventArgs;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Sashimi
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App
    {
        private const string ClTokenKey = "slack-access-token";
        private const string ClientId = "4228676926246.4237754035636";
        private const string Scope = "users.profile:write";

        private static SlackClient _slack;
        private static ApplicationDataContainer _localSettings;
        private TeamsAppEventWatcher _teams;

        private static bool _shouldHandleCopiedToken;

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            try
            {
                _slack = new SlackClient(ClientId, CredentialLockerHelper.Get(ClTokenKey));
            }
            catch
            {
                _slack = new SlackClient(ClientId);
            }

            _teams = new TeamsAppEventWatcher();
            _teams.CallStateChanged += HandleCallStateChanged;

            _localSettings = ApplicationData.Current.LocalSettings;

            Clipboard.ContentChanged += HandleClipboardContentChanged;

            _mWindow = new MainWindow();

            if (!_slack.HasToken)
            {
                Debug.WriteLine("No token; triggering sign-in prompt");
                SignIn();
            }

            Debug.WriteLine("Ready");
        }

        public static void SignIn()
        {
            _slack.Authorise(Scope);
            _shouldHandleCopiedToken = true;
        }

        public static void SignOut()
        {
            try
            {
                CredentialLockerHelper.Remove(ClTokenKey);
            }
            catch
            {
                // TODO: Handle not being able to remove the key
            }

            _slack.SetToken(null);
            _mWindow.NotifyAuthStatusChanged();
        }

        public static bool IsSignedIn => _slack.HasToken;

        // The window is on another thread; marshal to UI thread via dispatcher
        public static void HandleOtherActivation() =>
            _mWindow.DispatcherQueue.TryEnqueue(() => { _mWindow.Activate(); });
        public static void HandleProtocolActivation(AppActivationArguments args)
        {
            var uri = ((ProtocolActivatedEventArgs)args.Data).Uri;

            if (uri.Scheme == "sashimi" && uri.LocalPath == "auth" && uri.Query.StartsWith("?token=") && uri.Query.Length > 7) {
                var token = uri.Query[7..];
                try
                {
                    CredentialLockerHelper.Set(ClTokenKey, token);
                } catch
                {
                    // TODO: Handle not being able to save the key
                }

                _slack.SetToken(token);
                _shouldHandleCopiedToken = false;

                // The window is on another thread; marshal to UI thread via dispatcher
                _mWindow.DispatcherQueue.TryEnqueue(() =>
                {
                    _mWindow.NotifyAuthStatusChanged(); 
                    _mWindow.Activate();
                });
            }
            // TODO: Handle bad protocol requests
        }

        private static void HandleCallStateChanged(object sender, CallStateChangedEventArgs e)
        {
            switch (e.State)
            {
                case CallState.InCall:
                    _slack.SetStatus(
                            (string)_localSettings.Values["statusEmoji"] == string.Empty && (string)_localSettings.Values["statusText"] == string.Empty
                                ? new SlackStatus
                            (
                                ":sushi:", 
                                "In a call"
                            )
                                : new SlackStatus 
                            (
                                (string)_localSettings.Values["statusEmoji"],
                                (string)_localSettings.Values["statusText"]
                            )
                    );
                    break;

                case CallState.CallEnded:
                    _slack.ClearStatus();
                    break;

                default:
                    Debug.Fail($"Unexpected call state\"{e.State}\"");
                    break;
            }
        }

        private static async void HandleClipboardContentChanged(object sender, object e)
        {
            if (!_shouldHandleCopiedToken) return;

            var dataPackageView = Clipboard.GetContent();
            if (!dataPackageView.Contains(StandardDataFormats.Text)) return;
            var text = await dataPackageView.GetTextAsync();
            if (!text.StartsWith("xoxp-") || text.Length <= 5) return; // TODO: Check if the token actually works

            try
            {
                CredentialLockerHelper.Set(ClTokenKey, text);
            }
            catch
            {
                // TODO: Handle not being able to save the key
            }

            _slack.SetToken(text);
            _shouldHandleCopiedToken = false;

            // The window is on another thread; marshal to UI thread via dispatcher
            _mWindow.DispatcherQueue.TryEnqueue(() =>
            {
                _mWindow.Activate(); 
                _mWindow.NotifyAuthStatusChanged(); 
                _mWindow.ShowSignedInViaClipboardMessage();
            });
        }

        public static void SetPreferencesForMessage(string message)
        {
            // TODO: Verify message is no more than 100 chars, emoji is valid, etc.

            const string emojiPattern = "^:(?i)[a-z]+:";
            var emojiMatch = Regex.Match(message, emojiPattern);

            var statusEmoji = emojiMatch.Value;
            var statusMessage = Regex.Replace(message, emojiPattern, String.Empty).Trim();

            Debug.WriteLine($"Setting status preferences with emoji \"{statusEmoji}\" and message \"{statusMessage}\"");

            _localSettings.Values["statusEmoji"] = statusEmoji;
            _localSettings.Values["statusText"] = statusMessage;
        }

        private static MainWindow _mWindow;
    }
}
