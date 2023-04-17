// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Microsoft.AppCenter;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
using Microsoft.Windows.AppLifecycle;
using static Sashimi.SlackClient;
using LaunchActivatedEventArgs = Microsoft.UI.Xaml.LaunchActivatedEventArgs;
using System.Threading.Tasks;

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
        private const string Scope = "users.profile:write,users.profile:read";
        private const string AnalyticsAppSecret = SecretsManager.AnalyticsAppSecret;

        private static SlackClient _slack;
        private static ApplicationDataContainer _localSettings;
        private TeamsAppEventWatcher _teams;
        private static MainWindow _mWindow;

        private static bool _shouldHandleCopiedToken;
        public static bool IsSignedIn => _slack.HasToken;
        private static SlackStatus? previousStatus;

        /// <summary>
        /// Initializes the singleton application object.
        /// </summary>
        public App()
        {
            InitializeComponent();
            AppCenter.Start(AnalyticsAppSecret, typeof(Analytics), typeof(Crashes));
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override async void OnLaunched(LaunchActivatedEventArgs args)
        {
            try
            {
                _slack = new SlackClient(ClientId, CredentialLockerHelper.Get(ClTokenKey));
            }
            catch
            {
                _slack = new SlackClient(ClientId);
            }

            try
            {
                _teams = new TeamsAppEventWatcher();
                _teams.CallStateChanged += HandleCallStateChanged;
            }
            catch (Exception ex)
            {
                Crashes.TrackError(ex, new Dictionary<string, string>{
                    { "Where", "OnLaunched" },
                    { "Issue", "Teams isn't installed" }
                });
            }

            _localSettings = ApplicationData.Current.LocalSettings;

            Clipboard.ContentChanged += HandleClipboardContentChanged;

            _mWindow = new MainWindow();

            if (!_slack.HasToken)
            {
                Debug.WriteLine("No token; triggering sign-in prompt");
                SignIn();
            }

            if (_teams == null)
            {
                await Task.Delay(5).ContinueWith(_ => {
                    _mWindow.DispatcherQueue.TryEnqueue(() => { _mWindow.ShowContentDialog("Can't connect to Teams", "Check if Teams is installed, then restart Sashimi."); });
                });
            }

            Debug.WriteLine("Ready");
        }

        #region EventHandlers

        private static async void HandleCallStateChanged(object sender, CallStateChangedEventArgs e)
        {
            if (!_slack.HasToken) return;

            void TrySetStatus(SlackStatus status)
            {
                try
                {
                    _slack.SetStatus(status);
                }
                catch (HttpRequestException ex)
                {
                    if (ex.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        _slack.SetToken(null); // Can't SignOut() if the token isn't valid 😉

                        _mWindow.DispatcherQueue.TryEnqueue(() => { _mWindow.ShowContentDialog("Can't connect to Slack", "You need to sign in again."); });

                        Crashes.TrackError(ex, new Dictionary<string, string>{
                                { "Where", "HandleCallStateChanged" },
                                { "Issue", "Slack token is invalid" }
                            });
                    }

                    Crashes.TrackError(ex, new Dictionary<string, string>{
                            { "Where", "HandleCallStateChanged" },
                            { "Issue", "HTTP error" }
                        });
                }
                catch (Exception ex)
                {
                    Crashes.TrackError(ex, new Dictionary<string, string>{
                            { "Where", "HandleCallStateChanged" },
                            { "Issue", "Couldn't set status" }
                        });
                }
            }

            switch (e.State)
            {
                case CallState.InCall:

                    try
                    {
                    previousStatus = await _slack.GetStatus();
                    } 
                    catch (Exception ex)
                    {
                        Crashes.TrackError(ex, new Dictionary<string, string>{
                                { "Where", "HandleCallStateChanged" },
                                { "Issue", "Couldn't get status" }
                            });
                    }

                    TrySetStatus(string.IsNullOrEmpty((string)_localSettings.Values["statusEmoji"]) &&
                                 string.IsNullOrEmpty((string)_localSettings.Values["statusText"])
                        ? new SlackStatus
                        (
                            ":sushi:",
                            "In a call"
                        )
                        : new SlackStatus
                        (
                            (string)_localSettings.Values["statusEmoji"],
                            (string)_localSettings.Values["statusText"]
                        ));
                    Analytics.TrackEvent("StartedCall");
                    break;

                case CallState.CallEnded:
                    TrySetStatus(previousStatus ?? new SlackStatus { status_emoji = "", status_text = "" });
                    previousStatus = null;
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
            if (!text.StartsWith("xoxp-") || text.Length <= 5) return;

            try
            {
                CredentialLockerHelper.Set(ClTokenKey, text);
            }
            catch (Exception ex)
            {
                Crashes.TrackError(ex, new Dictionary<string, string>{
                    { "Where", "HandleClipboardContentChanged" },
                    { "Issue", "Couldn't save token to credential locker" }
                });

                _mWindow.DispatcherQueue.TryEnqueue(() => { _mWindow.ShowContentDialog("", "You're signed in for now, but you'll need to sign in again next time Sashimi starts."); });
            }

            _slack.SetToken(text);
            _shouldHandleCopiedToken = false;

            _mWindow.DispatcherQueue.TryEnqueue(() => { _mWindow.ShowContentDialog("Signed in to Slack", "We got the token you copied to the clipboard."); });

            Analytics.TrackEvent("SignedIn", new Dictionary<string, string> {
                { "Method", "Clipboard" },
            });
        }

        #endregion

        #region ActivationEventHandlers
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
                } catch (Exception ex) {
                    Crashes.TrackError(ex, new Dictionary<string, string>{
                        { "Where", "HandleProtocolActivation" },
                        { "Issue", "Couldn't save token to credential locker" }
                    });

                    _mWindow.DispatcherQueue.TryEnqueue(() => { _mWindow.ShowContentDialog("", "You're signed in for now, but you'll need to sign in again next time Sashimi starts."); });
                }

                _slack.SetToken(token);
                _shouldHandleCopiedToken = false;

                _mWindow.DispatcherQueue.TryEnqueue(() =>
                {
                    _mWindow.TriggerUiStateUpdate(); 
                    _mWindow.Activate();
                });

                Analytics.TrackEvent("SignedIn", new Dictionary<string, string> {
                    { "Method", "Protocol" },
                });
            }
            else
            {
                _mWindow.DispatcherQueue.TryEnqueue(() => { _mWindow.ShowContentDialog("Broken link", "You opened a Sashimi link, but we don't recognise the format."); });
            }
        }

        #endregion

        #region UI Actions

        /// <summary>
        /// Parses the custom message string and persists it to storage.
        /// </summary>
        /// <param name="message">The message to parse.</param>
        public static void SetCustomMessage(string message)
        {
            const string emojiPattern = "^:(?i)[a-z]+:";
            var emojiMatch = Regex.Match(message, emojiPattern);

            var statusEmoji = emojiMatch.Value;
            var statusMessage = Regex.Replace(message, emojiPattern, String.Empty).Trim();

            Debug.WriteLine($"Setting status preferences with emoji \"{statusEmoji}\" and message \"{statusMessage}\"");

            _localSettings.Values["statusEmoji"] = statusEmoji;
            _localSettings.Values["statusText"] = statusMessage;

            Analytics.TrackEvent("SetCustomMessage", new Dictionary<string, string> {
                { "DidUseEmoji", (!string.IsNullOrEmpty(statusEmoji)).ToString() },
                { "DidUseMessage", (!string.IsNullOrEmpty(statusMessage)).ToString() },
            });
        }

        /// <summary>
        /// Triggers the authorisation flow.
        /// </summary>
        public static void SignIn()
        {
            _slack.Authorise(Scope);
            _shouldHandleCopiedToken = true;
        }

        /// <summary>
        /// Triggers the deauthorisation flow.
        /// </summary>
        public static async void SignOut()
        {
            try
            {
                CredentialLockerHelper.Remove(ClTokenKey);
                await _slack.Deauthorise();
                _mWindow.TriggerUiStateUpdate();
            }
            catch (Exception ex)
            {
                Crashes.TrackError(ex, new Dictionary<string, string>{
                    { "Where", "SignOut" },
                    { "Issue", "Couldn't sign out" }
                });
            }

            Analytics.TrackEvent("SignedOut");
        }

        #endregion
    }
}
