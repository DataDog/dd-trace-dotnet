// <copyright file="Common.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Reflection;
using System.Threading;
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

            if (paramValue is string strValue)
            {
                return strValue;
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

        internal static void DecorateSpanWithRuntimeAndCiInformation(Span span)
        {
            // CI Environment variables data
            CIEnvironmentValues.Instance.DecorateSpan(span);

            // Runtime information
            var framework = FrameworkDescription.Instance;
            span.SetTag(CommonTags.LibraryVersion, TracerConstants.AssemblyVersion);
            span.SetTag(CommonTags.RuntimeName, framework.Name);
            span.SetTag(CommonTags.RuntimeVersion, framework.ProductVersion);
            span.SetTag(CommonTags.RuntimeArchitecture, framework.ProcessArchitecture);
            span.SetTag(CommonTags.OSArchitecture, framework.OSArchitecture);
            span.SetTag(CommonTags.OSPlatform, framework.OSPlatform);
            span.SetTag(CommonTags.OSVersion, Environment.OSVersion.VersionString);

            // Check if Intelligent Test Runner
            if (CIVisibility.HasSkippableTests())
            {
                span.SetTag("_dd.ci.itr.tests_skipped", "true");
            }
        }

        internal static void StartCoverage()
        {
            if (CIVisibility.Settings.CodeCoverageEnabled == true)
            {
                Ci.Coverage.CoverageReporter.Handler.StartSession();
            }
        }

        internal static void StopCoverage(Span span)
        {
            if (CIVisibility.Settings.CodeCoverageEnabled == true && Ci.Coverage.CoverageReporter.Handler.EndSession() is Ci.Coverage.Models.CoveragePayload coveragePayload)
            {
                if (span is not null)
                {
                    coveragePayload.TraceId = span.TraceId;
                    coveragePayload.SpanId = span.SpanId;
                }

                Log.Debug("Coverage data for TraceId={traceId} and SpanId={spanId} processed.", coveragePayload.TraceId, coveragePayload.SpanId);
                Ci.CIVisibility.Manager?.WriteEvent(coveragePayload);
            }
        }

        internal static void Prepare(MethodInfo methodInfo)
        {
            // Initialize Method Symbol Resolver
            _ = MethodSymbolResolver.Instance.GetModuleDef(methodInfo.Module);
        }

        internal static bool ShouldSkip(string testSuite, string testName, object[] testMethodArguments, ParameterInfo[] methodParameters)
        {
            var currentContext = SynchronizationContext.Current;
            try
            {
                SynchronizationContext.SetSynchronizationContext(null);
                var skippableTests = CIVisibility.GetSkippableTestsFromSuiteAndNameAsync(testSuite, testName).GetAwaiter().GetResult();
                if (skippableTests.Count > 0)
                {
                    foreach (var skippableTest in skippableTests)
                    {
                        var parameters = skippableTest.GetParameters();

                        // Same test name and no parameters
                        if ((parameters?.Arguments is null || parameters.Arguments.Count == 0) &&
                            (testMethodArguments is null || testMethodArguments.Length == 0))
                        {
                            return true;
                        }

                        if (parameters?.Arguments is not null)
                        {
                            var matchSignature = true;
                            for (var i = 0; i < methodParameters.Length; i++)
                            {
                                var targetValue = "(default)";
                                if (i < testMethodArguments.Length)
                                {
                                    targetValue = GetParametersValueData(testMethodArguments[i]);
                                }

                                if (!parameters.Arguments.TryGetValue(methodParameters[i].Name ?? string.Empty, out var argValue) ||
                                    (string)argValue != targetValue)
                                {
                                    matchSignature = false;
                                    break;
                                }
                            }

                            if (matchSignature)
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(currentContext);
            }

            return false;
        }
    }
}
