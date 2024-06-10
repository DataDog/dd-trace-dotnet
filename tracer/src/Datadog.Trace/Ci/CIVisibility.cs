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
using Datadog.Trace.Agent.StreamFactories;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.Ci.CiEnvironment;
using Datadog.Trace.Ci.Configuration;
using Datadog.Trace.Configuration;
using Datadog.Trace.HttpOverStreams;
using Datadog.Trace.Logging;
using Datadog.Trace.Pdb;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.Processors;
using Datadog.Trace.Util;

namespace Datadog.Trace.Ci
{
    internal class CIVisibility
    {
        private static Lazy<bool> _enabledLazy = new(InternalEnabled, true);
        private static CIVisibilitySettings? _settings;
        private static int _firstInitialization = 1;
        private static Task? _skippableTestsTask;
        private static string? _skippableTestsCorrelationId;
        private static Dictionary<string, Dictionary<string, IList<SkippableTest>>>? _skippableTestsBySuiteAndName;
        private static string? _osVersion;

        internal static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(CIVisibility));

        public static bool Enabled => _enabledLazy.Value;

        public static bool IsRunning
        {
            get
            {
                // We try first the fast path, if the value is 0 we are running, so we can avoid the Interlocked operation.
                if (_firstInitialization == 0)
                {
                    return true;
                }

                // If the value is not 0, maybe the value hasn't been updated yet, so we use the Interlocked operation to ensure the value is correct.
                return Interlocked.CompareExchange(ref _firstInitialization, 0, 0) == 0;
            }
        }

        public static CIVisibilitySettings Settings
        {
            get => LazyInitializer.EnsureInitialized(ref _settings, () => CIVisibilitySettings.FromDefaultSources())!;
            private set => _settings = value;
        }

        public static EventPlatformProxySupport EventPlatformProxySupport { get; private set; } = EventPlatformProxySupport.None;

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

        // Unlocked tracer manager is used in tests so tracer instance can be changed with a new configuration.
        internal static bool UseLockedTracerManager { get; set; } = true;

        internal static IntelligentTestRunnerClient.EarlyFlakeDetectionSettingsResponse EarlyFlakeDetectionSettings { get; private set; }

        internal static IntelligentTestRunnerClient.EarlyFlakeDetectionResponse? EarlyFlakeDetectionResponse { get; private set; }

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
                if (!string.IsNullOrWhiteSpace(settings.ForceAgentsEvpProxy))
                {
                    // if we force the evp proxy (internal switch)
                    EventPlatformProxySupport = Enum.TryParse<EventPlatformProxySupport>(settings.ForceAgentsEvpProxy, out var parsedValue) ?
                                                    parsedValue :
                                                    EventPlatformProxySupport.V2;
                }
                else
                {
                    discoveryService = DiscoveryService.Create(
                        new ImmutableExporterSettings(settings.TracerSettings.ExporterInternal, true),
                        tcpTimeout: TimeSpan.FromSeconds(5),
                        initialRetryDelayMs: 10,
                        maxRetryDelayMs: 1000,
                        recheckIntervalMs: int.MaxValue);
                    EventPlatformProxySupport = IsEventPlatformProxySupportedByAgent(discoveryService);
                }

