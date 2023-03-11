using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using WinRT;

namespace Sashimi
{
    // NOTE: We need to check for redirection as early as possible, and
    // before creating any windows. To do this, we must define this symbol
    // in the project build properties:
    // DISABLE_XAML_GENERATED_MAIN
    // ...and define a custom Program class with a Main method 

    public static class Program
    {
        // Replaces the standard App.g.i.cs.
        // Note: We can't declare Main to be async because in a WinUI app
        // this prevents Narrator from reading XAML elements.
        [STAThread]
        static void Main(string[] args)
        {
            ComWrappersSupport.InitializeComWrappers();

            var isRedirect = DecideRedirection();
            if (!isRedirect)
            {
                Application.Start(p =>
                {
                    var context = new DispatcherQueueSynchronizationContext(
                        DispatcherQueue.GetForCurrentThread());
                    SynchronizationContext.SetSynchronizationContext(context);
                    new App();
                });
            }
        }

        private static void OnActivated(object sender, AppActivationArguments args)
        {
            var kind = args.Kind;
            if (kind == ExtendedActivationKind.Protocol)
            {
                App.HandleProtocolActivation(args);
            }
            else
            {
                App.HandleOtherActivation();
            }
        }

        private static bool DecideRedirection()
        {
            var isRedirect = false;

            var args = AppInstance.GetCurrent().GetActivatedEventArgs();
            var keyInstance = AppInstance.FindOrRegisterForKey("sashimi");

            if (keyInstance.IsCurrent)
            {
                // Hook up the Activated event, to allow for this instance of the app
                // getting reactivated as a result of multi-instance redirection.
                keyInstance.Activated += OnActivated;
            }
            else
            {
                isRedirect = true;
                RedirectActivationTo(args, keyInstance);
            }

            return isRedirect;
        }

        // Do the redirection on another thread, and use a non-blocking
        // wait method to wait for the redirection to complete.
        private static void RedirectActivationTo(
            AppActivationArguments args, AppInstance keyInstance)
        {
            var redirectSemaphore = new Semaphore(0, 1);
            Task.Run(() =>
            {
                keyInstance.RedirectActivationToAsync(args).AsTask().Wait();
                redirectSemaphore.Release();
            });
            redirectSemaphore.WaitOne();
        }
    }
}