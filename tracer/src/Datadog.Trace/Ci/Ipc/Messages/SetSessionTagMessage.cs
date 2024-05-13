// <copyright file="SetSessionTagMessage.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Ci.Ipc.Messages;

internal class SetSessionTagMessage
{
    public SetSessionTagMessage()
    {
        Name = string.Empty;
    }

    public SetSessionTagMessage(string name, string value)
    {
        Name = name;
        Value = value;
    }

    public SetSessionTagMessage(string name, double numberValue)
    {
        Name = name;
        NumberValue = numberValue;
    }

    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("value")]
    public string? Value { get; set; }

    [JsonProperty("nvalue")]
    public double? NumberValue { get; set; }
}
