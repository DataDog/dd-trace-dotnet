// <copyright file="TelemetryDataExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using Datadog.Trace.Telemetry;

namespace Datadog.Trace.TestHelpers;

internal static class TelemetryDataExtensions
{
    public static bool IsRequestType(this TelemetryData data, string requestType)
        => data.RequestType == requestType
        || (data.RequestType == TelemetryRequestTypes.MessageBatch
         && data.Payload is MessageBatchPayload batchPayload
         && batchPayload.Any(x => x.RequestType == requestType));

    public static T TryGetPayload<T>(this TelemetryData data, string requestType)
    {
        if (data.RequestType == requestType && data.Payload is T p)
        {
            return p;
        }

        if (data.RequestType == TelemetryRequestTypes.MessageBatch
         && data.Payload is MessageBatchPayload batch)
        {
            foreach (var message in batch)
            {
                if (message.RequestType == requestType
                 && message.Payload is T batchPayload)
                {
                    return batchPayload;
                }
            }
        }

        return default;
    }
}
