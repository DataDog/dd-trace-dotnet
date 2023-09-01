// <copyright file="JsonAssertionRestriction.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

// Originally Based on https://github.com/fluentassertions/fluentassertions.json
// License: https://github.com/fluentassertions/fluentassertions.json/blob/master/LICENSE

namespace Datadog.Trace.TestHelpers.FluentAssertionsExtensions.Json;

internal sealed class JsonAssertionRestriction<T, TProperty> : IJsonAssertionRestriction<T, TProperty>
{
    private readonly JsonAssertionOptions<T>.Restriction<TProperty> restriction;

    internal JsonAssertionRestriction(JsonAssertionOptions<T>.Restriction<TProperty> restriction)
    {
        this.restriction = restriction;
    }

    public IJsonAssertionOptions<T> WhenTypeIs<TMemberType>()
        where TMemberType : TProperty
    {
        return (JsonAssertionOptions<T>)restriction.WhenTypeIs<TMemberType>();
    }
}
