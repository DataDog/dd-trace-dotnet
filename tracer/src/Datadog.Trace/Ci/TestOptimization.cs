// <copyright file="TestOptimization.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Ci.CiEnvironment;
using Datadog.Trace.Ci.Configuration;
using Datadog.Trace.Ci.Net;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Pdb;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Util;
using TaskExtensions = Datadog.Trace.ExtensionMethods.TaskExtensions;

namespace Datadog.Trace.Ci;

internal sealed class TestOptimization : ITestOptimization
{
    private static ITestOptimization? _instance;

    private readonly Lazy<CIEnvironmentValues> _ciVariablesLazy;
    private int _firstInitialization = 1;
    private TestOptimizationDetection.Enablement _enablement;
    private TestOptimizationSettings? _settings;
    private ITestOptimizationClient? _client;
    private Task? _additionalFeaturesTask;
    private ITestOptimizationTracerManagement? _tracerManagement;
    private ITestOptimizationHostInfo? _hostInfo;
    private ITestOptimizationKnownTestsFeature? _knownTestsFeature;
    private ITestOptimizationEarlyFlakeDetectionFeature? _earlyFlakeDetectionFeature;
    private ITestOptimizationSkippableFeature? _skippableFeature;
    private ITestOptimizationImpactedTestsDetectionFeature? _impactedTestsDetectionFeature;
    private ITestOptimizationFlakyRetryFeature? _flakyRetryFeature;
    private ITestOptimizationDynamicInstrumentationFeature? _dynamicInstrumentationFeature;
    private ITestOptimizationTestManagementFeature? _testManagementFeature;
    private string? _runId;

    public TestOptimization()
    {
        using var cd = CodeDurationRef.Create();
        _ciVariablesLazy = new(() => CIEnvironmentValues.Instance);
        Log = DatadogLogging.GetLoggerFor<TestOptimization>();
        _enablement = InternalEnabled(Log);
    }

    public static ITestOptimization Instance
    {
        get => LazyInitializer.EnsureInitialized(ref _instance, () => new TestOptimization())!;
        internal set => _instance = value;
    }

    public static bool DefaultUseLockedTracerManager { get; set; } = true;

    public IDatadogLogger Log { get; }

    public bool IsRunning
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

    public string RunId
    {
        get => _runId ??= EnsureRunId();
    }

    public bool Enabled => _enablement.IsEnabled;

    public TestOptimizationSettings Settings
    {
        get => LazyInitializer.EnsureInitialized(ref _settings, () => TestOptimizationSettings.FromDefaultSources())!;
        private set
        {
            _settings = value;
            _client = null;
            _firstInitialization = 1;
            _enablement = InternalEnabled(Log);
            _additionalFeaturesTask = null;
            _tracerManagement = null;
            _hostInfo = null;
            _earlyFlakeDetectionFeature = null;
            _skippableFeature = null;
            _impactedTestsDetectionFeature = null;
            _flakyRetryFeature = null;
        }
    }

    public ITestOptimizationClient Client
    {
        get => LazyInitializer.EnsureInitialized(ref _client, () => TestOptimizationClient.CreateCached(Environment.CurrentDirectory, this))!;
        private set => _client = value;
    }

    public ITestOptimizationHostInfo HostInfo
    {
        get => _hostInfo ??= new TestOptimizationHostInfo();
        private set => _hostInfo = value;
    }

    public ITestOptimizationTracerManagement? TracerManagement
    {
        get
        {
            if (_tracerManagement is null)
            {
                _additionalFeaturesTask?.SafeWait();
            }

            return _tracerManagement;
        }
        private set => _tracerManagement = value;
    }

    public ITestOptimizationKnownTestsFeature? KnownTestsFeature
    {
        get
        {
            if (_knownTestsFeature is null)
            {
                _additionalFeaturesTask?.SafeWait();
            }

            return _knownTestsFeature;
        }
        private set => _knownTestsFeature = value;
    }

