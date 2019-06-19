#if !NETSTANDARD2_0

using System;
using System.ServiceModel.Channels;
using Datadog.Trace.ClrProfiler.Interfaces;
using Datadog.Trace.ClrProfiler.Services;
using Datadog.Trace.Interfaces;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.Models
{
    internal class WcfRequestMessageSpanIntegrationDelegate : BaseSpanDecorationSource, ISpanIntegrationDelegate
    {
        protected static readonly ILog Log = LogProvider.GetLogger(typeof(HttpContextSpanIntegrationDelegate));

        protected WcfRequestMessageSpanIntegrationDelegate(RequestContext requestContext, IScope scope)
        {
            RequestContext = requestContext ?? throw new ArgumentNullException(nameof(requestContext));
            Scope = scope;
        }

        public IScope Scope { get; }

        protected RequestContext RequestContext { get; }

        public static ISpanIntegrationDelegate Create(RequestContext requestContext)
        {
            if (requestContext.RequestMessage.Properties.TryGetValue("httpRequest", out var httpRequestProperty) &&
                httpRequestProperty is HttpRequestMessageProperty httpRequestMessageProperty)
            {
                return WcfHttpRequestMessageSpanIntegrationDelegate.Create(requestContext, httpRequestMessageProperty);
            }

            return new WcfRequestMessageSpanIntegrationDelegate(requestContext, Tracer.Instance.StartActive("wcf.request"));
        }

        public static ISpanIntegrationDelegate CreateAndBegin(RequestContext requestContext)
        {
            var instance = Create(requestContext);

            instance.OnBegin();

            return instance;
        }

        public virtual void OnBegin()
        {
            if (Scope == null)
            {
                return;
            }

            DependencyFactory.SpanDecorationService(this).Decorate(Scope.Span, this);
        }

        public virtual void OnEnd()
        {
            if (Scope?.Span == null)
            {
                return;
            }

            Scope.Dispose();
        }

        public void OnError()
        {
            // Currently only used in extension for exception filtering, where we set the actual exception
        }

        public void Dispose()
        {
            try
            {
                OnEnd();
            }
            catch (Exception x)
            {
                Log.WarnException("Disposal exception", x);
            }
        }

        public override bool TryGetResourceName(out string resourceName)
        {
            resourceName = RequestContext.RequestMessage.ToString();

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
