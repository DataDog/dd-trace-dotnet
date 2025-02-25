// <copyright file="CiVisibility.cs" company="Datadog">
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
using Datadog.Trace.Util;

namespace Datadog.Trace.Ci;

internal class CiVisibility : ICiVisibility
{
    private static ICiVisibility? _instance;

    private Lazy<bool> _enabledLazy;
    private int _firstInitialization = 1;
    private CIVisibilitySettings? _settings;
    private ITestOptimizationClient? _client;
    private Task? _additionalFeaturesTask;
    private ICiVisibilityTracerManagement? _tracerManagement;
    private ICiVisibilityHostInfo? _hostInfo;
    private ICiVisibilityEarlyFlakeDetectionFeature? _earlyFlakeDetectionFeature;
    private ICiVisibilitySkippableFeature? _skippableFeature;
    private ICiVisibilityImpactedTestsDetectionFeature? _impactedTestsDetectionFeature;
    private ICiVisibilityFlakyRetryFeature? _flakyRetryFeature;

    public CiVisibility()
    {
        _enabledLazy = new Lazy<bool>(InternalEnabled, true);
    }

    public static ICiVisibility Instance
    {
        get => LazyInitializer.EnsureInitialized(ref _instance, () => new CiVisibility())!;
        internal set => _instance = value;
    }

    public static bool DefaultUseLockedTracerManager { get; set; } = true;

    public IDatadogLogger Log { get; } = DatadogLogging.GetLoggerFor(typeof(CiVisibility));

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

    public bool Enabled => _enabledLazy.Value;

    public CIVisibilitySettings Settings
    {
        get => LazyInitializer.EnsureInitialized(ref _settings, () => CIVisibilitySettings.FromDefaultSources())!;
        private set => _settings = value;
    }

    public ITestOptimizationClient Client
    {
        get => LazyInitializer.EnsureInitialized(ref _client, () => TestOptimizationClient.CreateCached(Environment.CurrentDirectory, Settings))!;
        private set => _client = value;
    }

    public ICiVisibilityTracerManagement? TracerManagement
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

    public ICiVisibilityHostInfo? HostInfo
    {
        get
        {
            if (_hostInfo is null)
            {
                _additionalFeaturesTask?.SafeWait();
            }

            return _hostInfo;
        }
        private set => _hostInfo = value;
    }

    public ICiVisibilityEarlyFlakeDetectionFeature? EarlyFlakeDetectionFeature
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

    public ICiVisibilitySkippableFeature? SkippableFeature
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

    public ICiVisibilityImpactedTestsDetectionFeature? ImpactedTestsDetectionFeature
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

    public ICiVisibilityFlakyRetryFeature? FlakyRetryFeature
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

