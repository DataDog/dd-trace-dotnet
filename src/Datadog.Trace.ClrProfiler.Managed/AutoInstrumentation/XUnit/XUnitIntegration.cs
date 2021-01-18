using System;
using System.Collections.Generic;
using System.Reflection;
using Datadog.Trace.Ci;
using Datadog.Trace.ExtensionMethods;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.XUnit
{
    internal static class XUnitIntegration
    {
        static XUnitIntegration()
        {
            // Preload environment variables.
            CIEnvironmentValues.DecorateSpan(null);
        }

        internal static Scope CreateScope(ref TestRunnerStruct runnerInstance, Type targetType)
        {
            string testSuite = runnerInstance.TestClass.ToString();
            string testName = runnerInstance.TestMethod.Name;

            AssemblyName testInvokerAssemblyName = targetType.Assembly.GetName();

            Tracer tracer = Tracer.Instance;
            string testFramework = "xUnit " + testInvokerAssemblyName.Version.ToString();

            Scope scope = tracer.StartActive("xunit.test");
            Span span = scope.Span;

            span.Type = SpanTypes.Test;
            span.SetTraceSamplingPriority(SamplingPriority.AutoKeep);
            span.ResourceName = $"{testSuite}.{testName}";
            span.SetTag(TestTags.Suite, testSuite);
            span.SetTag(TestTags.Name, testName);
            span.SetTag(TestTags.Framework, testFramework);
            span.SetTag(TestTags.Type, TestTags.TypeTest);
            CIEnvironmentValues.DecorateSpan(span);

            var framework = FrameworkDescription.Instance;

            span.SetTag(CommonTags.RuntimeName, framework.Name);
            span.SetTag(CommonTags.RuntimeOSArchitecture, framework.OSArchitecture);
            span.SetTag(CommonTags.RuntimeOSPlatform, framework.OSPlatform);
            span.SetTag(CommonTags.RuntimeProcessArchitecture, framework.ProcessArchitecture);
            span.SetTag(CommonTags.RuntimeVersion, framework.ProductVersion);

            // Get test parameters
            object[] testMethodArguments = runnerInstance.TestMethodArguments;
            ParameterInfo[] methodParameters = runnerInstance.TestMethod.GetParameters();
            if (methodParameters?.Length > 0 && testMethodArguments?.Length > 0)
            {
                for (int i = 0; i < methodParameters.Length; i++)
                {
                    if (i < testMethodArguments.Length)
                    {
                        span.SetTag($"{TestTags.Arguments}.{methodParameters[i].Name}", testMethodArguments[i]?.ToString() ?? "(null)");
                    }
                    else
                    {
                        span.SetTag($"{TestTags.Arguments}.{methodParameters[i].Name}", "(default)");
                    }
                }
            }

            // Get traits
            Dictionary<string, List<string>> traits = runnerInstance.TestCase.Traits;
            if (traits.Count > 0)
            {
                foreach (KeyValuePair<string, List<string>> traitValue in traits)
                {
                    span.SetTag($"{TestTags.Traits}.{traitValue.Key}", string.Join(", ", traitValue.Value) ?? "(null)");
                }
            }

            // Skip tests
            if (runnerInstance.SkipReason != null)
            {
                span.SetTag(TestTags.Status, TestTags.StatusSkip);
                span.SetTag(TestTags.SkipReason, runnerInstance.SkipReason);
                span.Finish(TimeSpan.Zero);
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
