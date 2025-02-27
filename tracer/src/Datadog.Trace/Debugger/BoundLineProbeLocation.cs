// <copyright file="BoundLineProbeLocation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Debugger.Configurations.Models;

#nullable enable

namespace Datadog.Trace.Debugger;

internal record BoundLineProbeLocation(ProbeDefinition ProbeDefinition, Guid Mvid, int MethodToken, int BytecodeOffset, int LineNumber)
{
    public ProbeDefinition ProbeDefinition { get; set; } = ProbeDefinition;

    public Guid Mvid { get; set; } = Mvid;

    public int MethodToken { get; set; } = MethodToken;

    public int BytecodeOffset { get; set; } = BytecodeOffset;

    public int LineNumber { get; set; } = LineNumber;
}
