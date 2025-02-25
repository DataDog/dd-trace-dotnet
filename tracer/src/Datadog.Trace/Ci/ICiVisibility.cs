// <copyright file="ICiVisibility.cs" company="Datadog">
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

internal interface ICiVisibility
{
    IDatadogLogger Log { get; }

    bool Enabled { get; }

    bool IsRunning { get; }

    CIVisibilitySettings Settings { get; }

    ITestOptimizationClient Client { get; }

    ICiVisibilityTracerManagement TracerManagement { get; }

    ICiVisibilityHostInfo HostInfo { get; }

    ICiVisibilityEarlyFlakeDetectionFeature EarlyFlakeDetectionFeature { get; }

    ICiVisibilitySkippableFeature SkippableFeature { get; }

    ICiVisibilityImpactedTestsDetectionFeature ImpactedTestsDetectionFeature { get; }

    void Initialize();

    void InitializeFromRunner(CIVisibilitySettings settings, IDiscoveryService discoveryService, bool eventPlatformProxyEnabled);

    void InitializeFromManualInstrumentation();

    void Flush();

    Task FlushAsync();

    void Close();

    void Reset();
}

/*
internal class CiVisibility : ICiVisibility
{
    // Lazy evaluation for the enabled flag.
    private static Lazy<bool> _enabledLazy = new(InternalEnabled, true);

    // Indicates if CI Visibility is running based on the initialization flag.
    private static int _firstInitialization = 1;

    // CIVisibilitySettings instance, loaded lazily.
    private CIVisibilitySettings? _settings;

    // ITestOptimizationClient instance, loaded lazily.
    private ITestOptimizationClient? _client;

    // Constructor to inject the component implementations.
    public CiVisibility(
        ICiVisibilityTracerManagement tracerManagement,
        ICiVisibilityHostInfo hostInfo,
        ICiVisibilityEarlyFlakeDetectionFeature earlyFlakeDetectionFeature,
        ICiVisibilitySkippableFeature skippableFeature,
        ICiVisibilityImpactedTestsDetectionFeature impactedTestsDetectionFeature)
    {
        TracerManagement = tracerManagement;
        HostInfo = hostInfo;
        EarlyFlakeDetectionFeature = earlyFlakeDetectionFeature;
        SkippableFeature = skippableFeature;
        ImpactedTestsDetectionFeature = impactedTestsDetectionFeature;
    }

    // Logger for this class.
    public IDatadogLogger Log { get; } = DatadogLogging.GetLoggerFor(typeof(CiVisibility));

    public bool Enabled => _enabledLazy.Value;

    public bool IsRunning => Interlocked.CompareExchange(ref _firstInitialization, 0, 0) == 0;

    public CIVisibilitySettings Settings => _settings ??= CIVisibilitySettings.FromDefaultSources();

    public ITestOptimizationClient Client => _client ??= TestOptimizationClient.CreateCached(Environment.CurrentDirectory, Settings);

    // Aggregated components.
    public ICiVisibilityTracerManagement TracerManagement { get; private set; }

    public ICiVisibilityHostInfo HostInfo { get; private set; }

    public ICiVisibilityEarlyFlakeDetectionFeature EarlyFlakeDetectionFeature { get; private set; }

    public ICiVisibilitySkippableFeature SkippableFeature { get; private set; }

    public ICiVisibilityImpactedTestsDetectionFeature ImpactedTestsDetectionFeature { get; private set; }

    // Initializes CI Visibility.
    public void Initialize()
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
                EventPlatformProxySupport = Enum.TryParse<EventPlatformProxySupport>(settings.ForceAgentsEvpProxy, out var parsedValue) ? parsedValue : EventPlatformProxySupport.V2;
            }
            else
            {
                discoveryService = DiscoveryService.Create(
                    settings.TracerSettings.Exporter,
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
        Log.Debug("Setting up the test session name to: {TestSessionName}", settings.TestSessionName);
        Log.Debug("Setting up the service name to: {ServiceName}", tracerSettings.ServiceName);

        // Initialize Tracer
        Log.Information("Initialize Test Tracer instance");
        TracerManager.ReplaceGlobalManager(tracerSettings, new CITracerManagerFactory(settings, discoveryService, eventPlatformProxyEnabled, UseLockedTracerManager));
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
                // _skippableTestsTask = GetIntelligentTestRunnerSkippableTestsAsync();
                // LifetimeManager.Instance.AddAsyncShutdownTask(_ => _skippableTestsTask);
            }
            else if (settings.GitUploadEnabled != false)
            {
                // Update and upload git tree metadata.
                Log.Information("ITR: Update and uploading git tree metadata.");
                // var tskItrUpdate = UploadGitMetadataAsync();
                // LifetimeManager.Instance.AddAsyncShutdownTask(_ => tskItrUpdate);
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

    // Initializes CI Visibility from runner-provided settings.
    public void InitializeFromRunner(CIVisibilitySettings settings, IDiscoveryService discoveryService, bool eventPlatformProxyEnabled)
    {
        if (Interlocked.Exchange(ref _firstInitialization, 0) != 1)
        {
            return;
        }

        Log.Information("Initializing CI Visibility from runner");
        _settings = settings;

        // Replace tracer manager using provided settings and discovery service.
        TracerManager.ReplaceGlobalManager(
            settings.TracerSettings,
            new CITracerManagerFactory(settings, discoveryService, eventPlatformProxyEnabled, TracerManagement.UseLockedTracerManager)
        );
        _ = Tracer.Instance;
        _ = FrameworkDescription.Instance;
        _ = CIEnvironmentValues.Instance;
    }

    // Initializes CI Visibility when using manual instrumentation.
    public void InitializeFromManualInstrumentation()
    {
        if (!IsRunning)
        {
            Settings.SetDefaultManualInstrumentationSettings();
            Initialize();
        }
    }

    // Flushes spans synchronously.
    public void Flush()
    {
        var originalContext = SynchronizationContext.Current;
        using var cts = new CancellationTokenSource(30000);
        try
        {
            // Set synchronization context to null to avoid deadlocks.
            SynchronizationContext.SetSynchronizationContext(null);
            AsyncUtil.RunSync(() => FlushAsync(), cts.Token);
            if (cts.IsCancellationRequested)
            {
                Log.Error("Timeout occurred when flushing spans.\n{0}", Environment.StackTrace);
            }
        }
        catch (TaskCanceledException)
        {
            Log.Error("Timeout occurred when flushing spans.\n{0}", Environment.StackTrace);
        }
        finally
        {
            // Restore original synchronization context.
            SynchronizationContext.SetSynchronizationContext(originalContext);
        }
    }

    // Asynchronously flushes spans.
    public async Task FlushAsync()
    {
        try
        {
            Log.Debug("Integration flushing spans.");
            if (Settings.Logs)
            {
                await Task.WhenAll(
                               Tracer.Instance.FlushAsync(),
                               Tracer.Instance.TracerManager.DirectLogSubmission.Sink.FlushAsync()
                           )
                          .ConfigureAwait(false);
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

    // Closes CI Visibility and runs shutdown tasks.
    public void Close()
    {
        if (IsRunning)
        {
            Log.Information("CI Visibility is exiting.");
            LifetimeManager.Instance.RunShutdownTasks();

            // If the continuous profiler is attached, flush remaining profiles.
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

    // Resets CI Visibility to its initial state (useful for testing).
    public void Reset()
    {
        _settings = null;
        _firstInitialization = 1;
        _enabledLazy = new Lazy<bool>(InternalEnabled, true);
        // Optionally reset sub-components if they expose a Reset method.
    }

    // Internal shutdown task that flushes pending work and closes active tests.
    private static async Task ShutdownAsync(Exception? exception)
    {
        // Close active tests.
        foreach (var test in Test.ActiveTests)
        {
            if (exception != null)
            {
                test.SetErrorInfo(exception);
            }

            test.Close(TestStatus.Skip, null, "Test is being closed due to test session shutdown.");
        }

        foreach (var testSuite in TestSuite.ActiveTestSuites)
        {
            if (exception != null)
            {
                testSuite.SetErrorInfo(exception);
            }

            testSuite.Close();
        }

        foreach (var testModule in TestModule.ActiveTestModules)
        {
            if (exception != null)
            {
                testModule.SetErrorInfo(exception);
            }

            await testModule.CloseAsync().ConfigureAwait(false);
        }

        foreach (var testSession in TestSession.ActiveTestSessions)
        {
            if (exception != null)
            {
                testSession.SetErrorInfo(exception);
            }

            await testSession.CloseAsync(TestStatus.Skip).ConfigureAwait(false);
        }

        await InstanceFlushAsync().ConfigureAwait(false);
        MethodSymbolResolver.Instance.Clear();
    }

    // Helper method for flushing spans during shutdown.
    private static Task InstanceFlushAsync()
    {
        return Tracer.Instance.FlushAsync();
    }

    // Internal method to determine if CI Visibility is enabled.
    private static bool InternalEnabled()
    {
        // Check if enabled is configured.
        if (CIVisibilitySettings.FromDefaultSources().Enabled is bool enabled)
        {
            if (enabled)
            {
                DatadogLogging.GetLoggerFor(typeof(CiVisibility)).Information("CI Visibility Enabled by Configuration");
                PropagateCiVisibilityEnvironmentVariable();
                return true;
            }

            DatadogLogging.GetLoggerFor(typeof(CiVisibility)).Information("CI Visibility Disabled by Configuration");
            return false;
        }

        // Enable based on domain and process name heuristics.
        var domainName = AppDomain.CurrentDomain.FriendlyName ?? string.Empty;
        if (domainName.StartsWith("testhost", StringComparison.Ordinal) ||
            domainName.StartsWith("xunit", StringComparison.Ordinal) ||
            domainName.StartsWith("nunit", StringComparison.Ordinal) ||
            domainName.StartsWith("MSBuild", StringComparison.Ordinal))
        {
            DatadogLogging.GetLoggerFor(typeof(CiVisibility)).Information("CI Visibility Enabled by Domain name whitelist");
            PropagateCiVisibilityEnvironmentVariable();
            return true;
        }

        var processName = GetProcessName();
        if (processName.StartsWith("testhost.", StringComparison.Ordinal))
        {
            DatadogLogging.GetLoggerFor(typeof(CiVisibility)).Information("CI Visibility Enabled by Process name whitelist");
            PropagateCiVisibilityEnvironmentVariable();
            return true;
        }

        return false;

        // Propagate the CI Visibility environment variable to child processes.
        static void PropagateCiVisibilityEnvironmentVariable()
        {
            try
            {
                Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.Enabled, "1", EnvironmentVariableTarget.Process);
            }
            catch
            {
                // Ignore exceptions.
            }
        }

        // Retrieve the current process name.
        static string GetProcessName()
        {
            try
            {
                return ProcessHelpers.GetCurrentProcessName();
            }
            catch (Exception exception)
            {
                DatadogLogging.GetLoggerFor(typeof(CiVisibility)).Warning(exception, "Error getting current process name when checking CI Visibility status");
            }

            return string.Empty;
        }
    }
}
*/
