// <copyright file="IastModule.Escape.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Net;
using Datadog.Trace.Configuration;
using Datadog.Trace.Iast.Helpers;

namespace Datadog.Trace.Iast;

internal static partial class IastModule
{
    public static string? OnXssEscape(string? text, string? encoded)
    {
        try
        {
            if (!IastSettings.Enabled || string.IsNullOrEmpty(text))
            {
                return encoded;
            }

            var tracer = Tracer.Instance;
            if (!tracer.Settings.IsIntegrationEnabled(IntegrationId.Xss))
            {
                return encoded;
            }

            var scope = tracer.ActiveScope as Scope;
            var traceContext = scope?.Span?.Context?.TraceContext;

            if (traceContext?.IastRequestContext?.AddVulnerabilitiesAllowed() != true)
            {
                return encoded;
            }

            var tainted = traceContext?.IastRequestContext?.GetTainted(text!);
            if (tainted is null)
            {
                return encoded;
            }

            // Add the mark (exclusion) to the tainted ranges
            tainted.Ranges = Ranges.CopyWithMark(tainted.Ranges, SecureMarks.Xss);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error while escaping string for XSS.");
        }

        return encoded;
    }
}
