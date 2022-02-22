// <copyright file="MsTestIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging;
using Datadog.Trace.Pdb;
using Datadog.Trace.Sampling;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.MsTestV2
{
    internal static class MsTestIntegration
    {
        internal const string IntegrationName = nameof(Configuration.IntegrationId.MsTestV2);
        internal const IntegrationId IntegrationId = Configuration.IntegrationId.MsTestV2;
        internal static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(MsTestIntegration));

        internal static readonly ThreadLocal<object> IsTestMethodRunnableThreadLocal = new();

        internal static bool IsEnabled => CIVisibility.IsRunning && Tracer.Instance.Settings.IsIntegrationEnabled(IntegrationId);

        internal static Scope OnMethodBegin<TTestMethod>(TTestMethod testMethodInfo, Type type)
            where TTestMethod : ITestMethod
        {
            MethodInfo testMethod = testMethodInfo.MethodInfo;
            object[] testMethodArguments = testMethodInfo.Arguments;

            string testFramework = "MSTestV2";
            string testBundle = testMethodInfo.MethodInfo?.DeclaringType?.Assembly?.GetName().Name;
            string testSuite = testMethodInfo.TestClassName;
            string testName = testMethodInfo.TestMethodName;

            Scope scope = Tracer.Instance.StartActiveInternal("mstest.test");
            Span span = scope.Span;

            span.Type = SpanTypes.Test;
            span.SetTraceSamplingDecision(SamplingPriorityValues.AutoKeep, SamplingMechanism.CiApp);
            span.ResourceName = $"{testSuite}.{testName}";
            span.SetTag(Tags.Origin, TestTags.CIAppTestOriginName);
            span.SetTag(TestTags.Bundle, testBundle);
            span.SetTag(TestTags.Suite, testSuite);
            span.SetTag(TestTags.Name, testName);
            span.SetTag(TestTags.Framework, testFramework);
            span.SetTag(TestTags.FrameworkVersion, type.Assembly?.GetName().Version.ToString());
            span.SetTag(TestTags.Type, TestTags.TypeTest);
            CIEnvironmentValues.Instance.DecorateSpan(span);

            var framework = FrameworkDescription.Instance;
            span.SetTag(CommonTags.LibraryVersion, TracerConstants.AssemblyVersion);
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

                for (int i = 0; i < methodParameters.Length; i++)
                {
                    if (testMethodArguments != null && i < testMethodArguments.Length)
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
            Dictionary<string, List<string>> testTraits = GetTraits(testMethod);
            if (testTraits != null && testTraits.Count > 0)
            {
                span.SetTag(TestTags.Traits, Datadog.Trace.Vendors.Newtonsoft.Json.JsonConvert.SerializeObject(testTraits));
            }

            // Test code and code owners
            Common.DecorateSpanWithSourceAndCodeOwners(span, testMethod);

            Tracer.Instance.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId);
            Common.StartCoverage();
            span.ResetStartTime();
            return scope;
        }

        private static Dictionary<string, List<string>> GetTraits(MethodInfo methodInfo)
        {
            Dictionary<string, List<string>> testProperties = null;
            try
            {
                var testAttributes = methodInfo.GetCustomAttributes(true);

                foreach (var tattr in testAttributes)
                {
                    var tAttrName = tattr.GetType().Name;

                    if (tAttrName == "TestCategoryAttribute")
                    {
                        testProperties ??= new Dictionary<string, List<string>>();
                        if (!testProperties.TryGetValue("Category", out var categoryList))
                        {
                            categoryList = new List<string>();
                            testProperties["Category"] = categoryList;
                        }

                        if (tattr.TryDuckCast<TestCategoryAttributeStruct>(out var tattrStruct))
                        {
                            categoryList.AddRange(tattrStruct.TestCategories);
                        }
                    }

                    if (tAttrName == "TestPropertyAttribute")
                    {
                        testProperties ??= new Dictionary<string, List<string>>();
                        if (tattr.TryDuckCast<TestPropertyAttributeStruct>(out var tattrStruct) && tattrStruct.Name != null)
                        {
                            if (!testProperties.TryGetValue(tattrStruct.Name, out var propertyList))
                            {
                                propertyList = new List<string>();
                                testProperties[tattrStruct.Name] = propertyList;
                            }

                            propertyList.Add(tattrStruct.Value ?? "(empty)");
                        }
                    }
                }

                var classCategories = methodInfo.DeclaringType?.GetCustomAttributes(true);
                if (classCategories is not null)
                {
                    foreach (var tattr in classCategories)
                    {
                        var tAttrName = tattr.GetType().Name;
                        if (tAttrName == "TestCategoryAttribute")
                        {
                            testProperties ??= new Dictionary<string, List<string>>();
                            if (!testProperties.TryGetValue("Category", out var categoryList))
                            {
                                categoryList = new List<string>();
                                testProperties["Category"] = categoryList;
                            }

                            if (tattr.TryDuckCast<TestCategoryAttributeStruct>(out var tattrStruct))
                            {
                                categoryList.AddRange(tattrStruct.TestCategories);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, ex.Message);
            }

            return testProperties;
        }
    }
}
