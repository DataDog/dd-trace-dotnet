using System;
using System.Diagnostics;

namespace Datadog.RuntimeMetrics
{
    public class MetricType
    {
        public static readonly MetricType Counting = new MetricType("Counting", "c");
        public static readonly MetricType Gauge = new MetricType("Gauge", "g");
        public static readonly MetricType Histogram = new MetricType("Histogram", "h");
        public static readonly MetricType Distribution = new MetricType("Distribution", "d");
        public static readonly MetricType Set = new MetricType("Set", "s");

        public string Name { get; }
        public string Code { get; }

        public MetricType(string name, string code)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Code = code ?? throw new ArgumentNullException(nameof(code));
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
