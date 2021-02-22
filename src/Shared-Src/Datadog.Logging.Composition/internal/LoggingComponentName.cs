using System;

namespace Datadog.Logging.Composition
{
    internal struct LoggingComponentName
    {
        public LoggingComponentName(string part1, string part2)
        {
            Part1 = part1;
            Part2 = part2;
        }

        public string Part1 { get; }

        public string Part2 { get; }

        public static LoggingComponentName Create(string part1, string part2)
        {
            return new LoggingComponentName(part1, part2);
        }
    }
}
