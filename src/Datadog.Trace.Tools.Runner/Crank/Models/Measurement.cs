using System;

namespace Datadog.Trace.Tools.Runner.Crank.Models
{
    internal class Measurement
    {
        public const string Delimiter = "$$Delimiter$$";

        public DateTimeOffset Timestamp { get; set; }

        public string Name { get; set; }

        public object Value { get; set; }

        public bool IsDelimiter => string.Equals(Name, Delimiter, StringComparison.OrdinalIgnoreCase);
    }
}
