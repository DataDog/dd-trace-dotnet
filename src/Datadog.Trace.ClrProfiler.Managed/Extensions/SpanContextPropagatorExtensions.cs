using System;
using System.Globalization;
using Datadog.Trace.ClrProfiler.Emit;

namespace Datadog.Trace.ClrProfiler.Extensions
{
    internal static class SpanContextPropagatorExtensions
    {
        private static readonly CultureInfo InvariantCulture = CultureInfo.InvariantCulture;

        public static void InjectWithReflection(this SpanContextPropagator spanContextPropagator, SpanContext context, object headers)
        {
            if (context == null) { throw new ArgumentNullException(nameof(context)); }

            if (headers == null) { return; }

            // lock sampling priority when span propagates.
            context.TraceContext?.LockSamplingPriority();

            headers.CallMethod<string, bool>("Remove", HttpHeaderNames.TraceId);
            headers.CallVoidMethod<string, string>("Add", HttpHeaderNames.TraceId, context.TraceId.ToString(InvariantCulture));

            headers.CallMethod<string, bool>("Remove", HttpHeaderNames.ParentId);
            headers.CallVoidMethod<string, string>("Add", HttpHeaderNames.ParentId, context.SpanId.ToString(InvariantCulture));

            var samplingPriority = (int?)(context.TraceContext?.SamplingPriority ?? context.SamplingPriority);

            headers.CallMethod<string, bool>("Remove", HttpHeaderNames.SamplingPriority);
            headers.CallVoidMethod<string, string>(
                "Add",
                HttpHeaderNames.SamplingPriority,
                samplingPriority?.ToString(InvariantCulture));
        }
    }
}
