// <copyright file="CIVisibility.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Ci
{
    internal class CIVisibility
    {
        private static readonly CIVisibilitySettings _settings = CIVisibilitySettings.FromDefaultSources();
        private static int _firstInitialization = 1;
        private static Lazy<bool> _enabledLazy = new Lazy<bool>(() => InternalEnabled(), true);
        internal static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(CIVisibility));

        public static bool Enabled => _enabledLazy.Value;

        public static bool IsRunning => _enabledLazy.Value && Interlocked.CompareExchange(ref _firstInitialization, 0, 0) == 0;

        public static void Initialize()
        {
            if (Interlocked.Exchange(ref _firstInitialization, 0) != 1)
            {
                // Initialize() was already called before
                return;
            }

            Log.Information("Initializing CI Visibility");

            // Set exit handlers
            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
            AppDomain.CurrentDomain.DomainUnload += CurrentDomain_DomainUnload;
            try
            {
                // Registering for the AppDomain.UnhandledException event cannot be called by a security transparent method
                // This will only happen if the Tracer is not run full-trust
                AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Unable to register a callback to the AppDomain.UnhandledException event.");
            }

            try
            {
                // Registering for the cancel key press event requires the System.Security.Permissions.UIPermission
                Console.CancelKeyPress += Console_CancelKeyPress;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Unable to register a callback to the Console.CancelKeyPress event.");
            }

            TracerSettings tracerSettings = _settings.TracerSettings ?? TracerSettings.FromDefaultSources();

            // Set the tracer buffer size to the max
            tracerSettings.TraceBufferSize = 1024 * 1024 * 45; // slightly lower than the 50mb payload agent limit.

            // Set the service name if empty
            Log.Information("Setting up the service name");
            if (string.IsNullOrEmpty(tracerSettings.ServiceName))
            {
                // Extract repository name from the git url and use it as a default service name.
                string repository = CIEnvironmentValues.Repository;
                if (!string.IsNullOrEmpty(repository))
                {
                    Regex regex = new Regex(@"/([a-zA-Z0-9\\\-_.]*)$");
                    Match match = regex.Match(repository);
                    if (match.Success && match.Groups.Count > 1)
                    {
                        const string gitSuffix = ".git";
                        string repoName = match.Groups[1].Value;
                        if (repoName.EndsWith(gitSuffix))
                        {
                            tracerSettings.ServiceName = repoName.Substring(0, repoName.Length - gitSuffix.Length);
                        }
                        else
                        {
                            tracerSettings.ServiceName = repoName;
                        }
                    }
                }
            }

            // Initialize Tracer
            Log.Information("Initialize Test Tracer instance");
            Tracer.Instance = new CITracer(tracerSettings);
        }

        internal static void FlushSpans()
        {
            try
            {
                var flushThread = new Thread(() => InternalFlush().GetAwaiter().GetResult());
                flushThread.IsBackground = false;
                flushThread.Name = "FlushThread";
                flushThread.Start();
                flushThread.Join();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception occurred when flushing spans.");
            }

            static async Task InternalFlush()
            {
                try
                {
                    // We have to ensure the flush of the buffer after we finish the tests of an assembly.
                    // For some reason, sometimes when all test are finished none of the callbacks to handling the tracer disposal is triggered.
                    // So the last spans in buffer aren't send to the agent.
                    Log.Debug("Integration flushing spans.");
                    await Tracer.Instance.FlushAsync().ConfigureAwait(false);
                    Log.Debug("Integration flushed.");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Exception occurred when flushing spans.");
                }
            }
        }

        private static bool InternalEnabled()
        {
            if (_settings.Enabled)
            {
                Log.Information("CI Visibility Enabled by Configuration");
                return true;
            }

            // Try to autodetect based in the domain name.
            string domainName = AppDomain.CurrentDomain.FriendlyName;
            if (domainName != null &&
                (domainName.StartsWith("testhost") == true ||
                 domainName.StartsWith("vstest") == true ||
                 domainName.StartsWith("xunit") == true ||
                 domainName.StartsWith("nunit") == true ||
                 domainName.StartsWith("MSBuild") == true))
            {
                Log.Information("CI Visibility Enabled by Domain name whitelist");

                try
                {
                    // Set the configuration key to propagate the configuration to child processes.
                    Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityEnabled, "1", EnvironmentVariableTarget.Process);
                }
                catch
                {
                    // .
                }

                return true;
            }

            // Try to autodetect based in the process name.
            if (Process.GetCurrentProcess()?.ProcessName?.StartsWith("testhost.") == true)
            {
                Log.Information("CI Visibility Enabled by Process name whitelist");

                try
                {
                    // Set the configuration key to propagate the configuration to child processes.
                    Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityEnabled, "1", EnvironmentVariableTarget.Process);
                }
                catch
                {
                    // .
                }

                return true;
            }

            return false;
        }

        private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            Log.Information("Console Cancel key press detected");

            // Let's ensure to flush the buffers.
            FlushSpans();
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Log.Information("Domain UnhandledException received");

            // Let's ensure to flush the buffers.
            FlushSpans();
        }

        private static void CurrentDomain_DomainUnload(object sender, EventArgs e)
        {
            Log.Information("Domain Unload");

            // Let's ensure to flush the buffers.
            FlushSpans();
        }

        private static void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            Log.Information("Exiting...");

            // Let's ensure to flush the buffers.
            FlushSpans();
        }
    }
}
