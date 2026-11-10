// <copyright file="CpuArchTagTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Datadog.Profiler.IntegrationTests.Helpers;
using FluentAssertions;
using Xunit.Abstractions;

namespace Datadog.Profiler.IntegrationTests
{
    public class CpuArchTagTest
    {
        // tags_profiler is a comma-separated list of key:value pairs. See ApplicationInfoTest.
        private static readonly Regex TagsProfilerPattern = new("\"tags_profiler\":\"(?<tags>[^\"]*)\"", RegexOptions.Compiled);

        private readonly ITestOutputHelper _output;

        public CpuArchTagTest(ITestOutputHelper output)
        {
            _output = output;
        }

        // cpu_arch is compiled into the native profiler, so one runtime is enough.
        [TestAppFact("Samples.Computer01", new[] { "net8.0" })]
        public void CheckCpuArchTag(string appName, string framework, string appAssembly)
        {
            var expectedCpuArch = GetExpectedCpuArch();
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, enableProfiler: true);

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);

            var cpuArchValues = new List<string>();
            agent.ProfilerRequestReceived += (_, ctx) =>
            {
                cpuArchValues.Add(ExtractCpuArch(ctx.Value.Request));
            };

            runner.Run(agent);

            agent.NbCallsOnProfilingEndpoint.Should().BeGreaterThan(0);
            cpuArchValues.Should().NotBeEmpty();
            cpuArchValues.Should().AllBe(expectedCpuArch);
        }

        private static string GetExpectedCpuArch()
        {
            return RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.Arm64 => "arm64",
                Architecture.X64 => "amd64",
                Architecture.X86 => "x86",
                Architecture.Arm => "arm",
                _ => throw new InvalidOperationException($"Unsupported process architecture: {RuntimeInformation.ProcessArchitecture}"),
            };
        }

        private static string ExtractCpuArch(HttpListenerRequest request)
        {
            var text = HttpRequestBodyReader.ReadBodyAsText(request);
            var match = TagsProfilerPattern.Match(text);
            if (!match.Success || string.IsNullOrWhiteSpace(match.Groups["tags"].Value))
            {
                return null;
            }

            foreach (var entry in match.Groups["tags"].Value.Split(','))
            {
                var separator = entry.IndexOf(':');
                if (separator <= 0)
                {
                    continue;
                }

                if (entry[..separator] == "cpu_arch")
                {
                    return entry[(separator + 1)..];
                }
            }

            return null;
        }
    }
}
