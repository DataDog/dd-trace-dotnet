// <copyright file="FunctionInvocationMiddlewareInvokeIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK
using System;
using System.ComponentModel;
using Datadog.Trace.AppSec;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Logging;
using Datadog.Trace.PlatformHelpers;
using Microsoft.AspNetCore.Http;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.Functions
{
    /// <summary>
    /// Azure Function calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "Microsoft.Azure.WebJobs.Script.WebHost",
        TypeName = "Microsoft.Azure.WebJobs.Script.WebHost.Middleware.FunctionInvocationMiddleware",
        MethodName = "Invoke",
        ReturnTypeName = "System.Threading.Tasks.Task",
        ParameterTypeNames = new[] { "Microsoft.AspNetCore.Http.HttpContext" },
        MinimumVersion = "3.0.0",
        MaximumVersion = "4.*.*",
        IntegrationName = AzureFunctionsCommon.IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class FunctionInvocationMiddlewareInvokeIntegration
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(FunctionInvocationMiddlewareInvokeIntegration));
        private static readonly AspNetCoreHttpRequestHandler AspNetCoreRequestHandler = new(Log, AzureFunctionsCommon.OperationName, AzureFunctionsCommon.IntegrationId);

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="httpContext">First argument</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, HttpContext httpContext)
        {
            var tracer = Tracer.Instance;

            if (tracer.Settings.IsIntegrationEnabled(AzureFunctionsCommon.IntegrationId))
            {
                var scope = AspNetCoreRequestHandler.StartAspNetCorePipelineScope(tracer, Security.Instance, httpContext, resourceName: null);

                if (scope != null)
                {
                    return new CallTargetState(scope, state: httpContext);
                }
            }

            return CallTargetState.GetDefault();
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
            if (state.Scope is { } scope)
            {
                var tracer = Tracer.Instance;
                var security = Security.Instance;
                var httpContext = state.State as HttpContext;
                try
                {
                    if (exception != null)
                    {
                        AspNetCoreRequestHandler.HandleAspNetCoreException(tracer, security, scope.Span, httpContext, exception);
                    }
                }
                finally
                {
                    AspNetCoreRequestHandler.StopAspNetCorePipelineScope(tracer, security, scope, httpContext);
                }
            }

            return returnValue;
        }
    }
}
#endif
