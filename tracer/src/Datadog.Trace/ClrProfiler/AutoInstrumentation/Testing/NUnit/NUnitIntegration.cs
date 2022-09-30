// <copyright file="NUnitIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.NUnit
{
    internal static class NUnitIntegration
    {
        private const string TestModuleConst = "Assembly";
        private const string TestSuiteConst = "TestFixture";

        private static readonly ConditionalWeakTable<object, object> TestItems = new();

        internal const string IntegrationName = nameof(Configuration.IntegrationId.NUnit);
        internal const IntegrationId IntegrationId = Configuration.IntegrationId.NUnit;
        internal const string SkipReasonKey = "_SKIPREASON";
        internal static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(NUnitIntegration));

        internal static bool IsEnabled => CIVisibility.IsRunning && Tracer.Instance.Settings.IsIntegrationEnabled(IntegrationId);

        internal static Test? CreateTest(ITest currentTest)
        {
            var testMethod = currentTest.Method.MethodInfo;
            var testMethodArguments = currentTest.Arguments;
            var testMethodProperties = currentTest.Properties;

            if (testMethod == null)
            {
                return null;
            }

            if (GetTestSuiteFrom(currentTest) is not { } suite)
            {
                return null;
            }

            var test = suite.CreateTest(testMethod.Name);
            string? skipReason = null;

            // Get test parameters
            var methodParameters = testMethod.GetParameters();
            if (methodParameters?.Length > 0)
            {
                var testParameters = new TestParameters();
                testParameters.Metadata = new Dictionary<string, object>();
                testParameters.Arguments = new Dictionary<string, object>();
                testParameters.Metadata[TestTags.MetadataTestName] = currentTest.Name;

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
                var traits = new Dictionary<string, List<string>>();
                skipReason = (string)testMethodProperties.Get(SkipReasonKey);
                foreach (var key in testMethodProperties.Keys)
                {
                    if (key == SkipReasonKey || key == "_JOINTYPE")
                    {
                        continue;
                    }

                    var value = testMethodProperties[key];
                    if (value != null)
                    {
                        var lstValues = new List<string>();
                        foreach (var valObj in value)
                        {
                            if (valObj is null)
                            {
                                continue;
                            }

                            lstValues.Add(valObj.ToString() ?? string.Empty);
                        }

                        traits[key] = lstValues;
                    }
                }

                if (traits.Count > 0)
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

            test.ResetStartDate();
            return test;
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
                    test.Close(Ci.TestStatus.Skip, skipReason: ex.Message);
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
                TestItems.TryGetValue(test.Instance, out var moduleObject) && moduleObject is TestModule module)
            {
                return module;
            }

            return null;
        }

        internal static void SetTestModuleTo(ITest test, TestModule module)
        {
            if (test.TestType == TestModuleConst)
            {
                TestItems.Add(test.Instance, module);
            }
            else if (GetParentWithTestType(test, TestModuleConst) is { } assemblyITest)
            {
                TestItems.Add(assemblyITest.Instance, module);
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
                TestItems.TryGetValue(test.Instance, out var suiteObject) && suiteObject is TestSuite suite)
            {
                return suite;
            }

            return null;
        }

        internal static void SetTestSuiteTo(ITest test, TestSuite suite)
        {
            if (test.TestType == TestSuiteConst)
            {
                TestItems.Add(test.Instance, suite);
            }
            else if (GetParentWithTestType(test, TestSuiteConst) is { } suiteITest)
            {
                TestItems.Add(suiteITest.Instance, suite);
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
