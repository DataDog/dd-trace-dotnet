// <copyright file="IApiRequest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Threading.Tasks;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Agent
{
    internal interface IApiRequest
    {
        void AddHeader(string name, string value);

        Task<IApiResponse> GetAsync();

        Task<IApiResponse> PostAsync(ArraySegment<byte> bytes, string contentType);

        Task<IApiResponse> PostAsync(ArraySegment<byte> bytes, string contentType, string contentEncoding);

        Task<IApiResponse> PostAsJsonAsync<T>(T payload, MultipartCompression compression);

        Task<IApiResponse> PostAsJsonAsync<T>(T payload, MultipartCompression compression, JsonSerializerSettings settings);

        Task<IApiResponse> PostAsync(Func<Stream, Task> writeToRequestStream, string contentType, string contentEncoding, string multipartBoundary);

        Task<IApiResponse> PostAsync(MultipartFormItem[] items, MultipartCompression multipartCompression = MultipartCompression.None);
    }
}
