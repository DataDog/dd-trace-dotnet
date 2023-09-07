// <copyright file="ApplicationInfoTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Datadog.Profiler.IntegrationTests.Helpers;
using FluentAssertions;
using FluentAssertions.Collections;
using FluentAssertions.Common;
using Xunit.Abstractions;

namespace Datadog.Profiler.IntegrationTests.ApplicationInfo
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
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, enableTracer: true)
            {
                // Set no service name through environment variables to force the tracer to use the value from the datadog.json file
                ServiceName = null
            };

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

        [TestAppFact("Samples.Computer01")]
        public void CheckMetadataAreSent(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output);

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);

            var infos = new List<JsonNode>();

            agent.ProfilerRequestReceived += (_, ctx) =>
            {
                infos.Add(ExtractMetadataFromProfilerRequest(ctx.Value.Request));
            };

            runner.Run(agent);

            agent.NbCallsOnProfilingEndpoint.Should().BeGreaterThan(0);

            infos.Select(x => x.ToJsonString()).Should().OnlyContain(x => x != "{}");
        }

        private static (string ServiceName, string Environment, string Version) ExtractServiceFromProfilerRequest(HttpListenerRequest request)
        {
            var text = GetRequestText(request);
            /*
             we want to extract the "tags_profiler" attribute from the http body. The format is as followed:
             {
                 "internal": <METADATA>,
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

        private static JsonNode ExtractMetadataFromProfilerRequest(HttpListenerRequest request)
        {
            var text = GetRequestText(request);

            /*
             we want to extract the "tags_profiler" attribute from the http body. The format is as followed:
             {
                 "internal": <METADATA>,
                 "start":"<START DATE>",
                 "end":"<END DATE>",
                 "attachments":["<ATTACHMENT1>", "<ATTACHMENT2>"],
                 "tags_profiler":"<PROFILER TAGS>",
                 "family":"<FAMILY>",
                 "version":"4"
             }
             <METADATA> json string
             */

            var offset = text.IndexOf("\"internal\":");
            if (offset == -1)
            {
                return JsonNode.Parse(string.Empty);
            }

            offset += 11; // move after ':';

            // skip whitespaces if any
            while (text[offset] == ' ')
            {
                offset++;
            }

            // next character must be the open-curly braces
            if (text[offset] != '{')
            {
                return JsonNode.Parse(string.Empty);
            }

            var start = offset;
            var end = start + 1;

            var nbCurlyBraces = 1;
            var curlyBraces = new[] { '{', '}' };
            while (nbCurlyBraces != 0)
            {
                int current = text.IndexOfAny(curlyBraces, end);
                if (text[current] == '{')
                {
                    nbCurlyBraces++;
                }

                if (text[current] == '}')
                {
                    nbCurlyBraces--;
                }

                end = current + 1;
            }

            return JsonNode.Parse(text[start..end]);
        }

        private static string GetRequestText(HttpListenerRequest request)
        {
            var text = string.Empty;
            using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
            {
                text = reader.ReadToEnd();
            }

            return text;
        }
    }
}
