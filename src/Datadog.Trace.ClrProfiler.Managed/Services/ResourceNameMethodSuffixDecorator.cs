using System;
using Datadog.Trace.ClrProfiler.ExtensionMethods;
using Datadog.Trace.ClrProfiler.Interfaces;
using Datadog.Trace.Interfaces;

namespace Datadog.Trace.ClrProfiler.Services
{
    internal class ResourceNameMethodSuffixDecorator : ISpanDecorator
    {
        private readonly IHasResourceNameSuffixResolver _suffixResolver;
        private readonly IHasHttpMethod _methodSource;

        internal ResourceNameMethodSuffixDecorator(
            IHasResourceNameSuffixResolver suffixResolver,
            IHasHttpMethod methodSource)
        {
            _suffixResolver = suffixResolver ?? throw new ArgumentNullException(nameof(suffixResolver));
            _methodSource = methodSource ?? throw new ArgumentNullException(nameof(methodSource));
        }

        public void Decorate(ISpan span)
        {
            var httpMethod = (_methodSource.GetHttpMethod() ?? span.GetHttpMethod() ?? "GET").ToUpperInvariant();

            var suffix = _suffixResolver.GetResourceNameSuffix()?.ToLowerInvariant() ?? string.Empty;

            span.ResourceName = string.Concat(httpMethod, " ", suffix).Trim();
        }
    }
}
