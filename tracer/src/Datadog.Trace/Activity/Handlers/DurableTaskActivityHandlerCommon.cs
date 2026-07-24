// <copyright file="DurableTaskActivityHandlerCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using Datadog.Trace.Activity.DuckTypes;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.Activity.Handlers
{
    internal static class DurableTaskActivityHandlerCommon
    {
        internal static string GetResourceName(string? operationName, string? taskName, string? taskType)
        {
            if (!string.IsNullOrEmpty(taskName))
            {
                return taskType switch
                {
                    DurableTaskConstants.TaskTypes.Orchestration => $"orchestration:{taskName}",
                    DurableTaskConstants.TaskTypes.CreateOrchestration => $"create_orchestration:{taskName}",
                    DurableTaskConstants.TaskTypes.Activity => $"activity:{taskName}",
                    DurableTaskConstants.TaskTypes.Entity => $"entity:{taskName}",
                    _ => taskName,
                };
            }

            return operationName ?? "durabletask";
        }

        internal static string? MapActivityKindToSpanKind(ActivityKind activityKind)
            => activityKind switch
            {
                ActivityKind.Server => SpanKinds.Server,
                ActivityKind.Client => SpanKinds.Client,
                ActivityKind.Producer => SpanKinds.Producer,
                ActivityKind.Consumer => SpanKinds.Consumer,
                _ => SpanKinds.Internal,
            };

        internal static bool TryGetTag(IEnumerable<KeyValuePair<string, object?>> tags, string key, out string? value)
        {
            foreach (var tag in tags)
            {
                if (tag.Key == key)
                {
                    value = tag.Value?.ToString();
                    return true;
                }
            }

            value = null;
            return false;
        }

        internal static void EnhanceSpan<T>(Span span, T activity)
            where T : IActivity
        {
            span.Type = SpanTypes.Serverless;
            span.SetTag(Tags.InstrumentationName, nameof(Configuration.IntegrationId.AzureFunctions));

            string? taskType = null;
            string? taskName = null;

            if (activity is IActivity5 activity5)
            {
                var tags = activity5.TagObjects;
                if (tags is not null)
                {
                    TryGetTag(tags, DurableTaskConstants.Tags.Type, out taskType);
                    TryGetTag(tags, DurableTaskConstants.Tags.Name, out taskName);

                    foreach (var tag in tags)
                    {
                        if (tag.Key.StartsWith("durabletask.", System.StringComparison.Ordinal))
                        {
                            span.SetTag(tag.Key, tag.Value?.ToString());
                        }
                    }
                }

                if (span.Tags is OpenTelemetryTags otelTags)
                {
                    otelTags.SpanKind = MapActivityKindToSpanKind(activity5.Kind);
                }
            }

            span.ResourceName = GetResourceName(activity.OperationName, taskName, taskType);
        }
    }
}
