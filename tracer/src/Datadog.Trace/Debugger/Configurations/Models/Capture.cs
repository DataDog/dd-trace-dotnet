// <copyright file="Capture.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Debugger.Configurations.Models;

internal class Capture
{
    public int MaxReferenceDepth { get; set; }

    public int MaxCollectionSize { get; set; }

    public int MaxLength { get; set; }

    public int MaxFieldDepth { get; set; }

    public int MaxFieldCount { get; set; }
}
