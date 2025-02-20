// <copyright file="AspNetCore5AutoUserEvents.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_0_OR_GREATER
#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name

using System.Net;
using System.Threading.Tasks;
using Datadog.Trace.AppSec.Rcm.Models.AsmData;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests
{
    public abstract class AspNetCore5AutoUserEvents : AspNetBase, IClassFixture<AspNetCoreTestFixture>
    {
        private readonly AspNetCoreTestFixture _fixture;
        private readonly bool _enableSecurity;

        protected AspNetCore5AutoUserEvents(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper, bool enableSecurity = false, string userTrackingMode = null)
            : base("AspNetCore5", outputHelper, "/shutdown", testName: $"{nameof(AspNetCore5AutoUserEvents)}.{(enableSecurity ? "SecurityOn" : "SecurityOff")}.{userTrackingMode ?? "default"}mode")
        {
            _fixture = fixture;
            _enableSecurity = enableSecurity;
            _fixture.SetOutput(outputHelper);
            EnableRasp(false);

            if (userTrackingMode != null)
            {
                if (userTrackingMode is "ident" or "anon")
                {
                    EnvironmentHelper.CustomEnvironmentVariables.Add("DD_APPSEC_AUTO_USER_INSTRUMENTATION_MODE", userTrackingMode);
                }
                else
                {
                    EnvironmentHelper.CustomEnvironmentVariables.Add("DD_APPSEC_AUTOMATED_USER_EVENTS_TRACKING", userTrackingMode);
                }
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            _fixture.SetOutput(null);
        }

        protected async Task TryStartApp()
        {
            await _fixture.TryStartApp(this, _enableSecurity, externalRulesFile: ("ruleset.blocked.users.json"));
            SetHttpPort(_fixture.HttpPort);
        }

        [SkippableTheory]
        [Trait("RunOnWindows", "True")]
        [InlineData("login.auto.success", "Input.UserName=TestUser&Input.Password=test")]
        [InlineData("login.auto.failure1", "Input.UserName=TestUser&Input.Password=wrong")]
        [InlineData("login.auto.failure2", "Input.UserName=NoSuchUser&Input.Password=test")]
        protected async Task TestUserLoginEvent(string eventName, string bodyString)
        {
            await TryStartApp();
            var agent = _fixture.Agent;
            var settings = VerifyHelper.GetSpanVerifierSettings(eventName, bodyString);
            settings.ScrubAuthenticationCollectionMode();
            await TestAppSecRequestWithVerifyAsync(
                agent,
                "/Account/Index",
                bodyString,
                1,
                1,
                settings,
                contentType: "application/x-www-form-urlencoded",
                methodNameOverride: nameof(TestUserLoginEvent),
                fileNameOverride: GetTestFileName(eventName));
            // reset memory database (useless for net7 as it runs with EF7 on app.db
            await SendRequestsAsync(_fixture.Agent, "/account/reset-memory-db");
            await SendRequestsAsync(_fixture.Agent, "/account/logout");
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        protected async Task TestAuthenticatedRequest()
        {
            await TryStartApp();
            var settings = VerifyHelper.GetSpanVerifierSettings();
            var request = await SubmitRequest("/Account/Index", "Input.UserName=TestUser2&Input.Password=test", contentType: "application/x-www-form-urlencoded");
            request.StatusCode.Should().Be(HttpStatusCode.OK);
            // this is for testuser2 in the in memory user store and appdb
            var userId = "7ccfa5b9-14c2-42b9-8064-834b8293aef4";
            var request2 = await _fixture.Agent.SetupRcmAndWait(
                               Output,
                               [
                                   (new Payload
                                    {
                                        RulesData =
                                        [
                                            new RuleData
                                            {
                                                Id = "blocked_users",
                                                Type = "data_with_expiration",
                                                Data =
                                                [
                                                    new Data { Expiration = 0, Value = userId }, new Data { Expiration = 0, Value = "blocked-user" }
                                                ]
                                            }
                                        ]
                                    },
                                    RcmProducts.AsmData, nameof(TestAuthenticatedRequest)),
                               ]);
            request2.Should().NotBeNull();
            request2.CachedTargetFiles.Should().HaveCount(_enableSecurity ? 1 : 0);
            await TestAppSecRequestWithVerifyAsync(_fixture.Agent, "/Account/SomeAuthenticatedAction", null, 1, 1, settings, fileNameOverride: GetTestFileName(nameof(TestAuthenticatedRequest)), scrubCookiesFingerprint: true);
            // reset memory database (useless for net7 as it runs with EF7 on app.db
            await SendRequestsAsync(_fixture.Agent, "/account/reset-memory-db");
            await SendRequestsAsync(_fixture.Agent, "/account/logout");
        }

        [SkippableTheory]
        [Trait("RunOnWindows", "True")]
        [InlineData("blocked-user")]
        [InlineData("not-blocked-user")]
        protected async Task TestLoginWithSdk(string userIdSdk)
        {
            await TryStartApp();
            var agent = _fixture.Agent;
            var settings = VerifyHelper.GetSpanVerifierSettings(nameof(TestLoginWithSdk), userIdSdk);
            VerifyScrubber.ScrubAuthenticationCollectionMode(settings);
            await TestAppSecRequestWithVerifyAsync(
                agent,
                $"/Account/Index?userIdSdk={userIdSdk}",
                "Input.UserName=TestUser&Input.Password=test",
                1,
                1,
                settings,
                contentType: "application/x-www-form-urlencoded",
                methodNameOverride: nameof(TestUserLoginEvent),
                fileNameOverride: GetTestFileName($"{nameof(TestLoginWithSdk)}.{userIdSdk}"),
                scrubCookiesFingerprint: true);
            // reset memory database (useless for net7 as it runs with EF7 on app.db
            await SendRequestsAsync(_fixture.Agent, "/account/reset-memory-db");
            await SendRequestsAsync(_fixture.Agent, "/account/logout");
        }

        private string GetTestFileName(string testName) => $"{_testName}-{testName}";
    }

    public class AspNetCore5AutoUserEventsDefaultModeSecurityEnabled(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
        : AspNetCore5AutoUserEvents(fixture, outputHelper, true);

    public class AspNetCore5AutoUserEventsExtendedModeSecurityEnabled(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
        : AspNetCore5AutoUserEvents(fixture, outputHelper, true, "extended");

    public class AspNetCore5AutoUserEventsIndentModeSecurityEnabled(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
        : AspNetCore5AutoUserEvents(fixture, outputHelper, true, "ident");

    public class AspNetCore5AutoUserEventsAnonModeSecurityEnabled(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
        : AspNetCore5AutoUserEvents(fixture, outputHelper, true, "anon");

    public class AspNetCore5AutoUserEventsExtendedModeSecurityDisabled(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
        : AspNetCore5AutoUserEvents(fixture, outputHelper, false, "extended")
    {
    }
}
#endif
