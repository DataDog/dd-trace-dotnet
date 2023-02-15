// <copyright file="DatabaseMonitoringPropagator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Text;
using Datadog.Trace.Configuration;
using Datadog.Trace.Propagators;
using Datadog.Trace.Util;

namespace Datadog.Trace.DatabaseMonitoring
{
    internal static class DatabaseMonitoringPropagator
    {
        private static string sqlCommentSpanService = "dddbs";
        private static string sqlCommentRootService = "ddps";
        private static string sqlCommentVersion = "ddpv";
        private static string sqlCommentEnv = "dde";

        internal static string PropagateSpanData(DbmPropagationLevel propagationStyle, string configuredServiceName, Span span)
        {
            var propagatorSringBuilder = StringBuilderCache.Acquire(StringBuilderCache.MaxBuilderSize);
            propagatorSringBuilder.Append($"{sqlCommentRootService}='{configuredServiceName}',{sqlCommentSpanService}='{span.ServiceName}'");

            if (span.GetTag(Tags.Version) != null)
            {
                propagatorSringBuilder.Append($",{sqlCommentVersion}='{span.GetTag(Tags.Version)}'");
            }

            if (span.GetTag(Tags.Env) != null)
            {
                propagatorSringBuilder.Append($",{sqlCommentEnv}='{span.GetTag(Tags.Env)}'");
            }

            if (propagationStyle == DbmPropagationLevel.Full)
            {
                propagatorSringBuilder.Append($",{W3CTraceContextPropagator.TraceParentHeaderName}='{W3CTraceContextPropagator.CreateTraceParentHeader(span.Context)}'");
            }

            return $"/*{StringBuilderCache.GetStringAndRelease(propagatorSringBuilder)}*/";
        }
    }
}
