// <copyright file="ApiRequestTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.StreamFactories;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.HttpOverStreams;
using Datadog.Trace.Telemetry;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Serialization;
using FluentAssertions;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Tests.Agent.Transports;

[Collection(nameof(WebRequestCollection))]
[UsesVerify]
public class ApiRequestTests
{
    // Matches SerializationHelpers.DefaultSettings
    private static readonly JsonSerializerSettings DefaultSettings = new() { NullValueHandling = NullValueHandling.Ignore, ContractResolver = new DefaultContractResolver { NamingStrategy = new SnakeCaseNamingStrategy(), } };

    private static readonly Uri Localhost = new Uri("http://localhost");
    private readonly ITestOutputHelper _output;

    public ApiRequestTests(ITestOutputHelper output)
    {
        _output = output;
        VerifyHelper.InitializeGlobalSettings();
    }

    [Theory]
    [CombinatorialData]
    public async Task ApiWebRequest(bool useGzip)
    {
        using var agent = MockTracerAgent.Create(_output);
        var url = new Uri($"http://localhost:{agent.Port}/");
        var factory = new ApiWebRequestFactory(url, AgentHttpHeaderNames.DefaultHeaders);
        await RunTest(agent, () => factory.Create(url), useGzip);
    }

#if NETCOREAPP3_1_OR_GREATER

    [Theory]
    [CombinatorialData]
    public async Task HttpClientRequest(bool useGzip)
    {
        using var agent = MockTracerAgent.Create(_output);
        var url = new Uri($"http://localhost:{agent.Port}/");
        var factory = new HttpClientRequestFactory(url, AgentHttpHeaderNames.DefaultHeaders);
        await RunTest(agent, () => factory.Create(url), useGzip);
    }

    [Theory]
    [CombinatorialData]
    public async Task HttpStreamRequest_UDS(bool useGzip)
    {
        using var agent = MockTracerAgent.Create(_output, new UnixDomainSocketConfig(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()), null));
        var factory = new HttpStreamRequestFactory(
            new UnixDomainSocketStreamFactory(agent.TracesUdsPath),
            new DatadogHttpClient(new TraceAgentHttpHeaderHelper()),
            Localhost);
        await RunTest(agent, () => factory.Create(Localhost), useGzip);
    }
#endif

#if NET6_0_OR_GREATER
    [Theory]
    [CombinatorialData]
    public async Task HttpClientRequest_UDS(bool useGzip)
    {
        using var agent = MockTracerAgent.Create(_output, new UnixDomainSocketConfig(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()), null));
        var factory = new SocketHandlerRequestFactory(
            new UnixDomainSocketStreamFactory(agent.TracesUdsPath),
            AgentHttpHeaderNames.DefaultHeaders,
            Localhost);
        await RunTest(agent, () => factory.Create(Localhost), useGzip);
    }
