// <copyright file="StringExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using DiffPlex.DiffBuilder;
using FluentAssertions;
using FluentAssertions.Execution;
using FluentAssertions.Primitives;

namespace Datadog.Trace.TestHelpers.FluentAssertionsExtensions;

public static class StringExtensions
{
    [CustomAssertion]
    public static AndConstraint<StringAssertions> Be(this StringAssertions value, string expected, bool outputDiffOnly, string because = "", params object[] becauseArgs)
    {
        if (!outputDiffOnly)
        {
            // fallback to Be()'s default behavior (show full strings instead of diffs)
            return value.Be(expected, because, becauseArgs);
        }

        Execute.Assertion
               .BecauseOf(because, becauseArgs)
               .Given(() => InlineDiffBuilder.Diff(expected, value.Subject))
               .ForCondition(diffModel => !diffModel.HasDifferences)
               .FailWith(
                    "{context:string} has differences from expected value{reason}. Diff:{0}",
                    diffModel => diffModel);

        return new AndConstraint<StringAssertions>(value);
    }
}
