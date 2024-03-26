// <copyright file="Diagnostics.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Converters;

namespace Datadog.Trace.Debugger.Sink.Models
{
    internal record Diagnostics
    {
        public Diagnostics(string probeId, Status status, int probeVersion)
        {
            ProbeId = probeId;
            Status = status;
            ProbeVersion = probeVersion;
            RuntimeId = Util.RuntimeId.Get();
        }

        [JsonProperty("probeId")]
        public string ProbeId { get; }

        [JsonProperty("status")]
        public Status Status { get; }

        [JsonProperty("probeVersion")]
        public int ProbeVersion { get; }

        [JsonProperty("runtimeId")]
        public string RuntimeId { get; }

        [JsonProperty("exception")]
        public ProbeException Exception { get; private set; }

        public void SetException(Exception exception, string errorMessage)
        {
            Exception = new ProbeException();

            if (exception != null)
            {
                Exception = new ProbeException()
                {
                    Type = exception.GetType().Name,
                    Message = exception.Message,
                    StackTrace = exception.StackTrace
                };
            }

            if (!string.IsNullOrWhiteSpace(errorMessage))
            {
                Exception.Type ??= "NO_TYPE";
                Exception.Message = errorMessage;
            }
        }
    }
}
