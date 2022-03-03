// <copyright file="ReflectedHttpActionDescriptor_ExecuteAsync_Integration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Web;
using Datadog.Trace.AppSec;
using Datadog.Trace.AspNet;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNet
{
    /// <summary>
    /// System.Web.Http.Controllers.ReflectedHttpActionDescriptor calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = SystemWebHttpAssemblyName,
        TypeName = "System.Web.Http.Controllers.ReflectedHttpActionDescriptor",
        MethodName = "ExecuteAsync",
        ReturnTypeName = ClrNames.HttpResponseMessageTask,
        ParameterTypeNames = new[] { HttpControllerContextTypeName, DictionaryTypeName, ClrNames.CancellationToken },
        MinimumVersion = Major5Minor1,
        MaximumVersion = Major5MinorX,
        IntegrationName = IntegrationName)]
    // ReSharper disable once InconsistentNaming
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class ReflectedHttpActionDescriptor_ExecuteAsync_Integration
    {
        private const string SystemWebHttpAssemblyName = "System.Web.Http";
        private const string HttpControllerContextTypeName = "System.Web.Http.Controllers.HttpControllerContext";
        private const string DictionaryTypeName = "System.Collections.Generic.IDictionary`2[System.String,System.Object]";
        private const string Major5Minor1 = "5.1";
        private const string Major5MinorX = "5";

        private const string IntegrationName = nameof(IntegrationId.AspNetWebApi2);

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<ReflectedHttpActionDescriptor_ExecuteAsync_Integration>();

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TController">Type of the controller context</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="controller">The context of the controller</param>
        /// <param name="parameters">The parameters of the mvc method</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget, TController>(TTarget instance, TController controller, IDictionary<string, object> parameters, CancellationToken cancellationToken)
        {
            try
            {
                Log.Debug("Starting {MethodName}", "System.Web.Http.Controllers.ReflectedHttpActionDescriptor.ExecuteAsync()");

                var security = Security.Instance;
                var context = HttpContext.Current;
                if (context != null && security.Settings.Enabled)
                {
                    var scope = SharedItems.TryPeakScope(context, AspNetWebApi2Integration.HttpContextKey);
                    security.InstrumentationGateway.RaiseBodyAvailable(context, scope.Span, parameters);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error instrumenting method {MethodName}", "System.Web.Http.Controllers.ReflectedHttpActionDescriptor.ExecuteAsync()");
            }

            return CallTargetState.GetDefault();
        }
    }
}
#endif
