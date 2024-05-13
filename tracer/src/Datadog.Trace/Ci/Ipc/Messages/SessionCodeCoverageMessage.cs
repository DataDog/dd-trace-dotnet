// <copyright file="SessionCodeCoverageMessage.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Ci.Ipc.Messages;

internal class SessionCodeCoverageMessage
{
    public SessionCodeCoverageMessage()
    {
    }

    public SessionCodeCoverageMessage(double value)
    {
        Value = value;
    }

    [JsonProperty("value")]
    public double Value { get; set; }
}
