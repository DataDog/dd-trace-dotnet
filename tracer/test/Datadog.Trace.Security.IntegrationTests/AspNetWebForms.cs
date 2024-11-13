// <copyright file="AspNetWebForms.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System.Net;
using System.Security.Policy;
using System.Threading.Tasks;
using Datadog.Trace.Iast.Telemetry;
using Datadog.Trace.Security.IntegrationTests.IAST;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name

namespace Datadog.Trace.Security.IntegrationTests
{
    [Collection("IisTests")]
    public class AspNetWebFormsIntegratedWithIast : AspNetWebFormsWithIast
    {
        public AspNetWebFormsIntegratedWithIast(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, classicMode: false, enableSecurity: true)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetWebFormsClassicIntegratedWithIast : AspNetWebFormsWithIast
    {
        public AspNetWebFormsClassicIntegratedWithIast(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, classicMode: true, enableSecurity: true)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetWebFormsIntegratedWithSecurity : AspNetWebForms
    {
        public AspNetWebFormsIntegratedWithSecurity(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, classicMode: false, enableSecurity: true)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetWebFormsIntegratedWithoutSecurity : AspNetWebForms
    {
        public AspNetWebFormsIntegratedWithoutSecurity(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, classicMode: false, enableSecurity: false)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetWebFormsClassicWithSecurity : AspNetWebForms
    {
        public AspNetWebFormsClassicWithSecurity(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, classicMode: true, enableSecurity: true)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetWebFormsClassicWithoutSecurity : AspNetWebForms
    {
        public AspNetWebFormsClassicWithoutSecurity(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, classicMode: true, enableSecurity: false)
        {
        }
    }

    public abstract class AspNetWebForms : AspNetBase, IClassFixture<IisFixture>, IAsyncLifetime
    {
        private readonly IisFixture _iisFixture;
        private readonly bool _classicMode;

        public AspNetWebForms(IisFixture iisFixture, ITestOutputHelper output, bool classicMode, bool enableSecurity, bool enableIast = false)
            : base("WebForms", output, "/home/shutdown", @"test\test-applications\security\aspnet")
        {
            SetSecurity(enableSecurity);
            SetEnvironmentVariable(Configuration.ConfigurationKeys.AppSec.Rules, DefaultRuleFile);
            SetEnvironmentVariable(Configuration.ConfigurationKeys.AppSec.StackTraceEnabled, "false");

            _iisFixture = iisFixture;
            _classicMode = classicMode;
            _testName = "Security." + nameof(AspNetWebForms)
                     + (classicMode ? ".Classic" : ".Integrated")
                     + ".enableSecurity=" + enableSecurity;
        }

        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("LoadFromGAC", "True")]
        [Theory]
        [InlineData("/Health?test&[$slice]", null)]
        [InlineData("/Health/Params/appscan_fingerprint", null)]
        [InlineData("/Health/wp-config", null)]
        [InlineData("/Health?arg=[$slice]", null)]
        [InlineData("/Health", "ctl00%24MainContent%24testBox=%5B%24slice%5D")]
        public Task TestSecurity(string url, string body)
        {
            // if blocking is enabled, request stops before reaching asp net mvc integrations intercepting before action methods, so no more spans are generated
            // NOTE: by integrating the latest version of the WAF, blocking was disabled, as it does not support blocking yet
            var sanitisedUrl = VerifyHelper.SanitisePathsForVerify(url);
            var settings = VerifyHelper.GetSpanVerifierSettings(sanitisedUrl, body);
            return TestAppSecRequestWithVerifyAsync(_iisFixture.Agent, url, body, 5, 1, settings, "application/x-www-form-urlencoded");
        }

        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("LoadFromGAC", "True")]
        [SkippableTheory]
        [InlineData("blocking")]
        public async Task TestBlockedRequest(string test)
        {
            var url = "/Health";

            var settings = VerifyHelper.GetSpanVerifierSettings(test);
            await TestAppSecRequestWithVerifyAsync(_iisFixture.Agent, url, null, 5, 1, settings, userAgent: "Hello/V");
        }

        public async Task InitializeAsync()
        {
            await _iisFixture.TryStartIis(this, _classicMode ? IisAppType.AspNetClassic : IisAppType.AspNetIntegrated);
            SetHttpPort(_iisFixture.HttpPort);
        }

        public Task DisposeAsync() => Task.CompletedTask;

        protected override string GetTestName() => _testName;
    }

    public abstract class AspNetWebFormsWithIast : AspNetBase, IClassFixture<IisFixture>, IAsyncLifetime
    {
        private readonly IisFixture _iisFixture;
        private readonly bool _classicMode;

        public AspNetWebFormsWithIast(IisFixture iisFixture, ITestOutputHelper output, bool classicMode, bool enableSecurity)
            : base("WebForms", output, "/home/shutdown", @"test\test-applications\security\aspnet")
        {
            EnableIast(true);
            EnableEvidenceRedaction(false);
            EnableIastTelemetry((int)IastMetricsVerbosityLevel.Off);
            SetEnvironmentVariable("DD_IAST_DEDUPLICATION_ENABLED", "false");
            SetEnvironmentVariable("DD_IAST_REQUEST_SAMPLING", "100");
            SetEnvironmentVariable("DD_IAST_MAX_CONCURRENT_REQUESTS", "100");
            SetEnvironmentVariable("DD_IAST_VULNERABILITIES_PER_REQUEST", "100");
            SetEnvironmentVariable(Configuration.ConfigurationKeys.AppSec.StackTraceEnabled, "false");

            _iisFixture = iisFixture;
            _classicMode = classicMode;
            _testName = "Security." + nameof(AspNetWebForms)
                     + (classicMode ? ".Classic" : ".Integrated")
                     + ".enableSecurity=" + enableSecurity;
        }

        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("LoadFromGAC", "True")]
        [SkippableTheory]
        [InlineData("TestQueryParameterNameVulnerability")]
        public async Task TestQueryParameterNameVulnerability(string test)
        {
            var url = "/print?Encrypt=True&ClientDatabase=774E4D65564946426A53694E48756B592B444A6C43673D3D&p=413&ID=2376&EntityType=114&Print=True&OutputType=WORDOPENXML&SSRSReportID=1";

            var settings = VerifyHelper.GetSpanVerifierSettings(test);
            settings.AddIastScrubbing();

            await TestAppSecRequestWithVerifyAsync(_iisFixture.Agent, url, null, 1, 1, settings, userAgent: "Hello/V");
        }

        public async Task InitializeAsync()
        {
            await _iisFixture.TryStartIis(this, _classicMode ? IisAppType.AspNetClassic : IisAppType.AspNetIntegrated);
            SetHttpPort(_iisFixture.HttpPort);
        }

        public Task DisposeAsync() => Task.CompletedTask;

        protected override string GetTestName() => _testName;
    }
}
#endif
