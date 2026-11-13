// <copyright file="EnvironmentalFailureTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.DogStatsd;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.Stats;
using Datadog.Trace.TestHelpers.TestTracer;
using FluentAssertions;
using MessagePack;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Tests.Agent;

public class EnvironmentalFailureTests
{
    private readonly ITestOutputHelper _output;

    public EnvironmentalFailureTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    [Trait("Category", "EnvironmentalFaultInjection")]
    public async Task TraceFlushWaitsForServerAcknowledgementAfterUploadCompletes()
    {
        using var releaseResponse = new ManualResetEventSlim();
        using var intake = new RecordingTraceIntake(
            _output,
            _ =>
            {
                releaseResponse.Wait();
                return new MockTracerResponse("{}", statusCode: 200);
            });
        var writer = CreateWriter(intake.Endpoint);
        const ulong traceId = 0x1001;

        writer.WriteTrace(CreateTrace(traceId));
        var flush = writer.FlushTracesAsync();

        try
        {
            var uploadedBody = await intake.FirstBody.WaitAsync(TimeSpan.FromSeconds(10));
            flush.IsCompleted.Should().BeFalse("the server has the complete body but has not acknowledged it");
            ReadTraceIds(uploadedBody).Should().ContainSingle().Which.Should().Be(traceId);
        }
        finally
        {
            releaseResponse.Set();
        }

        await flush.WaitAsync(TimeSpan.FromSeconds(10));
        await writer.FlushAndCloseAsync().WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Fact]
    [Trait("Category", "EnvironmentalFaultInjection")]
    public async Task TraceCloseWaitsForServerAcknowledgementAfterUploadCompletes()
    {
        using var releaseResponse = new ManualResetEventSlim();
        using var intake = new RecordingTraceIntake(
            _output,
            _ =>
            {
                releaseResponse.Wait();
                return new MockTracerResponse("{}", statusCode: 200);
            });
        var writer = CreateWriter(intake.Endpoint);
        const ulong traceId = 0x1002;

        writer.WriteTrace(CreateTrace(traceId));
        var close = writer.FlushAndCloseAsync();

        try
        {
            var uploadedBody = await intake.FirstBody.WaitAsync(TimeSpan.FromSeconds(10));
            close.IsCompleted.Should().BeFalse("close must include the active send and its acknowledgement");
            ReadTraceIds(uploadedBody).Should().ContainSingle().Which.Should().Be(traceId);
        }
        finally
        {
            releaseResponse.Set();
        }

        await close.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Fact]
    [Trait("Category", "EnvironmentalFaultInjection")]
    public async Task TraceUploadRetriesIdenticalPayloadAfterServerError()
    {
        using var intake = new RecordingTraceIntake(
            _output,
            attempt => attempt == 1
                           ? new MockTracerResponse("injected failure", statusCode: 500)
                           : new MockTracerResponse("{}", statusCode: 200));
        var writer = CreateWriter(intake.Endpoint);
        const ulong traceId = 0x1003;

        writer.WriteTrace(CreateTrace(traceId));
        await writer.FlushTracesAsync().WaitAsync(TimeSpan.FromSeconds(10));

        var bodies = intake.Bodies.ToArray();
        bodies.Should().HaveCount(2, "HTTP 500 is a retryable trace-intake response");
        bodies[1].Should().Equal(bodies[0]);
        bodies.SelectMany(ReadTraceIds).Should().Equal(traceId, traceId);

        await writer.FlushAndCloseAsync().WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Fact]
    [Trait("Category", "EnvironmentalFaultInjection")]
    [Trait("Category", "FormalMethodsPhase2")]
    public async Task Diagnostic_TraceTransportExceptionBypassesRetryLoop()
    {
        using var intake = new ResetAfterUploadTraceIntake();
        var exceptions = new ConcurrentQueue<Exception>();
        var attempts = 0;
        var innerFactory = new HttpClientRequestFactory(intake.Endpoint, AgentHttpHeaderNames.DefaultHeaders, timeout: TimeSpan.FromSeconds(30));
        var factory = new Mock<IApiRequestFactory>();
        factory.Setup(x => x.GetEndpoint(It.IsAny<string>())).Returns((string path) => innerFactory.GetEndpoint(path));
        factory.Setup(x => x.Info(It.IsAny<Uri>())).Returns((Uri endpoint) => innerFactory.Info(endpoint));
        factory.Setup(x => x.Create(It.IsAny<Uri>())).Returns(
            (Uri endpoint) =>
            {
                Interlocked.Increment(ref attempts);
                var innerRequest = innerFactory.Create(endpoint);
                var request = new Mock<IApiRequest>();
                request.Setup(x => x.AddHeader(It.IsAny<string>(), It.IsAny<string>()))
                       .Callback((string name, string value) => innerRequest.AddHeader(name, value));
                request.Setup(x => x.PostAsync(It.IsAny<ArraySegment<byte>>(), It.IsAny<string>()))
                       .Returns(
                            async (ArraySegment<byte> bytes, string contentType) =>
                            {
                                try
                                {
                                    return await innerRequest.PostAsync(bytes, contentType);
                                }
                                catch (Exception ex)
                                {
                                    exceptions.Enqueue(ex);
                                    throw;
                                }
                            });
                return request.Object;
            });
        var writer = CreateWriter(factory.Object);
        const ulong traceId = 0x1004;

        writer.WriteTrace(CreateTrace(traceId));
        await writer.FlushTracesAsync().WaitAsync(TimeSpan.FromSeconds(10));

        var bodies = intake.Bodies.ToArray();
        exceptions.Should().ContainSingle();
        GetExceptionChain(exceptions.Single()).Any(exception => exception is SocketException).Should().BeTrue();
        attempts.Should().Be(1, "a thrown callback leaves SendWithRetry's result as Failed_DontRetry");
        bodies.Should().ContainSingle("the connection reset prevents every configured retry attempt");
        ReadTraceIds(bodies[0]).Should().ContainSingle().Which.Should().Be(traceId);

        await writer.FlushAndCloseAsync().WaitAsync(TimeSpan.FromSeconds(10));
    }

