using System;
using System.Collections.Generic;
using System.Linq;
using DiffMatchPatch;
using Logger = Serilog.Log;

public static class DiffHelper
{
    public static void PrintDiff(List<Diff> diff, bool printEqual = false)
    {
        foreach (var t in diff)
        {
            if (printEqual || t.operation != Operation.EQUAL)
            {
                var str = DiffToString(t);
                if (str.Contains(value: '\n'))
                {
                    // if the diff is multiline, start with a newline so that all changes are aligned
                    // otherwise it's easy to miss the first line of the diff
                    str = "\n" + str;
                }

                Logger.Information(str);
            }
        }

        string DiffToString(Diff diff)
        {
            if (diff.operation == Operation.EQUAL)
            {
                return string.Empty;
            }

            var symbol = diff.operation switch
            {
                Operation.DELETE => '-',
                Operation.INSERT => '+',
                _ => throw new Exception("Unknown value of the Option enum.")
            };
            // put the symbol at the beginning of each line to make diff clearer when whole blocks of text are missing
            var lines = diff.text.TrimEnd(trimChar: '\n').Split(Environment.NewLine);
            return string.Join(Environment.NewLine, lines.Select(l => symbol + l));
        }
    }
}
