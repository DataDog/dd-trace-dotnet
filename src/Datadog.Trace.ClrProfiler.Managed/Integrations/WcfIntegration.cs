#if !NETSTANDARD2_0
using System;
using System.ServiceModel.Channels;
using Datadog.Trace.ClrProfiler.Emit;
using Datadog.Trace.ClrProfiler.ExtensionMethods;
using Datadog.Trace.ClrProfiler.Models;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    /// <summary>
    /// WcfIntegration
    /// </summary>
    public static class WcfIntegration
    {
        private const string IntegrationName = "Wcf";
        private const string Major4 = "4";

        private const string ChannelHandlerTypeName = "System.ServiceModel.Dispatcher.ChannelHandler";

        private static readonly ILog Log = LogProvider.GetLogger(typeof(WcfIntegration));

        /// <summary>
        /// Instrumentation wrapper for System.ServiceModel.Dispatcher.ChannelHandler
        /// </summary>
        /// <param name="channelHandler">The ChannelHandler instance.</param>
        /// <param name="requestContext">A System.ServiceModel.Channels.RequestContext implementation instance.</param>
        /// <param name="currentOperationContext">A System.ServiceModel.OperationContext instance.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>The value returned by the instrumented method.</returns>
        [InterceptMethod(
            TargetAssembly = "System.ServiceModel",
            TargetType = ChannelHandlerTypeName,
            TargetSignatureTypes = new[] { ClrNames.Bool, "System.ServiceModel.Channels.RequestContext", "System.ServiceModel.OperationContext" },
            TargetMinimumVersion = Major4,
            TargetMaximumVersion = Major4)]
        public static bool HandleRequest(
            object channelHandler,
            object requestContext,
            object currentOperationContext,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            Func<object, object, object, bool> instrumentedMethod;
            var declaringType = channelHandler.GetInstrumentedType(ChannelHandlerTypeName);

            try
            {
                instrumentedMethod = MethodBuilder<Func<object, object, object, bool>>
                                    .Start(moduleVersionPtr, mdToken, opCode, nameof(HandleRequest))
                                    .WithConcreteType(declaringType)
                                    .WithParameters(requestContext, currentOperationContext)
                                    .WithNamespaceAndNameFilters(
                                         ClrNames.Bool,
                                         "System.ServiceModel.Channels.RequestContext",
                                         "System.ServiceModel.OperationContext")
                                    .Build();
            }
            catch (Exception ex)
            {
                Log.ErrorException($"Error resolving {ChannelHandlerTypeName}.{nameof(HandleRequest)}(...)", ex);
                throw;
            }

            if (!Tracer.Instance.Settings.IsIntegrationEnabled(IntegrationName) ||
                !(requestContext is RequestContext castRequestContext))
            {
                return instrumentedMethod(channelHandler, requestContext, currentOperationContext);
            }

            using (var wcfDelegate = WcfRequestMessageSpanIntegrationDelegate.CreateAndBegin(castRequestContext))
            {
                try
                {
                    return instrumentedMethod(channelHandler, requestContext, currentOperationContext);
                }
                catch (Exception ex)
                {
                    wcfDelegate?.SetExceptionForFilter(ex);
                    throw;
                }
            }
        }
    }
}

#endif
