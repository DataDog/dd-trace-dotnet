// <copyright file="MockOtlpGrpcServer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Google.Protobuf;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Proto.Collector.Metrics.V1;
using OpenTelemetry.Proto.Metrics.V1;

namespace Datadog.Trace.TestHelpers
{
    /// <summary>
    /// Minimal h2c endpoint that accepts OTLP/gRPC Metrics Export (Unary).
    /// No Grpc.AspNetCore or Grpc.Core dependency. Test-only.
    /// Supports both TCP/IP and Unix Domain Sockets.
    /// </summary>
    public sealed class MockOtlpGrpcServer : IDisposable, IAsyncDisposable
    {
        private readonly IHost _host;
        private readonly string _udsPath;

        public MockOtlpGrpcServer(int? fixedPort = null, string udsPath = null)
        {
            // Allow plaintext HTTP/2 (h2c) client connections in this process
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

            _udsPath = udsPath;

            if (udsPath != null)
            {
                // Delete existing socket file if it exists
                if (File.Exists(udsPath))
                {
                    File.Delete(udsPath);
                }

                UdsPath = udsPath;
            }
            else
            {
                Port = fixedPort ?? TcpPortProvider.GetOpenPort();
            }

            _host = Host.CreateDefaultBuilder()
                .ConfigureWebHostDefaults(web =>
                {
                    web.ConfigureKestrel(k =>
                    {
                        if (_udsPath != null)
                        {
                            // Listen on Unix Domain Socket
                            k.ListenUnixSocket(_udsPath, o => o.Protocols = HttpProtocols.Http2);
                        }
                        else
                        {
                            // h2c on localhost
                            k.ListenLocalhost(Port, o => o.Protocols = HttpProtocols.Http2);
                        }
                    });

                    web.Configure(app =>
                    {
                        app.UseRouting();
                        app.UseEndpoints(endpoints =>
                        {
                            var grpcPath = "/opentelemetry.proto.collector.metrics.v1.MetricsService/Export";

                            endpoints.MapPost(grpcPath, async context =>
                            {
                                if (!(context.Request.ContentType?.Contains("application/grpc", StringComparison.OrdinalIgnoreCase) ?? false))
                                {
                                    context.Response.StatusCode = StatusCodes.Status415UnsupportedMediaType;
                                    await context.Response.BodyWriter.WriteAsync(Encoding.ASCII.GetBytes("unsupported content-type"));

                                    return;
                                }

                                byte[] framed;
                                using (var ms = new MemoryStream())
                                {
                                    await context.Request.Body.CopyToAsync(ms);
                                    framed = ms.ToArray();
                                }

                                // gRPC message framing: 1 byte compressed flag + 4 bytes big-endian length
                                if (framed.Length < 5)
                                {
                                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                                    return;
                                }

                                var compressed = framed[0] == 1;
                                var len = BinaryPrimitives.ReadUInt32BigEndian(framed.AsSpan(1, 4));
                                if (framed.Length < 5 + len)
                                {
                                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                                    return;
                                }

                                var payload = framed.AsSpan(5, (int)len).ToArray();

                                // Optional gzip (rare for OTLP client-to-server, but handle it)
                                if (compressed || string.Equals(context.Request.Headers["grpc-encoding"], "gzip", StringComparison.OrdinalIgnoreCase))
                                {
                                    using var input = new MemoryStream(payload);
                                    await using var gzip = new GZipStream(input, CompressionMode.Decompress);
                                    using var outMs = new MemoryStream();
                                    await gzip.CopyToAsync(outMs);
                                    payload = outMs.ToArray();
                                }

                                // Parse Export request (requires vendored Collector.Metrics proto)
                                var exportReq = ExportMetricsServiceRequest.Parser.ParseFrom(payload);

                                // Build a MetricsData equivalent so the rest of your code can reuse existing assertions
                                var metricsData = new MetricsData();
                                metricsData.ResourceMetrics.Add(exportReq.ResourceMetrics);

                                var headersDict = context.Request.Headers
                                                         .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToList(), StringComparer.OrdinalIgnoreCase);

                                Requests.Enqueue(new MockTracerAgent.MockOtlpRequest(
                                                     pathAndQuery: "grpc:MetricsService/Export",
                                                     headers: headersDict,
                                                     body: payload,           // raw protobuf of ExportMetricsServiceRequest
                                                     contentType: "application/grpc",
                                                     metricsData: metricsData));

                                // Respond with empty ExportMetricsServiceResponse
                                var respBytes = new ExportMetricsServiceResponse().ToByteArray();

                                var header = new byte[5];
                                header[0] = 0; // not compressed
                                BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(1, 4), (uint)respBytes.Length);

                                context.Response.StatusCode = StatusCodes.Status200OK;
                                context.Response.ContentType = "application/grpc";

                                await context.Response.Body.WriteAsync(header, 0, header.Length);
                                await context.Response.Body.WriteAsync(respBytes, 0, respBytes.Length);

                                // Set gRPC status trailers (best-effort across ASP.NET Core versions)
                                if (context.Response.SupportsTrailers())
                                {
                                    context.Response.DeclareTrailer("grpc-status");
                                    context.Response.AppendTrailer("grpc-status", "0"); // OK
                                }
                                else
                                {
                                    context.Response.Headers["grpc-status"] = "0";
                                }
                            });
                        });
                    });
                })
                .Build();

            _host.Start();
        }

        public int Port { get; }

        public string UdsPath { get; }

        public System.Collections.Concurrent.ConcurrentQueue<MockTracerAgent.MockOtlpRequest> Requests { get; } = new();

        public void Dispose()
        {
            _host.Dispose();

            // Clean up UDS socket file
            if (_udsPath != null && File.Exists(_udsPath))
            {
                try
                {
                    File.Delete(_udsPath);
                }
                catch
                {
                    // Best effort cleanup
                }
            }
        }

        public ValueTask DisposeAsync()
        {
            if (_host is IAsyncDisposable asyncHost)
            {
                return asyncHost.DisposeAsync();
            }

            _host.Dispose();
            return default;
        }
    }
}

#endif
