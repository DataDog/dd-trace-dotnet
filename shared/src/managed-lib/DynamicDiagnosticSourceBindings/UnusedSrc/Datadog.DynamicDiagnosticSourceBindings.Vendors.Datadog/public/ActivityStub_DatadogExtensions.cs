using System;
using System.Collections.Generic;
using System.ComponentModel;
using Datadog.DynamicDiagnosticSourceBindings;

namespace Datadog.DynamicDiagnosticSourceBindings.Vendors.Datadog
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class ActivityStub_DatadogExtensions
    {
        private static ActivityContextStub CreateNewActivityConext(string datadogParentSpanId, string datadogTraceId)
        {
            if (datadogParentSpanId == null && datadogTraceId == null)
            {
                return default(ActivityContextStub);
            }

            throw new NotImplementedException();
        }

        public static string GetDatadogTraceId(this ActivityStub activity)
        {
            string traceId = activity.TraceId;
            if (traceId == null)
            {
                return null;
            }

            int len = traceId.Length;
            if (len > 16)
            {
                traceId = traceId.Trim();
                traceId = traceId.Substring(len - 16);
            }

            return traceId;
        }

        private static string GetDatadogSpanId(this ActivityStub activity)
        {
            string spanId = activity.SpanId;
            return spanId;
        }

        private static string GetDatadogParentSpanId(this ActivityStub activity)
        {
            string parentSpanId = activity.ParentSpanId;
            return parentSpanId;
        }
    }
}
