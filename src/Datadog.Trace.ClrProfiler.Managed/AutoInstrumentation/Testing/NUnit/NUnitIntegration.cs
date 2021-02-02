using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Datadog.Trace.Ci;
using Datadog.Trace.ExtensionMethods;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.NUnit
{
    internal static class NUnitIntegration
    {
        static NUnitIntegration()
        {
            // Preload environment variables.
            CIEnvironmentValues.DecorateSpan(null);
        }

        internal static Scope CreateScope<TContext>(TContext executionContext, Type targetType)
            where TContext : ITestExecutionContext
        {
            ITest currentTest = executionContext.CurrentTest;
            MethodInfo testMethod = currentTest.Method.MethodInfo;
            object[] testMethodArguments = currentTest.Arguments;
            IPropertyBag testMethodProperties = currentTest.Properties;

            if (testMethod == null)
            {
                return null;
            }

            string testFramework = "NUnit " + targetType?.Assembly?.GetName().Version;
            string testSuite = testMethod.DeclaringType?.FullName;
            string testName = testMethod.Name;
            string skipReason = null;

            Tracer tracer = Tracer.Instance;
            Scope scope = tracer.StartActive("nunit.test");
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
            ParameterInfo[] methodParameters = testMethod.GetParameters();
            if (methodParameters?.Length > 0)
            {
                for (int i = 0; i < methodParameters.Length; i++)
                {
                    if (testMethodArguments != null && i < testMethodArguments.Length)
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
            if (testMethodProperties != null)
            {
                skipReason = (string)testMethodProperties.Get("_SKIPREASON");
                foreach (var key in testMethodProperties.Keys)
                {
                    if (key == "_SKIPREASON")
                    {
                        continue;
                    }

                    IList value = testMethodProperties[key];
                    if (value != null)
                    {
                        List<string> lstValues = new List<string>();
                        foreach (object valObj in value)
                        {
                            if (valObj is null)
                            {
                                continue;
                            }

                            lstValues.Add(valObj.ToString());
                        }

                        span.SetTag($"{TestTags.Traits}.{key}", string.Join(", ", lstValues));
                    }
                    else
                    {
                        span.SetTag($"{TestTags.Traits}.{key}", "(null)");
                    }
                }
            }

            if (skipReason != null)
            {
                span.SetTag(TestTags.Status, TestTags.StatusSkip);
                span.SetTag(TestTags.SkipReason, skipReason);
                span.Finish(TimeSpan.Zero);
                scope.Dispose();
                scope = null;
            }

            span.ResetStartTime();
            return scope;
        }

        internal static void FinishScope(Scope scope, Exception ex)
        {
            // unwrap the generic NUnitException
            if (ex != null && ex.GetType().FullName == "NUnit.Framework.Internal.NUnitException")
            {
                ex = ex.InnerException;
            }

            switch (ex?.GetType().FullName)
            {
                case "NUnit.Framework.SuccessException":
                    scope.Span.SetTag(TestTags.Status, TestTags.StatusPass);
                    break;
                case "NUnit.Framework.IgnoreException":
                    scope.Span.SetTag(TestTags.Status, TestTags.StatusSkip);
                    break;
                default:
                    scope.Span.SetException(ex);
                    scope.Span.SetTag(TestTags.Status, TestTags.StatusFail);
                    break;
            }
        }
    }
}
