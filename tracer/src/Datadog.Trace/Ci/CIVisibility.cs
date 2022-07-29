// <copyright file="CIVisibility.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.Ci.Configuration;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Pdb;

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
            Log.Information("Environment.CommandLine: {cmd}", Environment.CommandLine);

            LifetimeManager.Instance.AddAsyncShutdownTask(ShutdownAsync);

            TracerSettings tracerSettings = _settings.TracerSettings;

            // Set the service name if empty
            Log.Information("Setting up the service name");
            if (string.IsNullOrEmpty(tracerSettings.ServiceName))
            {
                // Extract repository name from the git url and use it as a default service name.
                tracerSettings.ServiceName = GetServiceNameFromRepository(CIEnvironmentValues.Instance.Repository);
            }

            // Update and upload git tree metadata.
            Log.Information("Update and uploading git tree metadata.");
            var itrClient = new ITRClient(CIEnvironmentValues.Instance.WorkspacePath, _settings);
            var tskItrUpdate = UploadGitMetadataAsync();
            LifetimeManager.Instance.AddAsyncShutdownTask(() => tskItrUpdate);

            // Initialize Tracer
            Log.Information("Initialize Test Tracer instance");
            TracerManager.ReplaceGlobalManager(tracerSettings.Build(), new CITracerManagerFactory(_settings));

            static async Task UploadGitMetadataAsync()
            {
                try
                {
                    var itrClient = new ITRClient(CIEnvironmentValues.Instance.WorkspacePath, _settings);
                    await itrClient.UploadRepositoryChangesAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "ITR: Error uploading repository git metadata.");
                }
            }
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

        internal static IApiRequestFactory GetRequestFactory(ImmutableTracerSettings settings)
        {
            IApiRequestFactory factory = null;
            TimeSpan agentlessTimeout = TimeSpan.FromSeconds(15);

#if NETCOREAPP
            Log.Information("Using {FactoryType} for trace transport.", nameof(HttpClientRequestFactory));
            factory = new HttpClientRequestFactory(settings.Exporter.AgentUri, AgentHttpHeaderNames.DefaultHeaders, timeout: agentlessTimeout);
#else
            Log.Information("Using {FactoryType} for trace transport.", nameof(ApiWebRequestFactory));
            factory = new ApiWebRequestFactory(settings.Exporter.AgentUri, AgentHttpHeaderNames.DefaultHeaders, timeout: agentlessTimeout);
#endif

            if (!string.IsNullOrWhiteSpace(_settings.ProxyHttps))
            {
                var proxyHttpsUriBuilder = new UriBuilder(_settings.ProxyHttps);

                var userName = proxyHttpsUriBuilder.UserName;
                var password = proxyHttpsUriBuilder.Password;

                proxyHttpsUriBuilder.UserName = string.Empty;
                proxyHttpsUriBuilder.Password = string.Empty;

                if (proxyHttpsUriBuilder.Scheme == "https")
                {
                    // HTTPS proxy is not supported by .NET BCL
                    Log.Error($"HTTPS proxy is not supported. ({proxyHttpsUriBuilder})");
                    return factory;
                }

                NetworkCredential credential = null;
                if (!string.IsNullOrWhiteSpace(userName))
                {
                    credential = new NetworkCredential(userName, password);
                }

                Log.Information("Setting proxy to: {ProxyHttps}", proxyHttpsUriBuilder.Uri.ToString());
                factory.SetProxy(new WebProxy(proxyHttpsUriBuilder.Uri, true, _settings.ProxyNoProxy, credential), credential);
            }

            return factory;
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
                    Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.Enabled, "1", EnvironmentVariableTarget.Process);
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
                    Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.Enabled, "1", EnvironmentVariableTarget.Process);
                }
                catch
                {
                    // .
                }

                return true;
            }

            return false;
        }
    }
}
