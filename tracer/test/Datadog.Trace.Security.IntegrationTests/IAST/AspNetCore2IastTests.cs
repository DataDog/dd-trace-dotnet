// <copyright file="AspNetCore2IastTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP2_1
#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name

using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Security.IntegrationTests.IAST;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests.Iast
{
    public class AspNetCore2IastTestsOneVulnerabilityPerRequestIastEnabled : AspNetCore2IastTestsVariableVulnerabilityPerRequestIastEnabled
    {
        public AspNetCore2IastTestsOneVulnerabilityPerRequestIastEnabled(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
    : base(fixture, outputHelper, vulnerabilitiesPerRequest: 1)
        {
        }
    }

    public class AspNetCore2IastTestsTwoVulnerabilityPerRequestIastEnabled : AspNetCore2IastTestsVariableVulnerabilityPerRequestIastEnabled
    {
        public AspNetCore2IastTestsTwoVulnerabilityPerRequestIastEnabled(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
    : base(fixture, outputHelper, vulnerabilitiesPerRequest: 2)
        {
        }
    }

    public abstract class AspNetCore2IastTestsVariableVulnerabilityPerRequestIastEnabled : AspNetCore2IastTests
    {
        public AspNetCore2IastTestsVariableVulnerabilityPerRequestIastEnabled(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper, int vulnerabilitiesPerRequest)
            : base(fixture, outputHelper, enableIast: true, testName: "AspNetCore2IastTestsEnabled", isIastDeduplicationEnabled: false, samplingRate: 100, vulnerabilitiesPerRequest: vulnerabilitiesPerRequest)
        {
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        public async Task TestIastWeakHashingRequestVulnerabilitiesPerRequest()
        {
            IncludeAllHttpSpans = true;
            var filename = VulnerabilitiesPerRequest == 1 ? "Iast.WeakHashing.AspNetCore2.IastEnabled.SingleVulnerability" : "Iast.WeakHashing.AspNetCore2.IastEnabled";
            await TryStartApp();
            var agent = Fixture.Agent;
            await TestWeakHashing(filename, agent);
        }
    }

    public class AspNetCore2IastTestsFullSamplingEnabled : AspNetCore2IastTestsFullSampling
    {
        public AspNetCore2IastTestsFullSamplingEnabled(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
            : base(fixture, outputHelper, enableIast: true, testName: "AspNetCore2IastTestsEnabled", isIastDeduplicationEnabled: false)
        {
        }
    }

    public class AspNetCore2IastTestsFullSamplingDisabled : AspNetCore2IastTestsFullSampling
    {
        public AspNetCore2IastTestsFullSamplingDisabled(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
            : base(fixture, outputHelper, enableIast: false, testName: "AspNetCore2IastTestsDisabled")
        {
        }
    }

    public abstract class AspNetCore2IastTestsFullSampling : AspNetCore2IastTests
    {
        public AspNetCore2IastTestsFullSampling(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper, bool enableIast, string testName, bool? isIastDeduplicationEnabled = null, int? vulnerabilitiesPerRequest = null)
            : base(fixture, outputHelper, enableIast: enableIast, testName: testName, samplingRate: 100, isIastDeduplicationEnabled: isIastDeduplicationEnabled, vulnerabilitiesPerRequest: vulnerabilitiesPerRequest)
        {
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        public async Task TestIastNotWeakRequest()
        {
            var filename = IastEnabled ? "Iast.NotWeak.AspNetCore2.IastEnabled" : "Iast.NotWeak.AspNetCore2.IastDisabled";
            var url = "/Iast";
            IncludeAllHttpSpans = true;
            await TryStartApp();
            var agent = Fixture.Agent;
            var spans = await SendRequestsAsync(agent, new string[] { url });

            var settings = VerifyHelper.GetSpanVerifierSettings();
            await VerifyHelper.VerifySpans(spans, settings)
                              .UseFileName(filename)
                              .DisableRequireUniquePrefix();
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        public async Task TestIastWeakHashingRequest()
        {
            var filename = IastEnabled ? "Iast.WeakHashing.AspNetCore2.IastEnabled" : "Iast.WeakHashing.AspNetCore2.IastDisabled";
            IncludeAllHttpSpans = true;
            await TryStartApp();
            var agent = Fixture.Agent;
            await TestWeakHashing(filename, agent);
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        public async Task TestIastSqlInjectionRequest()
        {
            var filename = IastEnabled ? "Iast.SqlInjection.AspNetCore2.IastEnabled" : "Iast.SqlInjection.AspNetCore2.IastDisabled";
            var url = "/Iast/SqlQuery?query=SELECT%20Surname%20from%20Persons%20where%20name%20=%20%27Vicent%27";
            IncludeAllHttpSpans = true;
            await TryStartApp();
            var agent = Fixture.Agent;
            var spans = await SendRequestsAsync(agent, new string[] { url });
            var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web).ToList();

            var settings = VerifyHelper.GetSpanVerifierSettings();
            settings.AddIastScrubbing();
            await VerifyHelper.VerifySpans(spansFiltered, settings)
                              .UseFileName(filename)
                              .DisableRequireUniquePrefix();
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        public async Task TestIastCommandInjectionRequest()
        {
            var filename = IastEnabled ? "Iast.CommandInjection.AspNetCore2.IastEnabled" : "Iast.CommandInjection.AspNetCore2.IastDisabled";
            var url = "/Iast/ExecuteCommand?file=nonexisting.exe&argumentLine=arg1";
            IncludeAllHttpSpans = true;
            await TryStartApp();
            var agent = Fixture.Agent;
            var spans = await SendRequestsAsync(agent, new string[] { url });
            var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web).ToList();

            var settings = VerifyHelper.GetSpanVerifierSettings();
            settings.AddIastScrubbing();
            await VerifyHelper.VerifySpans(spansFiltered, settings)
                              .UseFileName(filename)
                              .DisableRequireUniquePrefix();
        }
    }

    public class AspNetCore2IastTests50PctSamplingIastEnabled : AspNetCore2IastTests
    {
        public AspNetCore2IastTests50PctSamplingIastEnabled(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
            : base(fixture, outputHelper, enableIast: true, testName: "AspNetCore2IastTestsEnabled", isIastDeduplicationEnabled: false, vulnerabilitiesPerRequest: 100, samplingRate: 50)
        {
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        public async Task TestIastWeakHashingRequestSampling()
        {
            IncludeAllHttpSpans = true;
            var filename = "Iast.WeakHashing.AspNetCore2.IastEnabled";
            await TryStartApp();
            var agent = Fixture.Agent;
            await TestWeakHashing(filename, agent);

            filename = "Iast.WeakHashing.AspNetCore2.IastDisabled";
            await TestWeakHashing(filename, agent);

            filename = "Iast.WeakHashing.AspNetCore2.IastEnabled";
            await TestWeakHashing(filename, agent);
        }

        protected override async Task TryStartApp()
        {
            EnableIast(IastEnabled);
            DisableObfuscationQueryString();
            SetEnvironmentVariable(ConfigurationKeys.Iast.IsIastDeduplicationEnabled, IsIastDeduplicationEnabled?.ToString() ?? string.Empty);
            SetEnvironmentVariable(ConfigurationKeys.Iast.VulnerabilitiesPerRequest, VulnerabilitiesPerRequest?.ToString() ?? string.Empty);
            SetEnvironmentVariable(ConfigurationKeys.Iast.RequestSampling, SamplingRate?.ToString() ?? string.Empty);
            await Fixture.TryStartApp(this, enableSecurity: false, sendHealthCheck: false);
            SetHttpPort(Fixture.HttpPort);
        }
    }

    public abstract class AspNetCore2IastTests : AspNetBase, IClassFixture<AspNetCoreTestFixture>
    {
        public AspNetCore2IastTests(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper, bool enableIast, string testName, bool? isIastDeduplicationEnabled = null, int? samplingRate = null, int? vulnerabilitiesPerRequest = null)
            : base("AspNetCore2", outputHelper, "/shutdown", testName: testName)
        {
            Fixture = fixture;
            fixture.SetOutput(outputHelper);
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

        protected virtual async Task TryStartApp()
        {
            EnableIast(IastEnabled);
            DisableObfuscationQueryString();
            SetEnvironmentVariable(ConfigurationKeys.Iast.IsIastDeduplicationEnabled, IsIastDeduplicationEnabled?.ToString() ?? string.Empty);
            SetEnvironmentVariable(ConfigurationKeys.Iast.VulnerabilitiesPerRequest, VulnerabilitiesPerRequest?.ToString() ?? string.Empty);
            SetEnvironmentVariable(ConfigurationKeys.Iast.RequestSampling, SamplingRate?.ToString() ?? string.Empty);
            await Fixture.TryStartApp(this, enableSecurity: false);
            SetHttpPort(Fixture.HttpPort);
        }

        protected async Task TestWeakHashing(string filename, MockTracerAgent agent)
        {
            var url = "/Iast/WeakHashing";
            var spans = await SendRequestsAsync(agent, expectedSpansPerRequest: 2, url);

            var settings = VerifyHelper.GetSpanVerifierSettings();
            settings.AddIastScrubbing();
            await VerifyHelper.VerifySpans(spans, settings)
                              .UseFileName(filename)
                              .DisableRequireUniquePrefix();
        }
    }
}
#endif
