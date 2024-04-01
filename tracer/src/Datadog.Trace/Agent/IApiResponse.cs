// <copyright file="IApiResponse.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
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
        public static async Task<string> ReadAsStringAsync(this IApiResponse apiResponse)
        {
            using var reader = await GetStreamReader(apiResponse).ConfigureAwait(false);
            return await reader.ReadToEndAsync().ConfigureAwait(false);
        }

        public static async Task<T> ReadAsTypeAsync<T>(this IApiResponse apiResponse)
        {
            using var sr = await GetStreamReader(apiResponse).ConfigureAwait(false);
            using var jsonTextReader = new JsonTextReader(sr);
            return JsonSerializer.Create().Deserialize<T>(jsonTextReader);
        }

        private static async Task<StreamReader> GetStreamReader(IApiResponse apiResponse)
        {
            var stream = await apiResponse.GetStreamAsync().ConfigureAwait(false);
            // Server may not send the content length, in that case we use a default value.
            // https://source.dot.net/#System.Private.CoreLib/src/libraries/System.Private.CoreLib/src/System/IO/StreamReader.cs,25
            var length = apiResponse.ContentLength > 0 ? (int)apiResponse.ContentLength : 1024;
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
