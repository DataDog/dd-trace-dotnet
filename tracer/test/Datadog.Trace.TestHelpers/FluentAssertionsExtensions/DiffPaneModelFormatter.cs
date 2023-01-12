// <copyright file="DiffPaneModelFormatter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using System.Text;
using DiffPlex.DiffBuilder.Model;
using FluentAssertions.Formatting;

namespace Datadog.Trace.TestHelpers.FluentAssertionsExtensions;

public class DiffPaneModelFormatter : IValueFormatter
{
    public bool CanHandle(object value)
    {
        return value is DiffPaneModel;
    }

    public void Format(object value, FormattedObjectGraph formattedGraph, FormattingContext context, FormatChild formatChild)
    {
        if (value is DiffPaneModel { HasDifferences: true } diffModel)
        {
            var sb = new StringBuilder(Environment.NewLine);

            // used to right-align line numbers
            int highestLinePositionDigitCount = diffModel.Lines.Count.ToString().Length;

            for (var i = 0; i < diffModel.Lines.Count; i++)
            {
                var line = diffModel.Lines[i];

                if (line.Type is ChangeType.Deleted or ChangeType.Inserted or ChangeType.Modified)
                {
                    var changeTypeIndicator = line.Type switch
                                              {
                                                  ChangeType.Deleted => "-",
                                                  ChangeType.Inserted => "+",
                                                  ChangeType.Modified => "~",
                                                  _ => null
                                              };

                    // Do not rely on `line.Position` since it is null for deleted lines
                    sb.AppendFormat("{0}{1," + highestLinePositionDigitCount + "}:{2}" + Environment.NewLine, changeTypeIndicator, i + 1, line.Text);
                }
            }

            var result = sb.ToString();

            if (context.UseLineBreaks)
            {
                // Forces the result to be added as a separate line in the final output
                formattedGraph.AddLine(result);
            }
            else
            {
                // Appends the result to any existing fragments on the current line
                formattedGraph.AddFragment(result);
            }
        }
    }
}
