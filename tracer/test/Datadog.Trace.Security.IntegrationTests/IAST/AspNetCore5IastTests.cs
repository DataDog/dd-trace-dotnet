// <copyright file="AspNetCore5IastTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_0_OR_GREATER
#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name

using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests.Iast
{
    public abstract class AspNetCore5IastTests50PctSamplingIastEnabled : AspNetCore5IastTests
    {
        public AspNetCore5IastTests50PctSamplingIastEnabled(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
            : base(fixture, outputHelper, enableIast: true, testName: "AspNetCore5IastTestsEnabled", isIastDeduplicationEnabled: false, vulnerabilitiesPerRequest: 100, samplingRate: 50)
        {
        }

        public override async Task TryStartApp()
        {
            EnableIast(IastEnabled);
            SetEnvironmentVariable(ConfigurationKeys.Iast.IsIastDeduplicationEnabled, IsIastDeduplicationEnabled?.ToString() ?? string.Empty);
            SetEnvironmentVariable(ConfigurationKeys.Iast.VulnerabilitiesPerRequest, VulnerabilitiesPerRequest?.ToString() ?? string.Empty);
            SetEnvironmentVariable(ConfigurationKeys.Iast.RequestSampling, SamplingRate?.ToString() ?? string.Empty);
            await Fixture.TryStartApp(this, enableSecurity: false, sendHealthCheck: false);
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        public async Task TestIastWeakHashingRequestSampling()
        {
            var filename = "Iast.WeakHashing.AspNetCore5.IastEnabled";
            IncludeAllHttpSpans = true;
            await TryStartApp();
            SetHttpPort(Fixture.HttpPort);
            await TestWeakHashing(filename, Fixture.Agent);

            filename = "Iast.WeakHashing.AspNetCore5.IastDisabled";
            await TestWeakHashing(filename, Fixture.Agent);

            filename = "Iast.WeakHashing.AspNetCore5.IastEnabled";
            await TestWeakHashing(filename, Fixture.Agent);
        }
    }

    public class AspNetCore5IastTestsOneVulnerabilityPerRequestIastEnabled : AspNetCore5IastTestsVariableVulnerabilityPerRequestIastEnabled
    {
        public AspNetCore5IastTestsOneVulnerabilityPerRequestIastEnabled(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
    : base(fixture, outputHelper, vulnerabilitiesPerRequest: 1)
        {
        }
    }

    public class AspNetCore5IastTestsTwoVulnerabilityPerRequestIastEnabled : AspNetCore5IastTestsVariableVulnerabilityPerRequestIastEnabled
    {
        public AspNetCore5IastTestsTwoVulnerabilityPerRequestIastEnabled(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
    : base(fixture, outputHelper, vulnerabilitiesPerRequest: 2)
        {
        }
    }

    public abstract class AspNetCore5IastTestsVariableVulnerabilityPerRequestIastEnabled : AspNetCore5IastTests
    {
        public AspNetCore5IastTestsVariableVulnerabilityPerRequestIastEnabled(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper, int vulnerabilitiesPerRequest)
            : base(fixture, outputHelper, enableIast: true, testName: "AspNetCore5IastTestsEnabled", isIastDeduplicationEnabled: true, samplingRate: 100, vulnerabilitiesPerRequest: vulnerabilitiesPerRequest)
        {
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        public async Task TestIastWeakHashingRequestVulnerabilitiesPerRequest()
        {
            var filename = VulnerabilitiesPerRequest == 1 ? "Iast.WeakHashing.AspNetCore5.IastEnabled.SingleVulnerability" : "Iast.WeakHashing.AspNetCore5.IastEnabled";
            IncludeAllHttpSpans = true;
            await TryStartApp();
            SetHttpPort(Fixture.HttpPort);
            await TestWeakHashing(filename, Fixture.Agent);
        }
    }

    public class AspNetCore5IastTestsFullSamplingIastEnabled : AspNetCore5IastTestsFullSampling
    {
        public AspNetCore5IastTestsFullSamplingIastEnabled(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
            : base(fixture, outputHelper, enableIast: true, testName: "AspNetCore5IastTestsEnabled")
        {
        }
    }

    public class AspNetCore5IastTestsFullSamplingIastDisabled : AspNetCore5IastTestsFullSampling
    {
        public AspNetCore5IastTestsFullSamplingIastDisabled(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
            : base(fixture, outputHelper, enableIast: false, testName: "AspNetCore5IastTestsDisabled")
        {
        }
    }

    public abstract class AspNetCore5IastTestsFullSampling : AspNetCore5IastTests
    {
        public AspNetCore5IastTestsFullSampling(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper, bool enableIast, string testName, bool? isIastDeduplicationEnabled = null, int? vulnerabilitiesPerRequest = null)
            : base(fixture, outputHelper, enableIast: enableIast, testName: testName, samplingRate: 100, isIastDeduplicationEnabled: isIastDeduplicationEnabled, vulnerabilitiesPerRequest: vulnerabilitiesPerRequest)
        {
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        public async Task TestIastNotWeakRequest()
        {
            var filename = IastEnabled ? "Iast.NotWeak.AspNetCore5.IastEnabled" : "Iast.NotWeak.AspNetCore5.IastDisabled";
            var url = "/Iast";
            IncludeAllHttpSpans = true;
            await TryStartApp();
            SetHttpPort(Fixture.HttpPort);
            var spans = await SendRequestsAsync(Fixture.Agent, new string[] { url });

            var settings = VerifyHelper.GetSpanVerifierSettings();
            settings.AddRegexScrubber(ClientIp, string.Empty);
            settings.AddRegexScrubber(NetworkClientIp, string.Empty);
            await VerifyHelper.VerifySpans(spans, settings)
                              .UseFileName(filename)
                              .DisableRequireUniquePrefix();
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        public async Task TestIastWeakHashingRequest()
        {
            var filename = IastEnabled ? "Iast.WeakHashing.AspNetCore5.IastEnabled" : "Iast.WeakHashing.AspNetCore5.IastDisabled";
            var url = "/Iast/WeakHashing";
            IncludeAllHttpSpans = true;
            await TryStartApp();
            SetHttpPort(Fixture.HttpPort);
            var spans = await SendRequestsAsync(Fixture.Agent, new string[] { url });

            var settings = VerifyHelper.GetSpanVerifierSettings();
            settings.AddRegexScrubber(LocationMsgRegex, string.Empty);
            settings.AddRegexScrubber(ClientIp, string.Empty);
            settings.AddRegexScrubber(NetworkClientIp, string.Empty);
            await VerifyHelper.VerifySpans(spans, settings)
                              .UseFileName(filename)
                              .DisableRequireUniquePrefix();
        }
    }

    public abstract class AspNetCore5IastTests : AspNetBase, IClassFixture<AspNetCoreTestFixture>
    {
        protected static readonly Regex LocationMsgRegex = new(@"(\S)*""location"": {(\r|\n){1,2}(.*(\r|\n){1,2}){0,3}(\s)*},");
        protected static readonly Regex ClientIp = new(@"["" ""]*http.client_ip: .*,(\r|\n){1,2}");
        protected static readonly Regex NetworkClientIp = new(@"["" ""]*network.client.ip: .*,(\r|\n){1,2}");

        public AspNetCore5IastTests(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper, bool enableIast, string testName, bool? isIastDeduplicationEnabled = null, int? samplingRate = null, int? vulnerabilitiesPerRequest = null)
            : base("AspNetCore5", outputHelper, "/shutdown", testName: testName)
        {
            Fixture = fixture;
            IastEnabled = enableIast;
            IsIastDeduplicationEnabled = isIastDeduplicationEnabled;
            VulnerabilitiesPerRequest = vulnerabilitiesPerRequest;
            SamplingRate = samplingRate;
        }

        protected AspNetCoreTestFixture Fixture { get; }

        protected bool IastEnabled { get; }

        protected bool? IsIastDeduplicationEnabled { get; }

        protected int? VulnerabilitiesPerRequest { get; }

        protected int? SamplingRate { get; }

        public override void Dispose()
        {
            base.Dispose();
            Fixture.SetOutput(null);
        }

        public virtual async Task TryStartApp()
        {
            EnableIast(IastEnabled);
            SetEnvironmentVariable(ConfigurationKeys.Iast.IsIastDeduplicationEnabled, IsIastDeduplicationEnabled?.ToString() ?? string.Empty);
            SetEnvironmentVariable(ConfigurationKeys.Iast.VulnerabilitiesPerRequest, VulnerabilitiesPerRequest?.ToString() ?? string.Empty);
            SetEnvironmentVariable(ConfigurationKeys.Iast.RequestSampling, SamplingRate?.ToString() ?? string.Empty);
            await Fixture.TryStartApp(this, enableSecurity: false);
        }

        protected async Task TestWeakHashing(string filename, MockTracerAgent agent)
        {
            var url = "/Iast/WeakHashing";
            var spans = await SendRequestsAsync(agent, new string[] { url });

            var settings = VerifyHelper.GetSpanVerifierSettings();
            settings.AddRegexScrubber(LocationMsgRegex, string.Empty);
            settings.AddRegexScrubber(ClientIp, string.Empty);
            settings.AddRegexScrubber(NetworkClientIp, string.Empty);
            await VerifyHelper.VerifySpans(spans, settings)
                              .UseFileName(filename)
                              .DisableRequireUniquePrefix();
        }
    }
}
#endif
