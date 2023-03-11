// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.Windows.AppLifecycle;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Devices.Power;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using ABI.Windows.Media.Capture;
using static SlackClient;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Sashimi
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        private const string clTokenKey = "slack-access-token";
        private const string client_id = "4228676926246.4237754035636";
        private const string scope = "users.profile:write";

        private static SlackClient slack;
        private static ApplicationDataContainer localSettings;
        private TeamsAppEventWatcher teams;

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            // NOTE: OnLaunched will always report that the ActivationKind == Launch,
            // even when it isn't.
            Windows.ApplicationModel.Activation.ActivationKind kind
                = args.UWPLaunchActivatedEventArgs.Kind;
            Debug.WriteLine($"OnLaunched: Kind={kind}");

            try
            {
                slack = new(client_id, CredentialLockerHelper.Get(clTokenKey));
            }
            catch
            {
                slack = new(client_id);
            }

            teams = new TeamsAppEventWatcher();
            teams.CallStateChanged += HandleCallStateChanged;

            localSettings = ApplicationData.Current.LocalSettings;

            // NOTE: AppInstance is ambiguous between
            // Microsoft.Windows.AppLifecycle.AppInstance and
            // Windows.ApplicationModel.AppInstance
            var currentInstance =
                Microsoft.Windows.AppLifecycle.AppInstance.GetCurrent();
            if (currentInstance != null)
            {
                // AppInstance.GetActivatedEventArgs will report the correct ActivationKind,
                // even in WinUI's OnLaunched.
                Microsoft.Windows.AppLifecycle.AppActivationArguments activationArgs
                    = currentInstance.GetActivatedEventArgs();
                if (activationArgs != null)
                {
                    Microsoft.Windows.AppLifecycle.ExtendedActivationKind extendedKind
                        = activationArgs.Kind;
                    Debug.WriteLine($"activationArgs.Kind={extendedKind}");
                }
            }

            m_window = new MainWindow();

            if (slack.HasToken())
                m_window.Activate();
            else
            {
                Debug.WriteLine("No token; triggering sign-in prompt");
                slack.Authorise(scope);
            }
        }

        public static void SignIn()
        {
            slack.Authorise(scope);
        }

        public static void SignOut()
        {
            slack.SetToken(null);
        }

        public static bool IsSignedIn() => slack.HasToken();

        public static void HandleProtocolActivation(object sender, AppActivationArguments args)
        {
            Uri uri = ((ProtocolActivatedEventArgs)args.Data).Uri;

            if ((uri.Scheme == "sashimi" && uri.LocalPath == "auth" && uri.Query.StartsWith("?token=") && uri.Query.Length > 7)) {
                string token = uri.Query[7..];
                try
                {
                    CredentialLockerHelper.Set(clTokenKey, token);
                } catch
                {
                    // TODO: Handle not being able to save the key
                }

                slack.SetToken(token);
            } else
            {
                // TODO: Handle bad protocol requests
            }

            // TODO: Open preferences
        }

        private static void HandleCallStateChanged(object sender, CallStateChangedEventArgs e)
        {
            switch (e.State)
            {
                case CallState.InCall:
                    slack.SetStatus(
                            ((string)localSettings.Values["statusEmoji"] == string.Empty && (string)localSettings.Values["statusText"] == string.Empty)
                                ? new SlackStatus
                            (
                                ":sushi:", 
                                "In a call"
                            )
                                : new SlackStatus 
                            (
                                (string)localSettings.Values["statusEmoji"],
                                (string)localSettings.Values["statusText"]
                            )
                    );
                    break;

                case CallState.CallEnded:
                    slack.ClearStatus();
                    break;

                default:
                    Debug.Fail($"Unexpected call state\"{e.State}\"");
                    break;
            }
        }

        public static void SetPreferencesForMessage(string message)
        {
            // TODO: Verify message is no more than 100 chars, emoji is valid, etc.

            string emojiPattern = "^:(?i)[a-z]+:";
            Match emojiMatch = Regex.Match(message, emojiPattern);

            string statusEmoji = emojiMatch.Value;
            string statusMessage = Regex.Replace(message, emojiPattern, String.Empty).Trim();

            Debug.WriteLine($"Setting status preferences with emoji \"{statusEmoji}\" and message \"{statusMessage}\"");

            localSettings.Values["statusEmoji"] = statusEmoji;
            localSettings.Values["statusText"] = statusMessage;
        }

        private Window m_window;
    }
}
