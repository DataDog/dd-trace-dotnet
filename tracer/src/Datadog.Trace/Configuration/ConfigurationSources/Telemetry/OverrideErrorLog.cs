// <copyright file="OverrideErrorLog.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using Datadog.Trace.Logging;
using Datadog.Trace.Telemetry;

namespace Datadog.Trace.Configuration.ConfigurationSources.Telemetry;

/// <summary>
/// Records cases where configuration was overridden by startup telemetry,
/// so they can be written once the tracer has been fully initialized
/// </summary>
internal sealed class OverrideErrorLog : IConfigurationOverrideHandler
{
    private readonly object _lock = new();
    private List<Action<IDatadogLogger, IMetricsTelemetryCollector>>? _actions;

    public static OverrideErrorLog Instance { get; } = new();

    /// <summary>
    /// Enqueue an action to be executed
    /// </summary>
    public void EnqueueAction(Action<IDatadogLogger, IMetricsTelemetryCollector> action)
    {
        lock (_lock)
        {
            _actions ??= new();
            _actions.Add(action);
        }
    }

    public void ProcessAndClearActions(IDatadogLogger log, IMetricsTelemetryCollector metrics)
    {
        List<Action<IDatadogLogger, IMetricsTelemetryCollector>>? actions;

        lock (_lock)
        {
            actions = _actions;
            _actions = null;
        }

        if (actions is not null)
        {
            foreach (var logAction in actions)
            {
                logAction(log, metrics);
            }
        }
    }

    public OverrideErrorLog Clone()
    {
        var clone = new OverrideErrorLog();
        lock (_lock)
        {
            clone._actions = _actions?.ToList();
        }

        return clone;
    }

    public void LogDuplicateConfiguration(string datadogKey, string otelKey)
    {
        EnqueueAction(
            (log, metrics) =>
            {
                log.Warning(
                    "Both Datadog configuration {DatadogConfiguration} and OpenTelemetry configuration {OpenTelemetryConfiguration} are set. The Datadog configuration will be used.",
                    datadogKey,
                    otelKey);
                OpenTelemetryHelpers.GetConfigurationMetricTags(otelKey, out var openTelemetryConfig, out var datadogConfig);
                metrics.RecordCountOpenTelemetryConfigHiddenByDatadogConfig(datadogConfig, openTelemetryConfig);
            });
    }

    public void LogInvalidConfiguration(string otelKey)
    {
        EnqueueAction(
            (log, metrics) =>
            {
                log.Warning("OpenTelemetry configuration {OpenTelemetryConfiguration} is invalid.", otelKey);
                OpenTelemetryHelpers.GetConfigurationMetricTags(otelKey, out var openTelemetryConfig, out var datadogConfig);
                metrics.RecordCountOpenTelemetryConfigInvalid(datadogConfig, openTelemetryConfig);
            });
    }

    public void LogUnsupportedConfiguration(string otelKey, string otelValue, string replacementValue)
    {
        EnqueueAction(
            (log, _) =>
            {
                log.Warning(
                    "OpenTelemetry configuration {OpenTelemetryConfiguration}={OpenTelemetryValue} is not supported. {ModifiedValue} will be used instead.",
                    otelKey,
                    otelValue,
                    replacementValue);
            });
    }

    bool IConfigurationOverrideHandler.TryHandleOverrides<T>(
        string datadogKey,
        ConfigurationResult<T> datadogConfigResult,
        string otelKey,
        ConfigurationResult<T> otelConfigResult,
        [NotNullWhen(true)] out T? value)
        where T : default
    {
        Console.WriteLine("IConfigurationOverrideHandler.TryHandleOverrides: Evaluating OTEL Key: {0}, DD Key: {1}", otelKey, datadogKey);
        Console.WriteLine("IConfigurationOverrideHandler.TryHandleOverrides: OtelConfigResult.Result={0}, OtelConfigResult.IsValid={1}, OtelConfigResult.ShouldFallBack={2}, OtelConfigResult.IsPresent={3},", otelConfigResult.Result, otelConfigResult.IsValid, otelConfigResult.ShouldFallBack, otelConfigResult.IsPresent);
        if (datadogConfigResult.IsPresent && otelConfigResult.IsPresent)
        {
            Console.WriteLine("IConfigurationOverrideHandler.TryHandleOverrides: Both present");
            LogDuplicateConfiguration(datadogKey, otelKey);
        }
        else if (otelConfigResult is { IsPresent: true } config)
        {
            if (config is { Result: { } openTelemetryValue, IsValid: true })
            {
                {
                    value = openTelemetryValue;
                    Console.WriteLine("IConfigurationOverrideHandler.TryHandleOverrides: Exiting with valid OTEL Value: {0}", openTelemetryValue);
                    return true;
                }
            }

            Console.WriteLine("IConfigurationOverrideHandler.TryHandleOverrides: Invalid OTEL Value: {0}", otelKey);
            LogInvalidConfiguration(otelKey);
        }

        Console.WriteLine("IConfigurationOverrideHandler.TryHandleOverrides: Exiting");
        value = default;
        return false;
    }
}
