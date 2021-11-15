// <copyright file="ExceptionHandlerExtensions_HandleAsync_Integration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using System.Threading;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNet
{
    /// <summary>
    /// System.Web.Http.ExceptionHandling.ExceptionHandlerExtensions calltarget instrumentation
    /// This instrumentation is based off the ASP.NET Web API 2 error handling design that is documented here:
    /// https://docs.microsoft.com/en-us/aspnet/web-api/overview/error-handling/web-api-global-error-handling
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "System.Web.Http",
        TypeName = "System.Web.Http.ExceptionHandling.ExceptionHandlerExtensions",
        MethodName = "HandleAsync",
        ReturnTypeName = ClrNames.HttpResponseMessageTask,
        ParameterTypeNames = new[] { "System.Web.Http.ExceptionHandling.IExceptionHandler", "System.Web.Http.ExceptionHandling.ExceptionContext", ClrNames.CancellationToken },
        MinimumVersion = Major5Minor1,
        MaximumVersion = Major5MinorX,
        IntegrationName = IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class ExceptionHandlerExtensions_HandleAsync_Integration
    {
        private const string Major5Minor1 = "5.1";
        private const string Major5MinorX = "5";
        private const string IntegrationName = nameof(IntegrationId.AspNetWebApi2);

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TExceptionHandler">Type of the configuration callback action</typeparam>
        /// <typeparam name="TExceptionContext">Type of the exception context</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method. This will be null because the method is static</param>
        /// <param name="handler">The configured exception handler value</param>
        /// <param name="context">The exception context value</param>
        /// <param name="cancellationToken">CancellationToken value</param>
        /// <returns>Calltarget state value</returns>
        public static CallTargetState OnMethodBegin<TTarget, TExceptionHandler, TExceptionContext>(TTarget instance, TExceptionHandler handler, TExceptionContext context, CancellationToken cancellationToken)
            where TExceptionContext : IExceptionContext
        {
            var scope = Tracer.Instance.InternalActiveScope;
            var exception = context.Exception;

            if (scope is not null && exception is not null)
            {
                // Only try setting an exception if there's an active span
                // The rest of the instrumentation will handle disposing the scope
                scope.Span.SetException(exception);
            }

            return CallTargetState.GetDefault();
        }
    }
}
