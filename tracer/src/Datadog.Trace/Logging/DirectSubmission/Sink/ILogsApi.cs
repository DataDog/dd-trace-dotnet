// <copyright file="ILogsApi.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Threading.Tasks;

namespace Datadog.Trace.Logging.DirectSubmission.Sink
{
    internal interface ILogsApi : IDisposable
    {
        Task<bool> SendLogsAsync(ArraySegment<byte> logs, int numberOfLogs);
    }
}
