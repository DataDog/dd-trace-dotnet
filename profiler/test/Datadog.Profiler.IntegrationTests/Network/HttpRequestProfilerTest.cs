// <copyright file="HttpRequestProfilerTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using Datadog.Profiler.IntegrationTests.Helpers;
using Datadog.Profiler.IntegrationTests.Helpers.Profiles;
using Datadog.Profiler.IntegrationTests.Xunit;
using FluentAssertions;
using Xunit.Abstractions;

namespace Datadog.Profiler.IntegrationTests.Network
{
    public class HttpRequestProfilerTest
    {
        // for Samples.ParallelCountSites
        private const string All = "--iterations 5 --scenario 7";
        private const string Redirect = "--iterations 5 --scenario 1";
        private const string Error = "--iterations 5 --scenario 2";
        private const string Blog = "--iterations 2 --scenario 4";

        // for Samples.HttpRequest
        private const string RedirectTime = "--iterations 3 --code 200 --redir 2 --req 1000 --res 2000 --output redirections";

        private readonly ITestOutputHelper _output;

        public HttpRequestProfilerTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [TestAppFact(TestApplication.ParallelCountSites, TestFramework.Net5, TestFramework.Net6)]
        public void ShouldNotGetHttpSamplesInOldRuntimeVersions(TestAppContext app)
        {
            var runner = app.CreateRunner(_output, commandLine: All);
            runner.Environment.EnableOnly(ProfilerFeature.Network, ProfilerFeature.ForceHttpSampling);

            using var run = runner.Run();

            // HTTP events are not emitted in .NET 5 nor .NET 6
            run.Profiles.Samples.Should().BeEmpty();

            // we should also get warnings in the log
            run.Logs.NativeProfiler.Files.Should().ContainSingle();
            var nbWarning = run.Logs.NativeProfiler
                .LinesContaining("Outgoing HTTP profiling is disabled for .NET")
                .Count(match => match.Line.Contains(": .NET 7 + is required."));

            nbWarning.Should().Be(1);
        }

        [TestAppFact(TestApplication.ParallelCountSites, MinimumFramework = TestFramework.Net7)]
        public void ShouldNotGetHttpSamplesWhenDefaultSampling(TestAppContext app)
        {
            var runner = app.CreateRunner(_output, commandLine: All);
            // ProfilerFeature.ForceHttpSampling is not set --> need span + min duration to be sampled
            runner.Environment.EnableOnly(ProfilerFeature.Network);

            using var run = runner.Run();

            run.Profiles.Samples.Should().BeEmpty();
        }

        [TestAppFact(TestApplication.ParallelCountSites, MinimumFramework = TestFramework.Net7)]
        public void ShouldGetHttpSamples(TestAppContext app)
        {
            var runner = app.CreateRunner(_output, commandLine: All);
            runner.Environment.EnableOnly(ProfilerFeature.Network, ProfilerFeature.ForceHttpSampling);

            using var run = runner.Run();

            run.Profiles.Should().HaveSampleTypes(ProfileValue.RequestTime);
            run.Profiles.Samples.Should().NotContain(sample => !sample.Labels.Has(ProfileLabelNames.RequestUrl));
            run.Profiles.Samples.Should().NotContain(sample => !sample.Labels.Has(ProfileLabelNames.ResponseStatusCode));
        }

