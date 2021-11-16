// <copyright file="ChannelHandlerIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.ClrProfiler.Emit;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Wcf
{
    /// <summary>
    /// System.ServiceModel.Dispatcher.ChannelHandler calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "System.ServiceModel",
        TypeName = "System.ServiceModel.Dispatcher.ChannelHandler",
        MethodName = "HandleRequest",
        ReturnTypeName = ClrNames.Bool,
        ParameterTypeNames = new[] { "System.ServiceModel.Channels.RequestContext", "System.ServiceModel.OperationContext" },
        MinimumVersion = "4.0.0",
        MaximumVersion = "4.*.*",
        IntegrationName = IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class ChannelHandlerIntegration
    {
        private const string IntegrationName = nameof(IntegrationIds.Wcf);
        private const string ChannelHandlerTypeName = "System.ServiceModel.Dispatcher.ChannelHandler";
        private const string HttpRequestMessagePropertyTypeName = "System.ServiceModel.Channels.HttpRequestMessageProperty";

        private const IntegrationIds IntegrationId = IntegrationIds.Wcf;
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ChannelHandlerIntegration));

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TRequestContext">Type of the request context</typeparam>
        /// <typeparam name="TOperationContext">Type of the operation context</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="request">RequestContext instance</param>
        /// <param name="currentOperationContext">OperationContext instance</param>
        /// <returns>Calltarget state value</returns>
        public static CallTargetState OnMethodBegin<TTarget, TRequestContext, TOperationContext>(TTarget instance, TRequestContext request, TOperationContext currentOperationContext)
        {
            return new CallTargetState(CreateScope(request));
        }

        /// <summary>
        /// OnMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TReturn">Type of the response</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="returnValue">Return value</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>A response value, in an async scenario will be T of Task of T</returns>
        public static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, CallTargetState state)
        {
            state.Scope.DisposeWithException(exception);
            return new CallTargetReturn<TReturn>(returnValue);
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
                            tagsFromHeaders = SpanContextPropagator.Instance.ExtractHeaderTags(headers, tracer.Settings.HeaderTags, SpanContextPropagator.HttpRequestHeadersTagPrefix);
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
