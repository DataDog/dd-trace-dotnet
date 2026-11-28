// <copyright file="IStreamingApiRequest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if SHARED_REMOTE_CONFIG
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Datadog.Trace.Agent;

internal interface IStreamingApiRequest : IApiRequest
{
    Task<IApiResponse> PostStreamingAsync(ArraySegment<byte> bytes, string contentType, CancellationToken cancellationToken);
}
#endif
