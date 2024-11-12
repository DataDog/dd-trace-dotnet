// <copyright file="TelemetryController.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.ContinuousProfiler;
using Datadog.Trace.Logging;
using Datadog.Trace.Telemetry.Collectors;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.Telemetry.Transports;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using ConfigurationKeys = Datadog.Trace.Configuration.ConfigurationKeys;

namespace Datadog.Trace.Telemetry;

internal class TelemetryController : ITelemetryController
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<TelemetryController>();
    private readonly TelemetryDataBuilder _dataBuilder = new();
    private readonly ConcurrentQueue<WorkItem> _queue = new();
    private readonly TelemetryDataAggregator _aggregator = new(previous: null);
    private readonly ApplicationTelemetryCollector _application = new();
    private readonly IConfigurationTelemetry _configuration;
    private readonly IDependencyTelemetryCollector _dependencies;
    private readonly IntegrationTelemetryCollector _integrations = new();
    private readonly ProductsTelemetryCollector _products = new();
    private readonly IMetricsTelemetryCollector _metrics;
    private readonly RedactedErrorLogCollector? _redactedErrorLogs;
    private readonly TaskCompletionSource<bool> _processExit = new();
    private readonly Task _flushTask;
    private readonly Scheduler _scheduler;
    private TelemetryTransportManager _transportManager;
    private bool _sendTelemetry;
    private bool _isStarted;
    private string? _namingVersion;

    internal TelemetryController(
        IConfigurationTelemetry configuration,
        IDependencyTelemetryCollector dependencies,
        IMetricsTelemetryCollector metrics,
        RedactedErrorLogCollector? redactedErrorLogs,
        TelemetryTransportManager transportManager,
        TimeSpan flushInterval)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _dependencies = dependencies ?? throw new ArgumentNullException(nameof(dependencies));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _transportManager = transportManager ?? throw new ArgumentNullException(nameof(transportManager));
        _redactedErrorLogs = redactedErrorLogs;
        // We use Task.Delay(Timeout.Infinite) here as "a Task that never completes".
        // It simplifies some of the logic we need to do in the scheduler
        var redactedErrorLogsTask = () => _redactedErrorLogs?.WaitForLogsAsync() ?? Task.Delay(Timeout.Infinite);
        _scheduler = new(flushInterval, redactedErrorLogsTask, _processExit);

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
        _flushTask.ContinueWith(t => Log.Error(t.Exception, "Error in telemetry flush task"), TaskContinuationOptions.OnlyOnFaulted);
    }

    public void RecordTracerSettings(ImmutableTracerSettings settings, string defaultServiceName)
    {
        // Note that this _doesn't_ clear the configuration held by ImmutableTracerSettings
        // that's necessary because users could reconfigure the tracer to re-use an old
        // ImmutableTracerSettings, at which point that config would become "current", so we
        // need to keep it around
        settings.Telemetry.CopyTo(_configuration);
        _application.RecordTracerSettings(settings, defaultServiceName);
        _namingVersion = ((int)settings.MetadataSchemaVersion).ToString();
        _queue.Enqueue(new WorkItem(WorkItem.ItemType.EnableSending, null));
    }

    public void RecordGitMetadata(GitMetadata gitMetadata)
    {
        _application.RecordGitMetadata(gitMetadata);

        _configuration.Record(ConfigurationKeys.GitRepositoryUrl, gitMetadata.RepositoryUrl, recordValue: true, ConfigurationOrigins.Calculated);
        _configuration.Record(ConfigurationKeys.GitCommitSha, gitMetadata.CommitSha, recordValue: true, ConfigurationOrigins.Calculated);
    }

    public void Start()
    {
        _queue.Enqueue(new WorkItem(WorkItem.ItemType.SetTracerStarted, null));
        _scheduler.SetTracerInitialized();
    }

    public void ProductChanged(TelemetryProductType product, bool enabled, ErrorData? error)
        => _products.ProductChanged(product, enabled, error);

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

    public async Task DisposeAsync()
    {
        TerminateLoop();
        await _flushTask.ConfigureAwait(false);
    }

    public void DisableSending()
    {
        _queue.Enqueue(new WorkItem(WorkItem.ItemType.DisableSending, null));
    }

    public void SetTransportManager(TelemetryTransportManager manager)
    {
        _queue.Enqueue(new WorkItem(WorkItem.ItemType.SetTransportManager, manager));
    }

    public void SetFlushInterval(TimeSpan flushInterval)
    {
        _queue.Enqueue(new WorkItem(WorkItem.ItemType.SetFlushInterval, flushInterval));
    }

    public async Task DumpTelemetry(string filePath)
    {
        if (!_isStarted)
        {
            // we haven't performed all startup initialization yet, so we can't dump anything
            Log.Information("Unable to dump telemetry as controller not yet initialized");
            return;
        }

        try
        {
            var application = _application.GetApplicationData();
            var host = _application.GetHostData();
            if (application is null || host is null)
            {
                Log.Debug("Telemetry not initialized, skipping");
                return;
            }

            // fetch the "complete" values, and make sure to not impact real telemetry push
            var input = new TelemetryInput(
                configuration: null, // we don't store this indefinitely, so no way of dumping full config
                _dependencies.GetFullData(),
                _integrations.GetFullData(),
                metrics: null,
                _products.GetFullData(),
                sendAppStarted: false);

            // This will increment the data sequence, but shouldn't be an issue
            var data = _dataBuilder.BuildTelemetryData(application, host, in input, _namingVersion, sendAppClosing: false);
            Log.Debug("Dumping telemetry to file {Filename}", filePath);

            // serialize JSON directly to a file
            var serializer = JsonSerializer.Create(JsonTelemetryTransport.SerializerSettings);
            using var file = File.Open(filePath, FileMode.Create, FileAccess.Write);
            using var writer = new StreamWriter(file);
            serializer.Serialize(writer, data);
            await writer.FlushAsync().ConfigureAwait(false);
            Log.Debug("Telemetry dump complete");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error dumping telemetry to file {Filename}", filePath);
        }
    }

    private void TerminateLoop()
    {
        // If there's a fatal error, TerminateLoop() may be called more than once
        // (at error-time, and at process end). The following are idempotent so that's safe.
        _processExit.TrySetResult(true);
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
        while (true)
        {
            // Process all the messages in the queue before sending next telemetry
            while (_queue.TryDequeue(out var item))
            {
                switch (item.Type)
                {
                    case WorkItem.ItemType.SetTransportManager:
                        _transportManager.Dispose(); // dispose the old one
                        _transportManager = (TelemetryTransportManager)item.State!;
                        break;
                    case WorkItem.ItemType.SetTracerStarted:
                        _isStarted = true;
                        break;
                    case WorkItem.ItemType.EnableSending:
                        _sendTelemetry = true;
                        break;
                    case WorkItem.ItemType.DisableSending:
                        _sendTelemetry = false;
                        break;
                    case WorkItem.ItemType.SetFlushInterval:
                        _scheduler.SetFlushInterval((TimeSpan)item.State!);
                        break;
                }
            }

            var isFinalPush = _processExit.Task.IsCompleted;
            if (isFinalPush)
            {
                // wait for the final aggregation
                await _metrics.DisposeAsync().ConfigureAwait(false);
            }

            if (_isStarted && _sendTelemetry && _scheduler.ShouldFlushTelemetry)
            {
                await PushTelemetry(includeLogs: _scheduler.ShouldFlushRedactedErrorLogs, sendAppClosing: isFinalPush).ConfigureAwait(false);
            }

            if (isFinalPush)
            {
                Log.Debug("Process exit requested, ending telemetry loop");
                TerminateLoop();
                return;
            }

            await _scheduler.WaitForNextInterval().ConfigureAwait(false);
        }
    }

    private async Task PushTelemetry(bool includeLogs, bool sendAppClosing)
    {
        try
        {
            // Always retrieve the metrics data, regardless of whether it's consumed, because we
            // need to make sure we clear the buffers. If we don't we could get overflows.
            // We will lose these metrics if the endpoint errors, but better than growing too much.
            MetricResults? metrics = _metrics.GetMetrics();

            if (!_sendTelemetry)
            {
                // sending is currently disabled, so don't fetch the other data or attempt to send
                Log.Debug("Telemetry pushing currently disabled, skipping");
                return;
            }

            var application = _application.GetApplicationData();
            var host = _application.GetHostData();
            if (application is null || host is null)
            {
                Log.Debug("Telemetry not initialized, skipping");
                return;
            }

            if (includeLogs && _redactedErrorLogs?.GetLogs() is { } batches)
            {
                foreach (var batch in batches)
                {
                    var logPayload = _dataBuilder.BuildLogsTelemetryData(application, host, batch, _namingVersion);
                    Log.Debug<int>("Pushing diagnostic logs batch containing {LogCount} logs", batch.Count);
                    await _transportManager.TryPushTelemetry(logPayload).ConfigureAwait(false);
                }
            }

            // use values from previous failed attempt if necessary
            var input = _aggregator.Combine(
                _configuration.GetData(),
                _dependencies.GetData(),
                _integrations.GetData(),
                in metrics,
                _products.GetData());

            var data = _dataBuilder.BuildTelemetryData(application, host, in input, _namingVersion, sendAppClosing);

            Log.Debug("Pushing telemetry changes");
            var result = await _transportManager.TryPushTelemetry(data).ConfigureAwait(false);
            _aggregator.SaveDataIfRequired(result, in input);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error pushing telemetry");
        }
    }

    private readonly struct WorkItem
    {
        public WorkItem(ItemType type, object? state)
        {
            Type = type;
            State = state;
        }

        public enum ItemType
        {
            SetTransportManager,
            SetFlushInterval,
            EnableSending,
            DisableSending,
            SetTracerStarted
        }

        public ItemType Type { get; }

        public object? State { get; }
    }

    /// <summary>
    /// Internal for testing
    /// </summary>
    internal class Scheduler
    {
        private const int DelayTaskIndex = 0;
        private const int ProcessTaskIndex = 1;
        private const int InitializationTaskIndex = 2;
        private const int LogQueueSizeTaskIndex = 3;

        private readonly TaskCompletionSource<bool> _tracerInitialized = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _processExitSource;
        private readonly Task[] _tasks;
        private readonly Func<Task> _logQueueTaskGenerator;
        private readonly IClock _clock;
        private readonly IDelayFactory _delayFactory;
        private TimeSpan _flushInterval;
        private DateTime _lastFlush;
        private bool _initializationFlushExecuted = false;

        public Scheduler(TimeSpan flushInterval, Func<Task> logQueueTaskGenerator, TaskCompletionSource<bool> processExitSource)
            : this(flushInterval, logQueueTaskGenerator, processExitSource, new Clock(), new DelayFactory())
        {
        }

        // For testing only
        public Scheduler(TimeSpan flushInterval, Func<Task> logQueueTaskGenerator, TaskCompletionSource<bool> processExitSource, IClock clock, IDelayFactory delayFactory)
        {
            _clock = clock;
            _delayFactory = delayFactory;
            _processExitSource = processExitSource;
            _flushInterval = flushInterval;
            _logQueueTaskGenerator = logQueueTaskGenerator;
            ShouldFlushTelemetry = false; // wait for initialization before flushing metrics
            _lastFlush = _clock.UtcNow;

            // Using a task array instead of overloads to avoid allocating the array every loop
            _tasks = new Task[4];
            _tasks[DelayTaskIndex] = Task.CompletedTask; // Replaced on first iteration of WaitForNextInterval(), but ensures there's no nulls around
            _tasks[ProcessTaskIndex] = processExitSource.Task;
            _tasks[InitializationTaskIndex] = _tracerInitialized.Task;
            _tasks[LogQueueSizeTaskIndex] = _logQueueTaskGenerator();
        }

        public interface IDelayFactory
        {
            Task Delay(TimeSpan delay);
        }

        public bool ShouldFlushTelemetry { get; private set; }

        public bool ShouldFlushRedactedErrorLogs { get; private set; }

        public void SetFlushInterval(TimeSpan flushInterval)
        {
            _flushInterval = flushInterval;
        }

        public void SetTracerInitialized()
        {
            _tracerInitialized.TrySetResult(true);
        }

        public async Task WaitForNextInterval()
        {
            // Calculate how long before the next flush. Accounts for the fact that it might
            // take a long time to push telemetry if the network is slow or faulty

            var nextFlush = _lastFlush.Add(_flushInterval);

            // Note that we don't start flushing until initialized, so using infinite delay initially
            TimeSpan? waitPeriod = _initializationFlushExecuted
                                 ? nextFlush - _clock.UtcNow
                                 : null;

            var logFlushTask = _logQueueTaskGenerator();
            if (waitPeriod.HasValue && waitPeriod.Value <= TimeSpan.Zero)
            {
                Log.Debug(
                    "Time to push telemetry exceeded the flush interval, triggering the next iteration immediately");
            }
            else
            {
                // if we don't have a wait period, it's because we're waiting for initialization
                _tasks[DelayTaskIndex] = _delayFactory.Delay(waitPeriod ?? Timeout.InfiniteTimeSpan);
                _tasks[LogQueueSizeTaskIndex] = logFlushTask;
                await Task.WhenAny(_tasks).ConfigureAwait(false);
            }

            if (_processExitSource.Task.IsCompleted)
            {
                // end of the line, flush everything, don't bother recalculating;
                ShouldFlushTelemetry = true;
                ShouldFlushRedactedErrorLogs = true;
                return;
            }

            // Reset variables
            ShouldFlushTelemetry = false;
            ShouldFlushRedactedErrorLogs = false;
            var now = _clock.UtcNow;

            // Should we flush telemetry?
            if (!_initializationFlushExecuted && _tracerInitialized.Task.IsCompleted)
            {
                _initializationFlushExecuted = true;
                // We've just been started, so should always flush telemetry
                ShouldFlushTelemetry = true;
                // Including logs as we typically log a lot at startup
                ShouldFlushRedactedErrorLogs = true;
                // replace the tracerInitializedTask with a task that never completes
                _tasks[InitializationTaskIndex] = Task.Delay(Timeout.Infinite);
            }

            if (_initializationFlushExecuted && logFlushTask.IsCompleted)
            {
                // should flush logs if we've reached the threshold
                ShouldFlushRedactedErrorLogs = true;
            }

            if (_initializationFlushExecuted && (nextFlush <= now))
            {
                ShouldFlushTelemetry = true;
                ShouldFlushRedactedErrorLogs = true;
            }

            if (ShouldFlushTelemetry)
            {
                _lastFlush = now;
            }
        }

        private class Clock : IClock
        {
            public DateTime UtcNow => DateTime.UtcNow;
        }

        private class DelayFactory : IDelayFactory
        {
            public Task Delay(TimeSpan delay) => Task.Delay(delay);
        }
    }
}
