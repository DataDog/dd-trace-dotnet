// <copyright file="XUnitIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit
{
    internal static class XUnitIntegration
    {
        internal const string IntegrationName = nameof(Configuration.IntegrationId.XUnit);
        internal const IntegrationId IntegrationId = Configuration.IntegrationId.XUnit;

        internal static bool IsEnabled => CIVisibility.IsRunning && Tracer.Instance.Settings.IsIntegrationEnabled(IntegrationId);

        internal static Scope? CreateScope(ref TestRunnerStruct runnerInstance, Type targetType)
        {
            MethodInfo testMethod = runnerInstance.TestMethod;
            Type testClass = runnerInstance.TestClass;

            Common.Prepare(testMethod);

            string testBundle = testClass.Assembly.GetName().Name ?? string.Empty;
            string testSuite = testClass.ToString();
            string testName = testMethod.Name;

            string testFramework = "xUnit";

            Scope scope = Tracer.Instance.StartActiveInternal("xunit.test");
            Span span = scope.Span;

            span.Type = SpanTypes.Test;
            span.Context.TraceContext?.SetSamplingPriority(SamplingPriorityValues.AutoKeep);
            span.ResourceName = $"{testSuite}.{testName}";
            span.SetTag(Tags.Origin, TestTags.CIAppTestOriginName);
            span.SetTag(TestTags.Bundle, testBundle);
            span.SetTag(TestTags.Suite, testSuite);
            span.SetTag(TestTags.Name, testName);
            span.SetTag(TestTags.Framework, testFramework);
            span.SetTag(TestTags.FrameworkVersion, targetType.Assembly.GetName().Version?.ToString() ?? string.Empty);
            span.SetTag(TestTags.Type, TestTags.TypeTest);

            // Get test parameters
            object[] testMethodArguments = runnerInstance.TestMethodArguments;
            ParameterInfo[] methodParameters = testMethod.GetParameters();
            if (methodParameters?.Length > 0 && testMethodArguments?.Length > 0)
            {
                TestParameters testParameters = new TestParameters();
                testParameters.Metadata = new Dictionary<string, object>();
                testParameters.Arguments = new Dictionary<string, object>();
                testParameters.Metadata[TestTags.MetadataTestName] = runnerInstance.TestCase.DisplayName;

                for (int i = 0; i < methodParameters.Length; i++)
                {
                    if (i < testMethodArguments.Length)
                    {
                        testParameters.Arguments[methodParameters[i].Name] = Common.GetParametersValueData(testMethodArguments[i]);
                    }
                    else
                    {
                        testParameters.Arguments[methodParameters[i].Name] = "(default)";
                    }
                }

                span.SetTag(TestTags.Parameters, testParameters.ToJSON());
            }

            // Get traits
            Dictionary<string, List<string>> traits = runnerInstance.TestCase.Traits;
            if (traits.Count > 0)
            {
                span.SetTag(TestTags.Traits, Datadog.Trace.Vendors.Newtonsoft.Json.JsonConvert.SerializeObject(traits));
            }

            // CI Environment Variables and Runtime information
            Common.DecorateSpanWithRuntimeAndCiInformation(span);

            // Test code and code owners
            Common.DecorateSpanWithSourceAndCodeOwners(span, testMethod);

            Tracer.Instance.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId);
            Common.StartCoverage();

            // Skip tests
            if (runnerInstance.SkipReason != null)
            {
                span.SetTag(TestTags.Status, TestTags.StatusSkip);
                span.SetTag(TestTags.SkipReason, runnerInstance.SkipReason);
                span.Finish(TimeSpan.Zero);
                scope.Dispose();
                Common.StopCoverage(span);
                return null;
            }

            span.ResetStartTime();
            return scope;
        }

        internal static void FinishScope(Scope scope, IExceptionAggregator exceptionAggregator)
        {
            try
            {
                Exception exception = exceptionAggregator.ToException();

                if (exception != null)
                {
                    if (exception.GetType().Name == "SkipException")
                    {
                        scope.Span.SetTag(TestTags.Status, TestTags.StatusSkip);
                        scope.Span.SetTag(TestTags.SkipReason, exception.Message);
                    }
                    else
                    {
                        scope.Span.SetException(exception);
                        scope.Span.SetTag(TestTags.Status, TestTags.StatusFail);
                    }
                }
                else
                {
                    scope.Span.SetTag(TestTags.Status, TestTags.StatusPass);
                }
            }
            finally
            {
                scope.Dispose();
                Common.StopCoverage(scope.Span);
            }
        }

        internal static bool ShouldSkip(ref TestRunnerStruct runnerInstance)
        {
            if (CIVisibility.Settings.IntelligentTestRunnerEnabled != true)
            {
                return false;
            }

            var testMethod = runnerInstance.TestMethod;
            return Common.ShouldSkip(runnerInstance.TestClass.ToString(), testMethod.Name, runnerInstance.TestMethodArguments, testMethod.GetParameters());
        }
    }
}
