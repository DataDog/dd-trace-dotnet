// <copyright file="MutableSettingsService.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Configuration;

internal class MutableSettingsService(MutableSettings initialSettings)
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<MutableSettingsService>();
    private readonly List<MutableSettingsSubscription> _mutableSettingSubscribers = [];
    private MutableSettings _current = initialSettings;

    /// <summary>
    /// Gets the current <see cref="MutableSettings"/>. On app startup, these will be the values read from
    /// static sources. To subscribe to updates in these settings, call <see cref="Subscribe"/>.
    /// </summary>
    public MutableSettings Current => _current;

    /// <summary>
    /// Subscribe to changes in <see cref="MutableSettings"/>. Called whenever the settings change. Note
    /// that this method is not automatically invoked until a subsequent Publish.
    /// </summary>
    /// <param name="callback">The method to invoke</param>
    /// <returns>An <see cref="IDisposable"/> that should be disposed to unsubscribe</returns>
    public IDisposable Subscribe(Action<MutableSettings> callback)
    {
        // Note that calling subscribe twice with the same callback will register it twice
        var subscription = new MutableSettingsSubscription(this, callback);
        lock (_mutableSettingSubscribers)
        {
            _mutableSettingSubscribers.Add(subscription);
        }

        return subscription;
    }

    public void Publish(MutableSettings settings)
    {
        List<MutableSettingsSubscription> subscribers;
        lock (_mutableSettingSubscribers)
        {
            subscribers = [.._mutableSettingSubscribers];
            Interlocked.Exchange(ref _current, settings);
        }

        foreach (var subscriber in subscribers)
        {
            try
            {
                subscriber.Update(settings);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error notifying subscriber of MutableSettings change");
            }
        }
    }

    private sealed class MutableSettingsSubscription(MutableSettingsService owner, Action<MutableSettings> update) : IDisposable
    {
        private readonly MutableSettingsService _owner = owner;

        public Action<MutableSettings> Update { get; } = update;

        public void Dispose()
        {
            lock (_owner._mutableSettingSubscribers)
            {
                _owner._mutableSettingSubscribers.Remove(this);
            }
        }
    }
}
