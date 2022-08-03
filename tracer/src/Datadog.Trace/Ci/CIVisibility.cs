// <copyright file="CIVisibility.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Ci.Configuration;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Pdb;
using Datadog.Trace.Util;

namespace Datadog.Trace.Ci
{
    internal class CIVisibility
    {
        private static readonly CIVisibilitySettings _settings = CIVisibilitySettings.FromDefaultSources();
        private static int _firstInitialization = 1;
        private static Lazy<bool> _enabledLazy = new Lazy<bool>(() => InternalEnabled(), true);
        internal static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(CIVisibility));

        public static bool Enabled => _enabledLazy.Value;

        public static bool IsRunning => Interlocked.CompareExchange(ref _firstInitialization, 0, 0) == 0;

        public static CIVisibilitySettings Settings => _settings;

        public static CITracerManager Manager
        {
            get
            {
                if (Tracer.Instance.TracerManager is CITracerManager cITracerManager)
                {
                    return cITracerManager;
                }

                return null;
            }
        }

        public static void Initialize()
        {
            if (Interlocked.Exchange(ref _firstInitialization, 0) != 1)
            {
                // Initialize() was already called before
                return;
            }

            Log.Information("Initializing CI Visibility");

            LifetimeManager.Instance.AddAsyncShutdownTask(ShutdownAsync);

            TracerSettings tracerSettings = _settings.TracerSettings;

            // Set the service name if empty
            Log.Information("Setting up the service name");
            if (string.IsNullOrEmpty(tracerSettings.ServiceName))
            {
                // Extract repository name from the git url and use it as a default service name.
                tracerSettings.ServiceName = GetServiceNameFromRepository(CIEnvironmentValues.Instance.Repository);
            }

            // Initialize Tracer
            Log.Information("Initialize Test Tracer instance");
            TracerManager.ReplaceGlobalManager(tracerSettings.Build(), new CITracerManagerFactory(_settings));
        }

        internal static void FlushSpans()
        {
            try
            {
                var flushThread = new Thread(InternalFlush);
                flushThread.IsBackground = false;
                flushThread.Name = "FlushThread";
                flushThread.Start();
                flushThread.Join();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception occurred when flushing spans.");
            }

            static void InternalFlush()
            {
                if (!InternalFlushAsync().Wait(30_000))
                {
                    Log.Error("Timeout occurred when flushing spans.");
                }
            }
        }

        internal static string GetServiceNameFromRepository(string repository)
        {
            if (!string.IsNullOrEmpty(repository))
            {
                if (repository.EndsWith("/") || repository.EndsWith("\\"))
                {
                    repository = repository.Substring(0, repository.Length - 1);
                }

                Regex regex = new Regex(@"[/\\]?([a-zA-Z0-9\-_.]*)$");
                Match match = regex.Match(repository);
                if (match.Success && match.Groups.Count > 1)
                {
                    const string gitSuffix = ".git";
                    string repoName = match.Groups[1].Value;
                    if (repoName.EndsWith(gitSuffix))
                    {
                        return repoName.Substring(0, repoName.Length - gitSuffix.Length);
                    }
                    else
                    {
                        return repoName;
                    }
                }
            }

            return string.Empty;
        }

        private static async Task InternalFlushAsync()
        {
            try
            {
                // We have to ensure the flush of the buffer after we finish the tests of an assembly.
                // For some reason, sometimes when all test are finished none of the callbacks to handling the tracer disposal is triggered.
                // So the last spans in buffer aren't send to the agent.
                Log.Debug("Integration flushing spans.");

                if (_settings.Logs)
                {
                    await Task.WhenAll(
                        Tracer.Instance.FlushAsync(),
                        Tracer.Instance.TracerManager.DirectLogSubmission.Sink.FlushAsync()).ConfigureAwait(false);
                }
                else
                {
                    await Tracer.Instance.FlushAsync().ConfigureAwait(false);
                }

                Log.Debug("Integration flushed.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception occurred when flushing spans.");
            }
        }

        private static async Task ShutdownAsync()
        {
            await InternalFlushAsync().ConfigureAwait(false);
            MethodSymbolResolver.Instance.Clear();
        }

        private static bool InternalEnabled()
        {
            var processName = ProcessHelpers.GetCurrentProcessName() ?? string.Empty;

            // By configuration
            if (_settings.Enabled)
            {
                // When is enabled by configuration we only enable it to the testhost child process if the process name is dotnet.
                if (processName.Equals("dotnet", StringComparison.OrdinalIgnoreCase) && Environment.CommandLine.IndexOf("testhost.dll", StringComparison.OrdinalIgnoreCase) == -1)
                {
                    Log.Information("CI Visibility disabled because the process name is 'dotnet' but the commandline doesn't contain 'testhost.dll': {cmdline}", Environment.CommandLine);
                    return false;
                }

                Log.Information("CI Visibility Enabled by Configuration");
                return true;
            }

            // Try to autodetect based in the domain name.
            var domainName = AppDomain.CurrentDomain.FriendlyName ?? string.Empty;
            if (domainName.StartsWith("testhost", StringComparison.Ordinal) ||
                domainName.StartsWith("vstest", StringComparison.Ordinal) ||
                domainName.StartsWith("xunit", StringComparison.Ordinal) ||
                domainName.StartsWith("nunit", StringComparison.Ordinal) ||
                domainName.StartsWith("MSBuild", StringComparison.Ordinal))
            {
                Log.Information("CI Visibility Enabled by Domain name whitelist");
                PropagateCiVisibilityEnvironmentVariable();
                return true;
            }

            // Try to autodetect based in the process name.
            if (processName.StartsWith("testhost.", StringComparison.Ordinal))
            {
                Log.Information("CI Visibility Enabled by Process name whitelist");
                PropagateCiVisibilityEnvironmentVariable();
                return true;
            }

            return false;

            static void PropagateCiVisibilityEnvironmentVariable()
            {
                try
                {
                    // Set the configuration key to propagate the configuration to child processes.
                    Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.Enabled, "1", EnvironmentVariableTarget.Process);
                }
                catch
                {
                    // .
                }
            }
        }
    }
}
