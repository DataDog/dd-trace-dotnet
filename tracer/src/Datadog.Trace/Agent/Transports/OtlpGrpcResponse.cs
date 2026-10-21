// <copyright file="OtlpGrpcResponse.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_1_OR_GREATER
#nullable enable

using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;
using Datadog.Trace.Vendors.OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient.Grpc;
using GrpcStatusCode = Datadog.Trace.Vendors.OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient.Grpc.StatusCode;

namespace Datadog.Trace.Agent.Transports;

/// <summary>
/// The response to an <see cref="OtlpGrpcRequest"/>. A gRPC call reports its outcome in the grpc-status trailer, so
/// <see cref="StatusCode"/> is the HTTP status code with the same OTLP retry semantics, and a failed call's content
/// describes its gRPC status. This lets <see cref="ApiOtlp"/> handle gRPC and HTTP responses the same way.
/// </summary>
internal sealed class OtlpGrpcResponse : IApiResponse
{
    private readonly HttpClientResponse _response;
    private readonly byte[]? _grpcStatusDescription;

    public OtlpGrpcResponse(HttpResponseMessage response)
    {
        _response = new HttpClientResponse(response);

        // gRPC servers always reply with a 200 status code: any other one comes from a proxy or a non-gRPC server
        if (response.StatusCode != HttpStatusCode.OK)
        {
            StatusCode = (int)response.StatusCode;
            return;
        }

        var status = GrpcProtocolHelpers.GetResponseStatus(response, response.TrailingHeaders);
        StatusCode = GetHttpStatusCode(status.StatusCode, response);

        if (status.StatusCode != GrpcStatusCode.OK)
        {
            _grpcStatusDescription = Encoding.UTF8.GetBytes($"gRPC status {status.StatusCode}: {status.Detail}");
        }
    }

    public int StatusCode { get; }

    public long ContentLength => _grpcStatusDescription?.Length ?? _response.ContentLength;

    public string? ContentTypeHeader => _grpcStatusDescription is null ? _response.ContentTypeHeader : "text/plain; charset=utf-8";

    public string? ContentEncodingHeader => _grpcStatusDescription is null ? _response.ContentEncodingHeader : null;

    public string? GetHeader(string headerName) => _response.GetHeader(headerName);

    public Encoding GetCharsetEncoding() => _grpcStatusDescription is null ? _response.GetCharsetEncoding() : EncodingHelpers.Utf8NoBom;

    public ContentEncodingType GetContentEncodingType() => _grpcStatusDescription is null ? _response.GetContentEncodingType() : ContentEncodingType.None;

    public Task<Stream> GetStreamAsync()
        => _grpcStatusDescription is null ? _response.GetStreamAsync() : Task.FromResult<Stream>(new MemoryStream(_grpcStatusDescription));

    public void Dispose() => _response.Dispose();

    // The OTLP specification lists the retryable gRPC status codes, which are mapped to an HTTP status code that ApiOtlp retries.
    // RESOURCE_EXHAUSTED is only retryable when the server says so, with a RetryInfo in the status details.
    private static int GetHttpStatusCode(GrpcStatusCode statusCode, HttpResponseMessage response) => statusCode switch
    {
        GrpcStatusCode.OK => 200,
        GrpcStatusCode.ResourceExhausted when HasRetryInfo(response) => 429,
        GrpcStatusCode.Cancelled
            or GrpcStatusCode.DeadlineExceeded
            or GrpcStatusCode.Aborted
            or GrpcStatusCode.OutOfRange
            or GrpcStatusCode.Unavailable
            or GrpcStatusCode.DataLoss => 503,
        _ => 500,
    };

    private static bool HasRetryInfo(HttpResponseMessage response)
    {
        // Like the status, the status details are in the headers of trailers-only responses
        var statusDetails = GrpcProtocolHelpers.GetHeaderValue(response.TrailingHeaders, OtlpGrpcExportClient.GrpcStatusDetailsHeader, first: true)
                         ?? GrpcProtocolHelpers.GetHeaderValue(response.Headers, OtlpGrpcExportClient.GrpcStatusDetailsHeader, first: true);
        return GrpcStatusDeserializer.TryGetGrpcRetryDelay(statusDetails) is not null;
    }
}
#endif
