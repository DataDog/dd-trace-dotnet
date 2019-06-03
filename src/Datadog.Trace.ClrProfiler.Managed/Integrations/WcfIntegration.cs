#if !NETSTANDARD2_0

using System;
using System.ServiceModel.Channels;
using Datadog.Trace.ClrProfiler.ExtensionMethods;
using Datadog.Trace.ClrProfiler.Models;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    /// <summary>
    ///     WcfIntegration
    /// </summary>
    public static class WcfIntegration
    {
        private const string Major4 = "4";

        /// <summary>
        /// Instrumentation wrapper for System.ServiceModel.Dispatcher.ChannelHandler
        /// </summary>
        /// <param name="thisObj">The ChannelHandler instance.</param>
        /// <param name="requestContext">A System.ServiceModel.Channels.RequestContext implementation instance.</param>
        /// <param name="currentOperationContext">A System.ServiceModel.OperationContext instance.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <returns>The value returned by the instrumented method.</returns>
        [InterceptMethod(
            TargetAssembly = "System.ServiceModel",
            TargetType = "System.ServiceModel.Dispatcher.ChannelHandler",
            TargetMinimumVersion = Major4,
            TargetMaximumVersion = Major4)]
        public static bool HandleRequest(object thisObj, object requestContext, object currentOperationContext, int opCode)
        {
            var handleRequestDelegate = Emit.DynamicMethodBuilder<Func<object, object, object, bool>>.GetOrCreateMethodCallDelegate(thisObj.GetType(), "HandleRequest");

            if (!(requestContext is RequestContext castRequestContext))
            {
                return handleRequestDelegate(thisObj, requestContext, currentOperationContext);
            }

            using (var wcfDelegate = WcfRequestMessageSpanIntegrationDelegate.CreateAndBegin(castRequestContext))
            {
                try
                {
                    return handleRequestDelegate(thisObj, requestContext, currentOperationContext);
                }
                catch (Exception ex) when (wcfDelegate?.SetExceptionForFilter(ex) ?? false)
                {
                    // unreachable code
                    throw;
                }
            }
        }
    }
}

#endif
