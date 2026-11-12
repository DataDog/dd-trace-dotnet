using System;
using System.Text.RegularExpressions;
using Nuke.Common.Tooling;

internal static class UnitTestOutputFilter
{
    private static readonly Regex RoutineTestEvent = new(
        @"^\s*\[xUnit\.net [^\]]+\] [^:\r\n]+: (?:STARTED|SUCCESS):",
        RegexOptions.Compiled);

    public static Action<OutputType, string> Wrap(Action<OutputType, string> logger)
    {
        return (type, text) =>
        {
            // Filter only identified event headers. Unprefixed lines may be theory
            // arguments or interleaved failure details, so they must remain visible.
            if (type == OutputType.Err || !RoutineTestEvent.IsMatch(text))
            {
                logger(type, text);
            }
        };
    }
}
