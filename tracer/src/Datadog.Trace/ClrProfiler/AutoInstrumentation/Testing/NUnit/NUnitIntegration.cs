// <copyright file="NUnitIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
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
        private static readonly Dictionary<string, WeakReference<Test>> Tests = new();

        internal const string IntegrationName = nameof(Configuration.IntegrationId.NUnit);
        internal const IntegrationId IntegrationId = Configuration.IntegrationId.NUnit;
        internal const string SkipReasonKey = "_SKIPREASON";
        internal static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(NUnitIntegration));

        private static long _totalTestCases;
        private static long _newTestCases;

        internal static bool IsEnabled => CIVisibility.IsRunning && Tracer.Instance.Settings.IsIntegrationEnabled(IntegrationId);

        internal static Test? GetOrCreateTest(ITest currentTest, int repeatCount = 0)
        {
            var key = $"{currentTest.Id}|{repeatCount}";
            lock (Tests)
            {
                if (Tests.TryGetValue(key, out var testReference) && testReference.TryGetTarget(out var test))
                {
                    return test;
                }

                test = InternalCreateTest(currentTest, repeatCount > 0);
                if (test is not null)
                {
                    Tests[key] = new WeakReference<Test>(test);
                }

                return test;
            }
        }

        internal static void FinishTest(Test test, ITestResult testResult)
        {
            GetExceptionAndMessage(testResult, out var exceptionType, out var resultMessage);
            switch (testResult.ResultState.Status)
            {
                case TestStatus.Skipped or TestStatus.Inconclusive:
                    test.Close(Ci.TestStatus.Skip, TimeSpan.Zero, resultMessage);
                    break;
                case TestStatus.Failed:
                    test.SetErrorInfo(exceptionType, resultMessage, testResult.StackTrace);
                    test.Close(Ci.TestStatus.Fail);
                    break;
                default:
                    if (!string.IsNullOrEmpty(resultMessage))
                    {
                        test.SetTag(TestTags.Message, resultMessage);
                    }

                    test.Close(Ci.TestStatus.Pass);
                    break;
            }
        }

        internal static TTestCommand WrapWithRetryCommand<TTestCommand>(TTestCommand testCommand)
        {
            if (testCommand.TryDuckCast<ITestCommand>(out var duckTypedTestCommand))
            {
                var retryTestCommand = new CIVisibilityTestCommand(duckTypedTestCommand);
                return (TTestCommand)retryTestCommand.DuckImplement(typeof(TTestCommand));
            }

            return testCommand;
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

        internal static bool ShouldSkip(ITest currentTest, out bool isUnskippable, out bool isForcedRun, Dictionary<string, List<string>>? traits = null)
        {
            isUnskippable = false;
            isForcedRun = false;

            if (CIVisibility.Settings.IntelligentTestRunnerEnabled != true)
            {
                return false;
            }

            var testMethod = currentTest.Method.MethodInfo;
            var testSuite = testMethod.DeclaringType?.FullName ?? string.Empty;
            var itrShouldSkip = Common.ShouldSkip(testSuite, testMethod.Name, currentTest.Arguments, testMethod.GetParameters());
            if (traits is null)
            {
                ExtractTraits(currentTest, ref traits);
            }

            isUnskippable = traits?.TryGetValue(IntelligentTestRunnerTags.UnskippableTraitName, out _) == true;
            isForcedRun = itrShouldSkip && isUnskippable;
            return itrShouldSkip && !isUnskippable;
        }

        internal static void GetExceptionAndMessage(ITestResult result, out string exceptionType, out string resultMessage)
        {
            exceptionType = result.ResultState.Site switch
            {
                FailureSite.Child => "ChildException",
                FailureSite.Parent => "ParentException",
                FailureSite.Test => "TestException",
                FailureSite.SetUp => "SetUpException",
                FailureSite.TearDown => "TearDownException",
                _ => string.Empty
            };

            resultMessage = result.Message ?? string.Empty;
            while (true)
            {
                // Formatted result messages in NUnit contains the exception type and the type, but also can contain the origin so we can end up having something like:
                // SetUpException: System.Exception: Exception of type 'System.Exception' was thrown.
                // The goal of this algorithm is to extract the exception type and the message from the formatted message.
                var resultSplittedMessage = resultMessage.Split(':');
                var tmpExType = resultSplittedMessage[0].Trim();

                if (resultSplittedMessage.Length < 2 || string.IsNullOrWhiteSpace(tmpExType))
                {
                    Common.Log.Debug("Exception type: {ExceptionType}, Message: {ResultMessage}", exceptionType, resultMessage);
                    break;
                }

                resultMessage = string.Join(":", resultSplittedMessage.Skip(1)).Trim();
                exceptionType = tmpExType;
            }
        }

        private static Test? InternalCreateTest(ITest currentTest, bool isRetry)
        {
            var testMethod = currentTest.Method?.MethodInfo;
            var testMethodArguments = currentTest.Arguments;
            var testMethodProperties = currentTest.Properties;

            if (testMethod == null)
            {
                Log.Warning("Test method cannot be found. ITest.Method(IMethodInfo).MethodInfo is null.");
                return null;
            }

            if (GetTestSuiteFrom(currentTest) is not { } suite)
            {
                return null;
            }

            var test = suite.InternalCreateTest(testMethod.Name);
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
            Dictionary<string, List<string>>? traits = null;
            if (testMethodProperties != null)
            {
                skipReason = (string)testMethodProperties.Get(SkipReasonKey);
                ExtractTraits(currentTest, ref traits);
            }

            if (traits?.Count > 0)
            {
                // Unskippable test
                if (CIVisibility.Settings.IntelligentTestRunnerEnabled)
                {
                    ShouldSkip(currentTest, out var isUnskippable, out var isForcedRun, traits);
                    test.SetTag(IntelligentTestRunnerTags.UnskippableTag, isUnskippable ? "true" : "false");
                    test.SetTag(IntelligentTestRunnerTags.ForcedRunTag, isForcedRun ? "true" : "false");
                    traits.Remove(IntelligentTestRunnerTags.UnskippableTraitName);
                }

                test.SetTraits(traits);
            }
            else
            {
                // Unskippable test
                if (CIVisibility.Settings.IntelligentTestRunnerEnabled)
                {
                    test.SetTag(IntelligentTestRunnerTags.UnskippableTag, "false");
                    test.SetTag(IntelligentTestRunnerTags.ForcedRunTag, "false");
                }
            }

            // Early flake detection flags
            Common.SetEarlyFlakeDetectionTestTagsAndAbortReason(test, isRetry, ref _newTestCases, ref _totalTestCases);

            // Test code and code owners
            test.SetTestMethodInfo(testMethod);

            // Telemetry
            Tracer.Instance.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId);

            // Skip tests
            if (skipReason is not null)
            {
                test.Close(Ci.TestStatus.Skip, skipReason: skipReason, duration: TimeSpan.Zero);
                return test;
            }

            test.ResetStartTime();
            return test;
        }

        private static void ExtractTraits(ITest currentTest, ref Dictionary<string, List<string>>? traits)
        {
            if (currentTest?.Instance is null)
            {
                return;
            }

            if (currentTest.Parent is { Instance: { } })
            {
                ExtractTraits(currentTest.Parent, ref traits);
            }

            if (currentTest.Properties is { } properties)
            {
                var keys = properties.Keys;
                if (keys?.Count > 0)
                {
                    foreach (var key in keys)
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

        internal static void IncrementTotalTestCases()
        {
            Interlocked.Increment(ref _totalTestCases);
        }
    }
}
