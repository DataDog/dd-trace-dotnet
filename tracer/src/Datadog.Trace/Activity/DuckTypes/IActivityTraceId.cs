// <copyright file="IActivityTraceId.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.Activity.DuckTypes;

internal interface IActivityTraceId : IDuckType
{
    [DuckField(Name = "_hexString")]
    string? TraceId { get; }
}
