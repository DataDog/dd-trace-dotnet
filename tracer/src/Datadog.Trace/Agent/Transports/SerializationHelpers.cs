// <copyright file="SerializationHelpers.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Serialization;

namespace Datadog.Trace.Agent.Transports;

internal static class SerializationHelpers
{
    public static readonly JsonSerializerSettings DefaultJsonSettings = new()
    {
        NullValueHandling = NullValueHandling.Ignore,
        ContractResolver = new DefaultContractResolver
        {
            NamingStrategy = new SnakeCaseNamingStrategy(),
        }
    };

    public static async Task WriteAsJson<T>(Stream requestStream, T payload, JsonSerializerSettings serializationSettings, MultipartCompression compression)
    {
        // wrap in gzip if requested
        using Stream? gzip = compression == MultipartCompression.GZip
                                 ? new GZipStream(requestStream, CompressionMode.Compress, leaveOpen: true)
                                 : null;
        var streamToWriteTo = gzip ?? requestStream;

        using var streamWriter = new StreamWriter(streamToWriteTo, EncodingHelpers.Utf8NoBom, bufferSize: 1024, leaveOpen: true);
        using var jsonWriter = new JsonTextWriter(streamWriter)
        {
            CloseOutput = false
        };
        var serializer = JsonSerializer.Create(serializationSettings);
        serializer.Serialize(jsonWriter, payload);
        await streamWriter.FlushAsync().ConfigureAwait(false);
        await streamToWriteTo.FlushAsync().ConfigureAwait(false);
        await requestStream.FlushAsync().ConfigureAwait(false);
    }
}
