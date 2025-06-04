// <copyright file="IApiResponse.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Util;
using Datadog.Trace.Util.Streams;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Agent
{
    internal interface IApiResponse : IDisposable
    {
        int StatusCode { get; }

        long ContentLength { get; }

        /// <summary>
        /// Gets the "raw" content-type header, which may contain additional information like charset or boundary.
        /// </summary>
        string? ContentTypeHeader { get; }

        /// <summary>
        /// Gets the "raw" content-encoding header, which may contain multiple values
        /// </summary>
        string? ContentEncodingHeader { get; }

        string? GetHeader(string headerName);

        Encoding GetCharsetEncoding();

        ContentEncodingType GetContentEncodingType();

        Task<Stream> GetStreamAsync();
    }

    internal static class ApiResponseExtensions
    {
        private const int DefaultBufferSize = 1024;

        public static async Task<string> ReadAsStringAsync(this IApiResponse apiResponse)
        {
            var stream = await apiResponse.GetStreamAsync().ConfigureAwait(false);
            using var reader = GetStreamReader(apiResponse, stream);
            return await reader.ReadToEndAsync().ConfigureAwait(false);
        }

        public static async Task<T?> ReadAsTypeAsync<T>(this IApiResponse apiResponse)
        {
            InitiallyBufferedStream? bufferedStream = null;
            try
            {
                var stream = await apiResponse.GetStreamAsync().ConfigureAwait(false);
                bufferedStream = new InitiallyBufferedStream(stream);
                // wrap the stream in an "initially buffering" stream, so that if deserialization fails completely, we can get some details
                using var sr = GetStreamReader(apiResponse, bufferedStream);
                using var jsonTextReader = new JsonTextReader(sr);
                return JsonSerializer.Create().Deserialize<T>(jsonTextReader);
            }
            catch (JsonException ex) when (bufferedStream?.GetBufferedContent() is { } buffered)
            {
                throw new JsonException($"{ex.Message} Original content length {apiResponse.ContentLength} and content: '{buffered}'", ex);
            }
            finally
            {
                bufferedStream?.Dispose();
            }
        }

        private static StreamReader GetStreamReader(IApiResponse apiResponse, Stream stream)
        {
            // Server may not send the content length, in that case we use a default value.
            // https://source.dot.net/#System.Private.CoreLib/src/libraries/System.Private.CoreLib/src/System/IO/StreamReader.cs,25
            var length = apiResponse.ContentLength is > 0 and < DefaultBufferSize ? (int)apiResponse.ContentLength : DefaultBufferSize;
            return new StreamReader(stream, apiResponse.GetCharsetEncoding(), detectEncodingFromByteOrderMarks: false, length, leaveOpen: true);
        }

        public static bool ShouldRetry(this IApiResponse response)
        {
            var shouldRetry = response.StatusCode switch
            {
                400 => false, // Bad request (likely an issue in the payload formatting)
                401 => false, // Unauthorized (likely a missing API Key)
                403 => false, // Permission issue (likely using an invalid API Key)
                408 => true, // Request Timeout, request should be retried after some time
                413 => false, // Payload too large (batch is above 5MB uncompressed)
                429 => true, // Too Many Requests, request should be retried after some time
                >= 400 and < 500 => false, // generic "client" error, don't retry
                _ => true // Something else, probably server error, do retry
            };

            return shouldRetry;
        }

        /// <summary>
        /// Gets the <see cref="Encoding"/> represented by the charset defined in the content type header.
        /// </summary>
        /// <param name="contentTypeHeader">The raw content-type header, for example <c>"application/json;charset=utf-8"</c></param>
        /// <returns>The encoding associated with the charset, or <see cref="EncodingHelpers.Utf8NoBom"/> if the content-type header was not provided,
        /// if the charset was not provided, or if the charset was not recognized</returns>
        public static Encoding GetCharsetEncoding(string? contentTypeHeader)
        {
            // special casing application/json because it's so common
            if (string.IsNullOrEmpty(contentTypeHeader)
                || string.Equals("application/json", contentTypeHeader, StringComparison.OrdinalIgnoreCase))
            {
                // Default
                return EncodingHelpers.Utf8NoBom;
            }

            // text/plain; charset=utf-8; boundary=foo
            foreach (var pair in contentTypeHeader!.SplitIntoSpans(';'))
            {
                var parts = pair.AsSpan();
                var index = parts.IndexOf('=');

                if (index != -1)
                {
                    var firstPart = parts.Slice(0, index).Trim();

                    if (!firstPart.Equals("charset".AsSpan(), StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var secondPart = parts.Slice(index + 1).Trim();
                    if (EncodingHelpers.TryGetWellKnownCharset(secondPart, out var encoding))
                    {
                        return encoding;
                    }

                    return EncodingHelpers.TryGetFromCharset(secondPart.ToString(), out var parsed)
                               ? parsed
                               : EncodingHelpers.Utf8NoBom;
                }
            }

            return EncodingHelpers.Utf8NoBom;
        }

        public static ContentEncodingType GetContentEncodingType(string? contentEncodingHeader)
        {
            if (string.IsNullOrEmpty(contentEncodingHeader))
            {
                return ContentEncodingType.None;
            }

            if (contentEncodingHeader!.Contains(","))
            {
                return ContentEncodingType.Multiple;
            }

            var encoding = contentEncodingHeader.AsSpan().Trim();
            if (encoding.Equals("gzip".AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                return ContentEncodingType.GZip;
            }
            else if (encoding.Equals("deflate".AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                return ContentEncodingType.Deflate;
            }
            else if (encoding.Equals("compress".AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                return ContentEncodingType.Compress;
            }
            else if (encoding.Equals("br".AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                return ContentEncodingType.Brotli;
            }

            return ContentEncodingType.Other;
        }
    }
}
