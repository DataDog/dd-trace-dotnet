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
        return OnEscape(text, encoded, SecureMarks.Xss, IntegrationId.Xss);
    }

    public static string? OnSsrfEscape(string? text, string? encoded)
    {
        return OnEscape(text, encoded, SecureMarks.Ssrf, IntegrationId.Ssrf);
    }

    private static string? OnEscape(string? text, string? encoded, SecureMarks secureMarks, params IntegrationId[] integrations)
    {
        try
        {
            if (!IastSettings.Enabled ||
                text is null || encoded is null ||
                text.Length == 0 || encoded.Length == 0)
            {
                return encoded;
            }

            var tracer = Tracer.Instance;
            if (integrations != null && !integrations.Any((i) => tracer.Settings.IsIntegrationEnabled(i)))
            {
                return encoded;
            }

            var scope = tracer.ActiveScope as Scope;
            var traceContext = scope?.Span?.Context?.TraceContext;
            var iastContext = traceContext?.IastRequestContext;

            if (iastContext is null || iastContext.AddVulnerabilitiesAllowed() != true)
            {
                return encoded;
            }

            var tainted = traceContext?.IastRequestContext?.GetTainted(text!);
            if (tainted is null)
            {
                return encoded;
            }

            // Special case. The encoded string is already tainted. We must check instance is not the same as the original text
            if (object.ReferenceEquals(text, encoded))
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

            // Taint the escaped string whole with the new secure marks
            iastContext.GetTaintedObjects().Taint(encoded, [new Range(0, encoded.Length, tainted.Ranges[0].Source, tainted.Ranges[0].SecureMarks | secureMarks)]);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error while escaping string for XSS.");
        }

        return encoded;
    }
}
