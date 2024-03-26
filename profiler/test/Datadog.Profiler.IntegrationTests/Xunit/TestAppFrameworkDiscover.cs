// <copyright file="TestAppFrameworkDiscover.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Datadog.Profiler.IntegrationTests.Helpers;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Datadog.Profiler.IntegrationTests.Xunit
{
    /// <summary>
    /// This class allows to discover test cases for smoke application.
    /// </summary>
    internal class TestAppFrameworkDiscover : IXunitTestCaseDiscoverer
    {
        public TestAppFrameworkDiscover(IMessageSink messageSink)
        {
            MessageSink = messageSink;
        }

        public IMessageSink MessageSink { get; }

        public IEnumerable<IXunitTestCase> Discover(ITestFrameworkDiscoveryOptions discoveryOptions, ITestMethod testMethod, IAttributeInfo factAttribute)
        {
            var appName = factAttribute.GetNamedArgument<string>("AppName");
            var appAssembly = factAttribute.GetNamedArgument<string>("AppAssembly");
            var frameworks = factAttribute.GetNamedArgument<string[]>("Frameworks");
            var appFolderPath = TestApplicationRunner.GetApplicationOutputFolderPath(appName);

            MessageSink.OnMessage(new DiagnosticMessage("Discovering tests case in {0} for application {1}", appFolderPath, appName));

            var results = new List<IXunitTestCase>();

            if (!System.IO.Directory.Exists(appFolderPath))
            {
                results.Add(
                    new ExecutionErrorTestCase(
                        MessageSink,
                        TestMethodDisplay.Method,
                        TestMethodDisplayOptions.None,
                        testMethod,
                        $"Application folder path '{appFolderPath}' does not exist: try compiling application '{appName}' first."));
                return results;
            }

            // We only want to filter when running in VS
            // For CI/Nuke, we use passed the filter passed to dotnet test
            if (!EnvironmentHelper.IsRunningInCi())
            {
                var testStatus = GetSkippableStatus(testMethod);
                if (testStatus.Skip)
                {
                    MessageSink.OnMessage(new DiagnosticMessage("Skipping test. Reason: {0}", testStatus.Reason));
                    var xx = new SkippableTestCase(
                        $"Test case skipped. Reason: {testStatus.Reason}",
                        testMethod,
                        new object[] { appName, string.Empty, appAssembly });
                    results.Add(xx);
                    return results;
                }
            }

            foreach (var folder in System.IO.Directory.GetDirectories(appFolderPath))
            {
                var framework = System.IO.Path.GetFileName(folder);
                if (frameworks == null || frameworks.Contains(framework))
                {
                    results.Add(
                        new ProfilerTestCase(
                                MessageSink,
                                TestMethodDisplay.ClassAndMethod,
                                TestMethodDisplayOptions.All,
                                testMethod,
                                new object[] { appName, System.IO.Path.GetFileName(folder), appAssembly }));
                }
                else
                {
                    var xx = new SkippableTestCase(
                        $"Test case skipped: {framework} is an unsupported framework",
                        testMethod,
                        new object[] { appName, System.IO.Path.GetFileName(folder), appAssembly });
                    results.Add(xx);
                }
            }

            if (results.Count == 0)
            {
                results.Add(
                    new ExecutionErrorTestCase(
                            MessageSink,
                            TestMethodDisplay.Method,
                            TestMethodDisplayOptions.None,
                            testMethod,
                            $"Application '{appName}' does not have any test cases: try compiling the application '{appName}' first."));
            }

            return results;
        }

        private static TestSkippableStatus GetSkippableStatus(ITestMethod testMethod)
        {
            var traits = GetTraits(testMethod);
            if (traits.Count == 0)
            {
                return new TestSkippableStatus(false);
            }

            var categoryTraits = traits.Where(kv => kv.Key == "Category");

            var (skip, reason) = (Environment.OSVersion.Platform, categoryTraits.Any(kv => kv.Value == "LinuxOnly"), categoryTraits.Any(kv => kv.Value == "WindowsOnly")) switch
            {
                (PlatformID.Win32NT, true, _) => (true, $"Test can only be run on Linux"),
                (PlatformID.Unix, _, true) => (true, $"Test can only be run on Windows"),
                _ => (false, string.Empty)
            };

            return new TestSkippableStatus(skip, reason);
        }

        private static IReadOnlyList<KeyValuePair<string, string>> GetTraits(ITestMethod testMethod)
        {
            var result = new List<KeyValuePair<string, string>>();

            var method = testMethod.Method.ToRuntimeMethod();
            result.AddRange(ExtractTraits(method.CustomAttributes));

            var clazz = testMethod.TestClass.Class.ToRuntimeType();
            result.AddRange(ExtractTraits(clazz.CustomAttributes));

            return result;
        }

        private static List<KeyValuePair<string, string>> ExtractTraits(IEnumerable<CustomAttributeData> attributes)
        {
            var result = new List<KeyValuePair<string, string>>();
            var messageSink = new NullMessageSink();
            foreach (var traitAttributeData in attributes)
            {
                var traitAttributeType = traitAttributeData.AttributeType;
                if (!typeof(ITraitAttribute).GetTypeInfo().IsAssignableFrom(traitAttributeType.GetTypeInfo()))
                {
                    continue;
                }

                var discovererAttributeData = traitAttributeType.GetTypeInfo().CustomAttributes.FirstOrDefault(cad => cad.AttributeType == typeof(TraitDiscovererAttribute));
                if (discovererAttributeData == null)
                {
                    continue;
                }

                var discoverer = ExtensibilityPointFactory.GetTraitDiscoverer(messageSink, Reflector.Wrap(discovererAttributeData));
                if (discoverer == null)
                {
                    continue;
                }

                var traits = discoverer.GetTraits(Reflector.Wrap(traitAttributeData));
                if (traits != null)
                {
                    result.AddRange(traits);
                }
            }

            return result;
        }

        private record TestSkippableStatus(bool Skip, string Reason = "")
        {
        }
    }
}
