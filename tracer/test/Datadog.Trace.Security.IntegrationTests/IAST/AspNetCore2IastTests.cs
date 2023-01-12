// <copyright file="AspNetCore2IastTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP2_1
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
    public class AspNetCore2IastTestsEnabled : AspNetCore2IastTests
    {
        public AspNetCore2IastTestsEnabled(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
            : base(fixture, outputHelper, enableIast: true, testName: nameof(AspNetCore2IastTestsEnabled))
        {
        }

        [SkippableTheory]
        [InlineData(1)]
        [InlineData(2)]
        [Trait("RunOnWindows", "True")]
        public async Task TestIastWeakHashingRequestVulnerabilitiesPerRequest(int vulnerabilitiesPerRequest)
        {
            SetEnvironmentVariable(ConfigurationKeys.Iast.IsIastDeduplicationEnabled, "true");
            SetEnvironmentVariable(ConfigurationKeys.Iast.VulnerabilitiesPerRequest, vulnerabilitiesPerRequest.ToString());
            SetEnvironmentVariable(ConfigurationKeys.Iast.RequestSampling, "100");
            IncludeAllHttpSpans = true;
            var filename = vulnerabilitiesPerRequest == 1 ? "Iast.WeakHashing.AspNetCore2.IastEnabled.SingleVulnerability" : "Iast.WeakHashing.AspNetCore2.IastEnabled";
            await TryStartApp();
            SetHttpPort(Fixture.HttpPort);
            await TestWeakHashing(filename, Fixture.Agent);
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        public async Task TestIastWeakHashingRequestSampling()
        {
            SetEnvironmentVariable(ConfigurationKeys.Iast.IsIastDeduplicationEnabled, "false");
            SetEnvironmentVariable(ConfigurationKeys.Iast.VulnerabilitiesPerRequest, "100");
            SetEnvironmentVariable(ConfigurationKeys.Iast.RequestSampling, "50");
            IncludeAllHttpSpans = true;
            var filename = "Iast.WeakHashing.AspNetCore2.IastEnabled";
            await TryStartApp();
            SetHttpPort(Fixture.HttpPort);
            await TestWeakHashing(filename, Fixture.Agent);

            filename = "Iast.WeakHashing.AspNetCore2.IastDisabled";
            await TestWeakHashing(filename, Fixture.Agent);

            filename = "Iast.WeakHashing.AspNetCore2.IastEnabled";
            await TestWeakHashing(filename, Fixture.Agent);
        }
    }

    public class AspNetCore2IastTestsDisabled : AspNetCore2IastTests
    {
        public AspNetCore2IastTestsDisabled(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
            : base(fixture, outputHelper, enableIast: false, testName: nameof(AspNetCore2IastTestsDisabled))
        {
        }
    }

    public abstract class AspNetCore2IastTests : AspNetBase, IClassFixture<AspNetCoreTestFixture>
    {
        private static readonly Regex LocationMsgRegex = new(@"(\S)*""location"": {(\r|\n){1,2}(.*(\r|\n){1,2}){0,3}(\s)*},");
        private static readonly Regex ClientIp = new(@"["" ""]*http.client_ip: .*,(\r|\n){1,2}");
        private static readonly Regex NetworkClientIp = new(@"["" ""]*network.client.ip: .*,(\r|\n){1,2}");

        public AspNetCore2IastTests(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper, bool enableIast, string testName)
            : base("AspNetCore2", outputHelper, "/shutdown", testName: testName)
        {
            Fixture = fixture;
            IastEnabled = enableIast;
            EnableIast(enableIast);
        }

        protected AspNetCoreTestFixture Fixture { get; }

        protected bool IastEnabled { get; }

        public override void Dispose()
        {
            base.Dispose();
            Fixture.SetOutput(null);
        }

        public async Task TryStartApp()
        {
            await Fixture.TryStartApp(this, enableSecurity: false);
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        public async Task TestIastNotWeakRequest()
        {
            var filename = IastEnabled ? "Iast.NotWeak.AspNetCore2.IastEnabled" : "Iast.NotWeak.AspNetCore2.IastDisabled";
            var url = "/Iast";
            IncludeAllHttpSpans = true;
            await TryStartApp();
            SetHttpPort(Fixture.HttpPort);
            var spans = await SendRequestsAsync(Fixture.Agent, new string[] { url });

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
            SetHttpPort(Fixture.HttpPort);
            await TestWeakHashing(filename, Fixture.Agent);
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
