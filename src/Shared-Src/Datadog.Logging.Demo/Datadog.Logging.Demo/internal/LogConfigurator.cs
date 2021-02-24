using System;
using System.Collections.Generic;
using System.Threading;
using Datadog.Logging.Composition;

namespace Datadog.Logging.Demo
{
    internal static class LogConfigurator
    {
        private const bool UseConsoleLogInsteadOfFileLog = false;
        private const bool UseConsoleLogIfFileLogNotAvailable = true;
        private const int LogFileSizeBytes = 1024 * 50;

        private const string ProductFamily = "DotNet";
        private const string Product = "DemosAndTests";
        private const string ComponentGroup = "Logging-Demo";

        // If you copy this text, remember to re-generate a unique ID.
        private static readonly Guid LoggingDemoLogGroupId = Guid.Parse("8A335CC9-AAA7-435E-8794-87F9338ABFA2");

        private static IReadOnlyDictionary<Type, ComponentGroupCompositionLogSink> s_logRedirections = null;

#pragma warning disable CS0162 // Unreachable code detected: Purposeful control using const bool fields in this class.
        public static void SetupLogger()
        {
            if (UseConsoleLogInsteadOfFileLog)
            {
                if (TrySetupConsoleLogger())
                {
                    return;
                }
            }
            else
            {
                if (TrySetupFileLogger())
                {
                    return;
                }
            }

            if (UseConsoleLogIfFileLogNotAvailable)
            {
                if (TrySetupConsoleLogger())
                {
                    return;
                }
            }

            ConsoleWriteLine();
            ConsoleWriteLine($"PROBLEM! Could not setup logger.");
        }
#pragma warning restore CS0162 // Unreachable code detected

        public static void DisposeLogSinks()
        {
            IReadOnlyDictionary<Type, ComponentGroupCompositionLogSink> logRedirections = Interlocked.Exchange(ref s_logRedirections, null);
            if (logRedirections != null)
            {
                foreach (KeyValuePair<Type, ComponentGroupCompositionLogSink> redirection in logRedirections)
                {
                    if (redirection.Value != null)
                    {
                        try
                        {
                            redirection.Value.Dispose();
                        }
                        catch(Exception ex)
                        {
                            ConsoleWriteLine();
                            ConsoleWriteLine($"Error disposing a {nameof(ComponentGroupCompositionLogSink)}"
                                            + $" with {nameof(ComponentGroupCompositionLogSink.DownstreamLogSink)} of type {redirection.Value.DownstreamLogSink.GetType().FullName}.");
                            ConsoleWriteLine($"{ex}");
                        }
                    }
                }
            }
        }

        private static bool TrySetupFileLogger()
        {
            try
            {
                if (DatadogEnvironmentFileLogSinkFactory.TryCreateNewFileLogSink(ProductFamily,
                                                                                 Product,
                                                                                 ComponentGroup,
                                                                                 LoggingDemoLogGroupId,
                                                                                 LogFileSizeBytes,
                                                                                 out FileLogSink fileLogSink))
                {
                    RedirectLogs(fileLogSink, out s_logRedirections);
                    LogComposer.SetDebugLoggingEnabledBasedOnEnvironment();
                    return true;
                }
            }
            catch(Exception ex)
            {
                ConsoleWriteLine();
                ConsoleWriteLine($"Error setting up a file logger.");
                ConsoleWriteLine($"{ex}");
            }

            return false;
        }

        private static bool TrySetupConsoleLogger()
        {
            RedirectLogs(SimpleConsoleLogSink.SingeltonInstance, out s_logRedirections);
            LogComposer.IsDebugLoggingEnabled = true;
            return true;
        }

        private static void RedirectLogs(ILogSink logSink, out IReadOnlyDictionary<Type, ComponentGroupCompositionLogSink> redirections)
        {
            LogComposer.RedirectLogs(logSink, out redirections);

            ConsoleWriteLine();
            ConsoleWriteLine($"Configured a total of {redirections.Count} redirections:");

            foreach (KeyValuePair<Type, ComponentGroupCompositionLogSink> redirection in redirections)
            {
                ConsoleWriteLine( "{");
                ConsoleWriteLine($"    Logger Type:         \"{redirection.Key.FullName}\"");
                if (redirection.Value == null)
                {
                    ConsoleWriteLine($"    Destination:         Nothing will be logged");
                }
                else
                {
                    ConsoleWriteLine($"    Destination:         Log Sink Type:       \"{redirection.Value.DownstreamLogSink.GetType().Name}\"");
                    ConsoleWriteLine($"                         Log Component Group: \"{redirection.Value.LogComponentGroupMoniker}\"");
                }
                
                ConsoleWriteLine( "}");
            }
        }

        private static void ConsoleWriteLine()
        {
            ConsoleWriteLine(null);
        }

        private static void ConsoleWriteLine(string line)
        {
            if (line == null)
            {
                Console.WriteLine();
            }
            else
            {
                Console.WriteLine("Console-Message: " + line);
            }
        }
    }
}
