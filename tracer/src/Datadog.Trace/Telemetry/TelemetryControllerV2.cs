// <copyright file="TelemetryControllerV2.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.AppSec;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.ContinuousProfiler;
using Datadog.Trace.Iast.Settings;
using Datadog.Trace.Logging;
using Datadog.Trace.Telemetry.Collectors;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.Util;

namespace Datadog.Trace.Telemetry;

internal class TelemetryControllerV2 : ITelemetryController
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<TelemetryControllerV2>();
    private readonly TelemetryDataBuilderV2 _dataBuilder = new();
    private readonly TelemetryDataAggregator _aggregator = new(previous: null);
    private readonly ApplicationTelemetryCollectorV2 _application;
    private readonly IConfigurationTelemetry _configuration;
    private readonly IDependencyTelemetryCollector _dependencies;
    private readonly IntegrationTelemetryCollector _integrations;
    private readonly ProductsTelemetryCollector _products;
    private readonly TelemetryTransportManagerV2 _transportManager;
    private readonly IMetricsTelemetryCollector _metrics;
    private readonly TimeSpan _flushInterval;
    private readonly TaskCompletionSource<bool> _tracerInitialized = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<bool> _processExit = new();
    private readonly Task _flushTask;
    private bool _fatalError;
    private string? _namingVersion;

    internal TelemetryControllerV2(
        IConfigurationTelemetry configuration,
        IDependencyTelemetryCollector dependencies,
        IntegrationTelemetryCollector integrations,
        IMetricsTelemetryCollector metrics,
        ProductsTelemetryCollector products,
        ApplicationTelemetryCollectorV2 application,
        TelemetryTransportManagerV2 transportManager,
        TimeSpan flushInterval)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _dependencies = dependencies ?? throw new ArgumentNullException(nameof(dependencies));
        _integrations = integrations ?? throw new ArgumentNullException(nameof(integrations));
        _products = products ?? throw new ArgumentNullException(nameof(products));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _application = application ?? throw new ArgumentNullException(nameof(application));
        _transportManager = transportManager ?? throw new ArgumentNullException(nameof(transportManager));
        _flushInterval = flushInterval;

        try
        {
            // Registering for the AppDomain.AssemblyLoad event cannot be called by a security transparent method
            // This will only happen if the Tracer is not run full-trust
            AppDomain.CurrentDomain.AssemblyLoad += CurrentDomain_OnAssemblyLoad;
            var assembliesLoaded = AppDomain.CurrentDomain.GetAssemblies();

            foreach (var t in assembliesLoaded)
            {
                RecordAssembly(t);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Unable to register a callback to the AppDomain.AssemblyLoad event. Telemetry collection of loaded assemblies will be disabled.");
        }

        _flushTask = Task.Run(PushTelemetryLoopAsync);
    }

    public bool FatalError => Volatile.Read(ref _fatalError);

    public void RecordTracerSettings(ImmutableTracerSettings settings, string defaultServiceName)
    {
        // Note that this _doesn't_ clear the configuration held by ImmutableTracerSettings
        // that's necessary because users could reconfigure the tracer to re-use an old
        // ImmutableTracerSettings, at which point that config would become "current", so we
        // need to keep it around
        settings.Telemetry.CopyTo(_configuration);
        _application.RecordTracerSettings(settings, defaultServiceName);
        _namingVersion = ((int)settings.MetadataSchemaVersion).ToString();
    }

    public void Start()
    {
        _tracerInitialized.TrySetResult(true);
    }

    public void ProductChanged(TelemetryProductType product, bool enabled, ErrorData? error)
        => _products.ProductChanged(product, enabled, error);

    public void RecordSecuritySettings(SecuritySettings settings)
    {
        // Nothing to record, remove this method when telemetry V1 is removed
    }

    public void RecordIastSettings(IastSettings settings)
    {
        // Nothing to record, remove this method when telemetry V1 is removed
    }

    public void RecordProfilerSettings(Profiler profiler)
    {
        _configuration.Record(ConfigTelemetryData.ProfilerLoaded, profiler.Status.IsProfilerReady, ConfigurationOrigins.Default);
        _configuration.Record(ConfigTelemetryData.CodeHotspotsEnabled, profiler.ContextTracker.IsEnabled, ConfigurationOrigins.Default);
    }

    public void IntegrationRunning(IntegrationId integrationId)
        => _integrations.IntegrationRunning(integrationId);

    public void IntegrationGeneratedSpan(IntegrationId integrationId)
    {
        _metrics.RecordCountSpanCreated(integrationId.GetMetricTag());
        _integrations.IntegrationGeneratedSpan(integrationId);
    }

    public void IntegrationDisabledDueToError(IntegrationId integrationId, string error)
        => _integrations.IntegrationDisabledDueToError(integrationId, error);

    public async Task DisposeAsync(bool sendAppClosingTelemetry)
    {
        TerminateLoop(sendAppClosingTelemetry);
        await _flushTask.ConfigureAwait(false);
    }

    public Task DisposeAsync()
    {
        return DisposeAsync(sendAppClosingTelemetry: true);
    }

    private void TerminateLoop(bool sendAppClosingTelemetry)
    {
        // If there's a fatal error, TerminateLoop() may be called more than once
        // (at error-time, and at process end). The following are idempotent so that's safe.
        _processExit.TrySetResult(sendAppClosingTelemetry);
        _tracerInitialized.TrySetResult(true);
        AppDomain.CurrentDomain.AssemblyLoad -= CurrentDomain_OnAssemblyLoad;
    }

    private void CurrentDomain_OnAssemblyLoad(object? sender, AssemblyLoadEventArgs e)
    {
        RecordAssembly(e.LoadedAssembly);
    }

    private void RecordAssembly(Assembly assembly)
    {
        try
        {
            _dependencies.AssemblyLoaded(assembly);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error recording loaded assembly");
        }
    }

    private async Task PushTelemetryLoopAsync()
    {
#if !NET5_0_OR_GREATER
        var tasks = new Task[2];
        tasks[0] = _tracerInitialized.Task;
        tasks[1] = _processExit.Task;

        // wait for initialization before trying to send first telemetry
        // .NET 5.0 has an explicit overload for this
        await Task.WhenAny(tasks).ConfigureAwait(false);
#else
        await Task.WhenAny(_tracerInitialized.Task, _processExit.Task).ConfigureAwait(false);
#endif

        while (true)
        {
            if (_processExit.Task.IsCompleted)
            {
                Log.Debug("Process exit requested, ending telemetry loop");
                var sendAppClosingTelemetry = _processExit.Task.Result;

                await PushTelemetry(isFinalPush: sendAppClosingTelemetry).ConfigureAwait(false);

                return;
            }

            await PushTelemetry(isFinalPush: false).ConfigureAwait(false);

#if NET5_0_OR_GREATER
            // .NET 5.0 has an explicit overload for this
            await Task.WhenAny(Task.Delay(_flushInterval), _processExit.Task).ConfigureAwait(false);
#else
            tasks[0] = Task.Delay(_flushInterval);
            await Task.WhenAny(tasks).ConfigureAwait(false);
#endif
        }
    }

    private async Task PushTelemetry(bool isFinalPush)
    {
        try
        {
            var application = _application.GetApplicationData();
            var host = _application.GetHostData();
            if (application is null || host is null)
            {
                Log.Debug("Telemetry not initialized, skipping");
                return;
            }

            var success = await PushTelemetry(application, host).ConfigureAwait(false);
            if (!success)
            {
                if (isFinalPush)
                {
                    Log.Debug("Unable to send final telemetry, skipping app-closing telemetry ");
                    return;
                }
                else
                {
                    _fatalError = true;
                    Log.Debug("Unable to send telemetry, ending telemetry loop");
                    TerminateLoop(sendAppClosingTelemetry: false);
                }
            }

            if (isFinalPush)
            {
                var closingTelemetryData = _dataBuilder.BuildAppClosingTelemetryData(application, host, _namingVersion);

                Log.Debug("Pushing app-closing telemetry");
                await _transportManager.TryPushTelemetry(closingTelemetryData).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error pushing telemetry");
        }
    }

    private async Task<bool> PushTelemetry(ApplicationTelemetryDataV2 application, HostTelemetryDataV2 host)
    {
        // use values from previous failed attempt if necessary
        var input = _aggregator.Combine(
            _configuration.GetData(),
            _dependencies.GetData(),
            _integrations.GetData(),
            _metrics.GetMetrics(),
            _products.GetData());

        var data = _dataBuilder.BuildTelemetryData(application, host, in input, _namingVersion);

        Log.Debug("Pushing telemetry changes");
        var result = await _transportManager.TryPushTelemetry(data).ConfigureAwait(false);
        _aggregator.SaveDataIfRequired(result, in input);

        switch (result)
        {
            case TelemetryTransportResult.FatalError:
                return false; // big problem, abandon hope

            case TelemetryTransportResult.TransientError: // there was an error, but try again next time
            case TelemetryTransportResult.Success: // woo-hoo!
                return true;
            default:
                // Should never happen
                throw new Exception($"Unexpected telemetry result type: {result}");
        }
    }
}
