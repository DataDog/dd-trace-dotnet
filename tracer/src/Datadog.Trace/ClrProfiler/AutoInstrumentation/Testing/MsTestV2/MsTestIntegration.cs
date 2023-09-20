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
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.MsTestV2;

internal static class MsTestIntegration
{
    internal const string IntegrationName = nameof(Configuration.IntegrationId.MsTestV2);
    internal const IntegrationId IntegrationId = Configuration.IntegrationId.MsTestV2;
    internal static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(MsTestIntegration));

    internal static readonly ThreadLocal<object> IsTestMethodRunnableThreadLocal = new();

    internal static bool IsEnabled => CIVisibility.IsRunning && Tracer.Instance.Settings.IsIntegrationEnabled(IntegrationId);

    internal static Test OnMethodBegin<TTestMethod>(TTestMethod testMethodInfo, Type type)
        where TTestMethod : ITestMethod
    {
        var testMethod = testMethodInfo.MethodInfo;
        var testMethodArguments = testMethodInfo.Arguments;
        var testName = testMethodInfo.TestMethodName;

        if (TestSuite.Current is { } suite)
        {
            var test = suite.CreateTest(testName);

            // Get test parameters
            var methodParameters = testMethod.GetParameters();
            if (methodParameters?.Length > 0)
            {
                var testParameters = new TestParameters();
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

                test.SetParameters(testParameters);
            }

            // Get traits
            if (GetTraits(testMethod) is { } testTraits)
            {
                // Unskippable tests
                if (CIVisibility.Settings.IntelligentTestRunnerEnabled)
                {
                    ShouldSkip(testMethodInfo, out var isUnskippable, out var isForcedRun, testTraits);
                    test.SetTag(IntelligentTestRunnerTags.UnskippableTag, isUnskippable ? "true" : "false");
                    test.SetTag(IntelligentTestRunnerTags.ForcedRunTag, isForcedRun ? "true" : "false");
                    testTraits.Remove(IntelligentTestRunnerTags.UnskippableTraitName);
                }

                test.SetTraits(testTraits);
            }
            else if (CIVisibility.Settings.IntelligentTestRunnerEnabled)
            {
                // Unskippable tests
                test.SetTag(IntelligentTestRunnerTags.UnskippableTag, "false");
                test.SetTag(IntelligentTestRunnerTags.ForcedRunTag, "false");
            }

            // Set test method
            test.SetTestMethodInfo(testMethod);

            test.ResetStartTime();
            return test;
        }
        else
        {
            Log.Warning("There's no suite to create the Test instance.");
        }

        return null;
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
            Log.Error(ex, "Error reading MSTest traits");
        }

        return testProperties;
    }

    internal static bool ShouldSkip<TTestMethod>(TTestMethod testMethodInfo, out bool isUnskippable, out bool isForcedRun, Dictionary<string, List<string>> traits = null)
        where TTestMethod : ITestMethod
    {
        isUnskippable = false;
        isForcedRun = false;

        if (CIVisibility.Settings.IntelligentTestRunnerEnabled != true)
        {
            return false;
        }

        var testClass = testMethodInfo.TestClassName;
        var testMethod = testMethodInfo.MethodInfo;
        var itrShouldSkip = Common.ShouldSkip(testClass ?? string.Empty, testMethod?.Name ?? string.Empty, testMethodInfo.Arguments, testMethod?.GetParameters());
        traits ??= GetTraits(testMethod);
        isUnskippable = traits?.TryGetValue(IntelligentTestRunnerTags.UnskippableTraitName, out _) == true;
        isForcedRun = itrShouldSkip && isUnskippable;
        return itrShouldSkip && !isUnskippable;
    }
}
