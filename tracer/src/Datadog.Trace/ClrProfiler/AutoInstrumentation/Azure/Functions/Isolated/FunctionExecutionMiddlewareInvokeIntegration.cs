// <copyright file="FunctionExecutionMiddlewareInvokeIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK
using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.Functions;

/// <summary>
/// Azure Function calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Microsoft.Azure.Functions.Worker.Core",
    TypeName = "Microsoft.Azure.Functions.Worker.Pipeline.FunctionExecutionMiddleware",
    MethodName = "Invoke",
    ReturnTypeName = ClrNames.Task,
    ParameterTypeNames = new[] { "Microsoft.Azure.Functions.Worker.FunctionContext" },
    MinimumVersion = "1.4.0",
    MaximumVersion = "2.*.*",
    IntegrationName = AzureFunctionsCommon.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class FunctionExecutionMiddlewareInvokeIntegration
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(FunctionExecutionMiddlewareInvokeIntegration));

    internal static CallTargetState OnMethodBegin<TTarget, TFunctionContext>(TTarget instance, TFunctionContext functionContext)
        where TFunctionContext : IFunctionContext
    {
        if (HasOrchestrationTrigger(functionContext))
        {
            return new CallTargetState(scope: null, state: null, skipMethodBody: false, skipContinuation: true);
        }

        return AzureFunctionsCommon.OnIsolatedFunctionBegin(functionContext);
    }

    /// <summary>
    /// OnAsyncMethodEnd callback
    /// </summary>
    /// <typeparam name="TTarget">Type of the target</typeparam>
    /// <typeparam name="TReturn">Type of the return value, in an async scenario will be T of Task of T</typeparam>
    /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
    /// <param name="returnValue">Return value instance</param>
    /// <param name="exception">Exception instance in case the original code threw an exception.</param>
    /// <param name="state">Calltarget state value</param>
    /// <returns>A response value, in an async scenario will be T of Task of T</returns>
    internal static TReturn OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
    {
        state.Scope?.DisposeWithException(exception);
        return returnValue;
    }

    private static bool HasOrchestrationTrigger<TFunctionContext>(TFunctionContext functionContext)
        where TFunctionContext : IFunctionContext
    {
        try
        {
            var definition = functionContext.FunctionDefinition;
            if (definition.InputBindings == null)
            {
                return false;
            }

            foreach (System.Collections.DictionaryEntry binding in definition.InputBindings)
            {
                if (binding.Value == null)
                {
                    continue;
                }

                var bindingType = binding.Value.GetType();
                var typeProperty = bindingType.GetProperty("Type");
                if (typeProperty == null)
                {
                    continue;
                }

                var typeValue = typeProperty.GetValue(binding.Value);
                if (typeValue != null && typeValue.ToString().Equals("orchestrationTrigger"))
                {
                    return true;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Could not verify orchestration trigger");
            return false;
        }
    }
}

#endif
