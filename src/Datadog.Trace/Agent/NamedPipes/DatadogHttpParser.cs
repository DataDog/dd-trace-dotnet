using System;
using System.Collections.Generic;
using System.Linq;

namespace Datadog.Trace.HttpOverStreams.HttpOverStream
{
    internal static class DatadogHttpParser
    {
        public static Tuple<string, List<string>> ParseHeaderNameValues(string line)
        {
            var pos = line.IndexOf(':');

            if (pos == -1)
            {
                throw new FormatException("Invalid header format");
            }

            var name = line.Substring(0, pos).Trim();
            var values = line.Substring(pos + 1)
                .Split(',')
                .Select(v => v.Trim())
                .ToList();

            return new Tuple<string, List<string>>(name, values);
        }
    }
}
