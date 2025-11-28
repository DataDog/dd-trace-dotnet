// <copyright file="HttpModule_Integration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System.ComponentModel;
using System.Threading;
using System.Web;
using Datadog.Trace.AspNet;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNet
{
    /// <summary>
    /// System.Web.Compilation.BuildManager.InvokePreStartInitMethodsCore calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "System.Web",
        TypeName = "System.Web.Compilation.BuildManager",
        MethodName = "InvokePreStartInitMethodsCore",
        ReturnTypeName = ClrNames.Void,
        ParameterTypeNames = ["System.Collections.Generic.ICollection`1[System.Reflection.MethodInfo]", "System.Func`1[System.IDisposable]"],
        MinimumVersion = "4.0.0",
        MaximumVersion = "4.*.*",
        IntegrationName = nameof(IntegrationId.AspNet))]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class HttpModule_Integration
    {
        // WARNING: Do not add a static reference to `IDatadogLogger` or reference
        // anything related to Tracer.Instance etc. This method is called at application
        // start _before_ the Tracer is initialized; adding additional references could
        // cause recursion issues and deadlocks in some scenarios, e.g. where there are
        // multiple apps per app pool.

        /// <summary>
        /// Indicates whether we're initializing the HttpModule for the first time
        /// </summary>
        private static int _firstInitialization = 1;

        internal static CallTargetState OnMethodBegin<TTarget, TCollection, TFunc>(TTarget instance, TCollection methods, TFunc setHostingEnvironmentCultures)
        {
            if (Interlocked.Exchange(ref _firstInitialization, 0) != 1)
            {
                // The HttpModule was already registered
                return CallTargetState.GetDefault();
            }

            try
            {
                HttpApplication.RegisterModule(typeof(TracingHttpModule));
            }
            catch
            {
                // Unable to dynamically register module
                // We can't log here as it could cause recursion issues and deadlocks in some scenarios
                // where there are multiple apps per app pool
            }

            return CallTargetState.GetDefault();
        }
    }
}
#endif
