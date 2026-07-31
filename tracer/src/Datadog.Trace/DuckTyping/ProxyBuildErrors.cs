// <copyright file="ProxyBuildErrors.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.DuckTyping;

internal sealed class ProxyBuildErrors
{
    /// <summary>
    /// Gets the first recorded failure, or null while the build is still viable.
    /// </summary>
    internal DuckTypeException? Error { get; private set; }

    internal bool HasError => Error is not null;

    /// <summary>
    /// Records a failure. The first one wins, matching the throw-on-first-problem behaviour this
    /// replaces - later members are not inspected once the proxy is known to be unbuildable.
    /// </summary>
    internal void Record(DuckTypeException error) => Error ??= error;
}
