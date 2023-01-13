// <copyright file="AspNetCore5.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_0_OR_GREATER
#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name

using System;
using System.Net;
using System.Threading.Tasks;
using Datadog.Trace.AppSec;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests
{
    public class AspNetCore5TestsSecurityDisabled : AspNetCore5TestsWithoutExternalRulesFile
    {
        public AspNetCore5TestsSecurityDisabled(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
            : base(fixture, outputHelper, enableSecurity: false, testName: "AspNetCore5.SecurityDisabled")
        {
        }
    }

    public class AspNetCore5TestsSecurityEnabled : AspNetCore5TestsWithoutExternalRulesFile
    {
        public AspNetCore5TestsSecurityEnabled(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
            : base(fixture, outputHelper, enableSecurity: true, testName: "AspNetCore5.SecurityEnabled")
        {
        }
    }

    public class AspNetCore5TestsSecurityEnabledInitialization : AspNetBase, IClassFixture<AspNetCoreTestFixture>
    {
        public AspNetCore5TestsSecurityEnabledInitialization(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
            : base("AspNetCore5", outputHelper, "/shutdown", testName: "AspNetCore5.SecurityEnabled")
        {
            Fixture = fixture;
            Fixture.SetOutput(outputHelper);
        }

        protected AspNetCoreTestFixture Fixture { get; }

        public override void Dispose()
        {
            base.Dispose();
            Fixture.SetOutput(null);
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        public async Task TestSecurityInitialization()
        {
            var url = "/Health/?[$slice]=value";
            await Fixture.TryStartApp(this, enableSecurity: true, sendHealthCheck: false);
            SetHttpPort(Fixture.HttpPort);
            var settings = VerifyHelper.GetSpanVerifierSettings();
            await TestAppSecRequestWithVerifyAsync(Fixture.Agent, url, null, 1, 1, settings, testInit: true, methodNameOverride: nameof(TestSecurityInitialization));
        }
    }

    public abstract class AspNetCore5TestsWithoutExternalRulesFile : AspNetCoreBase
    {
        public AspNetCore5TestsWithoutExternalRulesFile(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper, bool enableSecurity, string testName)
            : base("AspNetCore5", fixture, outputHelper, "/shutdown", enableSecurity: enableSecurity, testName: testName)
        {
        }

        [SkippableTheory]
        [InlineData(AddressesConstants.RequestPathParams, HttpStatusCode.OK, "/params-endpoint/appscan_fingerprint")]
        [Trait("RunOnWindows", "True")]
        public async Task TestPathParamsEndpointRouting(string test, HttpStatusCode expectedStatusCode, string url)
        {
            await TryStartApp();
            SetHttpPort(Fixture.HttpPort);

            var sanitisedUrl = VerifyHelper.SanitisePathsForVerify(url);
            var settings = VerifyHelper.GetSpanVerifierSettings(test, (int)expectedStatusCode, sanitisedUrl);

            // for .NET 7+, the endpoint names changed from
            // aspnet_core.endpoint: /params-endpoint/{s} HTTP: GET,
            // to
            // aspnet_core.endpoint: HTTP: GET /params-endpoint/{s},
            // So normalize to the .NET 6 pattern for simplicity
#if NET7_0_OR_GREATER
            settings.AddSimpleScrubber("HTTP: GET /params-endpoint/{s}", "/params-endpoint/{s} HTTP: GET");
#endif
            await TestAppSecRequestWithVerifyAsync(Fixture.Agent, url, null, 5, 1, settings);
        }
    }

    public class AspNetCore5TestsSecurityDisabledWithDefaultExternalRulesFile : AspNetCoreSecurityDisabledWithExternalRulesFile
    {
        public AspNetCore5TestsSecurityDisabledWithDefaultExternalRulesFile(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
            : base("AspNetCore5", fixture, outputHelper, "/shutdown", ruleFile: DefaultRuleFile, testName: "AspNetCore5.SecurityDisabled")
        {
        }
    }

    public class AspNetCore5TestsSecurityEnabledWithDefaultExternalRulesFile : AspNetCoreSecurityEnabledWithExternalRulesFile
    {
        public AspNetCore5TestsSecurityEnabledWithDefaultExternalRulesFile(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
            : base("AspNetCore5", fixture, outputHelper, "/shutdown", ruleFile: DefaultRuleFile, testName: "AspNetCore5.SecurityEnabled")
        {
        }
    }

    public class AspNetCore5TestsSecurityEnabledWithFaultyExternalRulesFile : AspNetCore5WithFaultyExternalRulesFile
    {
        public AspNetCore5TestsSecurityEnabledWithFaultyExternalRulesFile(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
            : base(fixture, outputHelper, enableSecurity: true, testName: "AspNetCore5.SecurityEnabled.WithFaultyExternalRulesFile")
        {
        }
    }

    public class AspNetCore5TestsSecurityDisabledWithFaultyExternalRulesFile : AspNetCore5WithFaultyExternalRulesFile
    {
        public AspNetCore5TestsSecurityDisabledWithFaultyExternalRulesFile(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
            : base(fixture, outputHelper, enableSecurity: false, testName: "AspNetCore5.SecurityDisabled.WithFaultyExternalRulesFile")
        {
        }
    }

    public abstract class AspNetCore5WithFaultyExternalRulesFile : AspNetBase, IClassFixture<AspNetCoreTestFixture>
    {
        public AspNetCore5WithFaultyExternalRulesFile(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper, bool enableSecurity, string testName = null)
            : base("AspNetCore5", outputHelper, "/shutdown", testName: testName)
        {
            EnableSecurity = enableSecurity;
            Fixture = fixture;
            Fixture.SetOutput(outputHelper);
            RuleFile = "wrong-tags-name-rule-set.json";
        }

        protected AspNetCoreTestFixture Fixture { get; }

        protected bool EnableSecurity { get; }

        protected string RuleFile { get; }

        public override void Dispose()
        {
            base.Dispose();
            Fixture.SetOutput(null);
        }

        public async Task TryStartApp()
        {
            await Fixture.TryStartApp(this, EnableSecurity, externalRulesFile: RuleFile);
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        public async Task TestSecurityInitialization()
        {
            var url = "/Health/?[$slice]=value";
            await TryStartApp();
            SetHttpPort(Fixture.HttpPort);
            var settings = VerifyHelper.GetSpanVerifierSettings();
            await TestAppSecRequestWithVerifyAsync(Fixture.Agent, url, null, 1, 1, settings, testInit: true, methodNameOverride: nameof(TestSecurityInitialization));
        }
    }
}
#endif
