// <copyright file="DatabaseMonitoringPropagator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet
{
    internal static class DatabaseMonitoringPropagator
    {
        private const string SqlCommentSpanService = "dddbs";
        private const string SqlCommentRootService = "ddps";
        private const string SqlCommentVersion = "ddpv";
        private const string SqlCommentEnv = "dde";

        private const string SqlCommentTraceParent = "traceparent";

        internal static string PropagateSpanData(string propagationStyle, string configuredServiceName, Span span)
        {
            var propgationComment =
                $"{SqlCommentRootService}='{configuredServiceName}'," +
                $"{SqlCommentSpanService}='{span.ServiceName}'," +
                $"{SqlCommentVersion}='{span.GetTag(Tags.Version)}'," +
                $"{SqlCommentEnv}='{span.GetTag(Tags.Env)}'";

            if (propagationStyle == "full")
            {
                return $"/*{propgationComment},{CreateTraceParent(span.TraceId, span.SpanId, span.GetMetric(Tags.SamplingPriority))}*/";
            }

            return $"/*{propgationComment}*/";
        }

        internal static string CreateTraceParent(ulong traceId, ulong spanId, double? samplingProprity)
        {
            string sampling = samplingProprity > 0.0 ? "01" : "00";

            return $"{SqlCommentTraceParent}='00-{traceId.ToString("x32")}-{spanId.ToString("x16")}-{sampling}'";
        }
    }
}