#endif

    private async Task RunTest(MockTracerAgent agent, Func<IApiRequest> createRequest, bool useGzip)
    {
        agent.ShouldDeserializeTraces = false;
        byte[] requestBody = null;
        agent.RequestReceived += (_, args) =>
        {
         requestBody = args.Value.ReadStreamBody();
        };

        var request = createRequest();
        var compression = useGzip ? MultipartCompression.GZip : MultipartCompression.None;
        var payload = GetData();
        await request.PostAsJsonAsync(payload, compression);

        // payload should be the same as if we had serialized directly
        // We have to use the vendored NewtonsoftJson here to ensure it reads all the attributes etc correctly
        var expectedPayload = EncodingHelpers.Utf8NoBom.GetBytes(JsonConvert.SerializeObject(payload, DefaultSettings));
        requestBody.Should().NotBeNull().And.Equal(expectedPayload, "serialized request body was '{0}' but expected '{1}'", EncodingHelpers.Utf8NoBom.GetString(requestBody), EncodingHelpers.Utf8NoBom.GetString(expectedPayload));
    }

    private TelemetryData GetData() =>
        new TelemetryData(
            requestType: TelemetryRequestTypes.GenerateMetrics,
            runtimeId: "20338dfd-f700-4e5c-b3f6-0d470f054ae8",
            seqId: 5672,
            tracerTime: 1628099086,
            application: new ApplicationTelemetryData(
                serviceName: "myapp",
                env: "prod",
                serviceVersion: "1.2.3",
                tracerVersion: "0.33.1",
                languageName: "node.js",
                languageVersion: "14.16.1",
                runtimeName: "dotnet",
                runtimeVersion: "7.0.3",
                commitSha: "testCommitSha",
                repositoryUrl: "testRepositoryUrl",
                processTags: "entrypoint.basedir:Users,entrypoint.workdir:Downloads"),
            host: new HostTelemetryData(
                hostname: "i-09ecf74c319c49be8",
                os: "GNU/Linux",
                architecture: "x86_64")
            {
                OsVersion = "ubuntu 18.04.5 LTS (Bionic Beaver)",
                KernelName = "Linux",
                KernelRelease = "5.4.0-1037-gcp",
                KernelVersion = "#40~18.04.1-Ubuntu SMP Fri Feb 5 15:41:35 UTC 2021"
            },
            payload: new GenerateMetricsPayload(
                new MetricData[]
                {
                    new(
                        "tracer_init_time",
                        new MetricSeries()
                        {
                            new(1575317847, 2241),
                            new(1575317947, 2352),
                        },
                        common: true,
                        type: MetricTypeConstants.Count)
                    {
                        Tags = new[]
                        {
                            "org_id: 2",
                            "environment:test"
                        }
                    },
                    new(
                        "app_sec_initialization_time",
                        new MetricSeries()
                        {
                            new(1575317447, 254),
                            new(1575317547, 643),
                        },
                        common: false,
                        type: MetricTypeConstants.Gauge)
                    {
                        Namespace = MetricNamespaceConstants.ASM,
                        Interval = 60,
                    },
                }));
}


    // #endif
    //
    //     [Theory]
    //     [MemberData(nameof(GetTestData))]
    //     [Trait("Category", "LinuxUnsupported")]
    //     public async Task HttpStreamRequest_NamedPipes_MultipartTest(bool useStream, bool useGzip)
    //     {
    //         if (!EnvironmentTools.IsWindows())
    //         {
    //             // Can't use WindowsNamedPipes on non-Windows
    //             return;
    //         }
    //
    //         // named pipes is notoriously flaky
    //         var attemptsRemaining = 1;
    //         while (true)
    //         {
    //             try
    //             {
    //                 attemptsRemaining--;
    //                 await RunNamedPipesTest();
    //                 return;
    //             }
    //             catch (Exception ex) when (attemptsRemaining > 0 && ex is not SkipException)
    //             {
    //             }
    //         }
    //
    //         async Task RunNamedPipesTest()
    //         {
    //             using var agent = MockTracerAgent.Create(_output, new WindowsPipesConfig($"trace-{Guid.NewGuid()}", null));
    //             var factory = new HttpStreamRequestFactory(
    //                 new NamedPipeClientStreamFactory(agent.TracesWindowsPipeName, timeoutMs: 100),
    //                 new DatadogHttpClient(new TraceAgentHttpHeaderHelper()),
    //                 Localhost);
    //             await RunTest(agent, () => factory.Create(Localhost), useStream, useGzip, nameof(ApiWebRequest_MultipartTest));
    //         }
    //     }
    //
    //     [Theory]
    //     [MemberData(nameof(GetTestData))]
    //     [Trait("Category", "LinuxUnsupported")]
    //     public async Task HttpStreamRequest_NamedPipes_VerificationTest(bool useStream, bool useGzip)
    //     {
    //         if (!EnvironmentTools.IsWindows())
    //         {
    //             // Can't use WindowsNamedPipes on non-Windows
    //             return;
    //         }
    //
    //         // named pipes is notoriously flaky
    //         var attemptsRemaining = 1;
    //         while (true)
    //         {
    //             try
    //             {
    //                 attemptsRemaining--;
    //                 await RunNamedPipesTest();
    //                 return;
    //             }
    //             catch (Exception ex) when (attemptsRemaining > 0 && ex is not SkipException)
    //             {
    //             }
    //         }
    //
    //         async Task RunNamedPipesTest()
    //         {
    //             using var agent = MockTracerAgent.Create(_output, new WindowsPipesConfig($"trace-{Guid.NewGuid()}", null));
    //             var factory = new HttpStreamRequestFactory(
    //                 new NamedPipeClientStreamFactory(agent.TracesWindowsPipeName, timeoutMs: 100),
    //                 new DatadogHttpClient(new TraceAgentHttpHeaderHelper()),
    //                 Localhost);
    //             await RunValidationTest(agent, () => factory.Create(Localhost), useStream, useGzip, nameof(ApiWebRequest_ValidationTest));
    //         }
    //     }
