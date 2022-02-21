// <copyright file="ApiControllerActionInvoker_InvokeActionAsync_Integration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Web;
using Datadog.Trace.AppSec;
using Datadog.Trace.AspNet;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Tagging;
using Datadog.Trace.Vendors.Serilog;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNet
{
    /// <summary>
    /// System.Web.Http.Controllers.ApiControllerActionInvoker calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = SystemWebHttpAssemblyName,
        TypeName = "System.Web.Http.Controllers.ApiControllerActionInvoker",
        MethodName = "InvokeActionAsync",
        ReturnTypeName = ClrNames.HttpResponseMessageTask,
        ParameterTypeNames = new[] { HttpActionContextTypeName, ClrNames.CancellationToken },
        MinimumVersion = Major5Minor1,
        MaximumVersion = Major5MinorX,
        IntegrationName = IntegrationName)]
    // ReSharper disable once InconsistentNaming
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class ApiControllerActionInvoker_InvokeActionAsync_Integration
    {
        private const string SystemWebHttpAssemblyName = "System.Web.Http";
        private const string HttpActionContextTypeName = "System.Web.Http.Controllers.HttpActionContext";
        private const string Major5Minor1 = "5.1";
        private const string Major5MinorX = "5";

        private const string IntegrationName = nameof(IntegrationId.AspNetWebApi2);

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="THttpActionContext">Type of the action context</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="actionContext">The context of the controller</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget, THttpActionContext>(TTarget instance, THttpActionContext actionContext, CancellationToken cancellationToken)
            where THttpActionContext : IHttpActionContext
        {
            try
            {
                Log.Information("System.Web.Http.Controllers.ApiControllerActionInvoker.InvokeActionAsync()");
                var context = HttpContext.Current;
                var security = Security.Instance;
                if (context != null && security.Settings.Enabled)
                {
                    var boxedActionContext = (IHttpActionContext)actionContext;
                    var scope = SharedItems.TryPeakScope(HttpContext.Current, AspNetWebApi2Integration.HttpContextKey);
                    security.InstrumentationGateway.RaiseBodyAvailable(context, scope.Span, boxedActionContext.ActionArguments);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error instrumenting method {MethodName}", "System.Web.Http.Controllers.ApiControllerActionInvoker.InvokeActionAsync()");
            }

            return CallTargetState.GetDefault();
        }
    }
}
#endif
