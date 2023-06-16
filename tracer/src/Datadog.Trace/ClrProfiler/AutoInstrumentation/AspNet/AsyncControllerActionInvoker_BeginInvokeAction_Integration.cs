// <copyright file="AsyncControllerActionInvoker_BeginInvokeAction_Integration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System;
using System.ComponentModel;
using System.Web;
using Datadog.Trace.AspNet;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;

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
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class AsyncControllerActionInvoker_BeginInvokeAction_Integration
    {
        private const string AssemblyName = "System.Web.Mvc";
        private const string ControllerContextTypeName = "System.Web.Mvc.ControllerContext";
        private const string MinimumVersion = "4";
        private const string MaximumVersion = "5";

        private const string IntegrationName = nameof(IntegrationId.AspNetMvc);

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<AsyncControllerActionInvoker_BeginInvokeAction_Integration>();

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
        internal static CallTargetState OnMethodBegin<TTarget, TContext>(TTarget instance, TContext controllerContext, string actionName, AsyncCallback callback, object state)
        {
            Scope scope = null;

            try
            {
                if (HttpContext.Current != null)
                {
                    var duckedControllerContext = controllerContext.DuckCast<ControllerContextStruct>();
                    scope = AspNetMvcIntegration.CreateScope(duckedControllerContext);
                    SharedItems.PushScope(HttpContext.Current, AspNetMvcIntegration.HttpContextKey, scope);
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
