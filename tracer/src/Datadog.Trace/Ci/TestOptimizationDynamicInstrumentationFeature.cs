// <copyright file="TestOptimizationDynamicInstrumentationFeature.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Ci.Configuration;
using Datadog.Trace.Ci.Net;
using Datadog.Trace.Debugger.ExceptionAutoInstrumentation;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Ci;

internal sealed class TestOptimizationDynamicInstrumentationFeature : ITestOptimizationDynamicInstrumentationFeature
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(TestOptimizationDynamicInstrumentationFeature));
    private readonly Task<ExceptionIdentifier?> _doneTask;
    private TaskCompletionSource<ExceptionIdentifier?> _doneTaskSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public const int DefaultExceptionHandlerTimeout = 2_000;

    public TestOptimizationDynamicInstrumentationFeature(TestOptimizationSettings settings, TestOptimizationClient.SettingsResponse clientSettingsResponse)
    {
        _doneTask = Task.FromResult<ExceptionIdentifier?>(null);
        if (settings.DynamicInstrumentationEnabled == null && clientSettingsResponse.DynamicInstrumentationEnabled.HasValue)
        {
            Log.Information("TestOptimizationDynamicInstrumentationFeature: Dynamic instrumentation has been changed to {Value} by the settings api.", clientSettingsResponse.DynamicInstrumentationEnabled.Value);
            settings.SetFlakyRetryEnabled(clientSettingsResponse.DynamicInstrumentationEnabled.Value);
        }

        if (settings.DynamicInstrumentationEnabled == true)
        {
            Log.Information("TestOptimizationDynamicInstrumentationFeature: Dynamic instrumentation is enabled.");
            Enabled = true;
        }
        else
        {
            Log.Information("TestOptimizationDynamicInstrumentationFeature: Dynamic instrumentation is disabled.");
            Enabled = false;
        }

        if (Enabled)
        {
            settings.SetDynamicInstrumentationEnabled(true);
            ExceptionTrackManager.ExceptionCaseInstrumented += exceptionIdentifier =>
            {
                Log.Debug("TestOptimizationDynamicInstrumentationFeature: Exception instrumentation completed for {ExceptionIdentifier}", exceptionIdentifier);
                var tcs = Interlocked.Exchange(ref _doneTaskSource, new(TaskCreationOptions.RunContinuationsAsynchronously));
                tcs.TrySetResult(exceptionIdentifier);
            };
        }
        else
        {
            settings.SetDynamicInstrumentationEnabled(false);
        }
    }

    public bool Enabled { get; }

    public static ITestOptimizationDynamicInstrumentationFeature Create(TestOptimizationSettings settings, TestOptimizationClient.SettingsResponse clientSettingsResponse)
        => new TestOptimizationDynamicInstrumentationFeature(settings, clientSettingsResponse);

    public Task WaitForExceptionInstrumentation(int timeout)
    {
        if (!Enabled)
        {
            return _doneTask;
        }

        var dts = _doneTaskSource;
        dts = Interlocked.CompareExchange(ref _doneTaskSource, dts, dts);
        return Task.WhenAny(dts.Task, Task.Delay(timeout));
    }
}
