// <copyright file="IJsonAssertionRestriction.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

/// Based on https://github.com/fluentassertions/fluentassertions.json

namespace Datadog.Trace.Tests.Util.JsonAssertions;

/// <summary>
/// Defines additional overrides when used with <see cref="FluentAssertions.Json.JsonAssertionRestriction{T, TProperty}" />
/// Converted to use vendored version of NewtonsoftJson
/// </summary>
internal interface IJsonAssertionRestriction<T, TMember>
{
    /// <summary>
    /// Allows overriding the way structural equality is applied to (nested) objects of type
    /// <typeparamref name="TMemberType" />
    /// </summary>
    public IJsonAssertionOptions<T> WhenTypeIs<TMemberType>()
        where TMemberType : TMember;
}
