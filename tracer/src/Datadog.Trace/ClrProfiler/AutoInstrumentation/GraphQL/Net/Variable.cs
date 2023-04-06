// <copyright file="Variable.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL.Net;

/// <summary>
/// GraphQL.Validation.Variable interface for ducktyping
/// </summary>
[DuckCopy]
internal struct Variable
{
    public string Name;

    #nullable enable
    public object? Value;
}
