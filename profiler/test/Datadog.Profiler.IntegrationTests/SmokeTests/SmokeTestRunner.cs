// <copyright file="SmokeTestRunner.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Datadog.Profiler.IntegrationTests;
using Datadog.Profiler.IntegrationTests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Profiler.SmokeTests
{
    public class SmokeTestRunner : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly int _minimumExpectedPprofsCount = 2; // 1 empty and at least one normal

        private readonly TestApplicationRunner _testApplicationRunner;

        public SmokeTestRunner(
            string appName,
            string framework,
            string appAssembly,
            ITestOutputHelper output,
            bool enableNewPipeline = false)
            : this(appName, framework, appAssembly, commandLine: null, output, enableNewPipeline)
        {
        }

        public SmokeTestRunner(
            string appName,
            string framework,
            string appAssembly,
            string commandLine,
            ITestOutputHelper output,
            bool enableNewPipeline = false)
        {
            _output = output;
            _testApplicationRunner = new TestApplicationRunner(appName, framework, appAssembly, output, commandLine, enableNewPipeline);
        }

        private EnvironmentHelper EnvironmentHelper
        {
            get => _testApplicationRunner.Environment;
        }

        public void Dispose()
        {
            _testApplicationRunner.Dispose();
        }

        public void RunAndCheck()
        {
            using var agent = new MockDatadogAgent(_output);
            _testApplicationRunner.Run(agent);
            RunChecks(agent);
        }

        private void RunChecks(MockDatadogAgent agent)
        {
            // Today, with the old pipeline the checks are flaky.
            // With the new pipeline this should be fixed.
            // So run the checks only for the new
            if (!EnvironmentHelper.IsRunningWithNewPipeline())
            {
                return;
            }

            CheckLogFiles();
            CheckPprofFiles();
            CheckAgent(agent);
        }

        private void CheckLogFiles()
        {
            CheckLogFiles("DD-DotNet-Profiler-Native*.*");
            CheckNoLogFiles("DD-DotNet-Profiler-Managed*.*");
            CheckNoLogFiles("DD-DotNet-Common-ManagedLoader*.*");
        }

        private void CheckNoLogFiles(string filePattern)
        {
            var files = Directory.EnumerateFiles(EnvironmentHelper.LogDir, filePattern, SearchOption.AllDirectories).ToList();

            Assert.Empty(files);
        }

        private void CheckLogFiles(string filePattern)
        {
            var files = Directory.EnumerateFiles(EnvironmentHelper.LogDir, filePattern, SearchOption.AllDirectories).ToList();

            Assert.NotEmpty(files);

            foreach (string logFile in files)
            {
                Assert.False(LogFileContainsErrorMessage(logFile), $"Found error message in the log file {logFile}");
            }
        }

        private void CheckPprofFiles()
        {
            var pprofFiles = Directory.EnumerateFiles(EnvironmentHelper.PprofDir, "*.pprof", SearchOption.AllDirectories).ToList();
            Assert.True(pprofFiles.Count >= _minimumExpectedPprofsCount, $"The number of pprof files was not greater than or equal to {_minimumExpectedPprofsCount}. Actual value {pprofFiles.Count}");
        }

        private void CheckAgent(MockDatadogAgent agent)
        {
            Assert.True(agent.NbCallsOnProfilingEndpoint >= _minimumExpectedPprofsCount, $"The number of calls to the agent was not greater than or equal to {_minimumExpectedPprofsCount}. Actual value {agent.NbCallsOnProfilingEndpoint}");
        }

        private bool LogFileContainsErrorMessage(string logFile)
        {
            var errorLinePattern = new Regex(@"\| error \|", RegexOptions.IgnoreCase | RegexOptions.Compiled);

            using (var file = File.OpenRead(logFile))
            using (var reader = new StreamReader(file))
            {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();

                    if (line != null && errorLinePattern.IsMatch(line))
                    {
                        _output.WriteLine(line);
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
