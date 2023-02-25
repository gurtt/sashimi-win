using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Sashimi
{
    internal static class FileSystemHelper
    {
        private static string lastCallState;
        public static void OnChanged(object sender, FileSystemEventArgs e)
        {
            // TODO: Implement file watching

            Debug.WriteLine("Processing FileSystemWatcher event");

            //let data = String(data: FileManager.default.contents(atPath: event.path)!, encoding: .utf8) ?? ""
            string data = null;

            while (data == null)
            {
               try
                {
                    data = System.IO.File.ReadAllText(e.FullPath);
                } catch
                {
                    Debug.WriteLine("Failed to read; retrying");
                } 
            }
            
            //let match = try? NSRegularExpression(pattern: "(\\{\"appStates\":\\{\"states\":\")[^\"]+").firstMatch(in: data, range: NSMakeRange(0, data.count))
            var match = Regex.Match(data, "\"appStates\":{\"states\":\"[^\"]+", RegexOptions.Singleline).Value;

            if (match == String.Empty )
            {
                Debug.WriteLine("Didn't find a match in monitoring file");
                return;
            }

            string lastEvent = match.Substring(23).Split(',').Last();

            if (lastCallState == lastEvent)
            {
                Debug.WriteLine($"Ignoring unchanged event \"${lastEvent}\"");
                return;
            }

            lastCallState = lastEvent;

            switch (lastEvent)
            {
                case "InCall":
                    Debug.WriteLine("Detected call start");
                    // TODO: Update slack status
                    break;

                case "CallEnded":
                    Debug.WriteLine("Detected call end");
                    // TODO: Update slack status
                    break;

                default:
                    Debug.WriteLine($"Ignoring irrelevant event ${lastEvent}");
                    break;
            }
        }
    }
}
