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
using FluentAssertions;
using Xunit.Abstractions;

namespace Datadog.Profiler.IntegrationTests
{
    public class ApplicationInfoTest
    {
        private readonly ITestOutputHelper _output;

        public ApplicationInfoTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [TestAppFact("Samples.BuggyBits")]
        public void UseTracerServiceName(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, enableTracer: true);

            // Set no service name through environment variables to force the tracer to use the value from the datadog.json file
            runner.ServiceName = null;

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);

            var infos = new List<(string ServiceName, string Environment, string Version)>();

            agent.ProfilerRequestReceived += (_, ctx) =>
            {
                infos.Add(ExtractServiceFromProfilerRequest(ctx.Value.Request));
            };

            runner.Run(agent);

            agent.NbCallsOnProfilingEndpoint.Should().BeGreaterThan(0);

            var distinctInfos = infos.Distinct().ToList();

            // There is a possible race condition:
            // if the profiler sends a sample before the tracer is initialized, it will use the wrong service name
            distinctInfos.Count.Should().BeInRange(1, 2);

            infos.Last().Should().Be(("BuggyBitsService", "BuggyBitsEnv", "BuggyBitsVersion"));
        }

        private static (string ServiceName, string Environment, string Version) ExtractServiceFromProfilerRequest(HttpListenerRequest request)
        {
            string text;
            using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
            {
                text = reader.ReadToEnd();
            }

            /*
             we want to extract the "tags_profiler" attribute from the http body. The format is as followed:
             {
                 "start":"<START DATE>",
                 "end":"<END DATE>",
                 "attachments":["<ATTACHMENT1>", "<ATTACHMENT2>"],
                 "tags_profiler":"<PROFILER TAGS>",
                 "family":"<FAMILY>",
                 "version":"4"
             }
             <PROFILER TAGS> is a list of tag (2 strings separated by ':') separated by ','
             */
            var match_tags = Regex.Match(text, "\"tags_profiler\":\"(?<tags>[^\"]*)\"", RegexOptions.Compiled);

            if (!match_tags.Success || string.IsNullOrWhiteSpace(match_tags.Groups["tags"].Value))
            {
                return (ServiceName: null, Environment: null, Version: null);
            }

            var tags = match_tags.Groups["tags"].Value.Split(',').Select(s => s.Split(':')).ToDictionary(s => s[0], s => s[1]);

            return (
                ServiceName: tags.GetValueOrDefault("service"),
                Environment: tags.GetValueOrDefault("env"),
                Version: tags.GetValueOrDefault("version"));
        }
    }
}
