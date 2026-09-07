// <copyright file="UnitTestRunnerRunSingleTestAsyncIntegrationV4_4.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.MsTestV2;

/// <summary>
/// Coordinates the native retry sequence before applying Datadog retry policies.
/// </summary>
[InstrumentMethod(
    AssemblyName = "MSTestAdapter.PlatformServices",
    TypeName = "Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution.UnitTestRunner",
    MethodName = "RunSingleTestAsync",
    ReturnTypeName = "System.Threading.Tasks.Task`1[Microsoft.VisualStudio.TestTools.UnitTesting.TestResult[]]",
    ParameterTypeNames = ["Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel.UnitTestElement", "System.Collections.Generic.IDictionary`2[System.String,System.Object]", "System.Collections.Generic.IDictionary`2[System.String,System.Object]", "Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface.IAdapterMessageLogger"],
    MinimumVersion = "4.4.0",
    MaximumVersion = "4.*.*",
    IntegrationName = MsTestIntegration.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class UnitTestRunnerRunSingleTestAsyncIntegrationV4_4
{
    internal static CallTargetState OnMethodBegin<TTarget, TElement, TLogger>(TTarget instance, TElement element, IDictionary<string, object?> testProperties, IDictionary<string, object?> lifecycleProperties, TLogger logger)
    {
        if (!MsTestIntegration.IsEnabled)
        {
            return CallTargetState.GetDefault();
        }

        var previousState = UnitTestRunnerRunSingleTestAsyncIntegration3_8.OnMethodBegin(instance, element, testProperties, logger);
        var execution = new MsTestExecution(previousState, MsTestExecution.Current);
        MsTestExecution.Current = execution;
        return new CallTargetState(null, execution);
    }

    internal static CallTargetReturn<TReturn?> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn? returnValue, Exception? exception, in CallTargetState state)
    {
        if (state.State is MsTestExecution execution)
        {
            UnitTestRunnerRunSingleTestAsyncIntegration3_8.OnMethodEnd(instance, returnValue, exception, execution.PreviousState);
            MsTestExecution.Current = execution.Parent;
        }

        return new CallTargetReturn<TReturn?>(returnValue);
    }

    internal static async Task<TReturn?> OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn? returnValue, Exception? exception, CallTargetState state)
    {
        if (state.State is not MsTestExecution execution)
        {
            return returnValue;
        }

        var previous = MsTestExecution.Current;
        MsTestExecution.Current = execution;
        try
        {
            if (returnValue is IList results)
            {
                await execution.CompleteAsync(results, exception).ConfigureAwait(false);
            }

            UnitTestRunnerRunSingleTestAsyncIntegration3_8.OnAsyncMethodEnd(instance, returnValue, exception, execution.PreviousState);
            return returnValue;
        }
        finally
        {
            execution.FinishSpans();
            MsTestExecution.Current = previous;
        }
    }
}