    public ITestOptimizationEarlyFlakeDetectionFeature? EarlyFlakeDetectionFeature
    {
        get
        {
            if (_earlyFlakeDetectionFeature is null)
            {
                _additionalFeaturesTask?.SafeWait();
            }

            return _earlyFlakeDetectionFeature;
        }
        private set => _earlyFlakeDetectionFeature = value;
    }

    public ITestOptimizationSkippableFeature? SkippableFeature
    {
        get
        {
            if (_skippableFeature is null)
            {
                _additionalFeaturesTask?.SafeWait();
            }

            return _skippableFeature;
        }
        private set => _skippableFeature = value;
    }

    public ITestOptimizationImpactedTestsDetectionFeature? ImpactedTestsDetectionFeature
    {
        get
        {
            if (_impactedTestsDetectionFeature is null)
            {
                _additionalFeaturesTask?.SafeWait();
            }

            return _impactedTestsDetectionFeature;
        }
        private set => _impactedTestsDetectionFeature = value;
    }

    public ITestOptimizationFlakyRetryFeature? FlakyRetryFeature
    {
        get
        {
            if (_flakyRetryFeature is null)
            {
                _additionalFeaturesTask?.SafeWait();
            }

            return _flakyRetryFeature;
        }
        private set => _flakyRetryFeature = value;
    }

    public ITestOptimizationDynamicInstrumentationFeature? DynamicInstrumentationFeature
    {
        get
        {
            if (_dynamicInstrumentationFeature is null)
            {
                _additionalFeaturesTask?.SafeWait();
            }

            return _dynamicInstrumentationFeature;
        }
        private set => _dynamicInstrumentationFeature = value;
    }

    public ITestOptimizationTestManagementFeature? TestManagementFeature
    {
        get
        {
            if (_testManagementFeature is null)
            {
                _additionalFeaturesTask?.SafeWait();
            }

            return _testManagementFeature;
        }
        private set => _testManagementFeature = value;
    }

    public CIEnvironmentValues CIValues => _ciVariablesLazy.Value;

