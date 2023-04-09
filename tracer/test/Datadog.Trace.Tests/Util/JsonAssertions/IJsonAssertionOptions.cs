// <copyright file="IJsonAssertionOptions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

/// Based on https://github.com/fluentassertions/fluentassertions.json

using System;
using FluentAssertions.Equivalency;

namespace Datadog.Trace.Tests.Util.JsonAssertions;

/// <summary>
/// Provides the run-time details of the <see cref="FluentAssertions.Json.JsonAssertionOptions{T}" /> class.
/// Based on https://github.com/fluentassertions/fluentassertions.json
/// Converted to use vendored version of NewtonsoftJson
/// </summary>
#pragma warning disable SA1618 // The documentation for T is missing
internal interface IJsonAssertionOptions<T>
{
    /// <summary>
    /// Overrides the comparison of subject and expectation to use provided <paramref name="action" />
    /// when the predicate is met.
    /// </summary>
    /// <param name="action">
    /// The assertion to execute when the predicate is met.
    /// </param>
    IJsonAssertionRestriction<T, TProperty> Using<TProperty>(Action<IAssertionContext<TProperty>> action);
}
