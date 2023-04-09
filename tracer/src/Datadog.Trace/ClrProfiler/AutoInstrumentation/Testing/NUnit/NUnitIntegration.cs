// <copyright file="NUnitIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.NUnit
{
    internal static class NUnitIntegration
    {
        internal const string TestModuleConst = "Assembly";
        internal const string TestSuiteConst = "TestFixture";

        private static readonly ConditionalWeakTable<object, object> ModulesItems = new();
        private static readonly ConditionalWeakTable<object, object> SuiteItems = new();
        private static readonly ConditionalWeakTable<object, object> ExistingTestCreation = new();

        internal const string IntegrationName = nameof(Configuration.IntegrationId.NUnit);
        internal const IntegrationId IntegrationId = Configuration.IntegrationId.NUnit;
        internal const string SkipReasonKey = "_SKIPREASON";
        internal static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(NUnitIntegration));

        internal static bool IsEnabled => CIVisibility.IsRunning && Tracer.Instance.Settings.IsIntegrationEnabled(IntegrationId);

        internal static Test? CreateTest(ITest currentTest)
        {
            if (ExistingTestCreation.TryGetValue(currentTest.Instance!, out _))
            {
                return null;
            }

            var testMethod = currentTest.Method?.MethodInfo;
            var testMethodArguments = currentTest.Arguments;
            var testMethodProperties = currentTest.Properties;

            if (testMethod == null)
            {
                Log.Warning("Test method cannot be found.");
                return null;
            }

            if (GetTestSuiteFrom(currentTest) is not { } suite)
            {
                return null;
            }

            var test = suite.CreateTest(testMethod.Name);
            ExistingTestCreation.GetOrCreateValue(currentTest.Instance!);
            string? skipReason = null;

            // Get test parameters
            var methodParameters = testMethod.GetParameters();
            if (methodParameters?.Length > 0)
            {
                var testParameters = new TestParameters();
                testParameters.Metadata = new Dictionary<string, object>();
                testParameters.Arguments = new Dictionary<string, object>();
                testParameters.Metadata[TestTags.MetadataTestName] = currentTest.Name ?? string.Empty;

                for (int i = 0; i < methodParameters.Length; i++)
                {
                    var key = methodParameters[i].Name ?? string.Empty;
                    if (testMethodArguments != null && i < testMethodArguments.Length)
                    {
                        testParameters.Arguments[key] = Common.GetParametersValueData(testMethodArguments[i]);
                    }
                    else
                    {
                        testParameters.Arguments[key] = "(default)";
                    }
                }

                test.SetParameters(testParameters);
            }

            // Get traits
            if (testMethodProperties != null)
            {
                skipReason = (string)testMethodProperties.Get(SkipReasonKey);

                Dictionary<string, List<string>>? traits = null;
                ExtractTraits(currentTest, ref traits);
                if (traits?.Count > 0)
                {
                    test.SetTraits(traits);
                }
            }

            // Test code and code owners
            test.SetTestMethodInfo(testMethod);

            // Telemetry
            Tracer.Instance.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId);

            // Skip tests
            if (skipReason is not null)
            {
                test.Close(Ci.TestStatus.Skip, skipReason: skipReason, duration: TimeSpan.Zero);
                return null;
            }

            test.ResetStartTime();
            return test;
        }

        private static void ExtractTraits(ITest currentTest, ref Dictionary<string, List<string>>? traits)
        {
            if (currentTest.Instance is null)
            {
                return;
            }

            if (currentTest.Parent is { Instance: { } })
            {
                ExtractTraits(currentTest.Parent, ref traits);
            }

            if (currentTest.Properties is { Keys: { Count: > 0 } } properties)
            {
                foreach (var key in properties.Keys)
                {
                    if (key is SkipReasonKey or "_APPDOMAIN" or "_JOINTYPE" or "_PID" or "_PROVIDERSTACKTRACE")
                    {
                        continue;
                    }

                    var value = properties[key];
                    if (value is not null)
                    {
                        traits ??= new();
                        if (!traits.TryGetValue(key, out var lstValues))
                        {
                            lstValues = new List<string>();
                            traits[key] = lstValues;
                        }

                        foreach (var valObj in value)
                        {
                            if (valObj is null)
                            {
                                continue;
                            }

                            lstValues.Add(valObj.ToString() ?? string.Empty);
                        }
                    }
                }
            }
        }

        internal static void FinishTest(Test test, Exception? ex)
        {
            // unwrap the generic NUnitException
            if (ex?.GetType().FullName == "NUnit.Framework.Internal.NUnitException")
            {
                ex = ex.InnerException;
            }

            if (ex != null)
            {
                var exTypeName = ex.GetType().FullName;

                if (exTypeName == "NUnit.Framework.SuccessException")
                {
                    test.SetTag(TestTags.Message, ex.Message);
                    test.Close(Ci.TestStatus.Pass);
                }
                else if (exTypeName is "NUnit.Framework.IgnoreException" or "NUnit.Framework.InconclusiveException")
                {
                    test.Close(Ci.TestStatus.Skip, TimeSpan.Zero, ex.Message);
                }
                else
                {
                    test.SetErrorInfo(ex);
                    test.Close(Ci.TestStatus.Fail);
                }
            }
            else
            {
                test.Close(Ci.TestStatus.Pass);
            }
        }

        internal static TestModule? GetTestModuleFrom(ITest? test)
        {
            if (test is null)
            {
                return null;
            }

            if (test.TestType != TestModuleConst)
            {
                test = GetParentWithTestType(test, TestModuleConst);
            }

            if (test is not null &&
                ModulesItems.TryGetValue(test.Instance!, out var moduleObject) &&
                moduleObject is TestModule module)
            {
                return module;
            }

            return null;
        }

        internal static void SetTestModuleTo(ITest test, TestModule module)
        {
            if (test.TestType == TestModuleConst)
            {
                ModulesItems.Add(test.Instance!, module);
            }
            else if (GetParentWithTestType(test, TestModuleConst) is { } assemblyITest)
            {
                ModulesItems.Add(assemblyITest.Instance!, module);
            }
        }

        internal static TestSuite? GetTestSuiteFrom(ITest? test)
        {
            if (test is null)
            {
                return null;
            }

            if (test.TestType != TestSuiteConst)
            {
                test = GetParentWithTestType(test, TestSuiteConst);
            }

            if (test is not null &&
                SuiteItems.TryGetValue(test.Instance!, out var suiteObject) &&
                suiteObject is TestSuite suite)
            {
                return suite;
            }

            return null;
        }

        internal static void SetTestSuiteTo(ITest test, TestSuite suite)
        {
            if (test.TestType == TestSuiteConst)
            {
                SuiteItems.Add(test.Instance!, suite);
            }
            else if (GetParentWithTestType(test, TestSuiteConst) is { } suiteITest)
            {
                SuiteItems.Add(suiteITest.Instance!, suite);
            }
        }

        internal static bool ShouldSkip(ITest currentTest)
        {
            if (CIVisibility.Settings.IntelligentTestRunnerEnabled != true)
            {
                return false;
            }

            var testMethod = currentTest.Method.MethodInfo;
            var testSuite = testMethod.DeclaringType?.FullName ?? string.Empty;
            return Common.ShouldSkip(testSuite, testMethod.Name, currentTest.Arguments, testMethod.GetParameters());
        }

        internal static void WriteSetUpOrTearDownError(ICompositeWorkItem compositeWorkItem, string exceptionType)
        {
            WriteSetUpOrTearDownError(compositeWorkItem.Result, compositeWorkItem.Test, exceptionType);
            foreach (var item in compositeWorkItem.Children)
            {
                if (item?.GetType().Name is { Length: > 0 } itemName)
                {
                    if (itemName == "CompositeWorkItem" && item.TryDuckCast<ICompositeWorkItem>(out var compositeWorkItem2))
                    {
                        WriteSetUpOrTearDownError(compositeWorkItem2, exceptionType);
                    }
                    else if (item.TryDuckCast<IWorkItem>(out var itemWorkItem) && itemWorkItem is { Result: { } testResult })
                    {
                        WriteSetUpOrTearDownError(!string.IsNullOrEmpty(testResult.Message) ? testResult : compositeWorkItem.Result, testResult.Test, exceptionType);
                    }
                }
            }
        }

        internal static void WriteSkip(ITest item, string skipMessage)
        {
            if (item.Method?.MethodInfo is not null && CreateTest(item) is { } test)
            {
                test.Close(Ci.TestStatus.Skip, TimeSpan.Zero, skipMessage);
            }

            if (item.TestType == TestSuiteConst && GetTestSuiteFrom(item) is null && GetTestModuleFrom(item) is { } module)
            {
                SetTestSuiteTo(item, module.GetOrCreateSuite(item.FullName));
            }

            if (item.Tests is { Count: > 0 } tests)
            {
                foreach (var childTest in tests)
                {
                    if (childTest.TryDuckCast<ITest>(out var childTestDuckTyped))
                    {
                        WriteSkip(childTestDuckTyped, skipMessage);
                    }
                }
            }
        }

        private static void WriteSetUpOrTearDownError(ITestResult testResult, ITest item, string exceptionType)
        {
            if (item.Method?.MethodInfo is not null && CreateTest(item) is { } test)
            {
                test.SetErrorInfo(exceptionType, testResult.Message, testResult.StackTrace);
                test.Close(Ci.TestStatus.Fail, TimeSpan.Zero);
            }

            TestSuite? suite = null;
            if (item.TestType == TestSuiteConst)
            {
                if (GetTestSuiteFrom(item) is { } existingSuite)
                {
                    existingSuite.SetErrorInfo(exceptionType, testResult.Message, testResult.StackTrace);
                    existingSuite.Tags.Status = TestTags.StatusFail;
                }
                else if (GetTestModuleFrom(item) is { } module)
                {
                    suite = module.GetOrCreateSuite(item.FullName);
                    suite.SetErrorInfo(exceptionType, testResult.Message, testResult.StackTrace);
                    suite.Tags.Status = TestTags.StatusFail;
                    SetTestSuiteTo(item, suite);
                }
            }

            if (item.Tests is { Count: > 0 } tests)
            {
                foreach (var childTest in tests)
                {
                    if (childTest.TryDuckCast<ITest>(out var childTestDuckTyped))
                    {
                        WriteSetUpOrTearDownError(testResult, childTestDuckTyped, exceptionType);
                    }
                }
            }

            suite?.Close();
        }

        private static ITest? GetParentWithTestType(ITest test, string testType)
        {
            var parent = test?.Parent;
            if (parent?.Instance is null)
            {
                return null;
            }

            return parent.TestType == testType ? parent : GetParentWithTestType(parent, testType);
        }
    }
}
