// <copyright file="StringExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using FluentAssertions.Execution;
using FluentAssertions.Primitives;

namespace Datadog.Trace.TestHelpers.FluentAssertionsExtensions;

public static class StringExtensions
{
    public static StringAssertions HaveEmptyDiffWhenComparedTo(this StringAssertions value, string expected, string because = "", params object[] becauseArgs)
    {
        Execute.Assertion
               .BecauseOf(because, becauseArgs)
               .Given(() => Diff(expected, value.Subject))
               .ForCondition(diffLines => diffLines?.Count > 0)
               .FailWith(
                    "{context:string} has differences from expected value {reason}:{0}",
                    diffLines => diffLines);

        return value;
    }

    private static List<string> Diff(string expected, string actual)
    {
        var diff = InlineDiffBuilder.Diff(expected, actual);

        if (diff.HasDifferences)
        {
            var diffs = new List<string>();

            for (var i = 0; i < diff.Lines.Count; i++)
            {
                var line = diff.Lines[i];

                switch (line.Type)
                {
                    case ChangeType.Inserted:
                        diffs.Add($"{line.Position} + {line.Text}");
                        break;
                    case ChangeType.Deleted:
                        diffs.Add($"{line.Position} - {line.Text}");
                        break;
                    case ChangeType.Modified:
                        diffs.Add($"{line.Position} ~ {line.Text}");
                        break;
                }
            }

            return diffs;
        }

        return null;
    }
}
