// <copyright file="FunctionsOrchestratorRunAsyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK
#nullable enable

using System;
using System.ComponentModel;
using System.Threading;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.Functions;

/// <summary>
/// Microsoft.Azure.Functions.Worker.Extensions.DurableTask.FunctionsOrchestrator.RunAsync calltarget instrumentation.
/// </summary>
[InstrumentMethod(
    AssemblyName = "Microsoft.Azure.Functions.Worker.Extensions.DurableTask",
    TypeName = "Microsoft.Azure.Functions.Worker.Extensions.DurableTask.FunctionsOrchestrator",
    MethodName = "RunAsync",
    ReturnTypeName = "System.Threading.Tasks.Task`1[System.Object]",
    ParameterTypeNames = ["Microsoft.DurableTask.TaskOrchestrationContext", ClrNames.Object],
    MinimumVersion = "1.12.0",
    MaximumVersion = "1.*.*",
    IntegrationName = AzureFunctionsDurableCommon.IntegrationName)]
[InstrumentMethod(
    AssemblyName = "Microsoft.Azure.Functions.Worker.Extensions.DurableTask",
    TypeName = "Microsoft.Azure.Functions.Worker.Extensions.DurableTask.Execution.WrapperOrchestrator",
    MethodName = "RunAsync",
    ReturnTypeName = "System.Threading.Tasks.Task`1[System.Object]",
    ParameterTypeNames = ["Microsoft.DurableTask.TaskOrchestrationContext", ClrNames.Object],
    MinimumVersion = "1.12.0",
    MaximumVersion = "1.*.*",
    IntegrationName = AzureFunctionsDurableCommon.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class FunctionsOrchestratorRunAsyncIntegration
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<FunctionsOrchestratorRunAsyncIntegration>();

    internal static CallTargetState OnMethodBegin<TTarget, TOrchestrationContext, TInput>(
        TTarget instance,
        TOrchestrationContext orchestrationContext,
        TInput input)
        where TTarget : IDurableFunctionsOrchestrator
        where TOrchestrationContext : IDurableTaskOrchestrationContext
    {
        var startTime = DateTimeOffset.UtcNow;

        // IsReplaying is true for all executions other than the first one, don't create a scope
        if (orchestrationContext.IsReplaying)
        {
            return new CallTargetState(scope: null, state: null, startTime);
        }

        var initialState = AzureFunctionsDurableCommon.OnFunctionExecutionBegin(instance.FunctionContext, startTime);
        return new CallTargetState(initialState.Scope, new InitialOrchestrationScope(initialState.Scope), startTime);
    }

    internal static CallTargetReturn<TReturn?> OnMethodEnd<TTarget, TReturn>(
        TTarget instance,
        TReturn? returnValue,
        Exception? exception,
        in CallTargetState state)
    {
        // The first execution returns an incomplete task when it schedules durable work.
        // Close its clean span now because that task may be abandoned.
        if (state.State is InitialOrchestrationScope initialScope)
        {
            initialScope.TryDispose(exception);
        }

        return new CallTargetReturn<TReturn?>(returnValue);
    }

    internal static TReturn? OnAsyncMethodEnd<TTarget, TReturn>(
        TTarget instance,
        TReturn? returnValue,
        Exception? exception,
        in CallTargetState state)
        where TTarget : IDurableFunctionsOrchestrator
    {
        if (state.State is InitialOrchestrationScope initialScope && initialScope.TryDispose(exception))
        {
            return returnValue;
        }

        // Successful replays are suppressed
        if (exception is null)
        {
            return returnValue;
        }

        try
        {

            var errorState = AzureFunctionsDurableCommon.OnFunctionExecutionBegin(instance.FunctionContext, state.StartTime);
            errorState.Scope.DisposeWithException(exception);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error creating Azure Durable Functions orchestration scope");
        }

        return returnValue;
    }

    private sealed class InitialOrchestrationScope
    {
        private readonly Scope? _scope;
        private int _isDisposed;

        public InitialOrchestrationScope(Scope? scope)
        {
            _scope = scope;
        }

        public bool TryDispose(Exception? exception)
        {
            if (_scope is null || Interlocked.Exchange(ref _isDisposed, 1) != 0)
            {
                return false;
            }

            _scope.DisposeWithException(exception);
            return true;
        }
    }
}

#endif
