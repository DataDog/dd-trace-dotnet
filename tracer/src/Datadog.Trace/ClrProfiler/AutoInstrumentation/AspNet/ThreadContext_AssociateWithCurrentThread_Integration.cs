// <copyright file="ThreadContext_AssociateWithCurrentThread_Integration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

#if NETFRAMEWORK
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNet
{
    /// <summary>
    /// System.Web.ThreadContext.AssociateWithCurrentThread calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "System.Web",
        TypeName = "System.Web.ThreadContext",
        MethodName = "AssociateWithCurrentThread",
        ReturnTypeName = ClrNames.Void,
        ParameterTypeNames = new[] { ClrNames.Bool },
        MinimumVersion = "4.0.0",
        MaximumVersion = "4.*.*",
        IntegrationName = IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class ThreadContext_AssociateWithCurrentThread_Integration
    {
        private const string IntegrationName = nameof(IntegrationId.AspNet);
        private const string HttpContextScopeKey = "__Datadog.Trace.AspNet.TracingHttpModule-aspnet.request";

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="setImpersonationContext">A flag to set the impersonation context</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, bool setImpersonationContext)
            where TTarget : IThreadContext
        {
            // AssociateWithCurrentThread can only be used when HttpContext is non-null
            var httpContext = instance.HttpContext;
            if (httpContext.Items is not null
                && httpContext.Items[HttpContextScopeKey] is Scope scope
                && Tracer.Instance.ScopeManager is IScopeRawAccess rawAccess)
            {
                rawAccess.Active = scope;
            }

            return CallTargetState.GetDefault();
        }
    }
}
#endif
