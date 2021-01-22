#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Datadog.Trace.ClrProfiler.Emit;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    /// <summary>
    /// WcfIntegration
    /// </summary>
    public static class WcfIntegration
    {
        private const string Major4 = "4";

        private const string ChannelHandlerTypeName = "System.ServiceModel.Dispatcher.ChannelHandler";
        private const string HttpRequestMessagePropertyTypeName = "System.ServiceModel.Channels.HttpRequestMessageProperty";

        private static readonly IntegrationInfo IntegrationId = IntegrationRegistry.GetIntegrationInfo(nameof(IntegrationIds.Wcf));
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(WcfIntegration));

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
            if (channelHandler == null)
            {
                throw new ArgumentNullException(nameof(channelHandler));
            }

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
                Log.ErrorRetrievingMethod(
                    exception: ex,
                    moduleVersionPointer: moduleVersionPtr,
                    mdToken: mdToken,
                    opCode: opCode,
                    instrumentedType: ChannelHandlerTypeName,
                    methodName: nameof(HandleRequest),
                    instanceType: channelHandler.GetType().AssemblyQualifiedName);
                throw;
            }

            using (var scope = CreateScope(requestContext))
            {
                try
                {
                    return instrumentedMethod(channelHandler, requestContext, currentOperationContext);
                }
                catch (Exception ex)
                {
                    scope.Span.SetException(ex);
                    throw;
                }
            }
        }

        private static Scope CreateScope(object requestContext)
        {
            var requestMessage = requestContext.GetProperty<object>("RequestMessage").GetValueOrDefault();

            if (requestMessage == null)
            {
                return null;
            }

            var tracer = Tracer.Instance;

            if (!tracer.Settings.IsIntegrationEnabled(IntegrationId))
            {
                // integration disabled, don't create a scope, skip this trace
                return null;
            }

            Scope scope = null;

            try
            {
                SpanContext propagatedContext = null;
                var tagsFromHeaders = Enumerable.Empty<KeyValuePair<string, string>>();
                string host = null;
                string httpMethod = null;

                IDictionary<string, object> requestProperties = requestMessage.GetProperty<IDictionary<string, object>>("Properties").GetValueOrDefault();
                if (requestProperties.TryGetValue("httpRequest", out object httpRequestProperty) &&
                    httpRequestProperty.GetType().FullName.Equals(HttpRequestMessagePropertyTypeName, StringComparison.OrdinalIgnoreCase))
                {
                    var webHeaderCollection = httpRequestProperty.GetProperty<WebHeaderCollection>("Headers").GetValueOrDefault();

                    // we're using an http transport
                    host = webHeaderCollection[HttpRequestHeader.Host];
                    httpMethod = httpRequestProperty.GetProperty<string>("Method").GetValueOrDefault()?.ToUpperInvariant();

                    // try to extract propagated context values from http headers
                    if (tracer.ActiveScope == null)
                    {
                        try
                        {
                            var headers = webHeaderCollection.Wrap();
                            propagatedContext = SpanContextPropagator.Instance.Extract(headers);
                            tagsFromHeaders = SpanContextPropagator.Instance.ExtractHeaderTags(headers, tracer.Settings.HeaderTags);
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "Error extracting propagated HTTP headers.");
                        }
                    }
                }

                var tags = new WebTags();
                scope = tracer.StartActiveWithTags("wcf.request", propagatedContext, tags: tags);
                var span = scope.Span;

                object requestHeaders = requestMessage.GetProperty<object>("Headers").GetValueOrDefault();
                string action = requestHeaders.GetProperty<string>("Action").GetValueOrDefault();
                Uri requestHeadersTo = requestHeaders.GetProperty<Uri>("To").GetValueOrDefault();

                span.DecorateWebServerSpan(
                    resourceName: action ?? requestHeadersTo?.LocalPath,
                    httpMethod,
                    host,
                    httpUrl: requestHeadersTo?.AbsoluteUri,
                    tags,
                    tagsFromHeaders);

                tags.SetAnalyticsSampleRate(IntegrationId, tracer.Settings, enabledWithGlobalSetting: true);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or populating scope.");
            }

            // always returns the scope, even if it's null
            return scope;
        }
    }
}

#endif
