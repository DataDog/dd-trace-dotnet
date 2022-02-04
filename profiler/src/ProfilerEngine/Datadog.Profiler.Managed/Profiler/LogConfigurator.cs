// <copyright file="LogConfigurator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading;
using Datadog.Configuration;
using Datadog.Logging.Composition;
using Datadog.Logging.Emission;

namespace Datadog.Profiler
{
    internal static class LogConfigurator
    {
#if DEBUG
        private const bool UseConsoleLogInsteadOfFileLog = false;           // False for Prod!
        private const bool UseConsoleLogIfFileLogNotAvailable = true;       // False for Prod?
        private const int LogFileSizeBytes = 1024 * 512 * 1;                // Rotate log file after 0.5 MByte (Prod should be 100 MBytes)
        private const bool ShowConfiguratorDiagnosticInfoOnConsole = true;  // False for Prod!
#else
        private const bool UseConsoleLogInsteadOfFileLog = false;
        private const bool UseConsoleLogIfFileLogNotAvailable = true;       // False for Prod?
        private const int LogFileSizeBytes = 1024 * 1024 * 100;             // Rotate log file after 100 MBytes
        private const bool ShowConfiguratorDiagnosticInfoOnConsole = false;
#endif

        private const string ProductFamily = ConfigurationProviderUtils.ProductFamily;
        private const string Product = "Profiler";
        private const string ComponentGroup = "Managed";

        // If you copy this text, remember to re-generate a unique ID.
        private static readonly Guid LoggingDemoLogGroupId = Guid.Parse("37D9FA40-C04D-4E7F-8664-AA412194CF21");

        private static readonly DefaultFormat.Options FileFormatOptions =
                                    new DefaultFormat.Options(
                                            useUtcTimestamps: false,
                                            useNewLinesInErrorMessages: true,
                                            useNewLinesInDataNamesAndValues: true);

        private static IReadOnlyDictionary<Type, LogSourceNameCompositionLogSink> _logRedirections = null;

#pragma warning disable CS0162 // Unreachable code detected: Purposeful control using const bool fields in this class.
        public static void SetupLogger(IProductConfiguration config)
        {
            // In case that we are re-initializing, dispose previous log sinks (one log sink via one-or-more redirections).
            DisposeLogSinks();

            if (UseConsoleLogInsteadOfFileLog)
            {
                if (TrySetupConsoleLogger())
                {
                    return;
                }
            }
            else
            {
                if (TrySetupFileLogger(config))
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
            ConsoleWriteLine("PROBLEM! Could not setup logger.");
        }
#pragma warning restore CS0162 // Unreachable code detected

        public static void DisposeLogSinks()
        {
            IReadOnlyDictionary<Type, LogSourceNameCompositionLogSink> logRedirections = Interlocked.Exchange(ref _logRedirections, null);
            if (logRedirections != null)
            {
                // Log singks must be OK with being disposed multiple times, as we are are directing One-Or-More log
                // sources to One log sink, i.e. we have One-Or-More s_logRedirections backed by one log sink
                // and we will end up disposing that one log sink One-Or-More times accordingly.

                foreach (KeyValuePair<Type, LogSourceNameCompositionLogSink> redirection in logRedirections)
                {
                    if (redirection.Value != null)
                    {
                        try
                        {
                            redirection.Value.Dispose();
                        }
                        catch (Exception ex)
                        {
                            ConsoleWriteLine();
                            ConsoleWriteLine($"Error disposing a {nameof(LogSourceNameCompositionLogSink)}"
                                            + $" with {nameof(LogSourceNameCompositionLogSink.DownstreamLogSink)} of type {redirection.Value.DownstreamLogSink.GetType().FullName}.");
                            ConsoleWriteLine($"{ex}");
                        }
                    }
                }
            }
        }

        private static bool TrySetupFileLogger(IProductConfiguration config)
        {
            try
            {
                var filenameBaseInfo = new DatadogEnvironmentFileLogSinkFactory.FilenameBaseInfo(ProductFamily, Product, ComponentGroup);
                if (DatadogEnvironmentFileLogSinkFactory.TryCreateNewFileLogSink(
                                                            filenameBaseInfo,
                                                            LoggingDemoLogGroupId,
                                                            config?.Log_PreferredLogFileDirectory,
                                                            LogFileSizeBytes,
                                                            FileFormatOptions,
                                                            out FileLogSink fileLogSink))
                {
                    RedirectLogs(fileLogSink, out _logRedirections);

                    if (config != null)
                    {
                        LogComposer.IsDebugLoggingEnabled = config.Log_IsDebugEnabled;
                    }
                    else
                    {
                        LogComposer.SetDebugLoggingEnabledBasedOnEnvironment();
                    }

                    return true;
                }
            }
            catch (Exception ex)
            {
                ConsoleWriteLine();
                ConsoleWriteLine("Error setting up a file logger.");
                ConsoleWriteLine($"{ex}");
            }

            return false;
        }

        private static bool TrySetupConsoleLogger()
        {
            RedirectLogs(SimpleConsoleLogSink.SingeltonInstance, out _logRedirections);
            LogComposer.IsDebugLoggingEnabled = true;
            return true;
        }

        private static void RedirectLogs(ILogSink logSink, out IReadOnlyDictionary<Type, LogSourceNameCompositionLogSink> redirections)
        {
            LogComposer.RedirectLogs(logSink, out redirections);

            ConsoleWriteLine();
            ConsoleWriteLine($"Configured a total of {redirections.Count} redirections:");

            foreach (KeyValuePair<Type, LogSourceNameCompositionLogSink> redirection in redirections)
            {
                ConsoleWriteLine("{");
                ConsoleWriteLine($"    Logger Type:         \"{redirection.Key.FullName}\"");
                if (redirection.Value == null)
                {
                    ConsoleWriteLine($"    Destination:         Nothing will be logged");
                }
                else
                {
                    ConsoleWriteLine($"    Destination:         Log Sink Type:       \"{redirection.Value.DownstreamLogSink.GetType().Name}\"");
                    ConsoleWriteLine($"                         Log Component Group: \"{redirection.Value.LogSourcesGroupMoniker}\"");
                }

                ConsoleWriteLine("}");
            }
        }

        private static void ConsoleWriteLine()
        {
            ConsoleWriteLine(null);
        }

        private static void ConsoleWriteLine(string line)
        {
#pragma warning disable CS0162 // Unreachable code detected: Purposeful control using const bool fields in this class.
            if (ShowConfiguratorDiagnosticInfoOnConsole)
            {
                if (line == null)
                {
                    Console.WriteLine();
                }
                else
                {
                    Console.WriteLine($" # # # [{typeof(LogConfigurator).Namespace}::{typeof(LogConfigurator).Name}]: {line}");
                }
            }
#pragma warning restore CS0162 // Unreachable code detected
        }
    }
}
