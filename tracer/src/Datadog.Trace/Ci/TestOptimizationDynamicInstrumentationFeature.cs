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
    private static Task<ExceptionIdentifier?>? _doneTask;
    private TaskCompletionSource<ExceptionIdentifier?>? _doneTaskSource;

    public const int DefaultExceptionHandlerTimeout = 2_000;

    public TestOptimizationDynamicInstrumentationFeature(TestOptimizationSettings settings, TestOptimizationClient.SettingsResponse clientSettingsResponse)
    {
        if (!settings.DynamicInstrumentationEnabled.HasValue && clientSettingsResponse.DynamicInstrumentationEnabled.HasValue)
        {
            Log.Information("TestOptimizationDynamicInstrumentationFeature: Dynamic instrumentation has been changed to {Value} by the settings api.", clientSettingsResponse.DynamicInstrumentationEnabled);
            settings.SetDynamicInstrumentationEnabled(clientSettingsResponse.DynamicInstrumentationEnabled.Value);
        }

        Enabled = settings.DynamicInstrumentationEnabled == true;
        if (Enabled)
        {
            Log.Information("TestOptimizationDynamicInstrumentationFeature: Dynamic instrumentation is enabled.");
            _doneTaskSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
            ExceptionTrackManager.ExceptionCaseInstrumented += ExceptionCaseInstrumentedHandler;
        }
        else
        {
            Log.Information("TestOptimizationDynamicInstrumentationFeature: Dynamic instrumentation is disabled.");
        }

        settings.SetDynamicInstrumentationEnabled(Enabled);
    }

    public bool Enabled { get; }

    public static ITestOptimizationDynamicInstrumentationFeature Create(TestOptimizationSettings settings, TestOptimizationClient.SettingsResponse clientSettingsResponse)
        => new TestOptimizationDynamicInstrumentationFeature(settings, clientSettingsResponse);

    public Task WaitForExceptionInstrumentation(int timeout)
    {
        if (Enabled && Volatile.Read(ref _doneTaskSource) is { } tcs)
        {
            return Task.WhenAny(tcs.Task, Task.Delay(timeout));
        }

        return _doneTask ??= Task.FromResult<ExceptionIdentifier?>(null);
    }

    private void ExceptionCaseInstrumentedHandler(ExceptionIdentifier exceptionIdentifier)
    {
        Log.Debug("TestOptimizationDynamicInstrumentationFeature: Exception instrumentation completed for {ExceptionIdentifier}", exceptionIdentifier);
        Interlocked.Exchange(ref _doneTaskSource, new(TaskCreationOptions.RunContinuationsAsynchronously))?
           .TrySetResult(exceptionIdentifier);
    }
}
