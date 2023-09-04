// <copyright file="JsonAssertionOptions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

// Originally Based on https://github.com/fluentassertions/fluentassertions.json
// License: https://github.com/fluentassertions/fluentassertions.json/blob/master/LICENSE

using System;
using FluentAssertions.Equivalency;

namespace Datadog.Trace.TestHelpers.FluentAssertionsExtensions.Json;

/// <summary>
/// Represents the run-time type-specific behavior of a JSON structural equivalency assertion. It is the equivalent of <see cref="FluentAssertions.Equivalency.EquivalencyAssertionOptions{T}"/>
/// </summary>
/// <typeparam name="T">Type</typeparam>
internal sealed class JsonAssertionOptions<T>
    : EquivalencyAssertionOptions<T>, IJsonAssertionOptions<T>
{
    public JsonAssertionOptions(EquivalencyAssertionOptions<T> equivalencyAssertionOptions)
        : base(equivalencyAssertionOptions)
    {
    }

    public new IJsonAssertionRestriction<T, TProperty> Using<TProperty>(Action<IAssertionContext<TProperty>> action)
    {
        return new JsonAssertionRestriction<T, TProperty>(base.Using(action));
    }
}
