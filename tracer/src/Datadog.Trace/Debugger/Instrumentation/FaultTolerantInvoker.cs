// <copyright file="FaultTolerantInvoker.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using System.Diagnostics;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Debugger.Instrumentation
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
        /// <param name="instrumentedMethodName">The name of the thrower</param>
        /// <returns>`true` if the exception was thrown due to faulty instrumentation, `false` otherwise</returns>
        public static bool ShouldHeal(Exception ex, string instrumentedMethodName)
        {
            try
            {
                return ShouldHealInternal(ex, instrumentedMethodName);
            }
            catch (Exception e)
            {
                Log.Warning(e, "Failed to determine if we should-self-heal. ex = {ExceptionToString}, InstrumentedMethodName = {InstrumentedMethodName}", new object[] { ex.ToString(), instrumentedMethodName });
                return false;
            }
        }

        private static bool ShouldHealInternal(Exception ex, string instrumentedMethodName)
        {
            if (ex == null)
            {
                return false;
            }

            var stackTrace = new StackTrace(ex);
            var topFrame = stackTrace.GetFrame(0);
            if (topFrame == null)
            {
                return false;
            }

            // Check that the exception originated from the instrumented method
            var isFromInstrumentedMethod = topFrame.GetMethod()?.Name.Equals(instrumentedMethodName) == true;
            if (!isFromInstrumentedMethod)
            {
                return false;
            }

            var isILOffsetMinusOne = topFrame.GetILOffset() == -1;
            var hasNoInnerException = ex.InnerException == null;
            var hasExpectedFrameCount = stackTrace.FrameCount == 2; // Instrumented duplicate and the original method.
            var shouldHeal = isILOffsetMinusOne && hasNoInnerException && hasExpectedFrameCount;

            if (shouldHeal)
            {
                try
                {
                    Log.Information("Self-healing: Module: {AssemblyName} FQN: {Name}, Original method name: {InstrumentedMethodName}, Exception Type = {ExceptionType}, Exception message: {ExceptionMessage}, Exception Stack Trace = {ExceptionStackTrace}, Exception.ToString(): {ExceptionToString}", new object[] { (topFrame.GetMethod()?.Module?.FullyQualifiedName ?? "<FailedToGrabModule>"), ((topFrame.GetMethod()?.DeclaringType?.FullName) ?? "<FailedToGrabTypeName>") + "." + (topFrame.GetMethod()?.Name ?? "<FailedToGrabMethodName>"), instrumentedMethodName, ex.GetType().FullName, ex.Message, ex.StackTrace, ex.ToString() });
                }
                catch (Exception e)
                {
                    Log.Warning(e, "Self-healing. Could not log the corresponding information.");
                }
            }

            return shouldHeal;
        }
    }
}
