// <copyright file="FaultTolerantInvoker.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using System.Diagnostics;
using Datadog.Trace.Logging;

namespace Datadog.Trace.FaultTolerant
{
    /// <summary>
    /// FaultTolerantInvoker
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class FaultTolerantInvoker
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(FaultTolerantInvoker));

        /// <summary>
        /// Determines if the Fault-Tolerant's kickoff logic should call the Original version of the faulty method.
        /// </summary>
        /// <param name="ex">The excepion that was thrown</param>
        /// <param name="moduleId">Module ID used for revert.</param>
        /// <param name="methodToken">Metadata Token of the method for revert</param>
        /// <param name="instrumentationId">An identifier that uniquely describes the applied instrumentation</param>
        /// <param name="products">The product(s) applied the instrumentation</param>
        /// <returns>`true` if the exception was thrown due to faulty instrumentation, `false` otherwise</returns>
        public static bool ShouldHeal(Exception ex, IntPtr moduleId, int methodToken, string instrumentationId, int products)
        {
            try
            {
                return FaultTolerantNativeMethods.ShouldHeal(moduleId, methodToken, instrumentationId, products);
            }
            catch (Exception e)
            {
                Log.Warning(e, "Failed to determine if we should-self-heal. ex = {ExceptionToString}, InstrumentedMethodName = {InstrumentationVersion}, Products = {Products}", new object[] { ex.ToString(), instrumentationId, products.ToString() });
                return false;
            }
        }

        /// <summary>
        /// Called upon successful instrumentation. The corresponding Fault-Tolerant kickoff does not need to self-heal.
        /// </summary>
        /// <param name="moduleId">Module ID used for revert + rejit.</param>
        /// <param name="methodToken">Metadata Token of the method for revert + rejit</param>
        /// <param name="instrumentationId">An identifier that uniquely describes the applied instrumentation</param>
        /// <param name="products">The product(s) applied the instrumentation</param>
        public static void ReportSuccessfulInstrumentation(IntPtr moduleId, int methodToken, string instrumentationId, int products)
        {
            Log.Information("Succeeded to instrument using {Products} the method: {MethodToken}, Instrumentation Version: {InstrumentationVersion}", new object[] { ((InstrumentingProducts)products).ToString(), methodToken, instrumentationId });

            try
            {
                FaultTolerantNativeMethods.ReportSuccessfulInstrumentation(moduleId, methodToken, instrumentationId, products);
            }
            catch (Exception e)
            {
                Log.Warning(e, "Failed to mark the instrumentation as successful. InstrumentedMethodName = {InstrumentationVersion}, Products = {Products}", new object[] { instrumentationId, ((InstrumentingProducts)products).ToString() });
            }
        }
    }
}
