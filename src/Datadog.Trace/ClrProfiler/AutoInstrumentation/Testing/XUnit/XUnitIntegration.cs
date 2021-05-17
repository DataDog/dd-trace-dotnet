// <copyright file="XUnitIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Reflection;
using Datadog.Trace.Ci;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit
{
    internal static class XUnitIntegration
    {
        internal const string IntegrationName = nameof(IntegrationIds.XUnit);
        internal static readonly IntegrationInfo IntegrationId = IntegrationRegistry.GetIntegrationInfo(IntegrationName);

        internal static bool IsEnabled => Common.TestTracer.Settings.IsIntegrationEnabled(IntegrationId);

        internal static Scope CreateScope(ref TestRunnerStruct runnerInstance, Type targetType)
        {
            string testSuite = runnerInstance.TestClass.ToString();
            string testName = runnerInstance.TestMethod.Name;

            AssemblyName testInvokerAssemblyName = targetType.Assembly.GetName();

            string testFramework = "xUnit " + testInvokerAssemblyName.Version.ToString();

            Scope scope = Common.TestTracer.StartActive("xunit.test", serviceName: Common.TestTracer.DefaultServiceName);
            Span span = scope.Span;

            span.Type = SpanTypes.Test;
            span.SetTraceSamplingPriority(SamplingPriority.AutoKeep);
            span.ResourceName = $"{testSuite}.{testName}";
            span.SetTag(Tags.Origin, TestTags.CIAppTestOriginName);
            span.SetTag(TestTags.Suite, testSuite);
            span.SetTag(TestTags.Name, testName);
            span.SetTag(TestTags.Framework, testFramework);
            span.SetTag(TestTags.Type, TestTags.TypeTest);
            CIEnvironmentValues.DecorateSpan(span);

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

            // Skip tests
            if (runnerInstance.SkipReason != null)
            {
                span.SetTag(TestTags.Status, TestTags.StatusSkip);
                span.SetTag(TestTags.SkipReason, runnerInstance.SkipReason);
                span.Finish(new TimeSpan(10));
                scope.Dispose();
                return null;
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
    }
}
