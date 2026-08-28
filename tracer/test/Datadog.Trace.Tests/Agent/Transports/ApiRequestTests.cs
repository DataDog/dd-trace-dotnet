// <copyright file="ApiRequestTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
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
    public async Task ApiWebRequest(bool useGzip, bool withTimeout)
    {
        using var agent = MockTracerAgent.Create(_output);
        var url = new Uri($"http://localhost:{agent.Port}/");

        var timeout = withTimeout ? TimeSpan.FromSeconds(90) : (TimeSpan?)null;
        var factory = new ApiWebRequestFactory(url, AgentHttpHeaderNames.DefaultHeaders, timeout);
        await RunTest(agent, () => factory.Create(url), useGzip);
    }

    // These tests exercise the deadline-enforcement added to ApiWebRequest to work around
    // HttpWebRequest.Timeout having no effect on the async GetRequestStreamAsync/
    // GetResponseAsync APIs.

    [Fact]
    public async Task ApiWebRequest_PostAsync_AbortedMidFlight_ThrowsOperationCanceledException()
    {
        using var listener = new BlackHoleTcpListener();
        using var cts = new CancellationTokenSource();
        var request = CreateWebRequest(listener.Port);
        var bytes = Encoding.UTF8.GetBytes("{}");

        try
        {
            var pending = request.SendAsync("POST", "application/json", null, stream => stream.WriteAsync(bytes, 0, bytes.Length), cts);

            await listener.Accepted;
            cts.Cancel();

            Func<Task> act = () => pending;
            await act.Should().ThrowAsync<OperationCanceledException>();
        }
        finally
        {
            listener.Release();
        }
    }

    [Fact]
    public async Task ApiWebRequest_GetAsync_AbortedMidFlight_ThrowsOperationCanceledException()
    {
        using var listener = new BlackHoleTcpListener();
        using var cts = new CancellationTokenSource();
        var request = CreateWebRequest(listener.Port);

        try
        {
            var pending = request.SendAsync("GET", null, null, null, cts);

            await listener.Accepted;
            cts.Cancel();

            Func<Task> act = () => pending;
            await act.Should().ThrowAsync<OperationCanceledException>();
        }
        finally
        {
            listener.Release();
        }
    }

    [Fact]
    public async Task ApiWebRequest_AlreadyCancelledDeadline_ThrowsOperationCanceledException()
    {
        using var listener = new BlackHoleTcpListener();
        using var cts = new CancellationTokenSource();
        var request = CreateWebRequest(listener.Port);

        try
        {
            // Cancel before the send even starts, so the abort lands during
            // GetRequestStreamAsync/connect, not during GetResponseAsync.
            cts.Cancel();

            Func<Task> act = () => request.SendAsync("GET", null, null, null, cts);
            await act.Should().ThrowAsync<OperationCanceledException>();
        }
        finally
        {
            listener.Release();
        }
    }

    [Fact]
    public async Task ApiWebRequest_ProtocolError_WithoutTimeout_StillReturnsResponse()
    {
        using var agent = MockTracerAgent.Create(_output);
        agent.ShouldDeserializeTraces = false;
        agent.CustomResponses[MockTracerResponseType.Traces] = new MockTracerResponse("{\"errors\":[\"bad request\"]}", 400);

        var url = new Uri($"http://localhost:{agent.Port}/");
        var factory = new ApiWebRequestFactory(url, AgentHttpHeaderNames.DefaultHeaders, TimeSpan.FromSeconds(30));
        var request = factory.Create(url);
        var bytes = Encoding.UTF8.GetBytes("{}");

        var response = await request.PostAsync(new ArraySegment<byte>(bytes), "application/json");

        response.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task ApiWebRequest_PostAsync_ProducerStalledOnUnrelatedIO_ThrowsOperationCanceledException()
    {
        // The deadline must bound the request-body producer even when it's blocked on something
        // other than the request stream (e.g. reading a local file), where Abort() alone has no
        // effect. Use a body producer that never completes on its own -- if the deadline doesn't
        // bound it, this test hangs instead of failing.
        //
        // This producer never writes to the request stream, so we can't synchronize on
        // listener.Accepted: GetRequestStreamAsync() hands back a buffering stream (neither
        // ContentLength nor SendChunked is set), and HttpWebRequest defers the actual connect/send
        // until that stream sees data or is closed. Since nothing is ever written, no connection is
        // ever attempted - waiting on the listener would hang forever. Instead, synchronize on the
        // producer having actually been invoked.
        using var listener = new BlackHoleTcpListener();
        using var cts = new CancellationTokenSource();
        var request = CreateWebRequest(listener.Port);
        var neverCompletes = new TaskCompletionSource<bool>();
        var started = new TaskCompletionSource<bool>();

        try
        {
            var pending = request.SendAsync(
                "POST",
                "application/json",
                null,
                _ =>
                {
                    started.TrySetResult(true);
                    return neverCompletes.Task;
                },
                cts);

            await started.Task;
            cts.Cancel();

            Func<Task> act = () => pending;
            await act.Should().ThrowAsync<OperationCanceledException>();
        }
        finally
        {
            neverCompletes.TrySetResult(true);
            listener.Release();
        }
    }

    [Fact]
    public async Task ApiWebRequest_PostAsync_AbortedWhileBlockedWritingRequestStream_ThrowsOperationCanceledException()
    {
        // Make sure that request is aborted when the producer is blocked writing to the request stream
        // BlackHoleTcpListener never reads from the socket, so a large enough write blocks once the OS send buffer fills.
        using var listener = new BlackHoleTcpListener();
        using var cts = new CancellationTokenSource();
        var request = CreateWebRequest(listener.Port);
        var bytes = new byte[10 * 1024 * 1024];

        try
        {
            var pending = request.SendAsync(
                "POST",
                "application/json",
                null,
                async stream =>
                {
                    // Write in chunks so the write genuinely blocks on the full send buffer instead
                    // of buffering the whole payload in one WriteAsync call.
                    const int chunkSize = 64 * 1024;
                    for (var offset = 0; offset < bytes.Length; offset += chunkSize)
                    {
                        var count = Math.Min(chunkSize, bytes.Length - offset);
                        await stream.WriteAsync(bytes, offset, count);
                    }
                },
                cts);

            await listener.Accepted;
            cts.Cancel();

            Func<Task> act = () => pending;
            await act.Should().ThrowAsync<OperationCanceledException>();
        }
        finally
        {
            listener.Release();
        }
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
            new DatadogHttpClient(TraceAgentHttpHeaderHelper.Instance),
            Localhost);
        await RunTest(agent, () => factory.Create(Localhost), useGzip);
    }

    [Fact]
    public async Task HttpClientRequest_TimesOut_PropagatesTaskCanceledException()
    {
        var handler = new StubHttpMessageHandler(_ => throw new TaskCanceledException("Simulated HttpClient.Timeout"));
        var factory = new HttpClientRequestFactory(Localhost, AgentHttpHeaderNames.DefaultHeaders, handler);
        var request = factory.Create(Localhost);

        Func<Task> act = request.GetAsync;
        await act.Should().ThrowAsync<TaskCanceledException>();
    }

    [Fact]
    public async Task HttpClientRequest_OtherFailure_PropagatesUnchanged()
    {
        var handler = new StubHttpMessageHandler(_ => throw new HttpRequestException("Simulated connection failure"));
        var factory = new HttpClientRequestFactory(Localhost, AgentHttpHeaderNames.DefaultHeaders, handler);
        var request = factory.Create(Localhost);

        Func<Task> act = request.GetAsync;
        await act.Should().ThrowAsync<HttpRequestException>();
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

    [SkippableTheory]
    [CombinatorialData]
    [Trait("Category", "LinuxUnsupported")]
    [Flaky("Named pipes is notoriously flaky", maxRetries: 3)]
    public async Task HttpStreamRequest_NamedPipes(bool useGzip)
    {
        SkipOn.AllExcept(SkipOn.PlatformValue.Windows);

        using var agent = MockTracerAgent.Create(_output, new WindowsPipesConfig($"trace-{Guid.NewGuid()}", null));
        var factory = new HttpStreamRequestFactory(
            new NamedPipeClientStreamFactory(agent.TracesWindowsPipeName, timeoutMs: 100),
            new DatadogHttpClient(TraceAgentHttpHeaderHelper.Instance),
            Localhost);
        await RunTest(agent, () => factory.Create(Localhost), useGzip);
    }

    private static ApiWebRequest CreateWebRequest(int port) => new((HttpWebRequest)WebRequest.Create($"http://127.0.0.1:{port}/"));

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

#if NETCOREAPP3_1_OR_GREATER
    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_responder(request));
    }