    public void Initialize()
    {
        using var cd = CodeDurationRef.Create();
        if (Interlocked.Exchange(ref _firstInitialization, 0) != 1)
        {
            // Initialize() or InitializeFromRunner() or InitializeFromManualInstrumentation() was already called before
            return;
        }

        if (_enablement.InferredEnabled)
        {
            PropagateCiVisibilityEnvironmentVariable();
        }

        Log.Information("TestOptimization: Initializing CI Visibility with RunId: {RunId}", RunId);

        var settings = Settings;

        // In case we are running using the agent, check if the event platform proxy is supported.
        TracerManagement = new TestOptimizationTracerManagement(
            settings: Settings,
            getDiscoveryServiceFunc: static s => DiscoveryService.CreateUnmanaged(
                s.TracerSettings.Manager.InitialExporterSettings,
                tcpTimeout: TimeSpan.FromSeconds(5),
                initialRetryDelayMs: 100,
                maxRetryDelayMs: 1000,
                recheckIntervalMs: int.MaxValue),
            useLockedTracerManager: DefaultUseLockedTracerManager);

        var eventPlatformProxyEnabled = TracerManagement.EventPlatformProxySupport != EventPlatformProxySupport.None;
        if (eventPlatformProxyEnabled)
        {
            Log.Information("TestOptimization: EVP Proxy was enabled with mode: {Mode}", TracerManagement.EventPlatformProxySupport);
        }

        LifetimeManager.Instance.AddAsyncShutdownTask(ShutdownAsync);

        var tracerSettings = settings.TracerSettings;
        Log.Debug("TestOptimization: Setting up the test session name to: {TestSessionName}", settings.TestSessionName);
        Log.Debug("TestOptimization: Setting up the service name to: {ServiceName}", tracerSettings.Manager.InitialMutableSettings.ServiceName);

        // Initialize Tracer
        Log.Information("TestOptimization: Initialize Test Tracer instance");
        TracerManager.ReplaceGlobalManager(
            tracerSettings,
            new TestOptimizationTracerManagerFactory(
                settings: settings,
                testOptimizationTracerManagement: TracerManagement,
                enabledEventPlatformProxy: eventPlatformProxyEnabled));
        _ = Tracer.Instance;

        // Initialize FrameworkDescription
        _ = FrameworkDescription.Instance;

        // If we are running in agentless mode or the agent support the event platform proxy endpoint.
        // We can use the intelligent test runner
        if (settings.Agentless || eventPlatformProxyEnabled)
        {
            var additionalFeaturesTask = InitializeAdditionalFeaturesAsync();
            _additionalFeaturesTask = additionalFeaturesTask;
            LifetimeManager.Instance.AddAsyncShutdownTask(_ => additionalFeaturesTask);
        }
        else if (settings.IntelligentTestRunnerEnabled)
        {
            Log.Warning("TestOptimization: Intelligent test runner cannot be activated. Agent doesn't support the event platform proxy endpoint.");
            Client = new NoopTestOptimizationClient();
            InitializeDefaultFeatures(settings, TracerManagement, Client, CIValues);
        }
        else if (settings.GitUploadEnabled != false)
        {
            Log.Warning("TestOptimization: Upload git metadata cannot be activated. Agent doesn't support the event platform proxy endpoint.");
            Client = new NoopTestOptimizationClient();
            InitializeDefaultFeatures(settings, TracerManagement, Client, CIValues);
        }

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

    public void InitializeFromRunner(TestOptimizationSettings settings, IDiscoveryService discoveryService, bool eventPlatformProxyEnabled, bool? useLockedTracerManager = null)
    {
        using var cd = CodeDurationRef.Create();
        if (Interlocked.Exchange(ref _firstInitialization, 0) != 1)
        {
            // Initialize() or InitializeFromRunner() was already called before
            return;
        }

        Log.Information("TestOptimization: Initializing CI Visibility from dd-trace / runner with RunId: {RunId}", RunId);

        Settings = settings;
        LifetimeManager.Instance.AddAsyncShutdownTask(ShutdownAsync);

        var tracerSettings = settings.TracerSettings;
        Log.Debug("TestOptimization: Setting up the test session name to: {TestSessionName}", settings.TestSessionName);
        Log.Debug("TestOptimization: Setting up the service name to: {ServiceName}", tracerSettings.Manager.InitialMutableSettings.ServiceName);

        // Initialize Tracer
        Log.Information("TestOptimization: Initialize Test Tracer instance");
        TracerManagement = new TestOptimizationTracerManagement(
            settings: Settings,
            getDiscoveryServiceFunc: _ => discoveryService,
            useLockedTracerManager: useLockedTracerManager ?? DefaultUseLockedTracerManager);
        TracerManager.ReplaceGlobalManager(
            tracerSettings,
            new TestOptimizationTracerManagerFactory(
                settings: settings,
                testOptimizationTracerManagement: TracerManagement,
                enabledEventPlatformProxy: eventPlatformProxyEnabled));
        _ = Tracer.Instance;

        // Initialize FrameworkDescription
        _ = FrameworkDescription.Instance;

        // Initialize features
        Client = new NoopTestOptimizationClient();
        InitializeDefaultFeatures(settings, TracerManagement, Client, CIValues);
    }

    public void InitializeFromManualInstrumentation()
    {
        if (!IsRunning)
        {
            // If we are using only the Public API without auto-instrumentation (TestSession/TestModule/TestSuite/Test classes only)
            // then we can disable both GitUpload and Intelligent Test Runner feature (only used by our integration).
            Settings.SetDefaultManualInstrumentationSettings();
            Initialize();
        }
    }

    public void Flush()
    {
        using var cd = CodeDurationRef.Create();
        TaskExtensions.SafeWait(funcTask: FlushAsync, millisecondsTimeout: 30_000);
    }

    public async Task FlushAsync()
    {
        try
        {
            // We have to ensure the flush of the buffer after we finish the tests of an assembly.
            // For some reason, sometimes when all test are finished none of the callbacks to handling the tracer disposal is triggered.
            // So the last spans in buffer aren't send to the agent.
            Log.Debug("TestOptimization: Integration flushing spans.");

            if (Settings.Logs)
            {
                await Task.WhenAll(
                               Tracer.Instance.FlushAsync(),
                               Tracer.Instance.TracerManager.DirectLogSubmission.Sink.FlushAsync())
                          .ConfigureAwait(false);
            }
            else
            {
                await Tracer.Instance.FlushAsync().ConfigureAwait(false);
            }

            Log.Debug("TestOptimization: Integration flushed.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "TestOptimization: Exception occurred when flushing spans.");
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
    public void Close()
    {
        using var cd = CodeDurationRef.Create();
        if (!IsRunning)
        {
            return;
        }

        Log.Information("TestOptimization: CI Visibility is exiting.");
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
            Log.Error(ex, "TestOptimization: Error flushing the profiler.");
        }

        Interlocked.Exchange(ref _firstInitialization, 1);
    }

    public void Reset()
    {
        _settings = null;
        _client = null;
        _firstInitialization = 1;
        _enablement = InternalEnabled(Log);
        _additionalFeaturesTask = null;
        _tracerManagement = null;
        _hostInfo = null;
        _earlyFlakeDetectionFeature = null;
        _skippableFeature = null;
        _impactedTestsDetectionFeature = null;
        _flakyRetryFeature = null;
        _dynamicInstrumentationFeature = null;
    }

    private static TestOptimizationDetection.Enablement InternalEnabled(IDatadogLogger log)
        => TestOptimizationDetection.IsEnabled(GlobalConfigurationSource.Instance, TelemetryFactory.Config, log);

    private string EnsureRunId()
    {
        const string testOptimizationRunId = "_DD_INTERNAL_TOPT_RUNID";
        if (EnvironmentHelpers.GetEnvironmentVariable(testOptimizationRunId) is not { } runId)
        {
            runId = Guid.NewGuid().ToString("n");
            EnvironmentHelpers.SetEnvironmentVariable(testOptimizationRunId, runId);
        }

        return runId;
    }

    private async Task ShutdownAsync(Exception? exception)
    {
        using var cd = CodeDuration.Create();

        // Let's close any opened test, suite, modules and sessions before shutting down to avoid losing any data.
        // But marking them as failed.

        foreach (var test in Test.ActiveTests)
        {
            if (exception is not null)
            {
                test.SetErrorInfo(exception);
            }

            test.Close(TestStatus.Skip, null, "Test is being closed due to test session shutdown.");
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

            await testSession.CloseAsync(TestStatus.Skip).ConfigureAwait(false);
        }

        await FlushAsync().ConfigureAwait(false);
        MethodSymbolResolver.Instance.Clear();
    }

    private async Task InitializeAdditionalFeaturesAsync()
    {
        using var cd = CodeDuration.Create();
        try
        {
            Log.Information("TestOptimization: Initializing additional features.");
            var settings = Settings;
            var client = Client;

            Task? uploadRepositoryChangesTask = null;
            if (settings.GitUploadEnabled != false)
            {
                uploadRepositoryChangesTask = Task.Run(TryUploadRepositoryChangesAsync);
            }

            // If intelligent test runner is disabled we check if we need to move the upload repository changes task to the background
            if (!settings.IntelligentTestRunnerEnabled)
            {
                if (uploadRepositoryChangesTask is not null)
                {
                    Log.Information("TestOptimization: Intelligent Test Runner is disabled, but git upload is enabled. Uploading git metadata on background.");
                    LifetimeManager.Instance.AddAsyncShutdownTask(_ => uploadRepositoryChangesTask);
                }

                InitializeDefaultFeatures(settings, TracerManagement, client, CIValues);
                return;
            }

            // If any DD_CIVISIBILITY_CODE_COVERAGE_ENABLED or DD_CIVISIBILITY_TESTSSKIPPING_ENABLED has not been set
            // We query the settings api for those
            if (settings.CodeCoverageEnabled == null
             || settings.TestsSkippingEnabled == null
             || settings.EarlyFlakeDetectionEnabled != false
             || settings.FlakyRetryEnabled == null
             || settings.DynamicInstrumentationEnabled == null
             || settings.ImpactedTestsDetectionEnabled == null
             || settings.TestManagementEnabled == null)
            {
                Log.Information("TestOptimization: Calling the configuration api.");
                var remoteSettings = await client.GetSettingsAsync().ConfigureAwait(false);

                // we check if the backend require the git metadata first
                if (remoteSettings.RequireGit == true && uploadRepositoryChangesTask is not null)
                {
                    Log.Information("TestOptimization: Require git received, awaiting for the git repository upload.");
                    await uploadRepositoryChangesTask.ConfigureAwait(false);

                    Log.Information("TestOptimization: Calling the configuration api again.");
                    remoteSettings = await client.GetSettingsAsync(skipFrameworkInfo: true).ConfigureAwait(false);
                }

                FlakyRetryFeature = TestOptimizationFlakyRetryFeature.Create(settings, remoteSettings);
                DynamicInstrumentationFeature = TestOptimizationDynamicInstrumentationFeature.Create(settings, remoteSettings);
                KnownTestsFeature = TestOptimizationKnownTestsFeature.Create(settings, remoteSettings, client);
                EarlyFlakeDetectionFeature = TestOptimizationEarlyFlakeDetectionFeature.Create(settings, remoteSettings);
                ImpactedTestsDetectionFeature = TestOptimizationImpactedTestsDetectionFeature.Create(settings, remoteSettings, CIValues);
                SkippableFeature = TestOptimizationSkippableFeature.Create(settings, remoteSettings, client);
                TestManagementFeature = TestOptimizationTestManagementFeature.Create(settings, remoteSettings, client);

                if (settings.CodeCoverageEnabled == null && remoteSettings.CodeCoverage.HasValue)
                {
                    Log.Information("TestOptimization: Code Coverage has been changed to {Value} by settings api.", remoteSettings.CodeCoverage.Value);
                    settings.SetCodeCoverageEnabled(remoteSettings.CodeCoverage.Value);
                }
            }
            else
            {
                // If ITR is disabled we just need to make sure the git upload task has completed before leaving this method.
                if (uploadRepositoryChangesTask is not null)
                {
                    await uploadRepositoryChangesTask.ConfigureAwait(false);
                }

                InitializeDefaultFeatures(settings, TracerManagement, client, CIValues);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "TestOptimization: Error initializing additional features.");
        }
        finally
        {
            Log.Information("TestOptimization: Additional features intialized.");
        }
    }

    private async Task TryUploadRepositoryChangesAsync()
    {
        using var cd = CodeDuration.Create();
        try
        {
            await Client.UploadRepositoryChangesAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "TestOptimization: Error uploading repository git metadata.");
        }
    }

    private void InitializeDefaultFeatures(TestOptimizationSettings settings, ITestOptimizationTracerManagement? tracerManagement, ITestOptimizationClient client, CIEnvironmentValues environmentValues)
    {
        using var cd = CodeDurationRef.Create();
        var remoteSettings = TestOptimizationClient.CreateSettingsResponseFromTestOptimizationSettings(settings, tracerManagement);
        FlakyRetryFeature = TestOptimizationFlakyRetryFeature.Create(settings, remoteSettings);
        DynamicInstrumentationFeature = TestOptimizationDynamicInstrumentationFeature.Create(settings, remoteSettings);
        KnownTestsFeature = TestOptimizationKnownTestsFeature.Create(settings, remoteSettings, client);
        EarlyFlakeDetectionFeature = TestOptimizationEarlyFlakeDetectionFeature.Create(settings, remoteSettings);
        ImpactedTestsDetectionFeature = TestOptimizationImpactedTestsDetectionFeature.Create(settings, remoteSettings, environmentValues);
        SkippableFeature = TestOptimizationSkippableFeature.Create(settings, remoteSettings, client);
        TestManagementFeature = TestOptimizationTestManagementFeature.Create(settings, remoteSettings, client);
    }
}
