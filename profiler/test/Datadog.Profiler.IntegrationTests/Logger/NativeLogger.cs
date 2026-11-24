// <copyright file="NativeLogger.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.IO;
using Datadog.Profiler.IntegrationTests.Helpers;
using Datadog.Profiler.IntegrationTests.Helpers.Logs;
using FluentAssertions;
using Xunit.Abstractions;

namespace Datadog.Profiler.IntegrationTests.Logger
{
    public class NativeLogger
    {
        private readonly ITestOutputHelper _output;

        public NativeLogger(ITestOutputHelper output)
        {
            _output = output;
        }

        // only one framework suffice for this test
        [TestAppFact(TestApplication.Computer01, TestFramework.Net7)]
        public void EnsureBackCompatibilityForLogDirectory(TestAppContext app)
        {
            var runner = app.CreateRunner(_output, commandLine: "--scenario 1");
            runner.TestDurationInSeconds = 2;

            var deprecatedLogPath = Path.Combine(Path.GetTempPath(), "deprecated", System.Environment.ProcessId.ToString());
            runner.Environment.SetVariable("DD_PROFILING_LOG_DIR", deprecatedLogPath);

            using var run = runner.Run();

            ProfilerLogSet.Read(deprecatedLogPath).NativeProfiler.Files.Should().ContainSingle();

            run.Logs.NativeProfiler.Should().NotContainFiles();
        }
    }
}
