// <copyright file="IJsonAssertionRestriction.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

// Originally Based on https://github.com/fluentassertions/fluentassertions.json
// License: https://github.com/fluentassertions/fluentassertions.json/blob/master/LICENSE

namespace Datadog.Trace.TestHelpers.FluentAssertionsExtensions.Json;

/// <summary>
/// Defines additional overrides when used with <see cref="FluentAssertions.Json.JsonAssertionRestriction{T, TProperty}" />
/// </summary>
/// <typeparam name="T">Type</typeparam>
/// <typeparam name="TMember">Member Type</typeparam>
internal interface IJsonAssertionRestriction<T, TMember>
{
    /// <summary>
    /// Allows overriding the way structural equality is applied to (nested) objects of type
    /// <typeparamref name="TMemberType" />
    /// </summary>
    /// <typeparam name="TMemberType">Member Type</typeparam>
    /// <returns>Assertion Options</returns>
    public IJsonAssertionOptions<T> WhenTypeIs<TMemberType>()
        where TMemberType : TMember;
}