    private static AgentWriter CreateWriter(Uri endpoint)
    {
        var factory = new HttpClientRequestFactory(endpoint, AgentHttpHeaderNames.DefaultHeaders, timeout: TimeSpan.FromSeconds(30));
        return CreateWriter(factory);
    }

    private static AgentWriter CreateWriter(IApiRequestFactory factory)
    {
        var api = new Api(
            factory,
            TestStatsdManager.NoOp,
            new ContainerMetadata(containerId: null, entityId: null),
            updateSampleRates: null,
            updateConfigState: null,
            partialFlushEnabled: false,
            healthMetricsEnabled: false);
        return AgentWriterHelper.CreateWithManualFlush(api);
    }

    private static SpanCollection CreateTrace(ulong traceId)
    {
        var span = new Span(new SpanContext(traceId, traceId), DateTimeOffset.UtcNow)
        {
            OperationName = $"environmental-fault-{traceId}",
        };
        return new SpanCollection([span]);
    }

    private static IEnumerable<ulong> ReadTraceIds(byte[] body)
        => MessagePackSerializer.Deserialize<IList<IList<MockSpan>>>(body).SelectMany(trace => trace).Select(span => span.TraceId);

    private static IEnumerable<Exception> GetExceptionChain(Exception exception)
    {
        while (exception is not null)
        {
            yield return exception;
            exception = exception.InnerException;
        }
    }

    private sealed class RecordingTraceIntake : IDisposable
    {
        private readonly MockTracerAgent.TcpUdpAgent _agent;
        private readonly Func<int, MockTracerResponse> _responseForAttempt;
        private readonly TaskCompletionSource<byte[]> _firstBody = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _attempt;

        public RecordingTraceIntake(ITestOutputHelper output, Func<int, MockTracerResponse> responseForAttempt)
        {
            _responseForAttempt = responseForAttempt;
            _agent = MockTracerAgent.Create(output, useTelemetry: false);
            _agent.ShouldDeserializeTraces = false;
            _agent.RequestReceived += OnRequestReceived;
        }

