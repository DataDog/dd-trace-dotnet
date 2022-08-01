// <copyright file="Common.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Reflection;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Pdb;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing
{
    internal static class Common
    {
        internal static readonly IDatadogLogger Log = Ci.CIVisibility.Log;

        internal static void FlushSpans(IntegrationId integrationInfo)
        {
            if (!Tracer.Instance.Settings.IsIntegrationEnabled(integrationInfo))
            {
                return;
            }

            CIVisibility.FlushSpans();
        }

        internal static string GetParametersValueData(object paramValue)
        {
            if (paramValue is null)
            {
                return "(null)";
            }

            if (paramValue is Array pValueArray)
            {
                const int maxArrayLength = 50;
                int length = pValueArray.Length > maxArrayLength ? maxArrayLength : pValueArray.Length;

                string[] strValueArray = new string[length];
                for (var i = 0; i < length; i++)
                {
                    strValueArray[i] = GetParametersValueData(pValueArray.GetValue(i));
                }

                return "[" + string.Join(", ", strValueArray) + (pValueArray.Length > maxArrayLength ? ", ..." : string.Empty) + "]";
            }

            if (paramValue is Delegate pValueDelegate)
            {
                return $"{paramValue}[{pValueDelegate.Target}|{pValueDelegate.Method}]";
            }

            return paramValue.ToString();
        }

        internal static void DecorateSpanWithSourceAndCodeOwners(Span span, MethodInfo testMethod)
        {
            if (MethodSymbolResolver.Instance.TryGetMethodSymbol(testMethod, out var methodSymbol))
            {
                span.SetTag(TestTags.SourceFile, CIEnvironmentValues.Instance.MakeRelativePathFromSourceRoot(methodSymbol.File, false));
                span.SetMetric(TestTags.SourceStart, methodSymbol.StartLine);
                span.SetMetric(TestTags.SourceEnd, methodSymbol.EndLine);

                if (CIEnvironmentValues.Instance.CodeOwners is { } codeOwners)
                {
                    var match = codeOwners.Match("/" + CIEnvironmentValues.Instance.MakeRelativePathFromSourceRoot(methodSymbol.File, false));
                    if (match is not null)
                    {
                        span.SetTag(TestTags.CodeOwners, match.Value.GetOwnersString());
                    }
                }
            }
        }

        internal static void StartCoverage()
        {
            Ci.Coverage.CoverageReporter.Handler.StartSession();
        }

        internal static void StopCoverage(Span span)
        {
            if (Ci.Coverage.CoverageReporter.Handler.EndSession() is Ci.Coverage.Models.CoveragePayload coveragePayload)
            {
                if (span is not null)
                {
                    coveragePayload.TraceId = span.TraceId;
                    coveragePayload.SpanId = span.SpanId;
                }

                Ci.CIVisibility.Manager?.WriteEvent(coveragePayload);
            }
        }
    }
}
