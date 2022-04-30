// <copyright file="ControllerActionInvoker_InvokeAction_Integration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Web;
using Datadog.Trace.AppSec;
using Datadog.Trace.AspNet;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNet
{
    /// <summary>
    /// System.Web.Mvc.Async.AsyncControllerActionInvoker.BeginInvokeAction calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = AssemblyName,
        TypeName = "System.Web.Mvc.ControllerActionInvoker",
        MethodName = "InvokeActionMethod",
        ReturnTypeName = ActionResultTypeName,
        ParameterTypeNames = new[] { ControllerContextTypeName, ActionDescriptorTypeName, DictionaryTypeName },
        MinimumVersion = MinimumVersion,
        MaximumVersion = MaximumVersion,
        IntegrationName = IntegrationName,
        InstrumentationCategory = InstrumentationCategory.AppSec)]
    // ReSharper disable once InconsistentNaming
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class ControllerActionInvoker_InvokeAction_Integration
    {
        private const string AssemblyName = "System.Web.Mvc";
        private const string ActionResultTypeName = "System.Web.Mvc.ActionResult";
        private const string ControllerContextTypeName = "System.Web.Mvc.ControllerContext";
        private const string ActionDescriptorTypeName = "System.Web.Mvc.ActionDescriptor";
        private const string DictionaryTypeName = "System.Collections.Generic.IDictionary`2[System.String,System.Object]";
        private const string MinimumVersion = "4";
        private const string MaximumVersion = "5";

        private const string IntegrationName = nameof(IntegrationId.AspNetMvc);

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<ControllerActionInvoker_InvokeAction_Integration>();

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TContext">Controller context</typeparam>
        /// <typeparam name="TActionDescriptor">Action descriptor</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="controllerContext">The control context instance</param>
        /// <param name="actionDescriptor">The action descriptor instance</param>
        /// <param name="parameters">The parameters of the mvc method</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget, TContext, TActionDescriptor>(TTarget instance, TContext controllerContext, TActionDescriptor actionDescriptor, IDictionary<string, object> parameters)
            where TContext : IControllerContext
        {
            try
            {
                var security = Security.Instance;
                var context = HttpContext.Current;
                if (context != null && security.Settings.Enabled)
                {
                    var bodyDic = new Dictionary<string, object>(parameters.Count);
                    var pathParamsDic = new Dictionary<string, object>(parameters.Count);
                    foreach (var item in parameters)
                    {
                        if (controllerContext.RouteData.Values.ContainsKey(item.Key))
                        {
                            pathParamsDic[item.Key] = item.Value;
                        }
                        else
                        {
                            bodyDic[item.Key] = item.Value;
                        }
                    }

                    var scope = SharedItems.TryPeekScope(context, AspNetMvcIntegration.HttpContextKey);
                    security.InstrumentationGateway.RaiseBodyAvailable(context, scope.Span, bodyDic);
                    security.InstrumentationGateway.RaisePathParamsAvailable(context, scope.Span, pathParamsDic);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error instrumenting method {MethodName}", "System.Web.Mvc.ControllerActionInvoker.InvokeActionMethod()");
            }

            return CallTargetState.GetDefault();
        }
    }
}
#endif