    public void Initialize()
    {
        if (Interlocked.Exchange(ref _firstInitialization, 0) != 1)
        {
            // Initialize() or InitializeFromRunner() or InitializeFromManualInstrumentation() was already called before
            return;
        }

        Log.Information("CiVisibility: Initializing CI Visibility");
        var settings = Settings;

        HostInfo = new CiVisibilityHostInfo();

        // In case we are running using the agent, check if the event platform proxy is supported.
        TracerManagement = new CiVisibilityTracerManagement(
            Settings,
            static s => DiscoveryService.Create(
                s.TracerSettings.Exporter,
                tcpTimeout: TimeSpan.FromSeconds(5),
                initialRetryDelayMs: 10,
                maxRetryDelayMs: 1000,
                recheckIntervalMs: int.MaxValue),
            true);

        var eventPlatformProxyEnabled = TracerManagement.EventPlatformProxySupport != EventPlatformProxySupport.None;
        if (eventPlatformProxyEnabled)
        {
            Log.Information("CiVisibility: EVP Proxy was enabled with mode: {Mode}", TracerManagement.EventPlatformProxySupport);
        }

        LifetimeManager.Instance.AddAsyncShutdownTask(ShutdownAsync);

        var tracerSettings = settings.TracerSettings;
        Log.Debug("CiVisibility: Setting up the test session name to: {TestSessionName}", settings.TestSessionName);
        Log.Debug("CiVisibility: Setting up the service name to: {ServiceName}", tracerSettings.ServiceName);

        // Initialize Tracer
        Log.Information("CiVisibility: Initialize Test Tracer instance");
        TracerManager.ReplaceGlobalManager(tracerSettings, new CITracerManagerFactory(settings, TracerManagement, eventPlatformProxyEnabled));
        _ = Tracer.Instance;

        // Initialize FrameworkDescription
        _ = FrameworkDescription.Instance;

        // Initialize CIEnvironment
        _ = CIEnvironmentValues.Instance;

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
            Log.Warning("CiVisibility: Intelligent test runner cannot be activated. Agent doesn't support the event platform proxy endpoint.");
        }
        else if (settings.GitUploadEnabled != false)
        {
            Log.Warning("CiVisibility: Upload git metadata cannot be activated. Agent doesn't support the event platform proxy endpoint.");
        }
    }

    public void InitializeFromRunner(CIVisibilitySettings settings, IDiscoveryService discoveryService, bool eventPlatformProxyEnabled, bool? useLockedTracerManager = null)
    {
        if (Interlocked.Exchange(ref _firstInitialization, 0) != 1)
        {
            // Initialize() or InitializeFromRunner() was already called before
            return;
        }

        Log.Information("CiVisibility: Initializing CI Visibility from dd-trace / runner");
        Settings = settings;

        HostInfo = new CiVisibilityHostInfo();
        LifetimeManager.Instance.AddAsyncShutdownTask(ShutdownAsync);

        var tracerSettings = settings.TracerSettings;
        Log.Debug("CiVisibility: Setting up the test session name to: {TestSessionName}", settings.TestSessionName);
        Log.Debug("CiVisibility: Setting up the service name to: {ServiceName}", tracerSettings.ServiceName);

        // Initialize Tracer
        Log.Information("CiVisibility: Initialize Test Tracer instance");
        TracerManagement = new CiVisibilityTracerManagement(Settings, _ => discoveryService, useLockedTracerManager ?? DefaultUseLockedTracerManager);
        TracerManager.ReplaceGlobalManager(tracerSettings, new CITracerManagerFactory(settings, TracerManagement, eventPlatformProxyEnabled));
        _ = Tracer.Instance;

        // Initialize FrameworkDescription
        _ = FrameworkDescription.Instance;

        // Initialize CIEnvironment
        _ = CIEnvironmentValues.Instance;

        // Initialize features
        EarlyFlakeDetectionFeature = CiVisibilityEarlyFlakeDetectionFeature.CreateDisabledFeature();
        SkippableFeature = CiVisibilitySkippableFeature.CreateDisabledFeature();
        ImpactedTestsDetectionFeature = CiVisibilityImpactedTestsDetectionFeature.CreateDisabledFeature();
        FlakyRetryFeature = CiVisibilityFlakyRetryFeature.CreateDisabledFeature();
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
        var sContext = SynchronizationContext.Current;
        using var cts = new CancellationTokenSource(30_000);
        try
        {
            SynchronizationContext.SetSynchronizationContext(null);
            AsyncUtil.RunSync(() => FlushAsync(), cts.Token);
            if (cts.IsCancellationRequested)
            {
                Log.Error("CiVisibility: Timeout occurred when flushing spans.{NewLine}{StackTrace}", Environment.NewLine, Environment.StackTrace);
            }
        }
        catch (TaskCanceledException)
        {
            Log.Error("CiVisibility: Timeout occurred when flushing spans.{NewLine}{StackTrace}", Environment.NewLine, Environment.StackTrace);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(sContext);
        }
    }

    public async Task FlushAsync()
    {
        try
        {
            // We have to ensure the flush of the buffer after we finish the tests of an assembly.
            // For some reason, sometimes when all test are finished none of the callbacks to handling the tracer disposal is triggered.
            // So the last spans in buffer aren't send to the agent.
            Log.Debug("CiVisibility: Integration flushing spans.");

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

            Log.Debug("CiVisibility: Integration flushed.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "CiVisibility: Exception occurred when flushing spans.");
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
        if (IsRunning)
        {
            Log.Information("CiVisibility: CI Visibility is exiting.");
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
                Log.Error(ex, "CiVisibility: Error flushing the profiler.");
            }

            Interlocked.Exchange(ref _firstInitialization, 1);
        }
    }

    public void Reset()
    {
        _settings = null;
        _client = null;
        _firstInitialization = 1;
        _enabledLazy = new(InternalEnabled, true);
        _additionalFeaturesTask = null;
        _tracerManagement = null;
        _hostInfo = null;
        _earlyFlakeDetectionFeature = null;
        _skippableFeature = null;
        _impactedTestsDetectionFeature = null;
        _flakyRetryFeature = null;
    }

    private bool InternalEnabled()
    {
        string? processName = null;

        // By configuration
        if (Settings.Enabled is { } enabled)
        {
            if (enabled)
            {
                Log.Information("CiVisibility: CI Visibility Enabled by Configuration");
                return true;
            }

            // explicitly disabled
            Log.Information("CiVisibility: CI Visibility Disabled by Configuration");
            return false;
        }

        // Try to autodetect based in the domain name.
        var domainName = AppDomain.CurrentDomain.FriendlyName ?? string.Empty;
        if (domainName.StartsWith("testhost", StringComparison.Ordinal) ||
            domainName.StartsWith("xunit", StringComparison.Ordinal) ||
            domainName.StartsWith("nunit", StringComparison.Ordinal) ||
            domainName.StartsWith("MSBuild", StringComparison.Ordinal))
        {
            Log.Information("CiVisibility: CI Visibility Enabled by Domain name whitelist");
            PropagateCiVisibilityEnvironmentVariable();
            return true;
        }

        // Try to autodetect based in the process name.
        processName ??= GetProcessName();
        if (processName.StartsWith("testhost.", StringComparison.Ordinal))
        {
            Log.Information("CiVisibility: CI Visibility Enabled by Process name whitelist");
            PropagateCiVisibilityEnvironmentVariable();
            return true;
        }

        return false;

        void PropagateCiVisibilityEnvironmentVariable()
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

        string GetProcessName()
        {
            try
            {
                return ProcessHelpers.GetCurrentProcessName();
            }
            catch (Exception exception)
            {
                Log.Warning(exception, "CiVisibility: Error getting current process name when checking CI Visibility status");
            }

            return string.Empty;
        }
    }

    private async Task ShutdownAsync(Exception? exception)
    {
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
        try
        {
            Log.Information("CiVisibility: Initializing additional features.");
            var settings = Settings;
            var client = Client;

            Task? uploadRepositoryChangesTask = null;
            if (settings.GitUploadEnabled != false)
            {
                uploadRepositoryChangesTask = Task.Run(TryUploadRepositoryChangesAsync);
            }

            // If any DD_CIVISIBILITY_CODE_COVERAGE_ENABLED or DD_CIVISIBILITY_TESTSSKIPPING_ENABLED has not been set
            // We query the settings api for those
            if (settings.CodeCoverageEnabled == null
             || settings.TestsSkippingEnabled == null
             || settings.EarlyFlakeDetectionEnabled != false
             || settings.ImpactedTestsDetectionEnabled == null)
            {
                var remoteSettings = await client.GetSettingsAsync().ConfigureAwait(false);

                // we check if the backend require the git metadata first
                if (remoteSettings.RequireGit == true && uploadRepositoryChangesTask is not null)
                {
                    Log.Debug("CiVisibility: Require git received, awaiting for the git repository upload.");
                    await uploadRepositoryChangesTask.ConfigureAwait(false);

                    Log.Debug("CiVisibility: Calling the configuration api again.");
                    remoteSettings = await client.GetSettingsAsync(skipFrameworkInfo: true).ConfigureAwait(false);
                }

                FlakyRetryFeature = CiVisibilityFlakyRetryFeature.Create(settings, remoteSettings, client);
                EarlyFlakeDetectionFeature = CiVisibilityEarlyFlakeDetectionFeature.Create(settings, remoteSettings, client);
                ImpactedTestsDetectionFeature = CiVisibilityImpactedTestsDetectionFeature.Create(settings, remoteSettings, client);
                SkippableFeature = CiVisibilitySkippableFeature.Create(settings, remoteSettings, client);

                if (settings.CodeCoverageEnabled == null && remoteSettings.CodeCoverage.HasValue)
                {
                    Log.Information("CiVisibility: Code Coverage has been changed to {Value} by settings api.", remoteSettings.CodeCoverage.Value);
                    settings.SetCodeCoverageEnabled(remoteSettings.CodeCoverage.Value);
                }
            }
            else
            {
                // For ITR we need the git metadata upload before consulting the skippable tests.
                // If ITR is disabled we just need to make sure the git upload task has completed before leaving this method.
                if (uploadRepositoryChangesTask is not null)
                {
                    await uploadRepositoryChangesTask.ConfigureAwait(false);
                }

                var remoteSettings = new TestOptimizationClient.SettingsResponse(
                    settings.CodeCoverageEnabled,
                    settings.TestsSkippingEnabled,
                    false,
                    settings.ImpactedTestsDetectionEnabled,
                    settings.FlakyRetryEnabled,
                    new TestOptimizationClient.EarlyFlakeDetectionSettingsResponse(
                        settings.EarlyFlakeDetectionEnabled,
                        new TestOptimizationClient.SlowTestRetriesSettingsResponse(),
                        0));

                FlakyRetryFeature = CiVisibilityFlakyRetryFeature.Create(settings, remoteSettings, client);
                EarlyFlakeDetectionFeature = CiVisibilityEarlyFlakeDetectionFeature.Create(settings, remoteSettings, client);
                ImpactedTestsDetectionFeature = CiVisibilityImpactedTestsDetectionFeature.Create(settings, remoteSettings, client);
                SkippableFeature = CiVisibilitySkippableFeature.Create(settings, remoteSettings, client);
            }

            Log.Information("CiVisibility: Additional features intialized.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "CiVisibility: Error initializing additional features.");
        }
    }

    private async Task TryUploadRepositoryChangesAsync()
    {
        try
        {
            await Client.UploadRepositoryChangesAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "CiVisibility: Error uploading repository git metadata.");
        }
    }
}
