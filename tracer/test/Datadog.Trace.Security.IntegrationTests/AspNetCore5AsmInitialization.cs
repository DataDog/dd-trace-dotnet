// <copyright file="AspNetCore5AsmInitialization.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_0_OR_GREATER
#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name

using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests
{
    public class AspNetCore5AsmInitializationSecurityDisabled : AspNetCore5AsmInitialization
    {
        public AspNetCore5AsmInitializationSecurityDisabled(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
            : base(fixture, outputHelper, enableSecurity: false, ruleset: null, testName: nameof(AspNetCore5AsmInitializationSecurityDisabled))
        {
        }
    }

    public class AspNetCore5AsmInitializationSecurityDisabledWithBadRuleset : AspNetCore5AsmInitialization
    {
        public AspNetCore5AsmInitializationSecurityDisabledWithBadRuleset(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
            : base(fixture, outputHelper, enableSecurity: false, ruleset: "wrong-tags-name-rule-set.json", testName: nameof(AspNetCore5AsmInitializationSecurityDisabledWithBadRuleset))
        {
        }
    }

    public class AspNetCore5AsmInitializationSecurityEnabled : AspNetCore5AsmInitialization
    {
        public AspNetCore5AsmInitializationSecurityEnabled(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
            : base(fixture, outputHelper, enableSecurity: true, ruleset: null, testName: nameof(AspNetCore5AsmInitializationSecurityEnabled))
        {
        }
    }

    public class AspNetCore5AsmInitializationSecurityEnabledWithBadRuleset : AspNetCore5AsmInitialization
    {
        public AspNetCore5AsmInitializationSecurityEnabledWithBadRuleset(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
            : base(fixture, outputHelper, enableSecurity: true, ruleset: "wrong-tags-name-rule-set.json", testName: nameof(AspNetCore5AsmInitializationSecurityEnabledWithBadRuleset))
        {
        }
    }

    public abstract class AspNetCore5AsmInitialization : AspNetBase, IClassFixture<AspNetCoreTestFixture>
    {
        public AspNetCore5AsmInitialization(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper, bool enableSecurity, string ruleset, string testName)
            : base("AspNetCore5", outputHelper, testName: testName)
        {
            Fixture = fixture;
            Fixture.SetOutput(outputHelper);
            EnableSecurity = enableSecurity;
            RuleSet = ruleset;
        }

        protected AspNetCoreTestFixture Fixture { get; }

        protected bool EnableSecurity { get; }

        protected string RuleSet { get; }

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
            await Fixture.TryStartApp(this, enableSecurity: EnableSecurity, externalRulesFile: RuleSet, sendHealthCheck: false);
            SetHttpPort(Fixture.HttpPort);
            var agent = Fixture.Agent;
            var settings = VerifyHelper.GetSpanVerifierSettings();
            await TestAppSecRequestWithVerifyAsync(agent, url, null, 1, 1, settings, testInit: true, methodNameOverride: nameof(TestSecurityInitialization));
        }
    }
}
#endif
