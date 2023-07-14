// <copyright file="FaultTolerantInvoker.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using System.Diagnostics;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Debugger.Instrumentation
{
    /// <summary>
    /// MethodDebuggerInvoker
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

            var shouldHeal = topFrame.GetILOffset() == -1 && topFrame.GetMethod()?.Name.Equals(instrumentedMethodName) == true;

            if (shouldHeal)
            {
                Log.Information("Self-healing: Module: {AssemblyName} FQN: {Name}, Original method name: {InstrumentedMethodName}, Exception Type = {ExceptionType}, Exception message: {ExceptionMessage}, Exception Stack Trace = {ExceptionStackTrace}", new object[] { (topFrame.GetMethod()?.Module?.FullyQualifiedName ?? "<FailedToGrabModule>"), ((topFrame.GetMethod().DeclaringType?.FullName) ?? "<FailedToGrabTypeName>") + "." + topFrame.GetMethod().Name, instrumentedMethodName, ex.GetType().FullName, ex.Message, ex.StackTrace });
            }

            return shouldHeal;
        }
    }
}