        [TestAppFact(TestApplication.ParallelCountSites, MinimumFramework = TestFramework.Net7)]
        public void ShouldGetRedirect(TestAppContext app)
        {
            var runner = app.CreateRunner(_output, commandLine: Redirect);
            runner.Environment.EnableOnly(ProfilerFeature.Network, ProfilerFeature.ForceHttpSampling);

            using var run = runner.Run();

            // only network profiler enabled so should only see one value per sample
            run.Profiles.Should().HaveSampleTypes(ProfileValue.RequestTime);
            var samples = run.Profiles.WithNonZeroValue(ProfileValue.RequestTime);
            samples.Should().NotBeEmpty();

            foreach (var sample in samples)
            {
                sample.CallStack.FramesCount.Should().BePositive();

                var url = sample.Labels.GetRequired(ProfileLabelNames.RequestUrl);
                url.Should().NotBeNullOrWhiteSpace();

                // some unexpected requests could be emitted (e.g. telemetry, etc.)
                if (!url.Contains("Maoni"))
                {
                    continue;
                }

                // skip samples with errors (transient network failures to github.com in CI)
                if (sample.Labels.Has(ProfileLabelNames.ResponseError))
                {
                    continue;
                }

                var redirectUrl = sample.Labels.GetRequired(ProfileLabelNames.RedirectUrl);

                // in .NET 7, the redirect is detected but no redirected url is available
                if (app.Framework != TestFramework.Net7)
                {
                    redirectUrl.Should().NotBeNullOrWhiteSpace();
                }

                // request/response happens for all runtimes
                sample.Labels.GetRequired(ProfileLabelNames.RequestDuration).Should().NotBeNullOrWhiteSpace();
                sample.Labels.GetRequired(ProfileLabelNames.ResponseContentDuration).Should().NotBeNullOrWhiteSpace();
            }
        }

        [Flaky("HTTP profiling intermittently produces no request-time samples in CI")]
        [TestAppFact(TestApplication.ParallelCountSites, MinimumFramework = TestFramework.Net7)]
        public void ShouldGetError(TestAppContext app)
        {
            var runner = app.CreateRunner(_output, commandLine: Error);
            runner.Environment.EnableOnly(ProfilerFeature.Network, ProfilerFeature.ForceHttpSampling);

            using var run = runner.Run();

            // only network profiler enabled so should only see one value per sample
            run.Profiles.Should().HaveSampleTypes(ProfileValue.RequestTime);
            var samples = run.Profiles.WithNonZeroValue(ProfileValue.RequestTime);
            samples.Should().NotBeEmpty();
            foreach (var sample in samples)
            {
                sample.CallStack.FramesCount.Should().BePositive();

                var url = sample.Labels.GetRequired(ProfileLabelNames.RequestUrl);
                url.Should().NotBeNullOrWhiteSpace();

                // some unexpected requests could be emitted (e.g. telemetry, etc.)
                if (url != "http://this.does.not.exist.com:80/sorry")
                {
                    continue;
                }

                sample.Labels.GetRequired(ProfileLabelNames.ResponseError).Should().NotBeNullOrWhiteSpace();

                sample.Labels.Has(ProfileLabelNames.RequestDuration).Should().BeFalse();
                sample.Labels.Has(ProfileLabelNames.ResponseContentDuration).Should().BeFalse();
            }
        }

