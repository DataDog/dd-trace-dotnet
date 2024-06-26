// <copyright file="IApiResponse.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Util.Streams;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Agent
{
    internal interface IApiResponse : IDisposable
    {
        int StatusCode { get; }

        long ContentLength { get; }

        Encoding ContentEncoding { get; }

        string GetHeader(string headerName);

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

        public static async Task<T> ReadAsTypeAsync<T>(this IApiResponse apiResponse)
        {
            InitiallyBufferedStream bufferedStream = null;
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
            return new StreamReader(stream, apiResponse.ContentEncoding, detectEncodingFromByteOrderMarks: false, length, leaveOpen: true);
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
    }
}
