// <copyright file="BoundLineProbeLocation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Debugger.Configurations.Models;

#nullable enable

internal record BoundLineProbeLocation
{
    public BoundLineProbeLocation(ProbeDefinition probe, Guid mvid, int methodToken, int bytecodeOffset, int lineNumber)
    {
        this.ProbeDefinition = probe;
        this.MVID = mvid;
        this.MethodToken = methodToken;
        this.BytecodeOffset = bytecodeOffset;
        this.LineNumber = lineNumber;
    }

    public ProbeDefinition ProbeDefinition { get; set; }

    public Guid MVID { get; set; }

    public int MethodToken { get; set; }

    public int BytecodeOffset { get; set; }

    public int LineNumber { get; set; }
}
