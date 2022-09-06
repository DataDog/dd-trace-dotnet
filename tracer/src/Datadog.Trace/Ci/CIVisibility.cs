// <copyright file="CIVisibility.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
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
using Datadog.Trace.Util;

namespace Datadog.Trace.Ci
{
    internal class CIVisibility
    {
        private static readonly CIVisibilitySettings _settings = CIVisibilitySettings.FromDefaultSources();
        private static int _firstInitialization = 1;
        private static Lazy<bool> _enabledLazy = new Lazy<bool>(() => InternalEnabled(), true);
        private static Task? _skippableTask;
        private static Dictionary<string, Dictionary<string, IList<SkippeableTest>>>? _skippableTestsBySuiteAndName;

        internal static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(CIVisibility));

        public static bool Enabled => _enabledLazy.Value;

        public static bool IsRunning => Interlocked.CompareExchange(ref _firstInitialization, 0, 0) == 0;

        public static CIVisibilitySettings Settings => _settings;

        public static CITracerManager? Manager
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

            // Intelligent Test Runner
            if (_settings.IntelligentTestRunnerEnabled)
            {
                Log.Information("ITR: Update and uploading git tree metadata and getting skippeable tests.");
                _skippableTask = GetIntelligentTestRunnerSkippableTestsAsync();
                LifetimeManager.Instance.AddAsyncShutdownTask(() => _skippableTask);
            }
            else if (_settings.GitUploadEnabled)
            {
                // Update and upload git tree metadata.
                Log.Information("ITR: Update and uploading git tree metadata.");
                var tskItrUpdate = UploadGitMetadataAsync();
                LifetimeManager.Instance.AddAsyncShutdownTask(() => tskItrUpdate);
            }

            // Initialize Tracer
            Log.Information("Initialize Test Tracer instance");
            TracerManager.ReplaceGlobalManager(tracerSettings.Build(), new CITracerManagerFactory(_settings));
            _ = Tracer.Instance;
        }

        internal static void FlushSpans()
        {
            var sContext = SynchronizationContext.Current;
            try
            {
                SynchronizationContext.SetSynchronizationContext(null);
                if (!InternalFlushAsync().Wait(30_000))
                {
                    Log.Error("Timeout occurred when flushing spans.");
                }
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(sContext);
            }
        }

        internal static Task<IList<SkippeableTest>> GetSkippableTestsFromSuiteAndNameAsync(string suite, string name)
        {
            if (_skippableTask is { } skippableTask)
            {
                if (skippableTask.IsCompleted)
                {
                    return Task.FromResult(GetSkippableTestsFromSuiteAndName(suite, name));
                }

                return SlowGetSkippableTestsFromSuiteAndNameAsync(suite, name);
            }

            return Task.FromResult((IList<SkippeableTest>)Array.Empty<SkippeableTest>());

            static async Task<IList<SkippeableTest>> SlowGetSkippableTestsFromSuiteAndNameAsync(string suite, string name)
            {
                if (_skippableTask is { } skippableTask)
                {
                    await skippableTask.ConfigureAwait(false);
                }

                return GetSkippableTestsFromSuiteAndName(suite, name);
            }

            static IList<SkippeableTest> GetSkippableTestsFromSuiteAndName(string suite, string name)
            {
                if (_skippableTestsBySuiteAndName is { } skippeableTestBySuite)
                {
                    if (skippeableTestBySuite.TryGetValue(suite, out var testsInSuite) &&
                        testsInSuite.TryGetValue(name, out var tests))
                    {
                        return tests;
                    }
                }

                return Array.Empty<SkippeableTest>();
            }
        }

        internal static bool HasSkippableTests()
        {
            return _skippableTestsBySuiteAndName?.Count > 0;
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
            IApiRequestFactory? factory = null;
            TimeSpan agentlessTimeout = TimeSpan.FromSeconds(45);

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

                NetworkCredential? credential = null;
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

        private static async Task UploadGitMetadataAsync()
        {
            try
            {
                var itrClient = new IntelligentTestRunnerClient(CIEnvironmentValues.Instance.WorkspacePath, _settings);
                await itrClient.UploadRepositoryChangesAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "ITR: Error uploading repository git metadata.");
            }
        }

        private static async Task GetIntelligentTestRunnerSkippableTestsAsync()
        {
            try
            {
                var itrClient = new IntelligentTestRunnerClient(CIEnvironmentValues.Instance.WorkspacePath, _settings);
                await itrClient.UploadRepositoryChangesAsync().ConfigureAwait(false);
                var skippeableTests = await itrClient.GetSkippeableTestsAsync().ConfigureAwait(false);
                Log.Information<int>("ITR: SkippableTests = {length}.", skippeableTests.Length);

                var skippableTestsBySuiteAndName = new Dictionary<string, Dictionary<string, IList<SkippeableTest>>>();
                foreach (var item in skippeableTests)
                {
                    if (!skippableTestsBySuiteAndName.TryGetValue(item.Suite, out var suite))
                    {
                        suite = new Dictionary<string, IList<SkippeableTest>>();
                        skippableTestsBySuiteAndName[item.Suite] = suite;
                    }

                    if (!suite.TryGetValue(item.Name, out var name))
                    {
                        name = new List<SkippeableTest>();
                        suite[item.Name] = name;
                    }

                    name.Add(item);
                }

                _skippableTestsBySuiteAndName = skippableTestsBySuiteAndName;
                Log.Debug<int>("ITR: SkippableTests dictionary has been built.", skippeableTests.Length);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "ITR: Error getting skippeable tests.");
            }
        }
    }
}
