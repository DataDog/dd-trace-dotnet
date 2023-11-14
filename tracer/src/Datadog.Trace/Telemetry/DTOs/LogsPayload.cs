// <copyright file="LogsPayload.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;

#nullable enable

namespace Datadog.Trace.Telemetry.DTOs;

internal class LogsPayload : IPayload
{
    public LogsPayload(List<LogMessageData> logs)
    {
        Logs = logs;
    }

    public string? Tags { get; set; }

    public List<LogMessageData> Logs { get; set; }
}
