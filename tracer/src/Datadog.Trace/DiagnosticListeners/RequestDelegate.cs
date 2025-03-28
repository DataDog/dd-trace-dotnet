// <copyright file="RequestDelegate.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.DuckTyping;

#nullable enable

namespace Datadog.Trace.DiagnosticListeners;

/// <summary>
/// RequestDelegate for duck typing
/// https://github.com/dotnet/aspnetcore/blob/v3.0.3/src/Http/Http.Abstractions/src/RequestDelegate.cs
/// </summary>
[DuckCopy]
internal struct RequestDelegate
{
    /// <summary>
    /// Delegate to RequestDelegate.Method
    /// </summary>
    public System.Reflection.MethodInfo? Method;

    /// <summary>
    /// Delegate to RequestDelegate.Target
    /// </summary>
    public object? Target;
}
