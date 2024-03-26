// <copyright file="MessageBatchPayload.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;

namespace Datadog.Trace.Telemetry;

/// <summary>
/// message_batch payload, for batching multiple telemetry payloads together
/// </summary>
internal class MessageBatchPayload : List<MessageBatchData>, IPayload
{
    public MessageBatchPayload()
    {
    }

    public MessageBatchPayload(ICollection<MessageBatchData> payloads)
    {
        AddRange(payloads);
    }
}
