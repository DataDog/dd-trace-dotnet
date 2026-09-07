// <copyright file="TestMethodRunnerExecuteAsyncIntegrationV4_4.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections;
using System.ComponentModel;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.MsTestV2;

/// <summary>
/// Marks an attempt around all its data rows, rather than counting each row as a retry.
/// </summary>
[InstrumentMethod(
    AssemblyName = "MSTestAdapter.PlatformServices",
    TypeName = "Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution.TestMethodRunner",
    MethodName = "ExecuteAsync",
    ReturnTypeName = "System.Threading.Tasks.Task`1[Microsoft.VisualStudio.TestTools.UnitTesting.TestResult[]]",
    ParameterTypeNames = [ClrNames.String, ClrNames.String, ClrNames.String, ClrNames.String],
    MinimumVersion = "4.4.0",
    MaximumVersion = "4.*.*",
    IntegrationName = MsTestIntegration.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class TestMethodRunnerExecuteAsyncIntegrationV4_4
{
    internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, string? logs, string? errors, string? trace, string? messages)
    {
        if (MsTestExecution.Current is not { } execution)
        {
            return CallTargetState.GetDefault();
        }

        execution.StartNativeAttempt();
        return new CallTargetState(null, execution);
    }

    internal static async Task<TReturn?> OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn? returnValue, Exception? exception, CallTargetState state)
    {
        // MSTest never calls RetryBaseAttribute when the first attempt is already acceptable.
        // EFD and Attempt to Fix must still run before class and assembly cleanup in that case.
        if (exception is null &&
            state.State is MsTestExecution { HasNativeRetry: true, IsNativeRetry: false } execution &&
            returnValue is IList results &&
            MsTestExecution.IsAcceptableNativeResult(results))
        {
            await execution.ApplyDatadogRetriesAsync(results).ConfigureAwait(false);
        }

        return returnValue;
    }
}
