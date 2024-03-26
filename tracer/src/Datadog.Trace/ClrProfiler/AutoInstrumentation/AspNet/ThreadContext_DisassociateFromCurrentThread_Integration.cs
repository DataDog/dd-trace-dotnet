// <copyright file="ThreadContext_DisassociateFromCurrentThread_Integration.cs" company="Datadog">
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
    /// System.Web.ThreadContext.DisassociateFromCurrentThread calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "System.Web",
        TypeName = "System.Web.ThreadContext",
        MethodName = "DisassociateFromCurrentThread",
        ReturnTypeName = ClrNames.Void,
        MinimumVersion = "4.0.0",
        MaximumVersion = "4.*.*",
        IntegrationName = IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class ThreadContext_DisassociateFromCurrentThread_Integration
    {
        private const string IntegrationName = nameof(IntegrationId.AspNet);

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance)
            where TTarget : IThreadContext
        {
            if (Tracer.Instance.ScopeManager is IScopeRawAccess rawAccess)
            {
                rawAccess.Active = null;
            }

            return CallTargetState.GetDefault();
        }
    }
}
#endif
