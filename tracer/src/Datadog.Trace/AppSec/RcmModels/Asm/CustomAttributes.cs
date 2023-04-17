// <copyright file="CustomAttributes.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable
using System.Collections.Generic;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.AppSec.RcmModels.Asm;

internal class CustomAttributes
{
    [JsonProperty("Attributes")]
    public Dictionary<string, object>? Attributes { get; set; }
}
