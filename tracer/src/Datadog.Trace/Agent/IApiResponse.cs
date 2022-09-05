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
            return new StreamReader(stream, apiResponse.ContentEncoding, detectEncodingFromByteOrderMarks: false, (int)apiResponse.ContentLength, leaveOpen: true);
        }
    }
}
