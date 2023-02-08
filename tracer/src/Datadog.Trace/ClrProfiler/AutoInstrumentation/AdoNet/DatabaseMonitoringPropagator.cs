using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet
{
    internal static class DatabaseMonitoringPropagator
    {
        private const string SqlCommentSpanService = "dddbs";
        private const string SqlCommentRootService = "ddps";
        private const string SqlCommentVersion = "ddpv";
        private const string SqlCommentEnv = "dde";

        private const string SqlCommentTraceParent = "traceparent";

        internal static string PropagateSpanData(string propagationStyle, string configuredServiceName, string sqlCommand, Scope scope)
        {
            var propgationComment =
                $"{SqlCommentRootService}='{configuredServiceName}'," +
                $"{SqlCommentSpanService}='{scope.Span.ServiceName}'," +
                $"{SqlCommentVersion}='{scope.Span.GetTag(Tags.Version)}'," +
                $"{SqlCommentEnv}='{scope.Span.GetTag(Tags.Env)}'";

            if (propagationStyle == "full")
            {
                propgationComment = propgationComment + CreateTraceParent(scope.Span.TraceId, scope.Span.SpanId, scope.Span.GetMetric(Tags.SamplingPriority));
            }

            return $"/*{propgationComment}*/ {sqlCommand}";
        }

        internal static string CreateTraceParent(ulong traceId, ulong spanId, double? samplingProprity)
        {
            samplingProprity = samplingProprity > 0 ? 01 : 00;

            return $",{SqlCommentTraceParent}='00-{traceId.ToString("X32")}-{spanId.ToString("X16")}-{samplingProprity}'";
        }
    }
}
