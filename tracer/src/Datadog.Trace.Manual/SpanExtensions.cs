// <copyright file="SpanExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace;

/// <summary>
/// Extension methods for the <see cref="ISpan"/> interface
/// </summary>
public static class SpanExtensions
{
    /// <summary>
    /// Add the specified tag to this span.
    /// </summary>
    /// <param name="span">The span to be tagged</param>
    /// <param name="key">The tag's key.</param>
    /// <param name="value">The tag's value.</param>
    /// <returns>This span to allow method chaining.</returns>
    [Instrumented]
    public static ISpan SetTag(this ISpan span, string key, double? value) => span;

    /// <summary>
    /// Sets the details of the user on the local root span
    /// </summary>
    /// <param name="span">The span to be tagged</param>
    /// <param name="userDetails">The details of the current logged on user</param>
    public static void SetUser(this ISpan span, UserDetails userDetails)
        => SetUser(span, userDetails.Email, userDetails.Name, userDetails.Id, userDetails.PropagateId, userDetails.SessionId, userDetails.Role, userDetails.Scope);

    [Instrumented]
    private static void SetUser(
        ISpan span,
        string? email,
        string? name,
        string id,
        bool propagateId,
        string? sessionId,
        string? role,
        string? scope)
    {
    }
}
