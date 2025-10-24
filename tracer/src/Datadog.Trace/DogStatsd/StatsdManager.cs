// <copyright file="StatsdManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.StatsdClient;

namespace Datadog.Trace.DogStatsd;

/// <summary>
/// This acts as a wrapper around a "real" <see cref="DogStatsdService"/> service or a <see cref="NoOpStatsd"/> client,
/// but which responds to changes in settings caused by remote config or configuration in code.
/// </summary>
internal sealed class StatsdManager : IDogStatsd
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<StatsdManager>();
    private readonly object _lock = new();
    private readonly IDisposable _settingSubscription;
    private IDogStatsd _current;
    private bool _isDisposed;

    public StatsdManager(TracerSettings tracerSettings, bool includeDefaultTags)
    {
        _current = StatsdFactory.CreateDogStatsdClient(
            tracerSettings.Manager.InitialMutableSettings,
            tracerSettings.Manager.InitialExporterSettings,
            includeDefaultTags);

        _settingSubscription = tracerSettings.Manager.SubscribeToChanges(c =>
        {
            // To avoid expensive unnecessary replacements, we only rebuild the statsd instance
            // if something changes that could impact it. In other words, if StatsdFactory uses
            // a value then we should check if it's changed here
            if (!HasImpactingChanges(c))
            {
                return;
            }

            IDogStatsd previous;

            lock (_lock)
            {
                if (_isDisposed)
                {
                    return;
                }

                previous = _current;
                _current = StatsdFactory.CreateDogStatsdClient(
                    c.UpdatedMutable ?? c.PreviousMutable,
                    c.UpdatedExporter ?? c.PreviousExporter,
                    includeDefaultTags);
            }

            if (previous is DogStatsdService dogStatsdService)
            {
                // Kick off disposal in the background after a delay to make sure everything is flushed
                // There's a risk that something could have grabbed the instance, and if something
                // tries to write to the client, it will throw an exception.
                Task.Run(async () =>
                     {
                         await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
                         dogStatsdService.Flush();
                         dogStatsdService.Dispose();
                     })
                    .ContinueWith(t => Log.Error(t.Exception, "There was an error disposing the statsd client"), TaskContinuationOptions.OnlyOnFaulted);
            }
        });
    }

    // Delegated implementation
    public ITelemetryCounters TelemetryCounters => Volatile.Read(ref _current).TelemetryCounters;

    public void Configure(StatsdConfig config) => Volatile.Read(ref _current).Configure(config);

    public void Counter(string statName, double value, double sampleRate = 1, string[]? tags = null) => Volatile.Read(ref _current).Counter(statName, value, sampleRate, tags);

    public void Decrement(string statName, int value = 1, double sampleRate = 1, params string[] tags) => Volatile.Read(ref _current).Decrement(statName, value, sampleRate, tags);

    public void Event(string title, string text, string? alertType = null, string? aggregationKey = null, string? sourceType = null, int? dateHappened = null, string? priority = null, string? hostname = null, string[]? tags = null) => Volatile.Read(ref _current).Event(title, text, alertType, aggregationKey, sourceType, dateHappened, priority, hostname, tags);

    public void Gauge(string statName, double value, double sampleRate = 1, string[]? tags = null) => Volatile.Read(ref _current).Gauge(statName, value, sampleRate, tags);

    public void Histogram(string statName, double value, double sampleRate = 1, string[]? tags = null) => Volatile.Read(ref _current).Histogram(statName, value, sampleRate, tags);

    public void Distribution(string statName, double value, double sampleRate = 1, string[]? tags = null) => Volatile.Read(ref _current).Distribution(statName, value, sampleRate, tags);

    public void Increment(string statName, int value = 1, double sampleRate = 1, string[]? tags = null) => Volatile.Read(ref _current).Increment(statName, value, sampleRate, tags);

    public void Set<T>(string statName, T value, double sampleRate = 1, string[]? tags = null) => Volatile.Read(ref _current).Set(statName, value, sampleRate, tags);

    public void Set(string statName, string value, double sampleRate = 1, string[]? tags = null) => Volatile.Read(ref _current).Set(statName, value, sampleRate, tags);

    public IDisposable StartTimer(string name, double sampleRate = 1, string[]? tags = null) => Volatile.Read(ref _current).StartTimer(name, sampleRate, tags);

    public void Time(Action action, string statName, double sampleRate = 1, string[]? tags = null) => Volatile.Read(ref _current).Time(action, statName, sampleRate, tags);

    public T Time<T>(Func<T> func, string statName, double sampleRate = 1, string[]? tags = null) => Volatile.Read(ref _current).Time(func, statName, sampleRate, tags);

    public void Timer(string statName, double value, double sampleRate = 1, string[]? tags = null) => Volatile.Read(ref _current).Timer(statName, value, sampleRate, tags);

    public void ServiceCheck(string name, Status status, int? timestamp = null, string? hostname = null, string[]? tags = null, string? message = null) => Volatile.Read(ref _current).ServiceCheck(name, status, timestamp, hostname, tags, message);

    public void Dispose()
    {
        IDogStatsd previous;
        lock (_lock)
        {
            _isDisposed = true;
            _settingSubscription.Dispose();
            previous = _current;
            _current = NoOpStatsd.Instance;
        }

        // Given we're shutting down at this point, it doesn't seem like it's worth actually disposing
        // the instance (and risking an error) so we just flush to make sure everything is sent
        if (previous is DogStatsdService dogStatsdService)
        {
            dogStatsdService.Flush();
        }
    }

    // Internal for testing
    internal static bool HasImpactingChanges(TracerSettings.SettingsManager.SettingChanges changes)
    {
        var hasChanges = changes.UpdatedExporter is not null // relying on this to only be non null if _anything_ changed
                      || (changes.UpdatedMutable is { } updated
                       && !(
                               string.Equals(updated.Environment, changes.PreviousMutable.Environment, StringComparison.Ordinal)
                            && string.Equals(updated.ServiceVersion, changes.PreviousMutable.ServiceVersion, StringComparison.Ordinal)
                               // The service name comparison isn't _strictly_ correct, because we normalize it further, but this is probably good enough
                            && string.Equals(updated.DefaultServiceName, changes.PreviousMutable.DefaultServiceName, StringComparison.OrdinalIgnoreCase)
                            && updated.GlobalTags.SequenceEqual(changes.PreviousMutable.GlobalTags)));
        return hasChanges;
    }
}
