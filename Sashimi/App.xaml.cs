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
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;

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
        private const string teamsMonitoringPath = "~/Library/Application Support/Microsoft/Teams/storage.json";

        private static SlackClient slack;

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

            if (!slack.HasToken())
            {
                Debug.WriteLine("No token; triggering sign-in prompt");
                slack.Authorise(scope);
            }

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

            // Go ahead and do standard window initialization regardless.
            m_window = new MainWindow();
            m_window.Activate();
        }

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

        private Window m_window;
    }
}
