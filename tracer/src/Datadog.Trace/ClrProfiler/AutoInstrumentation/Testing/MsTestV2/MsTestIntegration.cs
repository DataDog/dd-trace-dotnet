// <copyright file="MsTestIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.Net;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.dnlib.DotNet;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.MsTestV2;

internal static class MsTestIntegration
{
    internal const string IntegrationName = nameof(Configuration.IntegrationId.MsTestV2);
    internal const IntegrationId IntegrationId = Configuration.IntegrationId.MsTestV2;
    internal static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(MsTestIntegration));

    internal static readonly ThreadLocal<MethodInfoCacheItem?> IsTestMethodRunnableThreadLocal = new();

    internal static readonly ConditionalWeakTable<object, TestModule?> TestModuleByTestAssemblyInfos = new();
    internal static readonly ConditionalWeakTable<object, TestSuite?> TestSuiteByTestClassInfos = new();

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
        ["545e509a-217d-4ae4-8e9c-44db060476bd"] = "3.4.0",
        ["1f5cd8fe-a77b-4cea-820e-edc966404148"] = "3.4.1",
        ["9ea7951a-d348-4320-add8-5f252ef638f5"] = "3.4.2",
        ["7e686320-f8ad-4bc0-bba1-924a5f80997a"] = "3.4.3",
        ["f97b48eb-3d66-49ec-883e-024105346c6a"] = "3.5.0",
        ["a27d601a-225f-46ac-a239-5cd1eb8f0e94"] = "3.5.1",
        ["809c6892-a3d4-4d2c-a814-f5161e1be707"] = "3.5.2",
        ["f5fedf4d-dd4d-4086-956b-a288dfe47482"] = "3.6.0",
        ["bfa965bf-7fea-4f88-a983-13899039abb0"] = "3.6.1",
        ["6996b6b2-0966-4698-a69e-2adc20bda49f"] = "3.6.2",
        ["cebe03e2-ed3e-4b48-889d-87bbe5c46fca"] = "3.6.3",
        ["ea501b1a-9500-43b9-9f3d-f638f1824d61"] = "3.6.4",
        ["931a3acb-90ac-4596-8d38-205ed6130bb8"] = "3.7.0",
        ["cc38e312-58a9-4de7-a090-66bf379cb735"] = "3.7.1",
        ["1413c06e-64df-4d94-bb85-a2dc568260d5"] = "3.7.2",
        ["1c2a401f-8769-4295-a96e-b842f45bbeac"] = "3.7.3",
        ["b754cc51-34ed-419c-8582-bff04c3db05f"] = "3.8.0",
        ["2b3d62e3-5607-4ebd-840e-ee80475cc0bc"] = "3.8.1",
        ["3fe23123-93a2-4c44-8219-0a5f27a10316"] = "3.8.2",
        ["102f7a9d-d61b-4864-b3c7-0a097a85f47b"] = "3.9.0",
        ["e7bba6ac-32f6-4e62-9a3a-cf259c9e7448"] = "3.9.1",
        ["71c969a3-3c6d-4215-87ef-48dd76421ad8"] = "3.10.0",
    };

    private static long _totalTestCases;
    private static long _newTestCases;

    internal static bool IsEnabled => TestOptimization.Instance.IsRunning && Tracer.Instance.CurrentTraceSettings.Settings.IsIntegrationEnabled(IntegrationId);

    internal static Test? OnMethodBegin<TTestMethod>(TTestMethod testMethodInstance, Type type, bool isRetry, DateTimeOffset? startDate = null)
        where TTestMethod : ITestMethod
    {
        var testMethod = testMethodInstance.MethodInfo;
        var testName = testMethodInstance.TestMethodName ?? string.Empty;

        var suite = TestSuite.Current;
        if (suite is null && testMethodInstance.Instance.TryDuckCast<ITestMethodInfoWithParent>(out var testMethodInfo))
        {
            suite = GetOrCreateTestSuiteFromTestClassInfo(testMethodInfo.Parent);
        }

        if (suite is null)
        {
            Log.Warning("There's no suite to create the Test instance.");
            return null;
        }

        var test = startDate is null ? suite.CreateTest(testName) : suite.CreateTest(testName, startDate.Value);
        var testTags = test.GetTags();

        // Get test parameters
        UpdateTestParameters(test, testMethodInstance);

        // Get traits
        if (GetTraits(testMethod) is { } testTraits)
        {
            // Unskippable tests
            if (TestOptimization.Instance.Settings.IntelligentTestRunnerEnabled)
            {
                ShouldSkip(testMethodInstance, out var isUnskippable, out var isForcedRun, testTraits);
                testTags.Unskippable = isUnskippable ? "true" : "false";
                testTags.ForcedRun = isForcedRun ? "true" : "false";
                testTraits.Remove(IntelligentTestRunnerTags.UnskippableTraitName);
            }

            test.SetTraits(testTraits);
        }
        else if (TestOptimization.Instance.Settings.IntelligentTestRunnerEnabled)
        {
            // Unskippable tests
            testTags.Unskippable = "false";
            testTags.ForcedRun = "false";
        }

        // Set known tests feature tags
        Common.SetKnownTestsFeatureTags(test);

        // Early flake detection flags
        Common.SetEarlyFlakeDetectionTestTagsAndAbortReason(test, isRetry, ref _newTestCases, ref _totalTestCases);

        // Flaky retry
        Common.SetFlakyRetryTags(test, isRetry);

        // Test management feature
        Common.SetTestManagementFeature(test, isRetry);

        // Set test method
        if (testMethod is not null)
        {
            test.SetTestMethodInfo(testMethod);
        }

        test.ResetStartTime();
        return test;
    }

    internal static void UpdateTestParameters<TTestMethod>(Test test, TTestMethod testMethodInstance, string? displayName = null)
        where TTestMethod : ITestMethod
    {
        var testMethod = testMethodInstance.MethodInfo;
        var testMethodArguments = testMethodInstance.Arguments;
        var testName = testMethodInstance.TestMethodName;

        // Get test parameters
        var methodParameters = testMethod?.GetParameters();
        if (methodParameters?.Length > 0)
        {
            var testParameters = new TestParameters
            {
                Metadata = new Dictionary<string, object?>(),
                Arguments = new Dictionary<string, object?>()
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

    private static Dictionary<string, List<string>?>? GetTraits(MethodInfo? methodInfo)
    {
        if (methodInfo is null)
        {
            return null;
        }

        Dictionary<string, List<string>?>? testProperties = null;
        try
        {
            var testAttributes = methodInfo.GetCustomAttributes(true);

            foreach (var tattr in testAttributes)
            {
                var tAttrName = tattr.GetType().Name;

                if (tAttrName == "TestCategoryAttribute")
                {
                    testProperties ??= new Dictionary<string, List<string>?>();
                    if (!testProperties.TryGetValue("Category", out var categoryList))
                    {
                        categoryList = [];
                        testProperties["Category"] = categoryList;
                    }

                    if (tattr.TryDuckCast<TestCategoryAttributeStruct>(out var tattrStruct))
                    {
                        categoryList?.AddRange(tattrStruct.TestCategories ?? []);
                    }
                }

                if (tAttrName == "TestPropertyAttribute")
                {
                    testProperties ??= new Dictionary<string, List<string>?>();
                    if (tattr.TryDuckCast<TestPropertyAttributeStruct>(out var tattrStruct) && tattrStruct.Name != null)
                    {
                        if (!testProperties.TryGetValue(tattrStruct.Name, out var propertyList))
                        {
                            propertyList = [];
                            testProperties[tattrStruct.Name] = propertyList;
                        }

                        propertyList?.Add(tattrStruct.Value ?? "(empty)");
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
                        testProperties ??= new Dictionary<string, List<string>?>();
                        if (!testProperties.TryGetValue("Category", out var categoryList))
                        {
                            categoryList = [];
                            testProperties["Category"] = categoryList;
                        }

                        if (tattr.TryDuckCast<TestCategoryAttributeStruct>(out var tattrStruct) && tattrStruct.TestCategories != null)
                        {
                            categoryList?.AddRange(tattrStruct.TestCategories);
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

    internal static bool ShouldSkip<TTestMethod>(TTestMethod testMethodInfo, out bool isUnskippable, out bool isForcedRun, Dictionary<string, List<string>?>? traits = null)
        where TTestMethod : ITestMethod
    {
        isUnskippable = false;
        isForcedRun = false;

        if (TestOptimization.Instance.Settings.IntelligentTestRunnerEnabled != true)
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

    internal static TestOptimizationClient.TestManagementResponseTestPropertiesAttributes GetTestProperties<TTestMethod>(TTestMethod testMethodInfo)
        where TTestMethod : ITestMethod
    {
        var testManagementFeature = TestOptimization.Instance.TestManagementFeature;
        if (testManagementFeature?.Enabled != true)
        {
            return new TestOptimizationClient.TestManagementResponseTestPropertiesAttributes();
        }

        var testModule = testMethodInfo.MethodInfo?.DeclaringType?.Assembly.GetName().Name ?? string.Empty;
        var testClass = testMethodInfo.TestClassName ?? string.Empty;
        var testMethod = testMethodInfo.MethodInfo?.Name ?? string.Empty;
        return testManagementFeature.GetTestProperties(testModule, testClass, testMethod);
    }

    internal static TestModule? GetOrCreateTestModuleFromTestAssemblyInfo<TAsmInfo>(TAsmInfo? testAssemblyInfo, string? assemblyName = null)
        where TAsmInfo : ITestAssemblyInfo
    {
        if (testAssemblyInfo?.Instance is not { } objTestAssemblyInfo)
        {
            return default;
        }

        TestOptimization.Instance.SkippableFeature?.WaitForSkippableTaskToFinish();

        return TestModuleByTestAssemblyInfos.GetValue(
            objTestAssemblyInfo,
            key =>
            {
                // assemblyName??? MsTest lies about this, if we check the usage it always use the Assembly.Location or similar...
                // eg:
                // https://github.com/microsoft/testfx/blob/5c829c1633fdca211c4a96a52a200f61ec85bd5c/src/Adapter/MSTest.TestAdapter/Discovery/AssemblyEnumerator.cs#L379
                // https://github.com/microsoft/testfx/blob/5c829c1633fdca211c4a96a52a200f61ec85bd5c/src/Adapter/MSTest.TestAdapter/Discovery/TypeEnumerator.cs#L145
                if (assemblyName is not null)
                {
                    try
                    {
                        if (File.Exists(assemblyName))
                        {
                            assemblyName = AssemblyName.GetAssemblyName(assemblyName).Name;
                        }
                        else
                        {
                            assemblyName = new AssemblyName(assemblyName).Name;
                        }
                    }
                    catch (Exception ex)
                    {
                        Common.Log.Warning(ex, "Error getting assembly name from {AssemblyName}", assemblyName);
                    }
                }

                assemblyName ??= string.Empty;
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
                var newModule = TestModule.Create(assemblyName, CommonTags.TestingFrameworkNameMsTestV2, frameworkVersion);
                newModule.EnableIpcClient();
                return newModule;
            });
    }

    internal static TestSuite? GetOrCreateTestSuiteFromTestClassInfo<TClassInfo>(TClassInfo? testClassInfo)
        where TClassInfo : ITestClassInfo
    {
        if (testClassInfo?.Instance is not { } objTestClassInfo)
        {
            return default;
        }

        return TestSuiteByTestClassInfos.GetValue(
            objTestClassInfo,
            key =>
            {
                var module = TestModule.Current ?? GetOrCreateTestModuleFromTestAssemblyInfo(testClassInfo.Parent, testClassInfo.ClassType?.Assembly.FullName);
                if (module is null)
                {
                    Common.Log.Error("There is no current module, a new suite cannot be created.");
                    return default;
                }

                var classTypeName = testClassInfo.ClassType?.FullName ?? throw new NullReferenceException("ClassType is null, a new suite cannot be created.");
                return module.GetOrCreateSuite(classTypeName);
            });
    }

    internal static void AddTotalTestCases(int count)
    {
        Interlocked.Add(ref _totalTestCases, count);
    }
}
