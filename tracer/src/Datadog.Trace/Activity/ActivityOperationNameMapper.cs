// <copyright file="ActivityOperationNameMapper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Activity
{
    /// <summary>
    /// Helper class to map <see cref="SpanKinds"/> and various tags on an Activity to a <see cref="Span.OperationName"/>.
    /// </summary>
    internal static class ActivityOperationNameMapper
    {
        public static void MapToOperationName(Span span)
        {
            string operationName = string.Empty;
            var spanKind = span.GetTag(Tags.SpanKind);
            switch (spanKind)
            {
                // TODO basic implementation first to get tests passing
                case SpanKinds.Internal:
                    if (span.TryGetTag("code.namespace", out var codeNamespace) &&
                        span.TryGetTag("code.function", out var codeFunction))
                    {
                        operationName = $"{codeNamespace}.{codeFunction}";
                    }

                    break;
                case SpanKinds.Server:
                    break;
                case SpanKinds.Client:
                    break;
                case SpanKinds.Producer:
                    break;
                case SpanKinds.Consumer:
                    break;
                default:
                    break;
            }

            if (string.IsNullOrEmpty(operationName))
            {
                operationName = spanKind;
            }

            // TODO what if there is a tag from the activity "operation.name" do we honour that?
            span.OperationName = operationName;
        }

        // TODO hacky extension to just get tests passing
        private static bool TryGetTag(this Span span, string key, out string value)
        {
            value = span.Tags.GetTag(key);
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            return true;
        }
    }
}
