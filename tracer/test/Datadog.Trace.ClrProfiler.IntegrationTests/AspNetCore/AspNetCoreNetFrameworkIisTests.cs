// <copyright file="AspNetCoreNetFrameworkIisTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
    [Collection("IisTests")]
    [Trait("RequiresDockerDependency", "true")]
    [Trait("DockerGroup", "2")]
    public class AspNetCoreNetFrameworkIisTests : TestHelper, IClassFixture<IisFixture>, IAsyncLifetime
    {
        private const string SampleName = "AspNetCoreNetFramework";
        private const string MongoResource = "find aspnet-core-net-framework-repro";

        private readonly IisFixture _fixture;

        public AspNetCoreNetFrameworkIisTests(IisFixture fixture, ITestOutputHelper output)
            : base(SampleName, output)
        {
            _fixture = fixture;
            _fixture.ShutdownPath = "/shutdown";
            SetEnvironmentVariable(ConfigurationKeys.FeatureFlags.AspNetCoreNetFrameworkEnabled, "true");
            SetEnvironmentVariable("DD_TRACE_ASPNETCORE_ENABLED", "true");
            SetEnvironmentVariable(ConfigurationKeys.PropagationStyleExtract, "Datadog,TraceContext");
            SetEnvironmentVariable("ENABLE_MANUAL_TRACING_MIDDLEWARE", "false");
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("Category", "LinuxUnsupported")]
        [Trait("RunOnWindows", "True")]
        public async Task Net48OutOfProcessColdStartPropagationParentingAndOwnership()
        {
            var datadogStart = DateTimeOffset.UtcNow;
            await SendRequest(
                "/attribute/items/cold",
                new Dictionary<string, string>
                {
                    ["x-datadog-trace-id"] = AspNetCoreNetFrameworkTopology.IncomingTraceId.ToString(),
                    ["x-datadog-parent-id"] = AspNetCoreNetFrameworkTopology.IncomingParentId.ToString(),
                    ["x-datadog-sampling-priority"] = "1",
                });

            var datadogSpans = await _fixture.Agent.WaitForSpansAsync(
                                   count: 1,
                                   timeoutInMilliseconds: 20_000,
                                   minDateTime: datadogStart,
                                   returnAllOperations: true);
            var coldStartSpan = datadogSpans.Single(
                                    span => span.Name == "aspnet_core.request"
                                         && span.TraceId == AspNetCoreNetFrameworkTopology.IncomingTraceId);
            coldStartSpan.ParentId.Should().Be(AspNetCoreNetFrameworkTopology.IncomingParentId);
            coldStartSpan.Resource.Should().Be("GET attribute/items/{id}");

            var applicationProcessId = GetApplicationProcessId(coldStartSpan);

            var w3cStart = DateTimeOffset.UtcNow;
            await SendRequest(
                "/baseline/mongo?item=42",
                new Dictionary<string, string>
                {
                    ["traceparent"] = "00-000000000000000000000000075bcd15-000000003ade68b1-01",
                });

            var w3cSpans = await _fixture.Agent.WaitForSpansAsync(
                               count: 2,
                               timeoutInMilliseconds: 20_000,
                               minDateTime: w3cStart,
                               returnAllOperations: true);
            var requestSpan = w3cSpans.Single(
                                  span => span.Name == "aspnet_core.request"
                                       && span.TraceId == AspNetCoreNetFrameworkTopology.IncomingTraceId);
            var mongoSpan = w3cSpans.Single(span => span.Name == "mongodb.query" && span.Resource == MongoResource);

            requestSpan.ParentId.Should().Be(AspNetCoreNetFrameworkTopology.IncomingParentId);
            requestSpan.Resource.Should().Be("GET /baseline/mongo");
            mongoSpan.TraceId.Should().Be(requestSpan.TraceId);
            mongoSpan.ParentId.Should().Be(requestSpan.SpanId);
            GetApplicationProcessId(requestSpan).Should().Be(applicationProcessId);
        }

        public Task InitializeAsync() => _fixture.TryStartIis(this, IisAppType.AspNetCoreOutOfProcess, sendHealthCheck: false);

        public Task DisposeAsync() => Task.CompletedTask;

        private int GetApplicationProcessId(MockSpan requestSpan)
        {
            var processIdMetric = requestSpan.GetMetric("process_id");
            processIdMetric.Should().NotBeNull("the ASP.NET Core request is the local root in the application process");
            var processId = Convert.ToInt32(processIdMetric.Value);
            var iisProcessId = _fixture.IisExpress.Process.Process.Id;

            processId.Should().NotBe(iisProcessId, "IIS Express/ANCM must not own the application request span");
            ProcessHelper.GetChildrenIds(iisProcessId).Should().Contain(processId, "ANCM must launch the profiled application process");

            using (var process = Process.GetProcessById(processId))
            {
                process.ProcessName.Should().Be(Path.GetFileNameWithoutExtension(EnvironmentHelper.GetSampleApplicationFileName()));
            }

            return processId;
        }

        private async Task SendRequest(string path, Dictionary<string, string> headers)
        {
            using (var client = new HttpClient())
            using (var request = new HttpRequestMessage(HttpMethod.Get, $"http://localhost:{_fixture.HttpPort}{path}"))
            {
                foreach (var header in headers)
                {
                    request.Headers.Add(header.Key, header.Value);
                }

                using (var response = await client.SendAsync(request))
                {
                    response.StatusCode.Should().Be(HttpStatusCode.OK);
                }
            }
        }
    }
}

#endif
