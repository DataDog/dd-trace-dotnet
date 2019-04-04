#if !NETSTANDARD2_0

using System;
using System.Collections.Generic;
using System.ServiceModel.Channels;
using Datadog.Trace.ClrProfiler.Interfaces;
using Datadog.Trace.ClrProfiler.Services;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Interfaces;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.Models
{
    internal class WcfHttpRequestMessageSpanIntegrationDelegate : WcfRequestMessageSpanIntegrationDelegate, IHttpSpanTagsSource
    {
        private static readonly char[] HttpActionDelimiters = { '/', '\\' };

        private readonly HttpRequestMessageProperty _httpRequestMessageProperty;
        private readonly Uri _toUri;

        private WcfHttpRequestMessageSpanIntegrationDelegate(
                                                             RequestContext requestContext,
                                                             IScope scope,
                                                             HttpRequestMessageProperty httpRequestMessageProperty)
            : base(requestContext, scope)
        {
            _httpRequestMessageProperty = httpRequestMessageProperty ?? throw new ArgumentNullException(nameof(httpRequestMessageProperty));
            _toUri = requestContext.RequestMessage.Headers?.To;
        }

        public static ISpanIntegrationDelegate Create(RequestContext requestContext, HttpRequestMessageProperty httpRequestMessageProperty)
        {
            // This logic really should be split out to a scope creation service, but for now it's here...
            SpanContext propagatedContext = null;

            if (Tracer.Instance.ActiveScope == null)
            {
                try
                {
                    var headers = httpRequestMessageProperty.Headers.Wrap();
                    propagatedContext = SpanContextPropagator.Instance.Extract(headers);
                }
                catch (Exception ex)
                {
                    Log.ErrorException("Error extracting propagated HTTP headers.", ex);
                }
            }

            return new WcfHttpRequestMessageSpanIntegrationDelegate(
                                                                    requestContext,
                                                                    Tracer.Instance.StartActive("wcf.request", propagatedContext),
                                                                    httpRequestMessageProperty);
        }

        public override void OnBegin()
        {
            if (Scope == null)
            {
                return;
            }

            DependencyFactory.SpanDecorationService(this).Decorate(Scope.Span, this);
        }

        public override void OnEnd()
        {
            var x = _httpRequestMessageProperty.Headers;

            base.OnEnd();
        }

        public override IEnumerable<KeyValuePair<string, string>> GetTags()
            => HttpSpanTagsProducer.Instance.GetTags(this);

        public string GetHttpMethod() => _httpRequestMessageProperty.Method ?? "POST";

        public string GetHttpHost() => _httpRequestMessageProperty.Headers["Host"];

        public string GetHttpUrl() => _toUri?.AbsoluteUri;

        public override bool TryGetResourceName(out string resourceName)
        {
            var splitTargetAt = RequestContext.RequestMessage.Headers?.Action?.LastIndexOfAny(HttpActionDelimiters) ?? -1;

            var rpcTarget = splitTargetAt >= 0
                                ? RequestContext.RequestMessage.Headers.Action.Substring(splitTargetAt)
                                : null;

            resourceName = rpcTarget == null
                               ? RequestContext.RequestMessage.ToString()
                               : string.Concat(GetHttpMethod(), " ", _toUri?.AbsolutePath, rpcTarget).Trim();

            return true;
        }

        public override bool TryGetType(out string spanType)
        {
            spanType = SpanTypes.Web;

            return true;
        }
    }
}

#endif
