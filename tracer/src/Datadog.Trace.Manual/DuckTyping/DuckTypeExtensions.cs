// <copyright file="DuckTypeExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.DuckTyping;

internal static class DuckTypeExtensions
{
    [Instrumented]
    public static T? DuckCast<T>(this object? instance) => default;
}
