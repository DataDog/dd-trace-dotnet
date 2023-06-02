// <copyright file="MessageBatchData.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Telemetry;

internal class MessageBatchData
{
    public MessageBatchData(string requestType, IPayload? payload)
    {
        RequestType = requestType;
        Payload = payload;
    }

    public string RequestType { get; }

    public IPayload? Payload { get; }
}