#endif

    private sealed class BlackHoleTcpListener : IDisposable
    {
        private readonly TcpListener _listener;
        private readonly TaskCompletionSource<bool> _accepted = new();
        private readonly TaskCompletionSource<bool> _release = new();
        private volatile bool _stopping;

        public BlackHoleTcpListener()
        {
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;

            var thread = new Thread(Run) { IsBackground = true };
            thread.Start();
        }

        public int Port { get; }

        public Task Accepted => _accepted.Task;

        public void Release() => _release.TrySetResult(true);

        public void Dispose()
        {
            // Tell Run() to give up instead of calling the blocking AcceptTcpClient() if nothing has
            // connected yet. On some runtimes (e.g. netcoreapp3.1 on Linux), Stop()/Dispose() spin-waits
            // for an in-flight accept to release the socket handle, and a newly-arriving connection
            // doesn't reliably wake it -- deadlocking test cleanup. Avoiding the blocking call entirely
            // once we're stopping sidesteps that instead of depending on the runtime to interrupt it.
            _stopping = true;
            Release();

            try
            {
                _listener.Stop();
            }
            catch
            {
                // best-effort cleanup
            }
        }

        private void Run()
        {
            TcpClient client = null;
            try
            {
                while (!_stopping && !_listener.Pending())
                {
                    Thread.Sleep(10);
                }

                if (_stopping)
                {
                    return;
                }

                client = _listener.AcceptTcpClient();
                _accepted.TrySetResult(true);

                // Hold the connection open -- never finish the response -- until the test is done with it.
                _release.Task.Wait();
            }
            catch (Exception ex)
            {
                _accepted.TrySetException(ex);
            }
            finally
            {
                client?.Close();
            }
        }
    }
}
