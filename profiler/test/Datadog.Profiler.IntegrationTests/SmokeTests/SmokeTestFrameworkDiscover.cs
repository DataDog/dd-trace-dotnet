// <copyright file="SmokeTestFrameworkDiscover.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Datadog.Profiler.SmokeTests
{
    /// <summary>
    /// This class allows to discover test cases for smoke application.
    /// </summary>
    internal class SmokeTestFrameworkDiscover : IXunitTestCaseDiscoverer
    {
        public SmokeTestFrameworkDiscover(IMessageSink messageSink)
        {
            MessageSink = messageSink;
        }

        public IMessageSink MessageSink { get; }

        public IEnumerable<IXunitTestCase> Discover(ITestFrameworkDiscoveryOptions discoveryOptions, ITestMethod testMethod, IAttributeInfo factAttribute)
        {
            string appName = factAttribute.GetNamedArgument<string>("AppName");
            string appAssembly = factAttribute.GetNamedArgument<string>("AppAssembly");
            string appFolderPath = SmokeTestRunner.GetApplicationOutputFolderPath(appName);

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

            foreach (string folder in System.IO.Directory.GetDirectories(appFolderPath))
            {
                results.Add(
                    new XunitTestCase(
                            MessageSink,
                            TestMethodDisplay.Method,
                            TestMethodDisplayOptions.All,
                            testMethod,
                            new object[] { appName, System.IO.Path.GetFileName(folder), appAssembly }));
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
    }
}
