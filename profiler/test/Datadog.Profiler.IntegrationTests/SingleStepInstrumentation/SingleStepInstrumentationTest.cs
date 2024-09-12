// <copyright file="SingleStepInstrumentationTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using Datadog.Profiler.IntegrationTests.Helpers;
using Datadog.Profiler.IntegrationTests.Xunit;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Profiler.IntegrationTests.SingleStepInstrumentation
{
    [EnvironmentRestorerAttribute(EnvironmentVariables.SsiDeployed)]
    public class SingleStepInstrumentationTest
    {
        private readonly ITestOutputHelper _output;

        public SingleStepInstrumentationTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [TestAppFact("Samples.Computer01")]
        public void ManualAndProfilingEnvVarNotSet(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 1", enableProfiler: false);

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);

            List<Serie> series = [];
            agent.TelemetryMetricsRequestReceived += (_, ctx) =>
            {
                var s = GetRequestText(ctx.Value.Request);
                series = GetSeries(s);
            };

            runner.Run(agent);

            series.Should().BeEmpty();

            agent.NbCallsOnProfilingEndpoint.Should().Be(0);
        }

        [TestAppFact("Samples.Computer01")]
        public void ManualAndProfilingEnvVarTrue(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 1", enableProfiler: true);

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);

            List<Serie> series = [];
            agent.TelemetryMetricsRequestReceived += (_, ctx) =>
            {
                var s = GetRequestText(ctx.Value.Request);
                series = GetSeries(s);
            };

            runner.Run(agent);

            series.Should().BeEmpty();

            agent.NbCallsOnProfilingEndpoint.Should().NotBe(0);
        }

        [TestAppFact("Samples.Computer01")]
        public void ManualAndProfilingEnvVarFalse(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 1", enableProfiler: false);
            runner.Environment.SetVariable(EnvironmentVariables.ProfilerEnabled, "false");

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);

            List<Serie> series = [];
            agent.TelemetryMetricsRequestReceived += (_, ctx) =>
            {
                var s = GetRequestText(ctx.Value.Request);
                series = GetSeries(s);
            };

            runner.Run(agent);

            series.Should().BeEmpty();

            agent.NbCallsOnProfilingEndpoint.Should().Be(0);
        }

        [TestAppFact("Samples.Computer01")]
        public void SsiAndProfilingEnvVarTrue(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 1", enableProfiler: true);

            // deployed with SSI
            runner.Environment.SetVariable(EnvironmentVariables.SsiDeployed, "tracer");
            runner.Environment.SetVariable(EnvironmentVariables.SsiTelemetryEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.TelemetryToDiskEnabled, "1");

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);
            runner.Run(agent);
            agent.NbCallsOnProfilingEndpoint.Should().BeGreaterThan(0);

            var parser = TelemetryMetricsFileParser.LoadFromDirectory(runner.Environment.PprofDir);

            var expectedTags = new[]
            {
                "heuristic_hypothetical_decision:no_span_short_lived",
                "installation:ssi",
                "enablement_choice:manually_enabled"
            };
            bool hasSentProfile = parser.HasSentProfile();
            Assert.True(hasSentProfile, "We must have some serie(s) with has_sent_profiles:true");
            Assert.True(parser.RuntimeIds.Count == 1);
        }

        [TestAppFact("Samples.Computer01")]
        public void SsiAndProfilingEnvVarFalse(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 1", enableProfiler: false);

            // deployed with SSI
            runner.Environment.SetVariable(EnvironmentVariables.SsiDeployed, "tracer");
            runner.Environment.SetVariable(EnvironmentVariables.ProfilerEnabled, "false");
            runner.Environment.SetVariable(EnvironmentVariables.SsiTelemetryEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.TelemetryToDiskEnabled, "1");

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);
            runner.Run(agent);
            agent.NbCallsOnProfilingEndpoint.Should().Be(0);

            var parser = TelemetryMetricsFileParser.LoadFromDirectory(runner.Environment.PprofDir);
            Assert.True(parser == null);
        }

        [TestAppFact("Samples.Computer01")]
        public void CheckSsiAndProfilingEnvVarSetToFalse(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 1", enableProfiler: false);

            // deployed with SSI
            runner.Environment.SetVariable(EnvironmentVariables.SsiDeployed, "tracer");
            runner.Environment.SetVariable(EnvironmentVariables.ProfilerEnabled, "false");

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);

            List<Serie> series = [];
            agent.TelemetryMetricsRequestReceived += (_, ctx) =>
            {
                var s = GetRequestText(ctx.Value.Request);
                series.AddRange(GetSeries(s));
            };

            runner.Run(agent);

            series.Should().HaveCount(0);

            agent.NbCallsOnProfilingEndpoint.Should().Be(0);
        }

        [TestAppFact("Samples.Computer01")]
        public void SsiAndProfilingNotSsiEnabled_ShortLivedAndNoSpan(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 1", enableProfiler: false);

            // deployed with SSI
            runner.Environment.SetVariable(EnvironmentVariables.SsiDeployed, "tracer");
            // short lived
            runner.Environment.SetVariable(EnvironmentVariables.SsiShortLivedThreshold, "600000");
            runner.Environment.SetVariable(EnvironmentVariables.SsiTelemetryEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.TelemetryToDiskEnabled, "1");

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);
            runner.Run(agent);

            var parser = TelemetryMetricsFileParser.LoadFromDirectory(runner.Environment.PprofDir);
            Assert.True(parser.RuntimeIds.Count == 1, "There must be only one 'ssi_heuristic.number_of_runtime_id' serie'");

            var expectedTags = new[] { "has_sent_profiles:false", "heuristic_hypothetical_decision:no_span_short_lived", "installation:ssi", "enablement_choice:not_enabled" };
            parser.ShouldContainTags(expectedTags);

            agent.NbCallsOnProfilingEndpoint.Should().Be(0);
        }

        [TestAppFact("Samples.Computer01")]
        public void SsiAndProfilingNotSsiEnabled_NoSpan(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 1", enableProfiler: false);

            // deployed with SSI
            runner.Environment.SetVariable(EnvironmentVariables.SsiDeployed, "tracer");

            // simulate long lived
            runner.Environment.SetVariable(EnvironmentVariables.SsiShortLivedThreshold, "1");
            runner.Environment.SetVariable(EnvironmentVariables.SsiTelemetryEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.TelemetryToDiskEnabled, "1");

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);
            runner.Run(agent);

            var parser = TelemetryMetricsFileParser.LoadFromDirectory(runner.Environment.PprofDir);
            Assert.True(parser.RuntimeIds.Count == 1, "There must be only one 'ssi_heuristic.number_of_runtime_id' serie'");

            var expectedTags = new[] { "has_sent_profiles:false", "heuristic_hypothetical_decision:no_span", "installation:ssi", "enablement_choice:not_enabled" };
            parser.ShouldContainTags(expectedTags);

            agent.NbCallsOnProfilingEndpoint.Should().Be(0);
        }

        [TestAppFact("Samples.BuggyBits")]
        public void SsiAndProfilingNotSsiEnabled_ShortLived(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 1", enableProfiler: false, enableTracer: true);

            // deployed with SSI
            runner.Environment.SetVariable(EnvironmentVariables.SsiDeployed, "tracer");
            // short lived with span
            runner.Environment.SetVariable(EnvironmentVariables.SsiShortLivedThreshold, "600000");
            runner.Environment.SetVariable(EnvironmentVariables.SsiTelemetryEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.TelemetryToDiskEnabled, "1");

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);
            runner.Run(agent);

            var parser = TelemetryMetricsFileParser.LoadFromDirectory(runner.Environment.PprofDir);
            Assert.True(parser.RuntimeIds.Count == 1, "There must be only one 'ssi_heuristic.number_of_runtime_id' serie'");

            var expectedTags = new[] { "has_sent_profiles:false", "heuristic_hypothetical_decision:short_lived", "installation:ssi", "enablement_choice:not_enabled" };
            parser.ShouldContainTags(expectedTags, mandatory: true);

            agent.NbCallsOnProfilingEndpoint.Should().Be(0);
        }

        [TestAppFact("Samples.BuggyBits")]
        public void SsiAndProfilingNotSsiEnabled_AllTriggered(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 1", enableProfiler: false, enableTracer: true);

            // deployed with SSI
            runner.Environment.SetVariable(EnvironmentVariables.SsiDeployed, "tracer");
            // simulate long lived
            runner.Environment.SetVariable(EnvironmentVariables.SsiShortLivedThreshold, "1");
            runner.Environment.SetVariable(EnvironmentVariables.SsiTelemetryEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.TelemetryToDiskEnabled, "1");

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);
            runner.Run(agent);

            var parser = TelemetryMetricsFileParser.LoadFromDirectory(runner.Environment.PprofDir);
            Assert.True(parser.RuntimeIds.Count == 1, "There must be only one 'ssi_heuristic.number_of_runtime_id' serie'");

            var expectedTags = new[] { "has_sent_profiles:false", "heuristic_hypothetical_decision:triggered", "installation:ssi", "enablement_choice:not_enabled" };
            parser.RuntimeIds.ShouldContainTags(expectedTags, mandatory: true);

            var hasTriggeredSeries = false;
            parser.RuntimeIds.Should().AllSatisfy(tm =>
            {
                tm.Tags.Should().HaveCount(4).And.OnlyHaveUniqueItems();
                tm.AssertTagsContains("installation", "ssi");
                tm.AssertTagsContains("enablement_choice", "not_enabled");
                if (tm.Tags.Contains("has_sent_profiles", "false"))
                {
                    tm.AssertTagsContains("heuristic_hypothetical_decision", "triggered");
                    hasTriggeredSeries = true;
                }
            });
            hasTriggeredSeries.Should().BeTrue("We must have some triggered serie(s)");

            agent.NbCallsOnProfilingEndpoint.Should().Be(0);
        }

        [TestAppFact("Samples.Computer01")]
        public void SsiAndProfilingSsiEnabled_ShortLivedAndNoSpan(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 1", enableProfiler: false);

            // deployed and enabled with SSI
            runner.Environment.SetVariable(EnvironmentVariables.SsiDeployed, "profiler");
            // short lived
            runner.Environment.SetVariable(EnvironmentVariables.SsiShortLivedThreshold, "600000");
            runner.Environment.SetVariable(EnvironmentVariables.SsiTelemetryEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.TelemetryToDiskEnabled, "1");

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);
            runner.Run(agent);

            var parser = TelemetryMetricsFileParser.LoadFromDirectory(runner.Environment.PprofDir);
            Assert.True(parser.RuntimeIds.Count == 1, "There must be only one 'ssi_heuristic.number_of_runtime_id' serie'");

            var expectedTags = new[] { "has_sent_profiles:false", "heuristic_hypothetical_decision:no_span_short_lived", "installation:ssi", "enablement_choice:ssi_enabled" };
            parser.ShouldContainTags(expectedTags, mandatory: true);

            agent.NbCallsOnProfilingEndpoint.Should().Be(0);
        }

        [TestAppFact("Samples.Computer01")]
        public void SsiAndProfilingSsiEnabled_NoSpan(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 1", enableProfiler: false);

            // deployed and enabled with SSI
            runner.Environment.SetVariable(EnvironmentVariables.SsiDeployed, "profiler");

            // simulate long lived
            runner.Environment.SetVariable(EnvironmentVariables.SsiShortLivedThreshold, "1");
            runner.Environment.SetVariable(EnvironmentVariables.SsiTelemetryEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.TelemetryToDiskEnabled, "1");

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);
            runner.Run(agent);

            var parser = TelemetryMetricsFileParser.LoadFromDirectory(runner.Environment.PprofDir);
            Assert.True(parser.RuntimeIds.Count == 1, "There must be only one 'ssi_heuristic.number_of_runtime_id' serie'");

            var expectedTags = new[] { "has_sent_profiles:false", "heuristic_hypothetical_decision:no_span", "installation:ssi", "enablement_choice:ssi_enabled" };
            parser.ShouldContainTags(expectedTags, mandatory: true);

            agent.NbCallsOnProfilingEndpoint.Should().Be(0);
        }

        [TestAppFact("Samples.BuggyBits")]
        public void SsiAndProfilingSsiEnabled_ShortLived(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 1", enableProfiler: false, enableTracer: true);

            // deployed and enabled with SSI
            runner.Environment.SetVariable(EnvironmentVariables.SsiDeployed, "profiler");
            // short lived with span
            runner.Environment.SetVariable(EnvironmentVariables.SsiShortLivedThreshold, "600000");
            runner.Environment.SetVariable(EnvironmentVariables.SsiTelemetryEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.TelemetryToDiskEnabled, "1");

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);
            runner.Run(agent);

            var parser = TelemetryMetricsFileParser.LoadFromDirectory(runner.Environment.PprofDir);
            Assert.True(parser.RuntimeIds.Count == 1, "There must be only one 'ssi_heuristic.number_of_runtime_id' serie'");

            var expectedTags = new[] { "has_sent_profiles:false", "heuristic_hypothetical_decision:short_lived", "installation:ssi", "enablement_choice:ssi_enabled" };
            parser.ShouldContainTags(expectedTags, mandatory: true);

            agent.NbCallsOnProfilingEndpoint.Should().Be(0);
        }

        [TestAppFact("Samples.BuggyBits")]
        public void SsiAndProfilingSsiEnabled_AllTriggered(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 1", enableProfiler: false, enableTracer: true);

            // deployed and enabled with SSI
            runner.Environment.SetVariable(EnvironmentVariables.SsiDeployed, "profiler");
            // simulate long lived
            runner.Environment.SetVariable(EnvironmentVariables.SsiShortLivedThreshold, "1");
            runner.Environment.SetVariable(EnvironmentVariables.SsiTelemetryEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.TelemetryToDiskEnabled, "1");

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);
            runner.Run(agent);

            var parser = TelemetryMetricsFileParser.LoadFromDirectory(runner.Environment.PprofDir);
            Assert.True(parser.RuntimeIds.Count == 1, "There must be only one 'ssi_heuristic.number_of_runtime_id' serie'");

            var expectedTags = new[] { "has_sent_profiles:true", "heuristic_hypothetical_decision:triggered", "installation:ssi", "enablement_choice:ssi_enabled" };
            parser.RuntimeIds.ShouldContainTags(expectedTags, mandatory: true);

            var hasTriggeredSeries = false;
            parser.Profiles.Should().AllSatisfy(tm =>
            {
                tm.Tags.Should().HaveCount(4).And.OnlyHaveUniqueItems();
                tm.AssertTagsContains("installation", "ssi");
                tm.AssertTagsContains("enablement_choice", "ssi_enabled");
                if (tm.Tags.Contains("has_sent_profiles", "true"))
                {
                    tm.AssertTagsContains("heuristic_hypothetical_decision", "triggered");
                    hasTriggeredSeries = true;
                }
            });

            hasTriggeredSeries.Should().BeTrue("We must have some triggered serie(s)");

            agent.NbCallsOnProfilingEndpoint.Should().NotBe(0);
        }

        [TestAppFact("Samples.BuggyBits")]
        public void SsiAndProfilingSsiEnabled_Delayed(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 1", enableProfiler: false, enableTracer: true);

            // deployed and enabled with SSI
            runner.Environment.SetVariable(EnvironmentVariables.SsiDeployed, "profiler");
            // simulate long lived
            runner.Environment.SetVariable(EnvironmentVariables.SsiShortLivedThreshold, TimeSpan.FromSeconds(6).TotalMilliseconds.ToString());
            runner.Environment.SetVariable(EnvironmentVariables.SsiTelemetryEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.TelemetryToDiskEnabled, "1");

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);
            runner.Run(agent);

            var parser = TelemetryMetricsFileParser.LoadFromDirectory(runner.Environment.PprofDir);
            Assert.True(parser.RuntimeIds.Count == 1, "There must be only one 'ssi_heuristic.number_of_runtime_id' serie'");

            var expectedTags = new[] { "has_sent_profiles:true", "heuristic_hypothetical_decision:triggered", "installation:ssi", "enablement_choice:ssi_enabled" };
            parser.RuntimeIds.ShouldContainTags(expectedTags, true);

            var hasNotSentProfiles = false;
            var hasTriggeredSeries = false;
            parser.Profiles.Should().AllSatisfy(tm =>
            {
                tm.Tags.Should().HaveCount(4).And.OnlyHaveUniqueItems();
                tm.AssertTagsContains("installation", "ssi");
                tm.AssertTagsContains("enablement_choice", "ssi_enabled");

                // we need to check for no_span in case of the timer finished after the first call (flakiness)
                if (
                    tm.Tags.Contains("heuristic_hypothetical_decision", "short_lived") ||
                    tm.Tags.Contains("heuristic_hypothetical_decision", "no_span") ||
                    tm.Tags.Contains("heuristic_hypothetical_decision", "no_span_short_lived"))
                {
                    tm.AssertTagsContains("has_sent_profiles", "false");
                    hasNotSentProfiles = true;
                }
                else if (tm.Tags.Contains("heuristic_hypothetical_decision", "triggered"))
                {
                    // it is possible that the final profile was not sent because the providers were stopped
                    hasTriggeredSeries = true;
                }
                else
                {
                    var decisionTag = tm.Tags.FirstOrDefault(x => x.Item1.StartsWith("heuristic_hypothetical_decision"));
                    if (decisionTag != null)
                    {
                        Assert.Fail($"Unrecognized {decisionTag}");
                    }
                    else
                    {
                        Assert.Fail("Missing heuristic_hypothetical_decision tag");
                    }
                }
            });

            hasNotSentProfiles.Should().BeTrue("We must have some short lived serie(s)");
            hasTriggeredSeries.Should().BeTrue("We must have some triggered serie(s)");
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

        private static List<Serie> GetSeries(string s)
        {
            var x = JObject.Parse(s);
            return x.SelectTokens("$.payload[*].payload.series[?(@.namespace=='profilers')]")?.Select(serie => serie.ToObject<Serie>())?.ToList() ?? [];
        }

        private static (Serie NumberRuntimeId, List<Serie> NumberProfiles) ExtractSeries(string s)
        {
            Serie numberRuntimeIds = null;
            List<Serie> numberProfiles = [];
            foreach (var serie in GetSeries(s))
            {
                if (serie.Metric == "ssi_heuristic.number_of_runtime_id")
                {
                    if (numberRuntimeIds != null)
                    {
                        Assert.Fail("There must be only one 'ssi_heuristic.number_of_runtime_id' serie'");
                    }

                    numberRuntimeIds = serie;
                }
                else
                {
                    serie.Metric.Should().Be("ssi_heuristic.number_of_profiles");
                    numberProfiles.Add(serie);
                }
            }

            return (numberRuntimeIds, numberProfiles);
        }

        // return true if tags contain value defined in containedTags
        // ex: "heuristic_hypothetical_decision:no_span_short_live", "heuristic_hypothetical_decision:short_live" return true
        private static bool ContainTags(string[] tags, string[] containedTags, bool mandatory, ref string error)
        {
            if (tags == null)
            {
                error = "tags is null";
                return false;
            }

            Dictionary<string, string> containedRefs = new Dictionary<string, string>();
            foreach (var tag in containedTags)
            {
                var kv = tag.Split(':');
                var key = kv[0];
                var value = kv[1];
                containedRefs[key] = value;
            }

            foreach (var tag in tags)
            {
                var kv = tag.Split(':');
                var key = kv[0];
                var value = kv[1];

                if (!containedRefs.TryGetValue(key, out var containedValue))
                {
                    if (mandatory)
                    {
                        error = $"Key {key} not found in containedTags";
                        return false;
                    }
                }
                else
                {
                    if (!value.Contains(containedValue))
                    {
                        error = $"{key}: '{value}' does not contain '{containedValue}'";
                        return false;
                    }
                }
            }

            return true;
        }

        private class Serie
        {
            public string @Namespace { get; set; }
            public string Metric { get; set; }
            public string[] Tags { get; set; }
        }
    }
}
