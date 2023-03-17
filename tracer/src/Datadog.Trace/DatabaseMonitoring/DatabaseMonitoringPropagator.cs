// <copyright file="DatabaseMonitoringPropagator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Configuration;
using Datadog.Trace.Propagators;
using Datadog.Trace.Util;

namespace Datadog.Trace.DatabaseMonitoring
{
    internal static class DatabaseMonitoringPropagator
    {
        private const string SqlCommentSpanService = "dddbs";
        private const string SqlCommentRootService = "ddps";
        private const string SqlCommentVersion = "ddpv";
        private const string SqlCommentEnv = "dde";

        internal static string PropagateSpanData(DbmPropagationLevel propagationStyle, string configuredServiceName, SpanContext context)
        {
            if (propagationStyle != DbmPropagationLevel.Service && propagationStyle != DbmPropagationLevel.Full)
            {
                return string.Empty;
            }

            var propagatorSringBuilder = StringBuilderCache.Acquire(StringBuilderCache.MaxBuilderSize);
            propagatorSringBuilder.Append($"/*{SqlCommentSpanService}='{Uri.EscapeDataString(context.ServiceName)}'");

            if (context.TraceContext?.Environment is { } envTag)
            {
                propagatorSringBuilder.Append($",{SqlCommentEnv}='{Uri.EscapeDataString(envTag)}'");
            }

            propagatorSringBuilder.Append($",{SqlCommentRootService}='{Uri.EscapeDataString(configuredServiceName)}'");

            if (context.TraceContext?.ServiceVersion is { } versionTag)
            {
                propagatorSringBuilder.Append($",{SqlCommentVersion}='{Uri.EscapeDataString(versionTag)}'");
            }

            if (propagationStyle == DbmPropagationLevel.Full)
            {
                propagatorSringBuilder.Append($",{W3CTraceContextPropagator.TraceParentHeaderName}='{W3CTraceContextPropagator.CreateTraceParentHeader(context)}'*/");
            }
            else
            {
                propagatorSringBuilder.Append("*/");
            }

            return StringBuilderCache.GetStringAndRelease(propagatorSringBuilder);
        }
    }
}
