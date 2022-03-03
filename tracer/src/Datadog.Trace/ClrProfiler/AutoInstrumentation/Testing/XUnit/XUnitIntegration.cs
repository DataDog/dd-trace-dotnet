// <copyright file="XUnitIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging.DirectSubmission;
using Datadog.Trace.Logging.DirectSubmission.Formatting;
using Datadog.Trace.Logging.DirectSubmission.Sink;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit
{
    internal static class XUnitIntegration
    {
        internal const string IntegrationName = nameof(Configuration.IntegrationId.XUnit);
        internal const IntegrationId IntegrationId = Configuration.IntegrationId.XUnit;

        internal static bool IsEnabled => CIVisibility.IsRunning && Tracer.Instance.Settings.IsIntegrationEnabled(IntegrationId);

        internal static Scope CreateScope(ref TestRunnerStruct runnerInstance, Type targetType)
        {
            string testSuite = runnerInstance.TestClass.ToString();
            string testName = runnerInstance.TestMethod.Name;

            string testFramework = "xUnit";

            Scope scope = Tracer.Instance.StartActiveInternal("xunit.test");
            Span span = scope.Span;

            span.Type = SpanTypes.Test;
            span.SetTraceSamplingPriority(SamplingPriorityValues.AutoKeep);
            span.ResourceName = $"{testSuite}.{testName}";
            span.SetTag(Tags.Origin, TestTags.CIAppTestOriginName);
            span.SetTag(Tags.Language, TracerConstants.Language);
            span.SetTag(TestTags.Suite, testSuite);
            span.SetTag(TestTags.Name, testName);
            span.SetTag(TestTags.Framework, testFramework);
            span.SetTag(TestTags.FrameworkVersion, targetType.Assembly?.GetName().Version.ToString());
            span.SetTag(TestTags.Type, TestTags.TypeTest);
            span.SetTag(CommonTags.LibraryVersion, TracerConstants.AssemblyVersion);
            CIEnvironmentValues.Instance.DecorateSpan(span);

            var framework = FrameworkDescription.Instance;

            span.SetTag(CommonTags.RuntimeName, framework.Name);
            span.SetTag(CommonTags.RuntimeVersion, framework.ProductVersion);
            span.SetTag(CommonTags.RuntimeArchitecture, framework.ProcessArchitecture);
            span.SetTag(CommonTags.OSArchitecture, framework.OSArchitecture);
            span.SetTag(CommonTags.OSPlatform, framework.OSPlatform);
            span.SetTag(CommonTags.OSVersion, Environment.OSVersion.VersionString);

            // Get test parameters
            object[] testMethodArguments = runnerInstance.TestMethodArguments;
            ParameterInfo[] methodParameters = runnerInstance.TestMethod.GetParameters();
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

            Tracer.Instance.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId);

            // Skip tests
            if (runnerInstance.SkipReason != null)
            {
                span.SetTag(TestTags.Status, TestTags.StatusSkip);
                span.SetTag(TestTags.SkipReason, runnerInstance.SkipReason);
                span.Finish(new TimeSpan(10));
                scope.Dispose();
                return null;
            }

            var sink = Tracer.Instance.TracerManager.DirectLogSubmission.Sink;
            for (var i = 0; i < 10; i++)
            {
                sink.EnqueueLog(new XUnitLogEvent("Hello", span));
                sink.EnqueueLog(new XUnitLogEvent("World", span));
            }

            span.ResetStartTime();
            return scope;
        }

        internal static void FinishScope(Scope scope, IExceptionAggregator exceptionAggregator)
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

            scope.Dispose();
        }

        private class XUnitLogEvent : DatadogLogEvent
        {
            private readonly string _message;
            private readonly Span _span;

            public XUnitLogEvent(string message, Span span)
            {
                _message = message;
                _span = span;
            }

            public override void Format(StringBuilder sb, LogFormatter formatter)
            {
                formatter.FormatLog<Span>(
                    sb,
                    _span,
                    DateTime.UtcNow,
                    _message,
                    null,
                    DirectSubmissionLogLevelExtensions.Information,
                    null,
                    (JsonTextWriter writer, in Span state) =>
                    {
                        writer.WritePropertyName("dd.trace_id");
                        writer.WriteValue($"{state.TraceId}");
                        writer.WritePropertyName("dd.span_id");
                        writer.WriteValue($"{state.SpanId}");
                        return default;
                    });
            }
        }
    }
}
