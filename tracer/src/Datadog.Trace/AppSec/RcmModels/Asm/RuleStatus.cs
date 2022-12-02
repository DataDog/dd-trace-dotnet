// <copyright file="RuleStatus.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
namespace Datadog.Trace.AppSec.RcmModels.Asm;

internal class RuleStatus
{
    public string? Id { get; set; }

    public bool? Enabled { get; set; }

    public override string ToString()
    {
        return $"{{{Id} : {Enabled}}}";
    }
}
