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
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.Ci.Configuration;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Pdb;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.Processors;
using Datadog.Trace.Util;

namespace Datadog.Trace.Ci
{
    internal class CIVisibility
    {
        private static readonly Lazy<bool> EnabledLazy = new(InternalEnabled, true);

        private static CIVisibilitySettings? _settings;
        private static int _firstInitialization = 1;
        private static Task? _skippableTestsTask;
        private static Dictionary<string, Dictionary<string, IList<SkippableTest>>>? _skippableTestsBySuiteAndName;

        internal static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(CIVisibility));

        public static bool Enabled => EnabledLazy.Value;

        public static bool IsRunning => Interlocked.CompareExchange(ref _firstInitialization, 0, 0) == 0;

        public static CIVisibilitySettings Settings
        {
            get => LazyInitializer.EnsureInitialized(ref _settings, () => CIVisibilitySettings.FromDefaultSources())!;
            private set => _settings = value;
        }

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
                // Initialize() or InitializeFromRunner() or InitializeFromManualInstrumentation() was already called before
                return;
            }

            Log.Information("Initializing CI Visibility");
            var settings = Settings;

            // In case we are running using the agent, check if the event platform proxy is supported.
            IDiscoveryService discoveryService = NullDiscoveryService.Instance;
            var eventPlatformProxyEnabled = false;
            if (!settings.Agentless)
            {
                if (!settings.ForceAgentsEvpProxy)
                {
                    discoveryService = DiscoveryService.Create(
                        new ImmutableExporterSettings(settings.TracerSettings.Exporter),
                        tcpTimeout: TimeSpan.FromSeconds(5),
                        initialRetryDelayMs: 10,
                        maxRetryDelayMs: 1000,
                        recheckIntervalMs: int.MaxValue);
                }
                else
                {
                    Log.Information("Using the Event platform proxy through the agent.");
                }

                eventPlatformProxyEnabled = settings.ForceAgentsEvpProxy || IsEventPlatformProxySupportedByAgent(discoveryService);
            }

            LifetimeManager.Instance.AddAsyncShutdownTask(ShutdownAsync);

            var tracerSettings = settings.TracerSettings;

            // Set the service name if empty
            Log.Debug("Setting up the service name");
            if (string.IsNullOrEmpty(tracerSettings.ServiceName))
            {
                // Extract repository name from the git url and use it as a default service name.
                tracerSettings.ServiceName = GetServiceNameFromRepository(CIEnvironmentValues.Instance.Repository);
            }

            // Normalize the service name
            tracerSettings.ServiceName = NormalizerTraceProcessor.NormalizeService(tracerSettings.ServiceName);

            // Initialize Tracer
            Log.Information("Initialize Test Tracer instance");
            TracerManager.ReplaceGlobalManager(tracerSettings.Build(), new CITracerManagerFactory(settings, discoveryService, eventPlatformProxyEnabled));
            _ = Tracer.Instance;

            // Initialize FrameworkDescription
            _ = FrameworkDescription.Instance;

            // Initialize CIEnvironment
            _ = CIEnvironmentValues.Instance;

            // If we are running in agentless mode or the agent support the event platform proxy endpoint.
            // We can use the intelligent test runner
            if (settings.Agentless || eventPlatformProxyEnabled)
            {
                // Intelligent Test Runner or GitUploadEnabled
                if (settings.IntelligentTestRunnerEnabled)
                {
                    Log.Information("ITR: Update and uploading git tree metadata and getting skippable tests.");
                    _skippableTestsTask = GetIntelligentTestRunnerSkippableTestsAsync();
                    LifetimeManager.Instance.AddAsyncShutdownTask(() => _skippableTestsTask);
                }
                else if (settings.GitUploadEnabled != false)
                {
                    // Update and upload git tree metadata.
                    Log.Information("ITR: Update and uploading git tree metadata.");
                    var tskItrUpdate = UploadGitMetadataAsync();
                    LifetimeManager.Instance.AddAsyncShutdownTask(() => tskItrUpdate);
                }
            }
            else if (settings.IntelligentTestRunnerEnabled)
            {
                Log.Warning("ITR: Intelligent test runner cannot be activated. Agent doesn't support the event platform proxy endpoint.");
            }
            else if (settings.GitUploadEnabled != false)
            {
                Log.Warning("ITR: Upload git metadata cannot be activated. Agent doesn't support the event platform proxy endpoint.");
            }
        }

        internal static void InitializeFromRunner(CIVisibilitySettings settings, IDiscoveryService discoveryService, bool eventPlatformProxyEnabled)
        {
            if (Interlocked.Exchange(ref _firstInitialization, 0) != 1)
            {
                // Initialize() or InitializeFromRunner() was already called before
                return;
            }

            Log.Information("Initializing CI Visibility from dd-trace / runner");
            Settings = settings;
            LifetimeManager.Instance.AddAsyncShutdownTask(ShutdownAsync);

            var tracerSettings = settings.TracerSettings;

            // Set the service name if empty
            Log.Debug("Setting up the service name");
            if (string.IsNullOrEmpty(tracerSettings.ServiceName))
            {
                // Extract repository name from the git url and use it as a default service name.
                tracerSettings.ServiceName = GetServiceNameFromRepository(CIEnvironmentValues.Instance.Repository);
            }

            // Normalize the service name
            tracerSettings.ServiceName = NormalizerTraceProcessor.NormalizeService(tracerSettings.ServiceName);

            // Initialize Tracer
            Log.Information("Initialize Test Tracer instance");
            TracerManager.ReplaceGlobalManager(tracerSettings.Build(), new CITracerManagerFactory(settings, discoveryService, eventPlatformProxyEnabled));
            _ = Tracer.Instance;

            // Initialize FrameworkDescription
            _ = FrameworkDescription.Instance;

            // Initialize CIEnvironment
            _ = CIEnvironmentValues.Instance;
        }

        internal static void InitializeFromManualInstrumentation()
        {
            if (!IsRunning)
            {
                // If we are using only the Public API without auto-instrumentation (TestSession/TestModule/TestSuite/Test classes only)
                // then we can disable both GitUpload and Intelligent Test Runner feature (only used by our integration).
                Settings.SetDefaultManualInstrumentationSettings();
                Initialize();
            }
        }

        internal static void Flush()
        {
            var sContext = SynchronizationContext.Current;
            using var cts = new CancellationTokenSource(30_000);
            try
            {
                SynchronizationContext.SetSynchronizationContext(null);
                AsyncUtil.RunSync(() => FlushAsync(), cts.Token);
                if (cts.IsCancellationRequested)
                {
                    Log.Error("Timeout occurred when flushing spans.{NewLine}{StackTrace}", Environment.NewLine, Environment.StackTrace);
                }
            }
            catch (TaskCanceledException)
            {
                Log.Error("Timeout occurred when flushing spans.{NewLine}{StackTrace}", Environment.NewLine, Environment.StackTrace);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(sContext);
            }
        }

        internal static async Task FlushAsync()
        {
            try
            {
                // We have to ensure the flush of the buffer after we finish the tests of an assembly.
                // For some reason, sometimes when all test are finished none of the callbacks to handling the tracer disposal is triggered.
                // So the last spans in buffer aren't send to the agent.
                Log.Debug("Integration flushing spans.");

                if (Settings.Logs)
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

        internal static void WaitForSkippableTaskToFinish()
        {
            if (_skippableTestsTask is { IsCompleted: false })
            {
                var sContext = SynchronizationContext.Current;
                try
                {
                    SynchronizationContext.SetSynchronizationContext(null);
                    AsyncUtil.RunSync(() => _skippableTestsTask);
                }
                finally
                {
                    SynchronizationContext.SetSynchronizationContext(sContext);
                }
            }
        }

        internal static Task<IList<SkippableTest>> GetSkippableTestsFromSuiteAndNameAsync(string suite, string name)
        {
            if (_skippableTestsTask is { } skippableTask)
            {
                if (skippableTask.IsCompleted)
                {
                    return Task.FromResult(GetSkippableTestsFromSuiteAndName(suite, name));
                }

                return SlowGetSkippableTestsFromSuiteAndNameAsync(suite, name);
            }

            return Task.FromResult((IList<SkippableTest>)Array.Empty<SkippableTest>());

            static async Task<IList<SkippableTest>> SlowGetSkippableTestsFromSuiteAndNameAsync(string suite, string name)
            {
                await _skippableTestsTask!.ConfigureAwait(false);
                return GetSkippableTestsFromSuiteAndName(suite, name);
            }

            static IList<SkippableTest> GetSkippableTestsFromSuiteAndName(string suite, string name)
            {
                if (_skippableTestsBySuiteAndName is { } skippeableTestBySuite)
                {
                    if (skippeableTestBySuite.TryGetValue(suite, out var testsInSuite) &&
                        testsInSuite.TryGetValue(name, out var tests))
                    {
                        return tests;
                    }
                }

                return Array.Empty<SkippableTest>();
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
            return GetRequestFactory(settings, TimeSpan.FromSeconds(15));
        }

        internal static IApiRequestFactory GetRequestFactory(ImmutableTracerSettings tracerSettings, TimeSpan timeout)
        {
            IApiRequestFactory? factory = null;

#if NETCOREAPP
            Log.Information("Using {FactoryType} for trace transport.", nameof(HttpClientRequestFactory));
            factory = new HttpClientRequestFactory(tracerSettings.Exporter.AgentUri, AgentHttpHeaderNames.DefaultHeaders, timeout: timeout);
#else
            Log.Information("Using {FactoryType} for trace transport.", nameof(ApiWebRequestFactory));
            factory = new ApiWebRequestFactory(tracerSettings.Exporter.AgentUri, AgentHttpHeaderNames.DefaultHeaders, timeout: timeout);
#endif

            var settings = Settings;
            if (!string.IsNullOrWhiteSpace(settings.ProxyHttps))
            {
                var proxyHttpsUriBuilder = new UriBuilder(settings.ProxyHttps);

                var userName = proxyHttpsUriBuilder.UserName;
                var password = proxyHttpsUriBuilder.Password;

                proxyHttpsUriBuilder.UserName = string.Empty;
                proxyHttpsUriBuilder.Password = string.Empty;

                if (proxyHttpsUriBuilder.Scheme == "https")
                {
                    // HTTPS proxy is not supported by .NET BCL
                    Log.Error("HTTPS proxy is not supported. ({ProxyHttpsUriBuilder})", proxyHttpsUriBuilder);
                    return factory;
                }

                NetworkCredential? credential = null;
                if (!string.IsNullOrWhiteSpace(userName))
                {
                    credential = new NetworkCredential(userName, password);
                }

                Log.Information("Setting proxy to: {ProxyHttps}", proxyHttpsUriBuilder.Uri.ToString());
                factory.SetProxy(new WebProxy(proxyHttpsUriBuilder.Uri, true, settings.ProxyNoProxy, credential), credential);
            }

            return factory;
        }

        internal static string GetOperatingSystemVersion()
        {
            switch (FrameworkDescription.Instance.OSPlatform)
            {
                case OSPlatformName.Linux:
                    if (!string.IsNullOrEmpty(HostMetadata.Instance.KernelRelease))
                    {
                        return HostMetadata.Instance.KernelRelease;
                    }

                    break;
                case OSPlatformName.MacOS:
                    var context = SynchronizationContext.Current;
                    try
                    {
                        if (context is not null && AppDomain.CurrentDomain.IsFullyTrusted)
                        {
                            SynchronizationContext.SetSynchronizationContext(null);
                        }

                        var osxVersionCommand = AsyncUtil.RunSync(() => ProcessHelpers.RunCommandAsync(new ProcessHelpers.Command("uname", "-r")));
                        var osxVersion = osxVersionCommand?.Output.Trim(' ', '\n');
                        if (!string.IsNullOrEmpty(osxVersion))
                        {
                            return osxVersion!;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Error getting OS version on macOS");
                    }
                    finally
                    {
                        if (context is not null && AppDomain.CurrentDomain.IsFullyTrusted)
                        {
                            SynchronizationContext.SetSynchronizationContext(null);
                        }
                    }

                    break;
            }

            return Environment.OSVersion.VersionString;
        }

        private static async Task ShutdownAsync()
        {
            await FlushAsync().ConfigureAwait(false);
            MethodSymbolResolver.Instance.Clear();
        }

        private static bool InternalEnabled()
        {
            var processName = string.Empty;

            try
            {
                processName = ProcessHelpers.GetCurrentProcessName() ?? string.Empty;
            }
            catch (Exception exception)
            {
                Log.Warning(exception, "Error getting current process name when checking CI Visibility status");
            }

            // By configuration
            if (Settings.Enabled)
            {
                // When is enabled by configuration we only enable it to the testhost child process if the process name is dotnet.
                if (processName.Equals("dotnet", StringComparison.OrdinalIgnoreCase) && Environment.CommandLine.IndexOf("testhost.dll", StringComparison.OrdinalIgnoreCase) == -1)
                {
                    Log.Information("CI Visibility disabled because the process name is 'dotnet' but the commandline doesn't contain 'testhost.dll': {Cmdline}", Environment.CommandLine);
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
                var itrClient = new IntelligentTestRunnerClient(CIEnvironmentValues.Instance.WorkspacePath, Settings);
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
                var settings = Settings;
                var lazyItrClient = new Lazy<IntelligentTestRunnerClient>(() => new(CIEnvironmentValues.Instance.WorkspacePath, settings));

                Task? uploadRepositoryChangesTask = null;
                if (settings.GitUploadEnabled != false)
                {
                    // Upload the git metadata
                    uploadRepositoryChangesTask = Task.Run(() => lazyItrClient.Value.UploadRepositoryChangesAsync());
                }

                if (!settings.Agentless || !string.IsNullOrEmpty(settings.ApplicationKey))
                {
                    // If any DD_CIVISIBILITY_CODE_COVERAGE_ENABLED or DD_CIVISIBILITY_TESTSSKIPPING_ENABLED has not been set
                    // We query the settings api for those
                    if (settings.CodeCoverageEnabled == null || settings.TestsSkippingEnabled == null)
                    {
                        var itrSettings = await lazyItrClient.Value.GetSettingsAsync().ConfigureAwait(false);

                        if (settings.CodeCoverageEnabled == null && itrSettings.CodeCoverage.HasValue)
                        {
                            Log.Information("ITR: Code Coverage has been changed to {Value} by settings api.", itrSettings.CodeCoverage.Value);
                            settings.SetCodeCoverageEnabled(itrSettings.CodeCoverage.Value);
                        }

                        if (settings.TestsSkippingEnabled == null && itrSettings.TestsSkipping.HasValue)
                        {
                            Log.Information("ITR: Tests Skipping has been changed to {Value} by settings api.", itrSettings.TestsSkipping.Value);
                            settings.SetTestsSkippingEnabled(itrSettings.TestsSkipping.Value);
                        }
                    }
                }

                // Log code coverage status
                Log.Information("{V}", settings.CodeCoverageEnabled == true ? "ITR: Tests code coverage is enabled." : "ITR: Tests code coverage is disabled.");

                // For ITR we need the git metadata upload before consulting the skippable tests.
                // If ITR is disable we just need to make sure the git upload task has completed before leaving this method.
                if (uploadRepositoryChangesTask is not null)
                {
                    await uploadRepositoryChangesTask.ConfigureAwait(false);
                }

                // If the tests skipping feature is enabled we query the api for the tests we have to skip
                if (settings.TestsSkippingEnabled == true)
                {
                    var skippeableTests = await lazyItrClient.Value.GetSkippableTestsAsync().ConfigureAwait(false);
                    Log.Information<int>("ITR: SkippableTests = {Length}.", skippeableTests.Length);

                    var skippableTestsBySuiteAndName = new Dictionary<string, Dictionary<string, IList<SkippableTest>>>();
                    foreach (var item in skippeableTests)
                    {
                        if (!skippableTestsBySuiteAndName.TryGetValue(item.Suite, out var suite))
                        {
                            suite = new Dictionary<string, IList<SkippableTest>>();
                            skippableTestsBySuiteAndName[item.Suite] = suite;
                        }

                        if (!suite.TryGetValue(item.Name, out var name))
                        {
                            name = new List<SkippableTest>();
                            suite[item.Name] = name;
                        }

                        name.Add(item);
                    }

                    _skippableTestsBySuiteAndName = skippableTestsBySuiteAndName;
                    Log.Debug("ITR: SkippableTests dictionary has been built.");
                }
                else
                {
                    Log.Information("ITR: Tests skipping is disabled.");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "ITR: Error getting skippeable tests.");
            }
        }

        private static bool IsEventPlatformProxySupportedByAgent(IDiscoveryService discoveryService)
        {
            var eventPlatformProxyEnabled = false;
            AgentConfiguration? agentConfiguration = null;
            ManualResetEventSlim manualResetEventSlim = new(false);
            LifetimeManager.Instance.AddShutdownTask(() => manualResetEventSlim.Set());

            Log.Debug("Waiting for agent configuration...");
            discoveryService.SubscribeToChanges(CallBack);
            void CallBack(AgentConfiguration aConfiguration)
            {
                agentConfiguration = aConfiguration;
                manualResetEventSlim.Set();
                discoveryService.RemoveSubscription(CallBack);
                Log.Debug("Agent configuration received.");
            }

            // We wait up to 5 seconds for the configuration to be retrieved.
            manualResetEventSlim.Wait(5_000);
            if (!string.IsNullOrEmpty(Volatile.Read(ref agentConfiguration)?.EventPlatformProxyEndpoint))
            {
                Log.Information("Event platform proxy supported by agent.");
                eventPlatformProxyEnabled = true;
            }
            else
            {
                Log.Information("Event platform proxy is not supported by the agent. Falling back to the APM protocol.");
            }

            return eventPlatformProxyEnabled;
        }
    }
}
