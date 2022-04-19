// <copyright file="ApplicationInfoTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using Datadog.Profiler.IntegrationTests.Helpers;
using Datadog.Profiler.SmokeTests;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Profiler.IntegrationTests
{
    public class ApplicationInfoTest
    {
        private static readonly Regex ServicePattern = new("service:(?<service>[A-Z0-9-]+)", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase);

        private readonly ITestOutputHelper _output;

        public ApplicationInfoTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [TestAppFact("Datadog.Demos.BuggyBits", UseNativeLoader = true)]
        public void UseTracerServiceName(string appName, string framework, string appAssembly)
        {
            using var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, enableNewPipeline: true, enableTracer: true);
            using var agent = new MockDatadogAgent(_output);

            var services = new List<string>();

            agent.ProfilerRequestReceived += (_, ctx) =>
            {
                services.Add(ExtractServiceFromProfilerRequest(ctx.Value.Request));
            };

            runner.Run(agent);

            Assert.True(agent.NbCallsOnProfilingEndpoint > 0);

            Assert.Single(services.Distinct());
            Assert.Equal("BuggyBitsService", services.First());
        }

        private static string ExtractServiceFromProfilerRequest(HttpListenerRequest request)
        {
            string text;
            using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
            {
                text = reader.ReadToEnd();
            }

            var match = ServicePattern.Match(text);
            return match.Groups["service"].Value;
        }
    }
}
