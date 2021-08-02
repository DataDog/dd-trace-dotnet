// <copyright file="AsyncControllerActionInvoker_BeginInvokeAction_Integration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System;
using System.Web;
using Datadog.Trace.AppSec;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.ClrProfiler.Integrations;
using Datadog.Trace.ClrProfiler.Integrations.AspNet;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Serilog;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNet
{
    /// <summary>
    /// System.Web.Mvc.Async.AsyncControllerActionInvoker.BeginInvokeAction calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = AssemblyName,
        TypeName = "System.Web.Mvc.Async.AsyncControllerActionInvoker",
        MethodName = "BeginInvokeAction",
        ReturnTypeName = ClrNames.IAsyncResult,
        ParameterTypeNames = new[] { ControllerContextTypeName, ClrNames.String, ClrNames.AsyncCallback, ClrNames.Object },
        MinimumVersion = MinimumVersion,
        MaximumVersion = MaximumVersion,
        IntegrationName = IntegrationName)]
    // ReSharper disable once InconsistentNaming
    public class AsyncControllerActionInvoker_BeginInvokeAction_Integration
    {
        private const string AssemblyName = "System.Web.Mvc";
        private const string ControllerContextTypeName = "System.Web.Mvc.ControllerContext";
        private const string MinimumVersion = "4";
        private const string MaximumVersion = "5";

        private const string IntegrationName = nameof(IntegrationIds.AspNetMvc);

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TContext">Controller context</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="controllerContext">The context of the controller</param>
        /// <param name="actionName">Name of the action</param>
        /// <param name="callback">Async callback</param>
        /// <param name="state">The state of the method</param>
        /// <returns>Calltarget state value</returns>
        public static CallTargetState OnMethodBegin<TTarget, TContext>(TTarget instance, TContext controllerContext, string actionName, AsyncCallback callback, object state)
        {
            Scope scope = null;

            try
            {
                if (HttpContext.Current != null)
                {
                    var duckedControllerContext = controllerContext.DuckCast<ControllerContextStruct>();
                    scope = AspNetMvcIntegration.CreateScope(duckedControllerContext);
                    HttpContext.Current.Items[AspNetMvcIntegration.HttpContextKey] = scope;

                    var security = Security.Instance;
                    if (security.Enabled)
                    {
                        AspNetMvcIntegration.RaiseIntrumentationEvent(security, HttpContext.Current, duckedControllerContext.RouteData);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error instrumenting method {MethodName}", "System.Web.Mvc.Async.AsyncControllerActionInvoker.BeginInvokeAction()");
            }

            if (scope == null)
            {
                return CallTargetState.GetDefault();
            }

            return new CallTargetState(scope);
        }
    }
}
#endif
