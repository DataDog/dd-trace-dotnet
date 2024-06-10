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
using FluentAssertions;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Profiler.IntegrationTests.SingleStepInstrumentation
{
    public class SingleStepInstrumentationTest
    {
        private readonly ITestOutputHelper _output;

        public SingleStepInstrumentationTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [TestAppFact("Samples.Computer01")]
        public void CheckManuallyDeployedAndProfilingEnvVarNotSet(string appName, string framework, string appAssembly)
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
        public void CheckManuallyDeployedAndProfilingEnvVarSetToTrue(string appName, string framework, string appAssembly)
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
        public void CheckManuallyDeployedAndProfilingEnvVarSetToFalse(string appName, string framework, string appAssembly)
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
        public void CheckSsiDeployedAndProfilingEnvVarSetToTrue(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 1", enableProfiler: true);

            // deployed with SSI
            runner.Environment.SetVariable(EnvironmentVariables.SsiDeployed, "tracer");

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);

            List<Serie> series = [];
            agent.TelemetryMetricsRequestReceived += (_, ctx) =>
            {
                var s = GetRequestText(ctx.Value.Request);
                series.AddRange(GetSeries(s));
            };

            runner.Run(agent);

            var expectedTags = new[]
            {
                "has_sent_profiles:true",
                "heuristic_hypothetical_decision:no_span_short_lived",
                "installation:ssi",
                "enablement_choice:manually_enabled"
            };
            series.Should().AllSatisfy(x => x.Tags.Should().BeEquivalentTo(expectedTags));
            series.Should().ContainSingle(x => x.Metric == "ssi_heuristic.number_of_runtime_id");
            series.Where(x => x.Metric == "ssi_heuristic.number_of_profiles").Should().HaveCount(series.Count - 1);

            agent.NbCallsOnProfilingEndpoint.Should().BeGreaterThan(0);
        }

        [TestAppFact("Samples.Computer01")]
        public void CheckSsiDeployedAndProfilingEnvVarSetToFalse(string appName, string framework, string appAssembly)
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
        public void CheckSsiDeployedAndProfilingNotSsiEnabled_ShortLivedAndNoSpan(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 1", enableProfiler: false);

            // deployed with SSI
            runner.Environment.SetVariable(EnvironmentVariables.SsiDeployed, "tracer");

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);

            List<Serie> nbProfilesSeries = [];
            Serie runtimeIdSerie = null;
            agent.TelemetryMetricsRequestReceived += (_, ctx) =>
            {
                var s = GetRequestText(ctx.Value.Request);

                var (nbRuntimeId, nbProfiles) = ExtractSeries(s);

                if (nbRuntimeId != null && runtimeIdSerie != null)
                {
                    Assert.Fail("There must be only one 'ssi_heuristic.number_of_runtime_id' serie'");
                }

                runtimeIdSerie = nbRuntimeId;
                nbProfilesSeries.AddRange(nbProfiles);
            };

            runner.Run(agent);

            var expectedTags = new[] { "has_sent_profiles:false", "heuristic_hypothetical_decision:no_span_short_lived", "installation:ssi", "enablement_choice:not_enabled" };
            runtimeIdSerie.Tags.Should().BeEquivalentTo(expectedTags);
            nbProfilesSeries.Should().AllSatisfy(x => x.Tags.Should().BeEquivalentTo(expectedTags));

            agent.NbCallsOnProfilingEndpoint.Should().Be(0);
        }

        [TestAppFact("Samples.Computer01")]
        public void CheckSsiDeployedAndProfilingNotSsiEnabled_NoSpan(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 1", enableProfiler: false);

            // deployed with SSI
            runner.Environment.SetVariable(EnvironmentVariables.SsiDeployed, "tracer");

            // simulate long lived
            runner.Environment.SetVariable(EnvironmentVariables.SsiShortLivedThreshold, "1");

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);

            List<Serie> nbProfilesSeries = [];
            Serie runtimeIdSerie = null;
            agent.TelemetryMetricsRequestReceived += (_, ctx) =>
            {
                var s = GetRequestText(ctx.Value.Request);

                var (nbRuntimeId, nbProfiles) = ExtractSeries(s);

                if (nbRuntimeId != null && runtimeIdSerie != null)
                {
                    Assert.Fail("There must be only one 'ssi_heuristic.number_of_runtime_id' serie'");
                }

                runtimeIdSerie = nbRuntimeId;
                nbProfilesSeries.AddRange(nbProfiles);
            };

            runner.Run(agent);

            var expectedTags = new[] { "has_sent_profiles:false", "heuristic_hypothetical_decision:no_span", "installation:ssi", "enablement_choice:not_enabled" };
            runtimeIdSerie.Tags.Should().BeEquivalentTo(expectedTags);
            nbProfilesSeries.Should().AllSatisfy(x => x.Tags.Should().BeEquivalentTo(expectedTags));

            agent.NbCallsOnProfilingEndpoint.Should().Be(0);
        }

        [TestAppFact("Samples.BuggyBits")]
        public void CheckSsiDeployedAndProfilingNotSsiEnabled_ShortLived(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 1", enableProfiler: false, enableTracer: true);

            // deployed with SSI
            runner.Environment.SetVariable(EnvironmentVariables.SsiDeployed, "tracer");
            // short lived with span

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);

            List<Serie> nbProfilesSeries = [];
            Serie runtimeIdSerie = null;
            agent.TelemetryMetricsRequestReceived += (_, ctx) =>
            {
                var s = GetRequestText(ctx.Value.Request);

                var (nbRuntimeId, nbProfiles) = ExtractSeries(s);

                if (nbRuntimeId != null && runtimeIdSerie != null)
                {
                    Assert.Fail("There must be only one 'ssi_heuristic.number_of_runtime_id' serie'");
                }

                runtimeIdSerie = nbRuntimeId;
                nbProfilesSeries.AddRange(nbProfiles);
            };

            runner.Run(agent);

            var expectedTags = new[] { "has_sent_profiles:false", "heuristic_hypothetical_decision:short_lived", "installation:ssi", "enablement_choice:not_enabled" };
            string error = string.Empty;
            Assert.True(ContainTags(runtimeIdSerie.Tags, expectedTags, ref error), $"{error}");
            nbProfilesSeries.Should().AllSatisfy(x => Assert.True(ContainTags(x.Tags, expectedTags, ref error), $"{error}"));

            agent.NbCallsOnProfilingEndpoint.Should().Be(0);
        }

        [TestAppFact("Samples.BuggyBits")]
        public void CheckSsiDeployedAndProfilingNotSsiEnabled_AllTriggered(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 1", enableProfiler: false, enableTracer: true);

            // deployed with SSI
            runner.Environment.SetVariable(EnvironmentVariables.SsiDeployed, "tracer");
            // simulate long lived
            runner.Environment.SetVariable(EnvironmentVariables.SsiShortLivedThreshold, "0");

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);

            List<Serie> nbProfilesSeries = [];
            Serie runtimeIdSerie = null;
            agent.TelemetryMetricsRequestReceived += (_, ctx) =>
            {
                var s = GetRequestText(ctx.Value.Request);

                var (nbRuntimeId, nbProfiles) = ExtractSeries(s);

                if (nbRuntimeId != null && runtimeIdSerie != null)
                {
                    Assert.Fail("There must be only one 'ssi_heuristic.number_of_runtime_id' serie'");
                }

                runtimeIdSerie = nbRuntimeId;
                nbProfilesSeries.AddRange(nbProfiles);
            };

            runner.Run(agent);

            runtimeIdSerie.Should().NotBeNull();
            var expectedTags = new[] { "has_sent_profiles:false", "heuristic_hypothetical_decision:triggered", "installation:ssi", "enablement_choice:not_enabled" };
            runtimeIdSerie.Tags.Should().BeEquivalentTo(expectedTags);

            var hasTriggeredSeries = false;
            nbProfilesSeries.Should().AllSatisfy(s =>
            {
                s.Metric.Should().Be("ssi_heuristic.number_of_profiles");
                s.Tags.Should().HaveCount(4).And.OnlyHaveUniqueItems().And.Contain("installation:ssi", "enablement_choice:ssi_enabled");
                if (s.Tags.Contains("has_sent_profiles:false"))
                {
                    s.Tags.Should().Contain("heuristic_hypothetical_decision:triggered");
                    hasTriggeredSeries = true;
                }
            });

            hasTriggeredSeries.Should().BeTrue("We must have some triggered serie(s)");

            agent.NbCallsOnProfilingEndpoint.Should().NotBe(0);
        }

        [TestAppFact("Samples.Computer01")]
        public void CheckSsiDeployedAndProfilingSsiEnabled_ShortLivedAndNoSpan(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 1", enableProfiler: false);

            // deployed and enabled with SSI
            runner.Environment.SetVariable(EnvironmentVariables.SsiDeployed, "profiler");
            // No need to tweak the SsiShortLivedThreshold env variable to simulate a shortlived app.
            // This app runs for ~10s and the threshold is 30s.

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);

            List<Serie> nbProfilesSeries = [];
            Serie runtimeIdSerie = null;
            agent.TelemetryMetricsRequestReceived += (_, ctx) =>
            {
                var s = GetRequestText(ctx.Value.Request);

                var (nbRuntimeId, nbProfiles) = ExtractSeries(s);

                if (nbRuntimeId != null && runtimeIdSerie != null)
                {
                    Assert.Fail("There must be only one 'ssi_heuristic.number_of_runtime_id' serie'");
                }

                runtimeIdSerie = nbRuntimeId;
                nbProfilesSeries.AddRange(nbProfiles);
            };

            runner.Run(agent);

            var expectedTags = new[] { "has_sent_profiles:false", "heuristic_hypothetical_decision:no_span_short_lived", "installation:ssi", "enablement_choice:ssi_enabled" };
            runtimeIdSerie.Tags.Should().BeEquivalentTo(expectedTags);
            nbProfilesSeries.Should().AllSatisfy(x => x.Tags.Should().BeEquivalentTo(expectedTags));

            agent.NbCallsOnProfilingEndpoint.Should().Be(0);
        }

        [TestAppFact("Samples.Computer01")]
        public void CheckSsiDeployedAndProfilingSsiEnabled_NoSpan(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 1", enableProfiler: false);

            // deployed and enabled with SSI
            runner.Environment.SetVariable(EnvironmentVariables.SsiDeployed, "profiler");

            // simulate long lived
            runner.Environment.SetVariable(EnvironmentVariables.SsiShortLivedThreshold, "1");

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);

            List<Serie> nbProfilesSeries = [];
            Serie runtimeIdSerie = null;
            agent.TelemetryMetricsRequestReceived += (_, ctx) =>
            {
                var s = GetRequestText(ctx.Value.Request);

                var (nbRuntimeId, nbProfiles) = ExtractSeries(s);

                if (nbRuntimeId != null && runtimeIdSerie != null)
                {
                    Assert.Fail("There must be only one 'ssi_heuristic.number_of_runtime_id' serie'");
                }

                runtimeIdSerie = nbRuntimeId;
                nbProfilesSeries.AddRange(nbProfiles);
            };

            runner.Run(agent);

            var expectedTags = new[] { "has_sent_profiles:false", "heuristic_hypothetical_decision:no_span", "installation:ssi", "enablement_choice:ssi_enabled" };
            runtimeIdSerie.Tags.Should().BeEquivalentTo(expectedTags);
            nbProfilesSeries.Should().AllSatisfy(x => x.Tags.Should().BeEquivalentTo(expectedTags));

            agent.NbCallsOnProfilingEndpoint.Should().Be(0);
        }

        [TestAppFact("Samples.BuggyBits")]
        public void CheckSsiDeployedAndProfilingSsiEnabled_ShortLived(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 1", enableProfiler: false, enableTracer: true);

            // deployed and enabled with SSI
            runner.Environment.SetVariable(EnvironmentVariables.SsiDeployed, "profiler");
            // short lived with span

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);

            List<Serie> nbProfilesSeries = [];
            Serie runtimeIdSerie = null;
            agent.TelemetryMetricsRequestReceived += (_, ctx) =>
            {
                var s = GetRequestText(ctx.Value.Request);

                var (nbRuntimeId, nbProfiles) = ExtractSeries(s);

                if (nbRuntimeId != null && runtimeIdSerie != null)
                {
                    Assert.Fail("There must be only one 'ssi_heuristic.number_of_runtime_id' serie'");
                }

                runtimeIdSerie = nbRuntimeId;
                nbProfilesSeries.AddRange(nbProfiles);
            };

            runner.Run(agent);

            var expectedTags = new[] { "has_sent_profiles:false", "heuristic_hypothetical_decision:short_lived", "installation:ssi", "enablement_choice:ssi_enabled" };
            runtimeIdSerie.Tags.Should().BeEquivalentTo(expectedTags);
            nbProfilesSeries.Should().AllSatisfy(x => x.Tags.Should().BeEquivalentTo(expectedTags));

            agent.NbCallsOnProfilingEndpoint.Should().Be(0);
        }

        [TestAppFact("Samples.BuggyBits")]
        public void CheckSsiDeployedAndProfilingSsiEnabled_AllTriggered(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 1", enableProfiler: false, enableTracer: true);

            // deployed and enabled with SSI
            runner.Environment.SetVariable(EnvironmentVariables.SsiDeployed, "profiler");
            // simulate long lived
            runner.Environment.SetVariable(EnvironmentVariables.SsiShortLivedThreshold, "0");

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);

            List<Serie> nbProfilesSeries = [];
            Serie runtimeIdSerie = null;
            agent.TelemetryMetricsRequestReceived += (_, ctx) =>
            {
                var s = GetRequestText(ctx.Value.Request);

                var (nbRuntimeId, nbProfiles) = ExtractSeries(s);

                if (nbRuntimeId != null && runtimeIdSerie != null)
                {
                    Assert.Fail("There must be only one 'ssi_heuristic.number_of_runtime_id' serie'");
                }

                runtimeIdSerie = nbRuntimeId;
                nbProfilesSeries.AddRange(nbProfiles);
            };

            runner.Run(agent);

            runtimeIdSerie.Should().NotBeNull();
            var expectedTags = new[] { "has_sent_profiles:true", "heuristic_hypothetical_decision:triggered", "installation:ssi", "enablement_choice:ssi_enabled" };
            runtimeIdSerie.Tags.Should().BeEquivalentTo(expectedTags);

            var hasTriggeredSeries = false;
            nbProfilesSeries.Should().AllSatisfy(s =>
            {
                s.Metric.Should().Be("ssi_heuristic.number_of_profiles");
                s.Tags.Should().HaveCount(4).And.OnlyHaveUniqueItems().And.Contain("installation:ssi", "enablement_choice:ssi_enabled");
                if (s.Tags.Contains("has_sent_profiles:true"))
                {
                    s.Tags.Should().Contain("heuristic_hypothetical_decision:triggered");
                    hasTriggeredSeries = true;
                }
            });

            hasTriggeredSeries.Should().BeTrue("We must have some triggered serie(s)");

            agent.NbCallsOnProfilingEndpoint.Should().NotBe(0);
        }

        [TestAppFact("Samples.BuggyBits")]
        public void CheckSsiDeployedAndProfilingSsiEnabled_Delayed(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 1", enableProfiler: false, enableTracer: true);

            // deployed and enabled with SSI
            runner.Environment.SetVariable(EnvironmentVariables.SsiDeployed, "profiler");
            // simulate long lived
            runner.Environment.SetVariable(EnvironmentVariables.SsiShortLivedThreshold, TimeSpan.FromSeconds(6).TotalMilliseconds.ToString());

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);

            List<Serie> nbProfilesSeries = [];
            Serie runtimeIdSerie = null;
            agent.TelemetryMetricsRequestReceived += (_, ctx) =>
            {
                var s = GetRequestText(ctx.Value.Request);

                var (nbRuntimeId, nbProfiles) = ExtractSeries(s);

                if (nbRuntimeId != null && runtimeIdSerie != null)
                {
                    Assert.Fail("There must be only one 'ssi_heuristic.number_of_runtime_id' serie'");
                }

                runtimeIdSerie ??= nbRuntimeId;
                nbProfilesSeries.AddRange(nbProfiles);
            };

            runner.Run(agent);

            runtimeIdSerie.Should().NotBeNull();
            runtimeIdSerie.Tags.Should().Contain("has_sent_profiles:true", "heuristic_hypothetical_decision:triggered", "installation:ssi", "enablement_choice:ssi_enabled");

            nbProfilesSeries.Should().NotBeEmpty();
            var hasNotSentProfiles = false;
            var hasTriggeredSeries = false;
            nbProfilesSeries.Should().AllSatisfy(s =>
            {
                s.Metric.Should().Be("ssi_heuristic.number_of_profiles");
                s.Tags.Should().HaveCount(4).And.OnlyHaveUniqueItems().And.Contain("installation:ssi", "enablement_choice:ssi_enabled");
                // we need to check for no_span in case of the timer finished after the first call (flakiness)
                if (s.Tags.Contains("heuristic_hypothetical_decision:short_lived") || s.Tags.Contains("heuristic_hypothetical_decision:no_span"))
                {
                    s.Tags.Should().Contain("has_sent_profiles:false");
                    hasNotSentProfiles = true;
                }
                else if (s.Tags.Contains("heuristic_hypothetical_decision:triggered"))
                {
                    s.Tags.Should().Contain("has_sent_profiles:true");
                    hasTriggeredSeries = true;
                }
                else
                {
                    Assert.Fail("Unrecognized heuristic_hypothetical_decision");
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
        private static bool ContainTags(string[] tags, string[] containedTags, ref string error)
        {
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
                    error = $"Key {key} not found in containedTags";
                    return false;
                }

                if (!value.Contains(containedValue))
                {
                    error = $"{key}: '{value}' does not contain '{containedValue}'";
                    return false;
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
