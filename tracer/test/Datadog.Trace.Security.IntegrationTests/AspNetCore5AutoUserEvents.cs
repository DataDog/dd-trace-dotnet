// <copyright file="AspNetCore5AutoUserEvents.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_0_OR_GREATER
#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name

using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
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

        public virtual string GetTestFileName(string testName)
        {
            return $"{_testName}-{testName}";
        }

        public override void Dispose()
        {
            base.Dispose();
            _fixture.SetOutput(null);
        }

        protected async Task TryStartApp()
        {
            await _fixture.TryStartApp(this, _enableSecurity);
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
            var url = "/Account/Index";
            var settings = VerifyHelper.GetSpanVerifierSettings(eventName, bodyString);
            await TestAppSecRequestWithVerifyAsync(agent, url, bodyString, 1, 1, settings, contentType: "application/x-www-form-urlencoded", methodNameOverride: nameof(TestUserLoginEvent), fileNameOverride: GetTestFileName(eventName));
            // reset memory database (useless for net7 as it runs with EF7 on app.db
            await SendRequestsAsync(_fixture.Agent, "/account/reset-memory-db");
            await SendRequestsAsync(_fixture.Agent, "/account/logout");
        }
    }

    public class AspNetCore5AutoUserEventsDefaultModeSecurityEnabled : AspNetCore5AutoUserEvents
    {
        public AspNetCore5AutoUserEventsDefaultModeSecurityEnabled(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
            : base(fixture, outputHelper, true)
        {
        }
    }

    public class AspNetCore5AutoUserEventsDefaultModeSecurityDisabled : AspNetCore5AutoUserEvents
    {
        public AspNetCore5AutoUserEventsDefaultModeSecurityDisabled(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
            : base(fixture, outputHelper, false)
        {
        }
    }

    public class AspNetCore5AutoUserEventsExtendedModeSecurityEnabled : AspNetCore5AutoUserEvents
    {
        public AspNetCore5AutoUserEventsExtendedModeSecurityEnabled(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
            : base(fixture, outputHelper, true, "extended")
        {
        }
    }

    public class AspNetCore5AutoUserEventsIndentModeSecurityEnabled : AspNetCore5AutoUserEvents
    {
        public AspNetCore5AutoUserEventsIndentModeSecurityEnabled(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
            : base(fixture, outputHelper, true, "ident")
        {
        }
    }

    public class AspNetCore5AutoUserEventsAnonModeSecurityEnabled : AspNetCore5AutoUserEvents
    {
        public AspNetCore5AutoUserEventsAnonModeSecurityEnabled(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
            : base(fixture, outputHelper, true, "anon")
        {
        }
    }

    public class AspNetCore5AutoUserEventsExtendedModeSecurityDisabled : AspNetCore5AutoUserEvents
    {
        public AspNetCore5AutoUserEventsExtendedModeSecurityDisabled(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
            : base(fixture, outputHelper, false, "extended")
        {
        }
    }
}
#endif
