// <copyright file="AspNetCore5ExternalRules.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_0_OR_GREATER

using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests
{
    public class AspNetCore5ExternalRules : AspNetBase, IClassFixture<AspNetCoreTestFixture>
    {
        public AspNetCore5ExternalRules(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
            : base("AspNetCore5", outputHelper, testName: nameof(AspNetCore5ExternalRules))
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
        [Trait("Category", "ArmUnsupported")]
        public async Task TestSecurity()
        {
            await Fixture.TryStartApp(this, enableSecurity: true, externalRulesFile: DefaultRuleFile);
            SetHttpPort(Fixture.HttpPort);
            var agent = Fixture.Agent;

            var settings = VerifyHelper.GetSpanVerifierSettings();

            await TestAppSecRequestWithVerifyAsync(agent, DefaultAttackUrl, null, 5, 1, settings);
        }
    }
}
#endif
