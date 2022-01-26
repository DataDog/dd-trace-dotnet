// <copyright file="IPathString.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore
{
    /// <summary>
    /// Microsoft.AspNetCore.Http.PathString interface for ducktyping
    /// </summary>
    internal interface IPathString
    {
        bool HasValue { get; }

        string ToUriComponent();
    }
}
