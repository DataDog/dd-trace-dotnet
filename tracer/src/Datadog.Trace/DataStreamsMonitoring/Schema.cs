// <copyright file="Schema.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.DataStreamsMonitoring;

internal sealed class Schema
{
    public Schema(string definition, string id)
    {
        Definition = definition;
        Id = id;
    }

    public string Definition { get; }

    public string Id { get; }
}
