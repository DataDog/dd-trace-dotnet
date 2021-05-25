// <copyright file="NUnitIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Datadog.Trace.Ci;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.NUnit
{
    internal static class NUnitIntegration
    {
        internal const string IntegrationName = nameof(IntegrationIds.NUnit);
        internal static readonly IntegrationInfo IntegrationId = IntegrationRegistry.GetIntegrationInfo(IntegrationName);
        internal static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(NUnitIntegration));

        internal static bool IsEnabled => Common.TestTracer.Settings.IsIntegrationEnabled(IntegrationId);

        internal static Scope CreateScope(ITest currentTest, Type targetType)
        {
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

            Scope scope = Common.TestTracer.StartActive("nunit.test", serviceName: Common.TestTracer.DefaultServiceName);
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
            ParameterInfo[] methodParameters = testMethod.GetParameters();
            if (methodParameters?.Length > 0)
            {
                TestParameters testParameters = new TestParameters();
                testParameters.Metadata = new Dictionary<string, object>();
                testParameters.Arguments = new Dictionary<string, object>();
                testParameters.Metadata[TestTags.MetadataTestName] = currentTest.Name;

                for (int i = 0; i < methodParameters.Length; i++)
                {
                    if (testMethodArguments != null && i < testMethodArguments.Length)
                    {
                        testParameters.Arguments[methodParameters[i].Name] = testMethodArguments[i]?.ToString() ?? "(null)";
                    }
                    else
                    {
                        testParameters.Arguments[methodParameters[i].Name] = "(default)";
                    }
                }

                span.SetTag(TestTags.Parameters, testParameters.ToJSON());
            }

            // Get traits
            if (testMethodProperties != null)
            {
                Dictionary<string, List<string>> traits = new Dictionary<string, List<string>>();
                skipReason = (string)testMethodProperties.Get("_SKIPREASON");
                foreach (var key in testMethodProperties.Keys)
                {
                    if (key == "_SKIPREASON" || key == "_JOINTYPE")
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

                        traits[key] = lstValues;
                    }
                    else
                    {
                        traits[key] = null;
                    }
                }

                if (traits.Count > 0)
                {
                    span.SetTag(TestTags.Traits, Datadog.Trace.Vendors.Newtonsoft.Json.JsonConvert.SerializeObject(traits));
                }
            }

            if (skipReason != null)
            {
                FinishSkippedScope(scope, skipReason);
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

            if (ex != null)
            {
                string exTypeName = ex.GetType().FullName;

                if (exTypeName == "NUnit.Framework.SuccessException")
                {
                    scope.Span.SetTag(TestTags.Status, TestTags.StatusPass);
                }
                else if (exTypeName == "NUnit.Framework.IgnoreException")
                {
                    scope.Span.SetTag(TestTags.Status, TestTags.StatusSkip);
                    scope.Span.SetTag(TestTags.SkipReason, ex.Message);
                }
                else
                {
                    scope.Span.SetException(ex);
                    scope.Span.SetTag(TestTags.Status, TestTags.StatusFail);
                }
            }
            else
            {
                scope.Span.SetTag(TestTags.Status, TestTags.StatusPass);
            }
        }

        internal static void FinishSkippedScope(Scope scope, string skipReason)
        {
            var span = scope?.Span;
            if (span != null)
            {
                span.SetTag(TestTags.Status, TestTags.StatusSkip);
                span.SetTag(TestTags.SkipReason, skipReason ?? string.Empty);
                span.Finish(new TimeSpan(10));
                scope.Dispose();
            }
        }
    }
}