                eventPlatformProxyEnabled = EventPlatformProxySupport != EventPlatformProxySupport.None;
                if (eventPlatformProxyEnabled)
                {
                    Log.Information("EVP Proxy was enabled with mode: {Mode}", EventPlatformProxySupport);
                }
            }

            LifetimeManager.Instance.AddAsyncShutdownTask(ShutdownAsync);

            var tracerSettings = settings.TracerSettings;

            // Set the service name if empty
            Log.Debug("Setting up the service name");
            if (string.IsNullOrEmpty(tracerSettings.ServiceNameInternal))
            {
                // Extract repository name from the git url and use it as a default service name.
                tracerSettings.ServiceNameInternal = GetServiceNameFromRepository(CIEnvironmentValues.Instance.Repository);
            }

            // Normalize the service name
            tracerSettings.ServiceNameInternal = NormalizerTraceProcessor.NormalizeService(tracerSettings.ServiceNameInternal);

            // Initialize Tracer
            Log.Information("Initialize Test Tracer instance");
            TracerManager.ReplaceGlobalManager(new ImmutableTracerSettings(tracerSettings, true), new CITracerManagerFactory(settings, discoveryService, eventPlatformProxyEnabled, UseLockedTracerManager));
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
                    LifetimeManager.Instance.AddAsyncShutdownTask(_ => _skippableTestsTask);
                }
                else if (settings.GitUploadEnabled != false)
                {
                    // Update and upload git tree metadata.
                    Log.Information("ITR: Update and uploading git tree metadata.");
                    var tskItrUpdate = UploadGitMetadataAsync();
                    LifetimeManager.Instance.AddAsyncShutdownTask(_ => tskItrUpdate);
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
            EventPlatformProxySupport = Enum.TryParse<EventPlatformProxySupport>(settings.ForceAgentsEvpProxy, out var parsedValue) ?
                                            parsedValue :
                                            IsEventPlatformProxySupportedByAgent(discoveryService);
            LifetimeManager.Instance.AddAsyncShutdownTask(ShutdownAsync);

            var tracerSettings = settings.TracerSettings;

            // Set the service name if empty
            Log.Debug("Setting up the service name");
            if (string.IsNullOrEmpty(tracerSettings.ServiceNameInternal))
            {
                // Extract repository name from the git url and use it as a default service name.
                tracerSettings.ServiceNameInternal = GetServiceNameFromRepository(CIEnvironmentValues.Instance.Repository);
            }

            // Normalize the service name
            tracerSettings.ServiceNameInternal = NormalizerTraceProcessor.NormalizeService(tracerSettings.ServiceNameInternal);

            // Initialize Tracer
            Log.Information("Initialize Test Tracer instance");
            TracerManager.ReplaceGlobalManager(new ImmutableTracerSettings(tracerSettings, true), new CITracerManagerFactory(settings, discoveryService, eventPlatformProxyEnabled, UseLockedTracerManager));
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

        /// <summary>
        /// Manually close the CI Visibility mode triggering the LifeManager to run the shutdown tasks
        /// This is required due to a weird behavior on the VSTest framework were the shutdown tasks are not awaited:
        /// ` if testhost doesn't shut down within 100ms(as the execution is completed, we expect it to shutdown fast).
        ///   vstest.console forcefully kills the process.`
        /// https://github.com/microsoft/vstest/issues/1900#issuecomment-457488472
        /// https://github.com/Microsoft/vstest/blob/2d4508232b6655a4f363b8bbcc887441c7d1d334/src/Microsoft.TestPlatform.CrossPlatEngine/Client/ProxyOperationManager.cs#L197
        /// </summary>
        internal static void Close()
        {
            if (IsRunning)
            {
                Log.Information("CI Visibility is exiting.");
                LifetimeManager.Instance.RunShutdownTasks();

                // If the continuous profiler is attached we ensure to flush the remaining profiles before closing.
                try
                {
                    if (ContinuousProfiler.Profiler.Instance.Status.IsProfilerReady)
                    {
                        ContinuousProfiler.NativeInterop.FlushProfile();
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error flushing the profiler.");
                }

                Interlocked.Exchange(ref _firstInitialization, 1);
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

        internal static bool IsAnEarlyFlakeDetectionTest(string moduleName, string testSuite, string testName)
        {
            if (EarlyFlakeDetectionResponse is { Tests: { } efdTests } &&
                efdTests.TryGetValue(moduleName, out var efdResponseSuites) &&
                efdResponseSuites?.TryGetValue(testSuite, out var efdResponseTests) == true &&
                efdResponseTests is not null)
            {
                foreach (var test in efdResponseTests)
                {
                    if (test == testName)
                    {
                        Log.Debug("Test is included in the early flake detection response. [ModuleName: {ModuleName}, TestSuite: {TestSuite}, TestName: {TestName}]", moduleName, testSuite, testName);
                        return true;
                    }
                }
            }

            Log.Debug("Test is not in the early flake detection response. [ModuleName: {ModuleName}, TestSuite: {TestSuite}, TestName: {TestName}]", moduleName, testSuite, testName);
            return false;
        }

        internal static bool HasSkippableTests() => _skippableTestsBySuiteAndName?.Count > 0;

        internal static string? GetSkippableTestsCorrelationId() => _skippableTestsCorrelationId;

        internal static string GetServiceNameFromRepository(string? repository)
        {
            if (!string.IsNullOrEmpty(repository))
            {
                if (repository!.EndsWith("/") || repository.EndsWith("\\"))
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
            var exporterSettings = tracerSettings.ExporterInternal;
            if (exporterSettings.TracesTransport != TracesTransportType.Default)
            {
                factory = AgentTransportStrategy.Get(
                    exporterSettings,
                    productName: "CI Visibility",
                    tcpTimeout: null,
                    AgentHttpHeaderNames.DefaultHeaders,
                    () => new TraceAgentHttpHeaderHelper(),
                    uri => uri);
            }
            else
            {
#if NETCOREAPP
                Log.Information("Using {FactoryType} for trace transport.", nameof(HttpClientRequestFactory));
                factory = new HttpClientRequestFactory(
                    exporterSettings.AgentUriInternal,
                    AgentHttpHeaderNames.DefaultHeaders,
                    handler: new System.Net.Http.HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate, },
                    timeout: timeout);
#else
                Log.Information("Using {FactoryType} for trace transport.", nameof(ApiWebRequestFactory));
                factory = new ApiWebRequestFactory(tracerSettings.ExporterInternal.AgentUriInternal, AgentHttpHeaderNames.DefaultHeaders, timeout: timeout);
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
            }

            return factory;
        }

        internal static string GetOperatingSystemVersion()
        {
            // we cache the OS version because is called multiple times during the test execution
            // and we want to avoid multiple system calls for Linux and macOS
            return _osVersion ??= GetOperatingSystemVersionInternal();

            static string GetOperatingSystemVersionInternal()
            {
                switch (FrameworkDescription.Instance.OSPlatform)
                {
                    case OSPlatformName.Linux:
                        if (!string.IsNullOrEmpty(HostMetadata.Instance.KernelRelease))
                        {
                            return HostMetadata.Instance.KernelRelease!;
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
        }

        /// <summary>
        /// Resets CI Visibility to the initial values. Used for testing purposes.
        /// </summary>
        internal static void Reset()
        {
            _settings = null;
            _firstInitialization = 1;
            _enabledLazy = new(InternalEnabled, true);
            _skippableTestsTask = null;
            _skippableTestsBySuiteAndName = null;
        }

        private static async Task ShutdownAsync(Exception? exception)
        {
            // Let's close any opened test, suite, modules and sessions before shutting down to avoid losing any data.
            // But marking them as failed.

            foreach (var test in Test.ActiveTests)
            {
                if (exception is not null)
                {
                    test.SetErrorInfo(exception);
                }

                test.Close(TestStatus.Fail);
            }

            foreach (var testSuite in TestSuite.ActiveTestSuites)
            {
                if (exception is not null)
                {
                    testSuite.SetErrorInfo(exception);
                }

                testSuite.Close();
            }

            foreach (var testModule in TestModule.ActiveTestModules)
            {
                if (exception is not null)
                {
                    testModule.SetErrorInfo(exception);
                }

                await testModule.CloseAsync().ConfigureAwait(false);
            }

            foreach (var testSession in TestSession.ActiveTestSessions)
            {
                if (exception is not null)
                {
                    testSession.SetErrorInfo(exception);
                }

                await testSession.CloseAsync(TestStatus.Fail).ConfigureAwait(false);
            }

            await FlushAsync().ConfigureAwait(false);
            MethodSymbolResolver.Instance.Clear();
        }

        private static bool InternalEnabled()
        {
            string? processName = null;

            // By configuration
            if (Settings.Enabled is { } enabled)
            {
                if (enabled)
                {
                    processName ??= GetProcessName();
                    // When is enabled by configuration we only enable it to the testhost child process if the process name is dotnet.
                    if (processName.Equals("dotnet", StringComparison.OrdinalIgnoreCase) &&
                        Environment.CommandLine.IndexOf("testhost", StringComparison.OrdinalIgnoreCase) == -1 &&
                        Environment.CommandLine.IndexOf("dotnet test", StringComparison.OrdinalIgnoreCase) == -1 &&
                        Environment.CommandLine.IndexOf("dotnet\" test", StringComparison.OrdinalIgnoreCase) == -1 &&
                        Environment.CommandLine.IndexOf("dotnet' test", StringComparison.OrdinalIgnoreCase) == -1 &&
                        Environment.CommandLine.IndexOf("dotnet.exe test", StringComparison.OrdinalIgnoreCase) == -1 &&
                        Environment.CommandLine.IndexOf("dotnet.exe\" test", StringComparison.OrdinalIgnoreCase) == -1 &&
                        Environment.CommandLine.IndexOf("dotnet.exe' test", StringComparison.OrdinalIgnoreCase) == -1 &&
                        Environment.CommandLine.IndexOf("dotnet.dll test", StringComparison.OrdinalIgnoreCase) == -1 &&
                        Environment.CommandLine.IndexOf("dotnet.dll\" test", StringComparison.OrdinalIgnoreCase) == -1 &&
                        Environment.CommandLine.IndexOf("dotnet.dll' test", StringComparison.OrdinalIgnoreCase) == -1 &&
                        Environment.CommandLine.IndexOf(" test ", StringComparison.OrdinalIgnoreCase) == -1 &&
                        Environment.CommandLine.IndexOf("datacollector", StringComparison.OrdinalIgnoreCase) == -1 &&
                        Environment.CommandLine.IndexOf("vstest.console.dll", StringComparison.OrdinalIgnoreCase) == -1)
                    {
                        Log.Information("CI Visibility disabled because the process name is 'dotnet' but the commandline doesn't contain 'testhost.dll': {Cmdline}", Environment.CommandLine);
                        return false;
                    }

                    Log.Information("CI Visibility Enabled by Configuration");
                    return true;
                }

                // explicitly disabled
                Log.Information("CI Visibility Disabled by Configuration");
                return false;
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
            processName ??= GetProcessName();
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

            static string GetProcessName()
            {
                try
                {
                    return ProcessHelpers.GetCurrentProcessName();
                }
                catch (Exception exception)
                {
                    Log.Warning(exception, "Error getting current process name when checking CI Visibility status");
                }

                return string.Empty;
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

                Task<long>? uploadRepositoryChangesTask = null;
                if (settings.GitUploadEnabled != false)
                {
                    // Upload the git metadata
                    uploadRepositoryChangesTask = Task.Run(() => lazyItrClient.Value.UploadRepositoryChangesAsync());
                }

                // If any DD_CIVISIBILITY_CODE_COVERAGE_ENABLED or DD_CIVISIBILITY_TESTSSKIPPING_ENABLED has not been set
                // We query the settings api for those
                if (settings.CodeCoverageEnabled == null || settings.TestsSkippingEnabled == null || settings.EarlyFlakeDetectionEnabled != false)
                {
                    var itrSettings = await lazyItrClient.Value.GetSettingsAsync().ConfigureAwait(false);

                    // we check if the backend require the git metadata first
                    if (itrSettings.RequireGit == true && uploadRepositoryChangesTask is not null)
                    {
                        Log.Debug("ITR: require git received, awaiting for the git repository upload.");
                        await uploadRepositoryChangesTask.ConfigureAwait(false);

                        Log.Debug("ITR: calling the configuration api again.");
                        itrSettings = await lazyItrClient.Value.GetSettingsAsync(skipFrameworkInfo: true).ConfigureAwait(false);
                    }

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

                    if (settings.EarlyFlakeDetectionEnabled == true || itrSettings.EarlyFlakeDetection.Enabled == true)
                    {
                        Log.Information("ITR: Early flake detection settings has been enabled by the settings api.");
                        EarlyFlakeDetectionSettings = itrSettings.EarlyFlakeDetection;
                        settings.SetEarlyFlakeDetectionEnabled(true);
                        EarlyFlakeDetectionResponse = await lazyItrClient.Value.GetEarlyFlakeDetectionTestsAsync().ConfigureAwait(false);
                    }
                    else
                    {
                        settings.SetEarlyFlakeDetectionEnabled(false);
                    }
                }

                // Log code coverage status
                Log.Information("{V}", settings.CodeCoverageEnabled == true ? "ITR: Tests code coverage is enabled." : "ITR: Tests code coverage is disabled.");

                // Log early flake detection status
                Log.Information("{V}", settings.EarlyFlakeDetectionEnabled == true ? "ITR: Early flake detection is enabled." : "ITR: Early flake detection is disabled.");

                // For ITR we need the git metadata upload before consulting the skippable tests.
                // If ITR is disabled we just need to make sure the git upload task has completed before leaving this method.
                if (uploadRepositoryChangesTask is not null)
                {
                    await uploadRepositoryChangesTask.ConfigureAwait(false);
                }

                // If the tests skipping feature is enabled we query the api for the tests we have to skip
                if (settings.TestsSkippingEnabled == true)
                {
                    var skippeableTests = await lazyItrClient.Value.GetSkippableTestsAsync().ConfigureAwait(false);
                    Log.Information<string?, int>("ITR: CorrelationId = {CorrelationId}, SkippableTests = {Length}.", skippeableTests.CorrelationId, skippeableTests.Tests.Length);

                    var skippableTestsBySuiteAndName = new Dictionary<string, Dictionary<string, IList<SkippableTest>>>();
                    foreach (var item in skippeableTests.Tests)
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

                    _skippableTestsCorrelationId = skippeableTests.CorrelationId;
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

        private static EventPlatformProxySupport IsEventPlatformProxySupportedByAgent(IDiscoveryService discoveryService)
        {
            if (discoveryService is NullDiscoveryService)
            {
                return EventPlatformProxySupport.None;
            }

            Log.Debug("Waiting for agent configuration...");
            var agentConfiguration = new DiscoveryAgentConfigurationCallback(discoveryService).WaitAndGet(5_000);
            if (agentConfiguration is null)
            {
                Log.Warning("Discovery service could not retrieve the agent configuration after 5 seconds.");
                return EventPlatformProxySupport.None;
            }

            var eventPlatformProxyEndpoint = agentConfiguration.EventPlatformProxyEndpoint;
            return EventPlatformProxySupportFromEndpointUrl(eventPlatformProxyEndpoint);
        }

        internal static EventPlatformProxySupport EventPlatformProxySupportFromEndpointUrl(string? eventPlatformProxyEndpoint)
        {
            if (!string.IsNullOrEmpty(eventPlatformProxyEndpoint))
            {
                if (eventPlatformProxyEndpoint?.Contains("/v2") == true)
                {
                    Log.Information("Event platform proxy V2 supported by agent.");
                    return EventPlatformProxySupport.V2;
                }

                if (eventPlatformProxyEndpoint?.Contains("/v4") == true)
                {
                    Log.Information("Event platform proxy V4 supported by agent.");
                    return EventPlatformProxySupport.V4;
                }

                Log.Information("EventPlatformProxyEndpoint: '{EVPEndpoint}' not supported.", eventPlatformProxyEndpoint);
            }
            else
            {
                Log.Information("Event platform proxy is not supported by the agent. Falling back to the APM protocol.");
            }

            return EventPlatformProxySupport.None;
        }

        private class DiscoveryAgentConfigurationCallback
        {
            private readonly ManualResetEventSlim _manualResetEventSlim;
            private readonly Action<AgentConfiguration> _callback;
            private readonly IDiscoveryService _discoveryService;
            private AgentConfiguration? _agentConfiguration;

            public DiscoveryAgentConfigurationCallback(IDiscoveryService discoveryService)
            {
                _manualResetEventSlim = new ManualResetEventSlim();
                LifetimeManager.Instance.AddShutdownTask(_ => _manualResetEventSlim.Set());
                _discoveryService = discoveryService;
                _callback = CallBack;
                _agentConfiguration = null;
                _discoveryService.SubscribeToChanges(_callback);
            }

            public AgentConfiguration? WaitAndGet(int timeoutInMs = 5_000)
            {
                _manualResetEventSlim.Wait(timeoutInMs);
                return _agentConfiguration;
            }

            private void CallBack(AgentConfiguration agentConfiguration)
            {
                _agentConfiguration = agentConfiguration;
                _manualResetEventSlim.Set();
                _discoveryService.RemoveSubscription(_callback);
                Log.Debug("Agent configuration received.");
            }
        }
    }
}
