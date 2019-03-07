#if !NETSTANDARD2_0

using System;
using System.Collections.Generic;
using System.Web;
using Datadog.Trace.ClrProfiler.ExtensionMethods;
using Datadog.Trace.ClrProfiler.Interfaces;
using Datadog.Trace.ClrProfiler.Services;
using Datadog.Trace.Interfaces;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.Models
{
    internal class HttpContextSpanIntegrationDelegate : BaseSpanDecorationSource, ISpanIntegrationDelegate, IHttpSpanTagsSource
    {
        private static readonly ILog Log = LogProvider.GetLogger(typeof(HttpContextSpanIntegrationDelegate));

        private readonly HttpContext _httpContext;

        private HttpContextSpanIntegrationDelegate(HttpContext context, IScope scope)
        {
            _httpContext = context ?? throw new ArgumentNullException(nameof(context));
            Scope = scope;
        }

        public IScope Scope { get; }

        public static ISpanIntegrationDelegate Create(HttpContext context, IScope scope)
            => new HttpContextSpanIntegrationDelegate(context, scope);

        public static ISpanIntegrationDelegate CreateAndBegin(HttpContext context, IScope scope)
        {
            var instance = Create(context, scope);

            instance.OnBegin();

            return instance;
        }

        public void OnBegin()
        {
            if (Scope == null)
            {
                return;
            }

            DependencyFactory.SpanDecorationService(this).Decorate(Scope.Span, this);
        }

        public void OnEnd()
        {
            if (Scope?.Span == null)
            {
                return;
            }

            Scope.Span.SetTag(Tags.HttpStatusCode, _httpContext.Response.StatusCode.ToString());

            Scope.Dispose();
        }

        public void OnError()
        {
            if (Scope?.Span == null || _httpContext.Error == null)
            {
                return;
            }

            Scope.Span.SetException(_httpContext.Error);
        }

        public string GetHttpMethod() => _httpContext.Request?.HttpMethod;

        public string GetHttpHost() => _httpContext.Request?.Headers?.Get("Host");

        public string GetHttpUrl()
            => _httpContext.Request == null
                   ? null
                   : _httpContext.Request.Url?.AbsoluteUri ?? _httpContext.Request.RawUrl;

        // Normally I'd take the producer here as a dependency, but we're trying to balance maintainability/etc. concerns with performance concerns
        // including limitting object creation, so for now this allows us to centralize logic for tag generation, while providing a decent path
        // for extending with minimal churn - if we have to generate more tags for HttpContexts, we'd just Concat the producers here for now
        public override IEnumerable<KeyValuePair<string, string>> GetTags()
            => HttpSpanTagsProducer.Instance.GetTags(this);

        public override bool TryGetResourceName(out string resourceName)
        { // TODO: This will need to be moved out into a strategy to support extending to other integrations...
            var httpMethod = (GetHttpMethod() ?? Scope?.Span?.GetHttpMethod() ?? "GET").ToUpperInvariant();

            var suffix = _httpContext.Request == null
                             ? string.Empty
                             : _httpContext.Request.Path ?? _httpContext.Request.RawUrl ?? string.Empty;

            resourceName = string.Concat(httpMethod, " ", suffix.ToLowerInvariant()).Trim();

            return true;
        }

        public override bool TryGetType(out string spanType)
        {
            spanType = SpanTypes.Web;

            return true;
        }

        public void Dispose()
        {
            try
            {
                Scope?.Dispose();
            }
            catch (Exception x)
            {
#if DEBUG
                Log.WarnException("Disposal exception", x);
#endif

                // Nothing else to do
            }
        }
    }
}

#endif
