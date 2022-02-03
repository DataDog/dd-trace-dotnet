// <copyright file="HostingApplication_DisposeContext_Integration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System;
using System.ComponentModel;
using Datadog.Trace.AppSec;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore.Proxies;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Headers;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore
{
    /// <summary>
    /// System.Web.Http.ExceptionHandling.ExceptionHandlerExtensions calltarget instrumentation
    /// This instrumentation is based off the ASP.NET Web API 2 error handling design that is documented here:
    /// https://docs.microsoft.com/en-us/aspnet/web-api/overview/error-handling/web-api-global-error-handling
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "Microsoft.AspNetCore.Hosting",
        TypeName = "Microsoft.AspNetCore.Hosting.Internal.HostingApplication",
        MethodName = "DisposeContext",
        ReturnTypeName = ClrNames.Void,
        ParameterTypeNames = new[] { "Context", "System.Exception" },
        MinimumVersion = Major2Minor1,
        MaximumVersion = Major2Minor1,
        IntegrationName = IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class HostingApplication_DisposeContext_Integration
    {
        private const string Major2Minor1 = "2.1";
        private const string IntegrationName = nameof(IntegrationId.AspNetCore);
        private const IntegrationId IntegrationId = Configuration.IntegrationId.AspNetCore;

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TContext">Type of the request context</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="context">The request context</param>
        /// <param name="exception">The exception that occurred from processing the request</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget, TContext>(TTarget instance, TContext context, Exception exception)
            where TContext : IContext
        {
            var tracer = Tracer.Instance;
            IHttpContext httpContext = context.HttpContext;

            if (tracer.Settings.IsIntegrationEnabled(IntegrationId) && httpContext.Items[AspNetCoreOnFrameworkHelpers.HttpContextAspNetCoreScopeKey] is Scope scope)
            {
                var span = scope.Span;

                // we may need to update the resource name if none of the routing/mvc events updated it
                // if we had an unhandled exception, the status code is already updated
                if (string.IsNullOrEmpty(span.ResourceName) || !span.Error)
                {
                    if (string.IsNullOrEmpty(span.ResourceName))
                    {
                        span.ResourceName = AspNetCoreOnFrameworkHelpers.GetDefaultResourceName(httpContext.Request);
                    }

                    if (!span.Error)
                    {
                        span.SetHttpStatusCode(httpContext.Response.StatusCode, isServer: true, tracer.Settings);

                        // For the sake of snapshot compatibility, let's not set response header tags if there's an exception
                        // TODO: See if .NET Core instrumentation can get response headers tags in this scenario too
                        if (exception is null)
                        {
                            span.SetHeaderTags(new IHeaderDictionaryHeadersCollection(httpContext.Response.Headers), tracer.Settings.HeaderTags, defaultTagPrefix: SpanContextPropagator.HttpResponseHeadersTagPrefix);
                        }
                    }
                }

                scope.DisposeWithException(exception);
            }

            return CallTargetState.GetDefault();
        }
    }
}
#endif
