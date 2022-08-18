// <copyright file="DataStreamsManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.DataStreamsMonitoring.Hashes;
using Datadog.Trace.Logging;

namespace Datadog.Trace.DataStreamsMonitoring;

/// <summary>
/// Manages all the data streams monitoring behaviour
/// </summary>
internal class DataStreamsManager
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<DataStreamsManager>();
    private readonly NodeHashBase _nodeHashBase;
    private bool _isEnabled;

    public DataStreamsManager(
        bool enabled,
        string env,
        string defaultServiceName)
    {
        _isEnabled = enabled;
        // We don't yet support primary tag in .NET yet
        _nodeHashBase = HashHelper.CalculateNodeHashBase(defaultServiceName, env, primaryTag: null);
    }

    public bool IsEnabled => Volatile.Read(ref _isEnabled);

    public static DataStreamsManager Create(
        ImmutableTracerSettings settings,
        string defaultServiceName)
        => new(settings.IsDataStreamsMonitoringEnabled, settings.Environment, defaultServiceName);

    public Task DisposeAsync()
    {
        Volatile.Write(ref _isEnabled, false);
        return Task.CompletedTask;
    }
}
