using System;
using Datadog.Trace.ClrProfiler.Interfaces;

namespace Datadog.Trace.ClrProfiler.ExtensionMethods
{
    internal static class SpanIntegrationDelegateExtensions
    {
        internal static bool SetExceptionForFilter(this ISpanIntegrationDelegate spanIntegrationDelegate, Exception exception)
        {
            spanIntegrationDelegate?.Scope?.Span?.SetException(exception);

            spanIntegrationDelegate?.OnError();

            return false;
        }
    }
}
