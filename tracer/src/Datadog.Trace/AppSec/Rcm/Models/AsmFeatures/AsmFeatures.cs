// <copyright file="AsmFeatures.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.AppSec.Rcm.Models.AsmFeatures;

using Datadog.Trace.Vendors.Newtonsoft.Json;

internal class AsmFeatures
{
    public AsmFeature? Asm { get; set; } = new();

    [JsonProperty("auto_user_instrum")]
    public AutoUserInstrum? AutoUserInstrum { get; set; } = new();
}
