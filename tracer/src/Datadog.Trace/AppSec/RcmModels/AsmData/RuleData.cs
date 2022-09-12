// <copyright file="RuleData.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.AppSec.RcmModels.AsmData;

internal class RuleData
{
    public string Type { get; set; }

    public string Id { get; set; }

    public Data[] Data { get; set; }
}
