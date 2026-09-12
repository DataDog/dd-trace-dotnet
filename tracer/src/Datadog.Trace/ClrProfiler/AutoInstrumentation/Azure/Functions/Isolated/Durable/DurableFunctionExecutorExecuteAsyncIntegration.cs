// <copyright file="DurableFunctionExecutorExecuteAsyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK
#nullable enable

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.Functions;

/// <summary>
/// Microsoft.Azure.Functions.Worker.Extensions.DurableTask.Execution.DurableFunctionExecutor.ExecuteAsync calltarget instrumentation.
/// </summary>
[InstrumentMethod(
    AssemblyName = "Microsoft.Azure.Functions.Worker.Extensions.DurableTask",
    TypeName = "Microsoft.Azure.Functions.Worker.Extensions.DurableTask.Execution.DurableFunctionExecutor",
    MethodName = "ExecuteAsync",
    ReturnTypeName = "System.Threading.Tasks.ValueTask",
    ParameterTypeNames = ["Microsoft.Azure.Functions.Worker.FunctionContext"],
    MinimumVersion = "1.12.0",
    MaximumVersion = "1.*.*",
    IntegrationName = AzureFunctionsDurableCommon.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class DurableFunctionExecutorExecuteAsyncIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget, TFunctionContext>(TTarget instance, TFunctionContext functionContext)
        where TFunctionContext : IDurableFunctionContext
    {
        return AzureFunctionsDurableCommon.OnFunctionExecutionBegin(functionContext);
    }

    internal static TReturn? OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn? returnValue, Exception? exception, in CallTargetState state)
    {
        state.Scope.DisposeWithException(exception);
        return returnValue;
    }
}

#endif
