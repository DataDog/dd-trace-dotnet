// <copyright file="IHttpContent.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.IO;
using System.Threading.Tasks;

namespace Datadog.Profiler.IntegrationTests.Helpers.HttpOverStreams
{
    internal interface IHttpContent
    {
        long? Length { get; }

        Task CopyToAsync(Stream destination);

        Task CopyToAsync(byte[] buffer);
    }
}
