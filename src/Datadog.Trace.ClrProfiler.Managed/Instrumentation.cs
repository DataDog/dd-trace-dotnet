using System;
using System.Collections.Concurrent;
using System.Configuration;
using System.Text;
using Datadog.Trace.ClrProfiler.Integrations;

// [assembly: System.Security.SecurityCritical]
// [assembly: System.Security.AllowPartiallyTrustedCallers]
namespace Datadog.Trace.ClrProfiler
{
    /// <summary>
    /// Provides instrumentation probes that can be injected into profiled code.
    /// </summary>
    public static class Instrumentation
    {
        private static readonly ConcurrentDictionary<string, MetadataNames> MetadataLookup = new ConcurrentDictionary<string, MetadataNames>();

        /// <summary>
        /// Called after an instrumented method is entered.
        /// </summary>
        /// <param name="integrationTypeValue">A <see cref="IntegrationType"/> tht indicated which integration is instrumenting this method.</param>
        /// <param name="moduleId">The id of the module where the instrumented method is defined.</param>
        /// <param name="methodToken">The <c>mdMemberDef</c> token of the instrumented method.</param>
        /// <param name="args">An array with all the arguments that were passed into the instrumented method. If it is an instance method, the first arguments is <c>this</c>.</param>
        /// <returns>A <see cref="Scope"/> created to instrument the method.</returns>
        // [System.Security.SecuritySafeCritical]
        public static object OnMethodEntered(
            int integrationTypeValue,
            ulong moduleId,
            uint methodToken,
            object[] args)
        {
            if (!IsProfilingEnabled())
            {
                return null;
            }

            // TODO: check if this integration type is enabled
            var integrationType = (IntegrationType)integrationTypeValue;
            Integration integration = null;

            switch (integrationType)
            {
                case IntegrationType.Custom:
                    MetadataNames metadataNames = MetadataLookup.GetOrAdd(
                        $"{moduleId}:{methodToken}",
                        key => GetMetadataNames((IntPtr)moduleId, methodToken));

                    integration = new CustomIntegration(metadataNames);
                    break;
                case IntegrationType.AspNetMvc5:
                    integration = new AspNetMvc5Integration(args);
                    break;

                default:
                    // invalid integration type
                    // TODO: log this
                    break;
            }

            // the return value will be left on the stack for the duration
            // of the instrumented method and passed into OnMethodExit()
            return integration;
        }

        /// <summary>
        /// Called before an instrumented method that returns void exits.
        /// </summary>
        /// <param name="integration">The <see cref="Integration"/> that was created by <see cref="OnMethodEntered"/>.</param>
        // [System.Security.SecuritySafeCritical]
        public static void OnMethodExit(object integration)
        {
            (integration as Integration)?.Dispose();
        }

        /// <summary>
        /// Called before an instrumented method with a return value exits.
        /// </summary>
        /// <param name="integration">The <see cref="Integration"/> that was created by <see cref="OnMethodEntered"/>.</param>
        /// <param name="originalReturnValue">The value returned by the instrumented method.</param>
        /// <returns>Returns the value that was originally returned by the instrumented method.</returns>
        // [System.Security.SecuritySafeCritical]
        public static object OnMethodExit(object integration, object originalReturnValue)
        {
            OnMethodExit(integration);
            return originalReturnValue;
        }

        /// <summary>
        /// Determines whether tracing with Datadog's profiler is enabled.
        /// </summary>
        /// <returns><c>true</c> if profiling is enabled; <c>false</c> otherwise.</returns>
        public static bool IsProfilingEnabled()
        {
            string setting = ConfigurationManager.AppSettings["Datadog.Tracing:Enabled"];
            return !string.Equals(setting, bool.FalseString, StringComparison.InvariantCultureIgnoreCase);
        }

        /// <summary>
        /// Determines whether Datadog's profiler is currently attached.
        /// </summary>
        /// <returns><c>true</c> if the profiler is currentl attached; <c>false</c> otherwise.</returns>
        public static bool IsProfilerAttached()
        {
            try
            {
                return NativeMethods.IsProfilerAttached();
            }
            catch
            {
                return false;
            }
        }

        private static MetadataNames GetMetadataNames(IntPtr moduleId, uint methodToken)
        {
            var modulePathBuffer = new StringBuilder(512);
            var typeNameBuffer = new StringBuilder(256);
            var methodNameBuffer = new StringBuilder(256);

            NativeMethods.GetMetadataNames(
                moduleId,
                methodToken,
                modulePathBuffer,
                (ulong)modulePathBuffer.Capacity,
                typeNameBuffer,
                (ulong)typeNameBuffer.Capacity,
                methodNameBuffer,
                (ulong)methodNameBuffer.Capacity);

            // TODO: is this the assembly name now?
            string module = System.IO.Path.GetFileName(modulePathBuffer.ToString());
            string type = typeNameBuffer.ToString();
            string method = methodNameBuffer.ToString();
            return new MetadataNames(module, type, method);
        }
    }
}
