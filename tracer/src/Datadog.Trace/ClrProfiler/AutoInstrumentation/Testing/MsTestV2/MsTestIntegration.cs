// <copyright file="MsTestIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
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

    internal static readonly ThreadLocal<MethodInfoCacheItem> IsTestMethodRunnableThreadLocal = new();

    internal static readonly ConditionalWeakTable<object, TestModule> TestModuleByTestAssemblyInfos = new();
    internal static readonly ConditionalWeakTable<object, TestSuite> TestSuiteByTestClassInfos = new();

    private static readonly Dictionary<string, string> MsTestVersionByModuleId = new()
    {
        ["ac1340e1-462b-4480-ac43-8e76805462fb"] = "1.4.0",
        ["329476f1-575d-44d9-a739-1642e867e4ae"] = "2.0.0",
        ["6da31905-7bda-4426-9718-8b227d1459ed"] = "2.1.0",
        ["31f86fec-2f3d-4f5f-a4c3-5ec01361289b"] = "2.1.1",
        ["34618455-3692-446e-965f-f6a103113a53"] = "2.1.2",
        ["18322e86-0271-44e9-a9b8-0b699d39647c"] = "2.2.1",
        ["ad41042f-522c-4a01-9198-e4e1d74f4d78"] = "2.2.2",
        ["6476fc3e-b436-4c36-896a-ab6f57e16ecc"] = "2.2.3",
        ["6002ad1a-da72-4178-89fe-3598b8df09d7"] = "2.2.4",
        ["cc144791-2265-41dc-97ea-b19d3254a037"] = "2.2.5",
        ["99cbd7af-0f2e-4019-8a0e-67c467f96658"] = "2.2.6",
        ["26671011-cacf-4203-8b5a-7d023d37851c"] = "2.2.7",
        ["60e77dfc-476b-4b69-937f-e6b73366af47"] = "2.2.8",
        ["a6135a87-904b-4976-b9cd-2ed60305f9b6"] = "2.2.9",
        ["4472e9d6-a19b-4c92-bcc9-a80bd60ca17d"] = "2.2.10",
        ["43b5503d-b0bb-4bd3-a538-a42c1996331c"] = "3.0.0",
        ["058e8d58-ca0b-437f-bbeb-e3dac0622385"] = "3.0.1",
        ["1fc9e418-9b26-4b39-bd75-982a2b47ce8f"] = "3.0.2",
        ["fca00577-fd3c-4b9a-b20f-13210bcb2bb0"] = "3.0.3",
        ["7a68ce0a-7f00-4fd2-9a39-e7e22579b7e6"] = "3.0.4",
        ["e78be70d-3050-48cb-9914-e5b7afd525a2"] = "3.1.1",
        ["76f80564-bfa8-4c6a-810d-fb8d8ff9904b"] = "3.2.0",
        ["601b9a9e-4ec5-4d00-bd02-b9990a2ef6c1"] = "3.2.1",
        ["82f48315-774a-4e06-afb3-f1f684eca38d"] = "3.2.2",
        ["82c42f21-febe-4eb2-80ad-8e793eabd8f2"] = "3.3.0",
        ["139449f1-8ab4-46b1-bf76-1a0e70ed75c7"] = "3.3.1",
        ["f5fedf4d-dd4d-4086-956b-a288dfe47482"] = "3.6.0"
    };

    private static long _totalTestCases;
    private static long _newTestCases;

    internal static bool IsEnabled => CIVisibility.IsRunning && Tracer.Instance.Settings.IsIntegrationEnabled(IntegrationId);

    internal static Test OnMethodBegin<TTestMethod>(TTestMethod testMethodInstance, Type type, bool isRetry, DateTimeOffset? startDate = null)
        where TTestMethod : ITestMethod
    {
        var testMethod = testMethodInstance.MethodInfo;
        var testName = testMethodInstance.TestMethodName;

        var suite = TestSuite.Current;
        if (suite is null && testMethodInstance.Instance.TryDuckCast<ITestMethodInfo>(out var testMethodInfo))
        {
            suite = GetOrCreateTestSuiteFromTestClassInfo(testMethodInfo.Parent);
        }

        if (suite is null)
        {
            Log.Warning("There's no suite to create the Test instance.");
            return null;
        }

        var test = startDate is null ? suite.InternalCreateTest(testName) : suite.InternalCreateTest(testName, startDate.Value);

        // Get test parameters
        UpdateTestParameters(test, testMethodInstance);

        // Get traits
        if (GetTraits(testMethod) is { } testTraits)
        {
            // Unskippable tests
            if (CIVisibility.Settings.IntelligentTestRunnerEnabled)
            {
                ShouldSkip(testMethodInstance, out var isUnskippable, out var isForcedRun, testTraits);
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

        // Early flake detection flags
        Common.SetEarlyFlakeDetectionTestTagsAndAbortReason(test, isRetry, ref _newTestCases, ref _totalTestCases);

        // Flaky retry
        Common.SetFlakyRetryTags(test, isRetry);

        // Set test method
        test.SetTestMethodInfo(testMethod);

        test.ResetStartTime();
        return test;
    }

    internal static void UpdateTestParameters<TTestMethod>(Test test, TTestMethod testMethodInstance, string displayName = null)
        where TTestMethod : ITestMethod
    {
        var testMethod = testMethodInstance.MethodInfo;
        var testMethodArguments = testMethodInstance.Arguments;
        var testName = testMethodInstance.TestMethodName;

        // Get test parameters
        var methodParameters = testMethod.GetParameters();
        if (methodParameters?.Length > 0)
        {
            var testParameters = new TestParameters
            {
                Metadata = new Dictionary<string, object>(),
                Arguments = new Dictionary<string, object>()
            };

            if (!string.IsNullOrEmpty(displayName) && displayName != testName)
            {
                testParameters.Metadata[TestTags.MetadataTestName] = displayName;
            }

            for (var i = 0; i < methodParameters.Length; i++)
            {
                if (testMethodArguments != null && i < testMethodArguments.Length)
                {
                    testParameters.Arguments[methodParameters[i].Name ?? i.ToString(CultureInfo.InvariantCulture)] = Common.GetParametersValueData(testMethodArguments[i]);
                }
                else
                {
                    testParameters.Arguments[methodParameters[i].Name ?? i.ToString(CultureInfo.InvariantCulture)] = "(default)";
                }
            }

            test.SetParameters(testParameters);
        }
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

    internal static TestModule GetOrCreateTestModuleFromTestAssemblyInfo<TAsmInfo>(TAsmInfo testAssemblyInfo, string assemblyName = null)
        where TAsmInfo : ITestAssemblyInfo
    {
        if (testAssemblyInfo.Instance is not { } objTestAssemblyInfo)
        {
            return default;
        }

        CIVisibility.WaitForSkippableTaskToFinish();

        return TestModuleByTestAssemblyInfos.GetValue(
            objTestAssemblyInfo,
            key =>
            {
                if (assemblyName is not null)
                {
                    assemblyName = AssemblyName.GetAssemblyName(assemblyName).Name ?? string.Empty;
                }
                else
                {
                    assemblyName = string.Empty;
                }

                var testAssembly = testAssemblyInfo.Type.Assembly;
                var frameworkVersion = testAssembly.GetName().Version?.ToString() ?? string.Empty;
                foreach (var module in testAssembly.Modules)
                {
                    if (MsTestVersionByModuleId.TryGetValue(module.ModuleVersionId.ToString(), out var actualVersion))
                    {
                        frameworkVersion = actualVersion;
                        break;
                    }

                    Common.Log.Warning("MSTest framework version could not be detected. MVID: {ModuleVersionId}", module.ModuleVersionId);
                }

                Common.Log.Debug("Module: {Module}, Framework version: {Version}", assemblyName, frameworkVersion);
                var newModule = TestModule.InternalCreate(assemblyName, CommonTags.TestingFrameworkNameMsTestV2, frameworkVersion);
                newModule.EnableIpcClient();
                return newModule;
            });
    }

    internal static TestSuite GetOrCreateTestSuiteFromTestClassInfo<TClassInfo>(TClassInfo testClassInfo)
        where TClassInfo : ITestClassInfo
    {
        if (testClassInfo.Instance is not { } objTestClassInfo)
        {
            return default;
        }

        return TestSuiteByTestClassInfos.GetValue(
            objTestClassInfo,
            key =>
            {
                var module = TestModule.Current ?? GetOrCreateTestModuleFromTestAssemblyInfo(testClassInfo.Parent, testClassInfo.ClassType.Assembly.FullName);
                if (module is null)
                {
                    Common.Log.Error("There is no current module, a new suite cannot be created.");
                    return default;
                }

                var classTypeName = testClassInfo.ClassType?.FullName ?? throw new NullReferenceException("ClassType is null, a new suite cannot be created.");
                return module.InternalGetOrCreateSuite(classTypeName);
            });
    }

    internal static void AddTotalTestCases(int count)
    {
        Interlocked.Add(ref _totalTestCases, count);
    }
}
