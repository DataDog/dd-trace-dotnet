// <copyright file="IastModule.Escape.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Linq;
using System.Net;
using Datadog.Trace.Configuration;
using Datadog.Trace.Iast.Helpers;

namespace Datadog.Trace.Iast;

internal static partial class IastModule
{
    public static string? OnXssEscape(string? text, string? encoded)
    {
        return (string?)OnEscape(text, encoded, SecureMarks.Xss, true, IntegrationId.Xss);
    }

    public static string? OnSsrfEscape(string? text, string? encoded)
    {
        return (string?)OnEscape(text, encoded, SecureMarks.Ssrf, true, IntegrationId.Ssrf);
    }

    public static object? OnCustomEscape(object? text, SecureMarks marks)
    {
        return OnEscape(text, text, marks, false);
    }

    private static object? OnEscape(object? textObj, object? encodedObj, SecureMarks secureMarks, bool ensureDifferentInstance, params IntegrationId[] integrations)
    {
        try
        {
            if (!IastSettings.Enabled ||
                textObj is null || encodedObj is null)
            {
                return encodedObj;
            }

            var text = textObj as string;
            var encoded = encodedObj as string;
            if (text is { Length: 0 } || encoded is { Length: 0 })
            {
                return encodedObj;
            }

            var tracer = Tracer.Instance;
            if (integrations != null && !integrations.Any((i) => tracer.Settings.IsIntegrationEnabled(i)))
            {
                return encodedObj;
            }

            var scope = tracer.ActiveScope as Scope;
            var traceContext = scope?.Span?.Context?.TraceContext;
            var iastContext = traceContext?.IastRequestContext;

            if (iastContext is null || iastContext.AddVulnerabilitiesAllowed() != true)
            {
                return encodedObj;
            }

            var tainted = traceContext?.IastRequestContext?.GetTainted(textObj!);
            if (tainted is null)
            {
                return encodedObj;
            }

            // Special case. The encoded string is already tainted. We must check instance is not the same as the original text
            if (ensureDifferentInstance && text is not null && encoded is not null && object.ReferenceEquals(text, encoded))
            {
                // return a new instance of the encoded string and taint it whole
#if NETCOREAPP3_0_OR_GREATER
                var newEncoded = new string(encoded.AsSpan());
#else
                var newEncoded = new string(encoded.ToCharArray());
#endif
                iastContext.GetTaintedObjects().Taint(newEncoded, Ranges.CopyWithMark(tainted.Ranges, secureMarks));
                return newEncoded;
            }

            // Taint the escaped string with the new secure marks
            iastContext.GetTaintedObjects().Taint(encodedObj, Ranges.CopyWithMark(tainted.Ranges, secureMarks));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error while escaping string.");
        }

        return encodedObj;
    }
}
