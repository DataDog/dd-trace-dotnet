// <copyright file="IBatchUploadApi.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Threading.Tasks;

namespace Datadog.Trace.Debugger.Upload
{
    internal interface IBatchUploadApi
    {
        Task<bool> SendBatchAsync(ArraySegment<byte> data);
    }
}
