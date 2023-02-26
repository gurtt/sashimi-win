using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Sashimi
{
    public enum CallState
    {
        InCall,
        CallEnded
    }

    public class CallStateChangedEventArgs : EventArgs
    {
        public CallStateChangedEventArgs(CallState state)
        {
            State = state;
        }

        public CallState State { get; }
    }

    /// <summary>
    /// Watches Team's analytics store and raises events when the state of the app changes.
    /// </summary>
    internal class TeamsAppEventWatcher
    {
        private static readonly string TeamsMonitoringPath = Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.ApplicationData), @"Microsoft\Teams");

        private readonly FileSystemWatcher _watcher;
        private CallState _previousCallState;

        public TeamsAppEventWatcher()
        {
            try
            {
                _watcher = new FileSystemWatcher(TeamsMonitoringPath);
                _watcher.Changed += OnChanged;
                _watcher.Filter = "storage.json";
                _watcher.EnableRaisingEvents = true;
            }
            catch
            {
                Debug.WriteLine("Failed to init FileWatcher");
            }
        }

        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            string data = null;
            while (data == null)
            {
                try
                {
                    data = File.ReadAllText(e.FullPath);
                }
                catch
                {
                    Debug.WriteLine("Couldn't read file; retrying");
                }
            }

            var match = Regex.Match(data, "\"appStates\":{\"states\":\"[^\"]+", RegexOptions.Singleline).Value;

            if (match == string.Empty)
            {
                Debug.Fail("Didn't find state history in file");
                return;
            }

            if (Enum.TryParse(match[23..].Split(',').Last(), out CallState lastEvent))
            {
                if (_previousCallState == lastEvent)
                {
                    Debug.WriteLine($"Ignoring unchanged event \"${lastEvent}\"");
                    return;
                }

                OnRaiseCallStateChanged(new CallStateChangedEventArgs(lastEvent));
                _previousCallState = lastEvent;
                return;
            }

            Debug.WriteLine($"Ignoring irrelevant event ${lastEvent}");
        }

        /// <summary>
        /// Raised when Teams reports a call state change.
        /// </summary>
        public event EventHandler<CallStateChangedEventArgs> CallStateChanged;

        // Wrap event invocations inside a protected virtual method
        // to allow derived classes to override the event invocation behavior
        protected virtual void OnRaiseCallStateChanged(CallStateChangedEventArgs e)
        {
            // Make a temporary copy of the event to avoid possibility of
            // a race condition if the last subscriber unsubscribes
            // immediately after the null check and before the event is raised.
            EventHandler<CallStateChangedEventArgs> raiseEvent = CallStateChanged;

            // Event will be null if there are no subscribers
            raiseEvent?.Invoke(this, e);
        }


    }
}
