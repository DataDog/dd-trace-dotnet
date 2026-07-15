// <copyright file="AspNetCoreNetFrameworkColdStartTestBase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AspNetCore
{
    public abstract class AspNetCoreNetFrameworkColdStartTestBase : TestHelper
    {
        private readonly AspNetCoreTestFixture _fixture;

        protected AspNetCoreNetFrameworkColdStartTestBase(string sampleName, AspNetCoreTestFixture fixture, ITestOutputHelper output)
            : base(sampleName, output)
        {
            _fixture = fixture;
            _fixture.SetOutput(output);
            SetEnvironmentVariable(ConfigurationKeys.FeatureFlags.AspNetCoreNetFrameworkEnabled, "true");
            SetEnvironmentVariable("DD_TRACE_ASPNETCORE_ENABLED", "true");
            SetEnvironmentVariable("ENABLE_MANUAL_TRACING_MIDDLEWARE", "false");
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public async Task FirstRequestAfterColdStartIsTraced()
        {
            await _fixture.TryStartApp(this, sendHealthCheck: false);

            var startTime = DateTimeOffset.UtcNow;
            using (var request = _fixture.CreateRequest(HttpMethod.Get, "/attribute/items/cold"))
            {
                var statusCode = await _fixture.SendHttpRequest(request);
                statusCode.Should().Be(HttpStatusCode.OK);
            }

            var spans = await _fixture.Agent.WaitForSpansAsync(
                            count: 1,
                            timeoutInMilliseconds: 20_000,
                            minDateTime: startTime,
                            returnAllOperations: true);
            var requestSpan = spans.Single(span => span.Name == "aspnet_core.request");
            requestSpan.Resource.Should().Be("GET attribute/items/{id}");
            requestSpan.ParentId.Should().BeNull();
            requestSpan.GetTag("http.status_code").Should().Be("200");
        }

        public override void Dispose()
        {
            _fixture.SetOutput(null);
            base.Dispose();
        }
    }
}

#endif
