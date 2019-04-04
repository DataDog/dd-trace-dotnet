#if !NETSTANDARD2_0

using System;
using System.Runtime.Remoting.Messaging;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Xml;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    /// <summary>
    ///     WcfIntegration
    /// </summary>
    public static class WcfIntegration
    {
        private static readonly ILog Log = LogProvider.GetLogger(typeof(WcfIntegration));

        /// <summary>
        ///     Instrumentation wrapper for System.ServiceModel.Dispatcher.ChannelHandler
        /// </summary>
        /// <param name="thisObj">The ChannelHandler instance.</param>
        /// <param name="requestContext">A System.ServiceModel.Channels.RequestContext implementation instance.</param>
        /// <param name="currentOperationContext">A System.ServiceModel.OperationContext instance.</param>
        /// <returns>The value returned by the instrumented method.</returns>
        [InterceptMethod(
            TargetAssembly = "System.ServiceModel",
            TargetType = "System.ServiceModel.Dispatcher.ChannelHandler")]
        public static bool HandleRequest(object thisObj, object requestContext, object currentOperationContext)
        {
            using (var scope = CreateScope(requestContext as RequestContext, currentOperationContext as OperationContext))
            {
                try
                {
                    var handleRequestDelegate = DynamicMethodBuilder<Func<object, object, object, bool>>.GetOrCreateMethodCallDelegate(thisObj.GetType(), "HandleRequest");

                    return handleRequestDelegate(thisObj, requestContext, currentOperationContext);
                }
                catch (Exception ex) when (scope?.Span.SetExceptionForFilter(ex) ?? false)
                {
                    // unreachable code
                    throw;
                }
            }
        }

        private static Scope CreateScope(RequestContext requestContext, OperationContext operationContext)
        {
            if (requestContext == null)
            {
                return null;
            }

            if (requestContext.RequestMessage.Properties.TryGetValue("httpRequest", out var httpRequestProperty) &&
                httpRequestProperty is HttpRequestMessageProperty httpRequestMessageProperty)
            {
                var httpMethod = httpRequestMessageProperty.Method;
                var x = httpRequestMessageProperty.Headers;
            }

            foreach (var messageHeader in requestContext.RequestMessage.Headers)
            {
                switch (messageHeader.Name)
                {
                        case "To":
                            // var toUrl = messageHeader as XmlDictionaryString;
                            return null;
                        case "Action":
                            return null;
                        default:
                            return null;
                }
            }

            return null;

            /*
            SpanContext propagatedContext = null;

            if (Tracer.Instance.ActiveScope == null)
            {
                try
                {
                    // extract propagated http headers
                    var headers = requestContext.RequestMessage.Headers..RequestMessage.Headers.Headers.Wrap();
                    propagatedContext = SpanContextPropagator.Instance.Extract(headers);
                }
                catch (Exception ex)
                {
                    Log.ErrorException("Error extracting propagated HTTP headers.", ex);
                }
            }

            var tracer = Tracer.Instance.StartActive("wcf.request");

            // tracer.Span.ResourceName = httpMethod path
            tracer.Span.Type = SpanTypes.Web;

            // tracer.Span.SetTag(Tags.HttpMethod, )
            // tracer.Span.SetTag(Tags.HttpRequestHeadersHost, ) // _httpContext.Request?.Headers?.Get("Host")
            // tracer.Span.SetTag(Tags.HttpUrl, )   _httpContext.Request.Url?.AbsoluteUri ?? _httpContext.Request.RawUrl;

            // tracer.Span.SetTag(Tags.AspNetAction)

            return tracer;
            /*
            Scope scope = null;

            try
            {
                string dbType = GetDbType(command.GetType().Name);

                if (dbType == null)
                {
                    // don't create a scope, skip this trace
                    return null;
                }

                Tracer tracer = Tracer.Instance;
                string serviceName = $"{tracer.DefaultServiceName}-{dbType}";
                string operationName = $"{dbType}.query";

                scope = tracer.StartActive(operationName, serviceName: serviceName);
                scope.Span.SetTag(Tags.DbType, dbType);
            }
            catch (Exception ex)
            {
                Log.ErrorException("Error creating or populating scope.", ex);
            }

            return scope;
            */
        }
    }
}

#endif
