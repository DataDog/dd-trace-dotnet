// <copyright file="IastModule.Escape.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Datadog.Trace.Configuration;
using Datadog.Trace.Iast.Aspects.System;
using Datadog.Trace.Iast.Propagation;
using Datadog.Trace.Iast.SensitiveData;
using Datadog.Trace.Iast.Settings;
using Datadog.Trace.Iast.Telemetry;
using Datadog.Trace.Logging;
using static System.Net.Mime.MediaTypeNames;
using static Datadog.Trace.Telemetry.Metrics.MetricTags;

namespace Datadog.Trace.Iast;

internal static partial class IastModule
{
    public static string? OnXssEscape(string? text)
    {
        try
        {
            if (!iastSettings.Enabled || string.IsNullOrEmpty(text))
            {
                return text;
            }

            var tracer = Tracer.Instance;
            if (!tracer.Settings.IsIntegrationEnabled(IntegrationId.Xss))
            {
                return text;
            }

            var scope = tracer.ActiveScope as Scope;
            var currentSpan = scope?.Span;
            var traceContext = currentSpan?.Context?.TraceContext;

            if (traceContext?.IastRequestContext?.AddVulnerabilitiesAllowed() != true)
            {
                return text;
            }

            var tainted = traceContext?.IastRequestContext?.GetTainted(text!);
            if (tainted is null)
            {
                return text;
            }

            // OnExecutedSinkTelemetry(IastInstrumentedSinks.Xss);
            // Mark as safe (by now we return a new string)
            return new string(text!.ToCharArray());
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error while checking for Sql injection.");
            return text;
        }
    }
}
