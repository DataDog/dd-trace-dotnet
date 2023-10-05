// <copyright file="ActivityOperationNameMapper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.Activity.DuckTypes;

namespace Datadog.Trace.Activity
{
    /// <summary>
    /// Helper class to map <see cref="SpanKinds"/> and various tags on an Activity to a <see cref="Span.OperationName"/>.
    /// </summary>
    internal static class ActivityOperationNameMapper
    {
        public static string MapToOperationName(IActivity5 activity)
        {
            string operationName = string.Empty;
            switch (activity.Kind)
            {
                case ActivityKind.Internal:
                    break;
                case ActivityKind.Server:
                    break;
                case ActivityKind.Client:
                    break;
                case ActivityKind.Producer:
                    break;
                case ActivityKind.Consumer:
                    break;
                default:
                    break;
            }

            if (string.IsNullOrEmpty(operationName))
            {
                operationName = activity.Kind.ToString().ToLower();
            }

            return operationName;
        }
    }
}
