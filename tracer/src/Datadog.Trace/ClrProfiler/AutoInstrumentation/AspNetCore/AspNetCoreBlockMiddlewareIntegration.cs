// <copyright file="AspNetCoreBlockMiddlewareIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK

using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.AppSec;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Builder.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore
{
    /// <summary>
    /// The ASP.NET Core middleware integration.
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "Microsoft.AspNetCore.Hosting",
        TypeName = "Microsoft.AspNetCore.Hosting.Builder.ApplicationBuilderFactory",
        MethodName = "CreateBuilder",
        ParameterTypeNames = new[] { "Microsoft.AspNetCore.Http.Features.IFeatureCollection" },
        ReturnTypeName = "Microsoft.AspNetCore.Builder.IApplicationBuilder",
        MinimumVersion = "2",
        MaximumVersion = "6",
        IntegrationName = nameof(IntegrationId.AspNetCore))]
    internal class AspNetCoreBlockMiddlewareIntegration
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(AspNetCoreBlockMiddlewareIntegration));

        /// <summary>
        /// test
        /// </summary>
        /// <param name="instance">instance</param>
        /// <param name="returnValue">returnValue</param>
        /// <param name="exception">exception</param>
        /// <param name="state">state</param>
        /// <typeparam name="TTarget">TTarget</typeparam>
        /// <typeparam name="TReturn">TReturn</typeparam>
        /// <returns>CallTargetReturn</returns>
        internal static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
        {
            Log.Warning("on method end");
            if (Security.Instance.Settings.Enabled)
            {
                var appb = (IApplicationBuilder)returnValue;
                appb.MapWhen(context => context.Items["block"] is true, HandleBranch);
            }

            return new CallTargetReturn<TReturn>(returnValue);
        }

        internal static void HandleBranch(Microsoft.AspNetCore.Builder.IApplicationBuilder app)
        {
            app.Run(context =>
            {
                var sec = Security.Instance;
                var settings = sec.Settings;
                var httpResponse = context.Response;
                httpResponse.Clear();

                foreach (var cookie in context.Request.Cookies)
                {
                    httpResponse.Cookies.Delete(cookie.Key);
                }

                // this should always be true for core, but it would seem foolish to ignore it, as potential source of future bugs
                if (sec.CanAccessHeaders())
                {
                    httpResponse.Headers.Clear();
                }

                httpResponse.StatusCode = 403;
                var syncIOFeature = context.Features.Get<IHttpBodyControlFeature>();
                if (syncIOFeature != null)
                {
                    // allow synchronous operations for net core >=3.1 otherwise invalidoperation exception
                    syncIOFeature.AllowSynchronousIO = true;
                }

                var template = settings.BlockedJsonTemplate;
                if (context.Request.Headers["Accept"].ToString().Contains("text/html"))
                {
                    httpResponse.ContentType = "text/html";
                    template = settings.BlockedHtmlTemplate;
                }
                else
                {
                    httpResponse.ContentType = "application/json";
                }

                return httpResponse.WriteAsync(template);
            });
        }
    }
}
#endif