        [Flaky("Test flaked in CI")]
        [TestAppFact(TestApplication.ParallelCountSites, MinimumFramework = TestFramework.Net7)]
        public void ShouldGetDetails(TestAppContext app)
        {
            var runner = app.CreateRunner(_output, commandLine: Blog);
            runner.Environment.EnableOnly(ProfilerFeature.Network, ProfilerFeature.ForceHttpSampling);

            using var run = runner.Run();

            // only network profiler enabled so should only see one value per sample
            run.Profiles.Should().HaveSampleTypes(ProfileValue.RequestTime);
            var samples = run.Profiles.WithNonZeroValue(ProfileValue.RequestTime);
            samples.Should().NotBeEmpty();
            int checkedSamples = 0;
            foreach (var sample in samples)
            {
                sample.CallStack.FramesCount.Should().BePositive();

                var labels = sample.Labels;
                var url = labels.GetRequired(ProfileLabelNames.RequestUrl);
                url.Should().NotBeNullOrWhiteSpace();

                // some unexpected requests could be emitted (e.g. telemetry, etc.)
                if (url != "https://github.com:443/Maoni0")
                {
                    continue;
                }

                // skip samples with errors (transient network failures to github.com in CI)
                if (labels.Has(ProfileLabelNames.ResponseError))
                {
                    continue;
                }

                checkedSamples++;

                // since there are 2 iterations, it is possible that low level connection has been cached for the second iteration and thus no labels are emitted
                if (labels.Has(ProfileLabelNames.DnsSuccess))
                {
                    labels.GetRequired(ProfileLabelNames.DnsSuccess).Should().Be("true");

                    labels.GetRequired(ProfileLabelNames.DnsDuration).Should().NotBeNullOrWhiteSpace();

                    // no socket duration on Linux
                    if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                    {
                        labels.GetRequired(ProfileLabelNames.SocketDuration).Should().NotBeNullOrWhiteSpace();
                    }

                    labels.GetRequired(ProfileLabelNames.TlsDuration).Should().NotBeNullOrWhiteSpace();
                    labels.GetRequired(ProfileLabelNames.RequestDuration).Should().NotBeNullOrWhiteSpace();
                }

                labels.GetRequired(ProfileLabelNames.ResponseContentDuration).Should().NotBeNullOrWhiteSpace();
                labels.GetRequired(ProfileLabelNames.ResponseThreadId).Should().NotBeNullOrWhiteSpace();
                labels.GetRequired(ProfileLabelNames.ResponseThreadName).Should().NotBeNullOrWhiteSpace();
                labels.GetRequired(ProfileLabelNames.ResponseStatusCode).Should().Be("200");
            }

            checkedSamples.Should().BePositive();
        }

        [Flaky("Test flaked in CI")]
        [TestAppFact(TestApplication.HttpRequest, MinimumFramework = TestFramework.Net7)]
        public void ShouldGetExactDurations(TestAppContext app)
        {
            var runner = app.CreateRunner(_output, commandLine: RedirectTime);
            runner.Environment.EnableOnly(ProfilerFeature.Network, ProfilerFeature.ForceHttpSampling);

            using var run = runner.Run();

            var appListenerPort = runner.AppListenerPort;

            // only network profiler enabled so should only see one value per sample
            run.Profiles.Should().HaveSampleTypes(ProfileValue.RequestTime);
            var samples = run.Profiles.WithNonZeroValue(ProfileValue.RequestTime);
            samples.Should().NotBeEmpty();

            int checkedSamples = 0;

            // should get 3x (2 redirections + 3 x 1000ms as request duration + 2000ms as response duration)
            foreach (var sample in samples)
            {
                sample.CallStack.FramesCount.Should().BePositive();

                var labels = sample.Labels;
                var url = labels.GetRequired(ProfileLabelNames.RequestUrl);

                // some unexpected requests could be emitted (e.g. telemetry, etc.)
                if (!url.StartsWith(appListenerPort))
                {
                    continue;
                }

                checkedSamples++;

                var redirectUrl = labels.GetRequired(ProfileLabelNames.RedirectUrl);

                // in .NET 7, the redirect is detected but no redirected url is available
                if (app.Framework != TestFramework.Net7)
                {
                    redirectUrl.Split('|').Should().HaveCount(2);
                }

                var requestDurationLabel = labels.GetRequired(ProfileLabelNames.RequestDuration);
                requestDurationLabel.Should().NotBeNullOrWhiteSpace();
                double.TryParse(requestDurationLabel, out double requestDuration).Should().BeTrue();
                requestDuration.Should().BeGreaterThanOrEqualTo(3L * 900 * 1000000);  // avoid flaky test but should be larger than 3 x 1000ms

                double.TryParse(labels.GetRequired(ProfileLabelNames.ResponseContentDuration), out double responseDuration).Should().BeTrue();
                responseDuration.Should().BeGreaterThanOrEqualTo(1500L * 1000000);  // avoid flaky test but should be larger than 2000ms
            }

            checkedSamples.Should().BePositive();
        }
    }
}
