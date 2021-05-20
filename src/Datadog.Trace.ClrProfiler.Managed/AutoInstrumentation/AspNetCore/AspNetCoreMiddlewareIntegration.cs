#if !NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Datadog.Trace.AppSec;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.ClrProfiler.Emit;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;
using Datadog.Trace.Util;
using Microsoft.AspNetCore.Http;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore
{
    /// <summary>
    /// The ASP.NET Core middleware integration.
    /// </summary>
    [InstrumentMethod(
        AssemblyName = AssemblyName,
        TypeName = ApplicationBuilder,
        MethodName = "Build",
        ReturnTypeName = ClrNames.Void,
        MinimumVersion = MinimumVersion,
        MaximumVersion = MaximumVersion,
        IntegrationName = nameof(IntegrationIds.AspNetCore))]
    public static class AspNetCoreMiddlewareIntegration
    {
        private const string HttpContextKey = "__Datadog.Trace.ClrProfiler.Integrations.AspNetCoreMiddlewareIntegration";
        private const string MinimumVersion = "2";
        private const string MaximumVersion = "6";
        private const string AssemblyName = "Microsoft.AspNetCore.Http";

        private const string ApplicationBuilder = "Microsoft.AspNetCore.Builder.ApplicationBuilder";
        private const string RequestDelegate = "Microsoft.AspNetCore.Http.RequestDelegate";
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(AspNetCoreMiddlewareIntegration));

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <returns>Calltarget state value</returns>
        public static CallTargetState OnMethodBegin<TTarget>(TTarget instance)
        {
            try
            {
                if (instance.TryDuckCast<ApplicationBuilderDuck>(out var applicationBuilder))
                {
                    InsertMiddlewares(applicationBuilder.Components);
                    return new CallTargetState(null, applicationBuilder);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to create duck");
            }

            return default;
        }

        private static void InsertMiddlewares(List<Func<RequestDelegate, RequestDelegate>> components)
        {
            try
            {
                Func<HttpContext, Func<Task>, Task> middleware = async (context, next) =>
                {
                    if (context.Items.ContainsKey(SecurityConstants.KillKey) && context.Items[SecurityConstants.KillKey] is bool killKey && killKey)
                    {
                        await BlockRequest(context);
                    }
                    else
                    {
                        try
                        {
                            context.Items[SecurityConstants.InHttpPipeKey] = true;
                            await next.Invoke();
                        }
                        catch (BlockActionException)
                        {
                            await BlockRequest(context);
                        }
                    }
                };

                Func<RequestDelegate, RequestDelegate> middlewareWrapper = next =>
                {
                    return context =>
                    {
                        Func<Task> simpleNext = () => next(context);
                        return middleware(context, simpleNext);
                    };
                };

                components.Insert(0, middlewareWrapper);
                if (components.Count > 2)
                {
                    // insert 2nd to last, making a guess that the last one will be the user action
                    components.Insert(components.Count - 2, middlewareWrapper);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to alter pipe");
            }
        }

        private static async Task BlockRequest(HttpContext context)
        {
            context.Response.StatusCode = 403;
            context.Response.ContentType = "text/html";
            await context.Response.WriteAsync(SecurityConstants.AttackBlockedHtml);
        }

        /// <summary>
        /// Application builder proxy
        /// </summary>
        [DuckCopy]
        public struct ApplicationBuilderDuck
        {
            /// <summary>
            /// The components that will make up the application http pipe
            /// </summary>
            [Duck(Name = "_components", Kind = DuckKind.Field)]
            public List<Func<RequestDelegate, RequestDelegate>> Components;
        }
    }
}

#endif
