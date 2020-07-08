using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Datadog.Trace.ClrProfiler.Emit;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.Helpers
{
    internal static class SpanContextPropagatorHelpers
    {
        private const NumberStyles NumberStyles = System.Globalization.NumberStyles.Integer;

        private static readonly CultureInfo InvariantCulture = CultureInfo.InvariantCulture;
        private static readonly Vendors.Serilog.ILogger Log = DatadogLogging.GetLogger(typeof(SpanContextPropagatorHelpers));

        public static void InjectHttpHeadersWithReflection(SpanContext context, object headers)
        {
            if (context == null) { throw new ArgumentNullException(nameof(context)); }

            if (headers == null) { throw new ArgumentNullException(nameof(headers)); }

            // lock sampling priority when span propagates.
            context.TraceContext?.LockSamplingPriority();

            headers.CallMethod<string, bool>("Remove", HttpHeaderNames.TraceId);
            headers.CallVoidMethod<string, string>("Add", HttpHeaderNames.TraceId, context.TraceId.ToString(InvariantCulture));

            headers.CallMethod<string, bool>("Remove", HttpHeaderNames.ParentId);
            headers.CallVoidMethod<string, string>("Add", HttpHeaderNames.ParentId, context.SpanId.ToString(InvariantCulture));

            headers.CallMethod<string, bool>("Remove", HttpHeaderNames.Origin);
            // avoid writing origin header if not set, keeping the previous behavior.
            if (context.Origin != null)
            {
                headers.CallVoidMethod<string, string>("Add", HttpHeaderNames.Origin, context.Origin);
            }

            var samplingPriority = (int?)(context.TraceContext?.SamplingPriority ?? context.SamplingPriority);

            headers.CallMethod<string, bool>("Remove", HttpHeaderNames.SamplingPriority);
            headers.CallVoidMethod<string, string>(
                "Add",
                HttpHeaderNames.SamplingPriority,
                samplingPriority?.ToString(InvariantCulture));
        }

        public static SpanContext ExtractHttpHeadersWithReflection(object headers)
        {
            if (headers == null)
            {
                throw new ArgumentNullException(nameof(headers));
            }

            var traceId = ParseUInt64(headers, HttpHeaderNames.TraceId);

            if (traceId == 0)
            {
                // a valid traceId is required to use distributed tracing
                return null;
            }

            var parentId = ParseUInt64(headers, HttpHeaderNames.ParentId);
            var samplingPriority = ParseEnum<SamplingPriority>(headers, HttpHeaderNames.SamplingPriority);
            var origin = ParseString(headers, HttpHeaderNames.Origin);

            return new SpanContext(traceId, parentId, samplingPriority, null, origin);
        }

        private static ulong ParseUInt64(object headers, string headerName)
        {
            if (headers.CallMethod<string, bool>("Contains", headerName).Value)
            {
                var headerValues = headers.CallMethod<string, IEnumerable<string>>("GetValues", headerName).Value;
                if (headerValues != null && headerValues.Any())
                {
                    foreach (string headerValue in headerValues)
                    {
                        if (ulong.TryParse(headerValue, NumberStyles, InvariantCulture, out var result))
                        {
                            return result;
                        }
                    }

                    Log.Information("Could not parse {0} headers: {1}", headerName, string.Join(",", headerValues));
                }
            }

            return 0;
        }

        private static T? ParseEnum<T>(object headers, string headerName)
            where T : struct, Enum
        {
            if (headers.CallMethod<string, bool>("Contains", headerName).Value)
            {
                var headerValues = headers.CallMethod<string, IEnumerable<string>>("GetValues", headerName).Value;
                if (headerValues != null && headerValues.Any())
                {
                    foreach (string headerValue in headerValues)
                    {
                        if (Enum.TryParse<T>(headerValue, out var result) &&
                            Enum.IsDefined(typeof(T), result))
                        {
                            return result;
                        }
                    }

                    Log.Information(
                        "Could not parse {0} headers: {1}",
                        headerName,
                        string.Join(",", headerValues));
                }
            }

            return default;
        }

        private static string ParseString(object headers, string headerName)
        {
            if (headers.CallMethod<string, bool>("Contains", headerName).Value)
            {
                var headerValues = headers.CallMethod<string, IEnumerable<string>>("GetValues", headerName).Value;
                if (headerValues != null)
                {
                    foreach (string headerValue in headerValues)
                    {
                        if (!string.IsNullOrEmpty(headerValue))
                        {
                            return headerValue;
                        }
                    }
                }
            }

            return null;
        }
    }
}
