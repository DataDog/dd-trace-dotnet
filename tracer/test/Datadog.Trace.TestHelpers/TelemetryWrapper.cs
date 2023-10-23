// <copyright file="TelemetryWrapper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using Datadog.Trace.Telemetry;

namespace Datadog.Trace.TestHelpers;

internal abstract class TelemetryWrapper
{
    public abstract string ApiVersion { get; }

    public abstract int SeqId { get; }

    public abstract bool IsRequestType(string requestType);

    public abstract T TryGetPayload<T>(string requestType);

    public class V2 : TelemetryWrapper
    {
        public V2(TelemetryDataV2 data)
        {
            Data = data;
        }

        public TelemetryDataV2 Data { get; }

        public override string ApiVersion => Data.ApiVersion;

        public override int SeqId => Data.SeqId;

        private string RequestType => Data.RequestType;

        private IPayload Payload => Data.Payload;

        public override bool IsRequestType(string requestType)
            => RequestType == requestType
            || (RequestType == TelemetryRequestTypes.MessageBatch
             && Payload is MessageBatchPayload batchPayload
             && batchPayload.Any(x => x.RequestType == requestType));

        public override T TryGetPayload<T>(string requestType)
        {
            if (RequestType == requestType && Payload is T p)
            {
                return p;
            }

            if (RequestType == TelemetryRequestTypes.MessageBatch
             && Payload is MessageBatchPayload batch)
            {
                foreach (var data in batch)
                {
                    if (data.RequestType == requestType
                     && data.Payload is T batchPayload)
                    {
                        return batchPayload;
                    }
                }
            }

            return default;
        }
    }
}
