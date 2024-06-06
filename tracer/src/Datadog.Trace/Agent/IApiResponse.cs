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

        /// <summary>
        /// Gets the "raw" content-type header, which may contain additional information like charset or boundary.
        /// Prefer using <see cref="HasMimeType"/> to check for specific mime types.
        /// </summary>
        string RawContentType { get; }

        string GetHeader(string headerName);

        Task<Stream> GetStreamAsync();

        bool HasMimeType(string mimeType);
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

        internal static bool HasMimeType(string rawContentType, string mimeType)
        {
            if (string.IsNullOrEmpty(mimeType))
            {
                ThrowHelper.ThrowArgumentNullException(nameof(mimeType));
            }

            if (string.IsNullOrEmpty(rawContentType))
            {
                return false;
            }

            if (string.Equals(rawContentType, mimeType, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // handle when we have charsets and directives, assuming (hopefully sensibly) that the mime type is the first part
            // but that it might have white space around it
            // e.g. text/html; charset=utf-8
            var indexOfSemicolon = rawContentType.IndexOf(';');
            if (indexOfSemicolon >= 0 && mimeType.Length > indexOfSemicolon)
            {
                return false;
            }

#if NETCOREAPP
            var untrimmedMediaType = indexOfSemicolon > 0
                                      ? rawContentType.AsSpan(0, indexOfSemicolon)
                                      : rawContentType.AsSpan();
            return untrimmedMediaType.Trim().Equals(mimeType.AsSpan(), StringComparison.OrdinalIgnoreCase);
#else
            var endIndex = indexOfSemicolon > 0 ? indexOfSemicolon : mimeType.Length;

            // ideally we'd avoid the double allocation here, but that's a lot of faff

            var untrimmedMediaType = indexOfSemicolon > 0
                                         ? rawContentType.Substring(0, indexOfSemicolon)
                                         : rawContentType;
            return string.Equals(untrimmedMediaType.Trim(), mimeType, StringComparison.OrdinalIgnoreCase);
#endif
        }
    }
}
