// <copyright file="AspNetCoreNetFrameworkDisabledTestBase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AspNetCore
{
    public abstract class AspNetCoreNetFrameworkDisabledTestBase : TestHelper
    {
        private const string SqlResource = "SELECT 1";

        private readonly string _snapshotPrefix;

        protected AspNetCoreNetFrameworkDisabledTestBase(string sampleName, string snapshotPrefix, ITestOutputHelper output)
            : base(sampleName, output)
        {
            _snapshotPrefix = snapshotPrefix;
            SetEnvironmentVariable(ConfigurationKeys.HeaderTags, AspNetCoreNetFrameworkTopology.HeaderTags);
            SetEnvironmentVariable(ConfigurationKeys.PropagationStyleExtract, AspNetCoreNetFrameworkTopology.PropagationStyleExtract);
            SetEnvironmentVariable("ENABLE_MANUAL_TRACING_MIDDLEWARE", "false");
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public async Task DisabledSwitchesProduceNoRequestSpanAndLeaveSqlQueryAsRoot()
        {
            var featureDisabledRootSpans = await GetSpansWithDisabledSwitch(featureEnabled: false, integrationEnabled: true, headers: null);
            AssertOnlyRootSqlSpan(featureDisabledRootSpans);

            var featureDisabledPropagationSpans = await GetSpansWithDisabledSwitch(
                                                      featureEnabled: false,
                                                      integrationEnabled: true,
                                                      headers: AspNetCoreNetFrameworkTopology.CreateIncomingHeaders());
            AssertOnlyRootSqlSpan(featureDisabledPropagationSpans);

            var integrationDisabledSpans = await GetSpansWithDisabledSwitch(
                                               featureEnabled: true,
                                               integrationEnabled: false,
                                               headers: AspNetCoreNetFrameworkTopology.CreateIncomingHeaders());
            AssertOnlyRootSqlSpan(integrationDisabledSpans);

            var settings = AspNetCoreNetFrameworkTopology.GetSpanVerifierSettings();
            await VerifyHelper.VerifySpans(
                                  AspNetCoreNetFrameworkTopology.IncludeUpstreamSpan(featureDisabledPropagationSpans),
                                  settings,
                                  AspNetCoreNetFrameworkTopology.OrderSpans)
                              .UseFileName($"{_snapshotPrefix}.PropagationTopology.Disabled");

            await VerifyHelper.VerifySpans(featureDisabledRootSpans, AspNetCoreNetFrameworkTopology.GetSpanVerifierSettings())
                              .UseFileName($"{_snapshotPrefix}.Topology.Disabled");
        }

        private void AssertOnlyRootSqlSpan(IImmutableList<MockSpan> spans)
        {
            spans.Should().ContainSingle();
            var sqlSpan = spans.Single();
            sqlSpan.Name.Should().Be("sql-server.query");
            sqlSpan.Resource.Should().Be(SqlResource);
            sqlSpan.TraceId.Should().NotBe(AspNetCoreNetFrameworkTopology.IncomingTraceId);
            sqlSpan.ParentId.Should().BeNull();
        }

        private async Task<IImmutableList<MockSpan>> GetSpansWithDisabledSwitch(
            bool featureEnabled,
            bool integrationEnabled,
            Dictionary<string, string> headers)
        {
            SetEnvironmentVariable(ConfigurationKeys.FeatureFlags.AspNetCoreNetFrameworkEnabled, featureEnabled.ToString());
            SetEnvironmentVariable("DD_TRACE_ASPNETCORE_ENABLED", integrationEnabled.ToString());

            using (var fixture = new AspNetCoreTestFixture())
            {
                fixture.SetOutput(Output);
                await fixture.TryStartApp(this, sendHealthCheck: false);

                var startTime = DateTimeOffset.UtcNow;
                using (var request = fixture.CreateRequest(HttpMethod.Get, "/baseline/sql?item=42", headers))
                {
                    var statusCode = await fixture.SendHttpRequest(request);
                    statusCode.Should().Be(HttpStatusCode.OK);
                }

                var spans = await fixture.Agent.WaitForSpansAsync(
                                count: 1,
                                timeoutInMilliseconds: 20_000,
                                minDateTime: startTime,
                                returnAllOperations: true);
                return spans;
            }
        }
    }
}

#endif
