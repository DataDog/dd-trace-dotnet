// <copyright file="StatsdManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
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
internal sealed class StatsdManager : IStatsdManager
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<StatsdManager>();
    private readonly object _lock = new();
    private readonly IDisposable _settingSubscription;
    private int _isRequiredMask;
    private StatsdClientHolder? _current;
    private Func<StatsdClientHolder> _factory;

    public StatsdManager(TracerSettings tracerSettings)
        : this(tracerSettings, CreateClient)
    {
    }

    // Internal for testing
    internal StatsdManager(TracerSettings tracerSettings, Func<MutableSettings, ExporterSettings, IList<string>, StatsdClientHolder> statsdFactory)
    {
        // The initial factory, assuming there's no updates
        _factory = () => statsdFactory(
            tracerSettings.Manager.InitialMutableSettings,
            tracerSettings.Manager.InitialExporterSettings,
            tracerSettings.PropagateProcessTags ? ProcessTags.TagsList : []);

        // We don't create a new client unless we need one, and we rely on consumers of the manager to tell us when it's needed
        _current = null;

        _settingSubscription = tracerSettings.Manager.SubscribeToChanges(c =>
        {
            // To avoid expensive unnecessary replacements, we only rebuild the statsd instance
            // if something changes that could impact it. In other words, if StatsdFactory uses
            // a value then we should check if it's changed here
            if (!HasImpactingChanges(c))
            {
                Log.Debug("No impacting changes found for StatsdManager, ignoring settings update");
                return;
            }

            // update the factory
            Log.Debug("Updating statsdClient factory to use new configuration");
            Interlocked.Exchange(
                ref _factory,
                () => statsdFactory(
                    c.UpdatedMutable ?? c.PreviousMutable,
                    c.UpdatedExporter ?? c.PreviousExporter,
                    tracerSettings.PropagateProcessTags ? ProcessTags.TagsList : []));

            // check if we actually need to do an update or if noone is using the client yet
            if (Volatile.Read(ref _isRequiredMask) != 0)
            {
                // Someone needs it, so create
                EnsureClient(ensureCreated: true, forceRecreate: true);
            }
        });
    }

    /// <inheritdoc cref="IStatsdManager.TryGetClientLease"/>
    public StatsdClientLease TryGetClientLease()
    {
        while (true)
        {
            var current = Volatile.Read(ref _current);
            if (current == null)
            {
                return default;
            }

            if (current.TryRetain())
            {
                return new StatsdClientLease(current);
            }

            // The client was marked for closing, there should be a new one
            // we can use instead, so loop around and grab that.
        }
    }

    /// <inheritdoc cref="IStatsdManager.SetRequired"/>
    public void SetRequired(StatsdConsumer consumer, bool enabled)
    {
        var bitToSet = (int)consumer;

        if (enabled)
        {
            // Set the consumer bit; and check if there's been a change from before
#if NET6_0_OR_GREATER
            var prev = Interlocked.Or(ref _isRequiredMask, bitToSet);
#else
            // Can't use Interlocked.Or, so have to use a loop to be sure
            int prev, updated;
            do
            {
                prev = Volatile.Read(ref _isRequiredMask);
                updated = prev | bitToSet;
                if (prev == updated)
                {
                    // already set, nothing to do
                    return;
                }
            }
            while (Interlocked.CompareExchange(ref _isRequiredMask, updated, prev) != prev);
#endif
            if (prev == 0)
            {
                // We transitioned from 0 -> non-zero: ensure client exists
                EnsureClient(ensureCreated: true, forceRecreate: false);
            }
        }
        else
        {
            // Atomically clear bit; clearMask is all ones, excluding the bit we're clearing
            var clearMask = ~bitToSet;
#if NET6_0_OR_GREATER

            var prev = Interlocked.And(ref _isRequiredMask, clearMask);
#else
            int prev, updated;
            do
            {
                prev = Volatile.Read(ref _isRequiredMask);
                updated = prev & clearMask;
                if (prev == updated)
                {
                    // already cleared, nothing to do
                    return;
                }
            }
            while (Interlocked.CompareExchange(ref _isRequiredMask, updated, prev) != prev);
#endif
            if (prev == bitToSet)
            {
                // We transitioned from 1 -> 0: get rid of the client
                EnsureClient(ensureCreated: false, forceRecreate: false);
            }
        }
    }

    public void Dispose()
    {
        _settingSubscription.Dispose();
        // We swap out the client to make sure we do any flushes.
        EnsureClient(ensureCreated: false, forceRecreate: true);
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

    private static StatsdClientHolder CreateClient(MutableSettings settings, ExporterSettings exporter, IList<string> processTags)
    {
        return new StatsdClientHolder(StatsdFactory.CreateDogStatsdClient(settings, exporter, includeDefaultTags: true, processTags));
    }

    private void EnsureClient(bool ensureCreated, bool forceRecreate)
    {
        StatsdClientHolder? previous;
        Log.Debug("Recreating statsdClient: Create new client: {CreateClient}, Force recreate: {ForceRecreate}", ensureCreated, forceRecreate);

        lock (_lock)
        {
            previous = _current;
            if (ensureCreated && previous != null && !forceRecreate)
            {
                // Already created
                return;
            }

            _current = ensureCreated ? _factory() : null;
        }

        previous?.MarkClosing(); // will dispose when last lease releases
    }

    internal readonly struct StatsdClientLease(StatsdClientHolder? holder) : IDisposable
    {
        private readonly StatsdClientHolder? _holder = holder;

        public IDogStatsd? Client => _holder?.Client;

        public void Dispose() => _holder?.Release();
    }

    internal sealed class StatsdClientHolder(IDogStatsd client)
    {
        private const int ClosingBit = 1 << 31;  // sign bit = closing

        // Logically, _state represents two values we need to check:
        // - Was MarkClosing() called?
        // - How many references does it have?
        // We keep this all in the same variable to avoid race conditions that
        // would occur if we had separate flag variables for count and closing
        // high bit = closing, low 31 bits = refcount
        private int _state;
        private int _disposed;

        public IDogStatsd Client { get; } = client;

        // Internal for testing
        public bool IsDisposed => Volatile.Read(ref _disposed) == 1;

        public bool TryRetain()
        {
            while (true)
            {
                var state = Volatile.Read(ref _state);
                if ((state & ClosingBit) != 0)
                {
                    // already closing; deny new leases
                    return false;
                }

                if ((state & int.MaxValue) == int.MaxValue)
                {
                    // Guard against int.MaxValue retentions, won't happen, but play it safe
                    return false;
                }

                // Conditionally bump ref count
                if (Interlocked.CompareExchange(ref _state, state + 1, state) == state)
                {
                    // ok, increment ref count
                    return true;
                }

                // The state of the client holder changed out from under us (someone else retained it, or it was closed), try again
            }
        }

        /// <summary>
        /// Invoked by <see cref="StatsdClientLease"/> to indicate client is done with it
        /// </summary>
        public void Release()
        {
            var v = Interlocked.Decrement(ref _state);
            if (v == ClosingBit)
            {
                // count hit zero and we're marked as closing
                Dispose();
            }
        }

        /// <summary>
        /// Invoked by <see cref="StatsdManager"/> when swapping clients
        /// </summary>
        public void MarkClosing()
        {
            // Set the closing bit to ensure no more retention of client
#if NET6_0_OR_GREATER
            Interlocked.Or(ref _state, ClosingBit);
#else
            // Interlocked.Or is not available in < .NET 6, so have to emulate it
            int state;
            do
            {
                state = Volatile.Read(ref _state);
            }
            while (Interlocked.CompareExchange(ref _state, state | ClosingBit, state) != state);
#endif

            // If ref count is 0 (i.e., state == ClosingBit), dispose now; else wait for Release() to reach 0
            if ((Volatile.Read(ref _state) & int.MaxValue) == 0)
            {
                Dispose();
            }
        }

        private void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                Log.Debug("Disposing DogStatsdService");

                // We push this all to a background thread to avoid the disposes running in-line
                // the DogStatsdService does sync-over-async, and this can cause thread exhaustion
                _ = Task.Run(() =>
                {
                    if (Client is DogStatsdService dogStatsd)
                    {
                        dogStatsd.Flush();
                    }

                    Client.Dispose();
                });
            }
        }
    }
}