        public Uri Endpoint => new($"http://127.0.0.1:{_agent.Port}/");

        public Task<byte[]> FirstBody => _firstBody.Task;

        public ConcurrentQueue<byte[]> Bodies { get; } = new();

        public void Dispose()
        {
            _agent.RequestReceived -= OnRequestReceived;
            _agent.Dispose();
        }

        private void OnRequestReceived(object sender, EventArgs<MockHttpRequest> args)
        {
            if (!args.Value.PathAndQuery.StartsWith("/v0.4/traces", StringComparison.Ordinal))
            {
                return;
            }

            var body = args.Value.ReadStreamBody();
            Bodies.Enqueue(body);
            _firstBody.TrySetResult(body);

            var attempt = Interlocked.Increment(ref _attempt);
            _agent.CustomResponses[MockTracerResponseType.Traces] = _responseForAttempt(attempt);
        }
    }

    private sealed class ResetAfterUploadTraceIntake : IDisposable
    {
        private static readonly byte[] SuccessResponse = Encoding.ASCII.GetBytes(
            "HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: 2\r\nConnection: close\r\n\r\n{}");

        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly Task _listenerTask;
        private int _attempt;

        public ResetAfterUploadTraceIntake()
        {
            _listener.Start();
            var endpoint = (IPEndPoint)_listener.LocalEndpoint;
            Endpoint = new Uri($"http://127.0.0.1:{endpoint.Port}/");
            _listenerTask = Task.Run(ListenAsync);
        }

        public Uri Endpoint { get; }

        public ConcurrentQueue<byte[]> Bodies { get; } = new();

        public void Dispose()
        {
            _listener.Stop();
            try
            {
                _listenerTask.GetAwaiter().GetResult();
            }
            catch (SocketException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private async Task ListenAsync()
        {
            while (true)
            {
                TcpClient client;
                try
                {
                    client = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);
                }
                catch (SocketException) when (!_listener.Server.IsBound)
                {
                    return;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }

                using (client)
                {
                    var attempt = Interlocked.Increment(ref _attempt);
                    if (attempt == 1)
                    {
                        client.Client.LingerState = new LingerOption(enable: true, seconds: 0);
                    }

                    var stream = client.GetStream();
                    Bodies.Enqueue(await ReadRequestBodyAsync(stream).ConfigureAwait(false));

                    if (attempt == 1)
                    {
                        client.Client.Close();
                        continue;
                    }

                    await stream.WriteAsync(SuccessResponse, 0, SuccessResponse.Length).ConfigureAwait(false);
                }
            }
        }

        private async Task<byte[]> ReadRequestBodyAsync(Stream stream)
        {
            using var headerBytes = new MemoryStream();
            var singleByte = new byte[1];
            var matchedTerminatorBytes = 0;
            var terminator = new byte[] { (byte)'\r', (byte)'\n', (byte)'\r', (byte)'\n' };

            while (matchedTerminatorBytes < terminator.Length)
            {
                var count = await stream.ReadAsync(singleByte, 0, 1).ConfigureAwait(false);
                if (count == 0)
                {
                    throw new EndOfStreamException("Connection ended before the HTTP headers were complete.");
                }

                var value = singleByte[0];
                headerBytes.WriteByte(value);
                matchedTerminatorBytes = value == terminator[matchedTerminatorBytes]
                                             ? matchedTerminatorBytes + 1
                                             : value == terminator[0] ? 1 : 0;
            }

            var headers = Encoding.ASCII.GetString(headerBytes.ToArray());
            var contentLengthLine = headers.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries)
                                           .Single(line => line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase));
            var contentLength = int.Parse(contentLengthLine.Substring(contentLengthLine.IndexOf(':') + 1).Trim());
            var body = new byte[contentLength];
            var offset = 0;
            while (offset < body.Length)
            {
                var count = await stream.ReadAsync(body, offset, body.Length - offset).ConfigureAwait(false);
                if (count == 0)
                {
                    throw new EndOfStreamException("Connection ended before the HTTP body was complete.");
                }

                offset += count;
            }

            return body;
        }
    }
}
