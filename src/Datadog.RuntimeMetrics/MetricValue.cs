using System.Diagnostics;

namespace Datadog.RuntimeMetrics
{
    [DebuggerDisplay("Metric = {Metric}, Value = {Value}, Tags = {Tags}")]
    public struct MetricValue
    {
        private static readonly string[] Empty = new string[0];

        public Metric Metric;
        public double Value;
        public string[] Tags;

        public MetricValue(Metric metric, double value)
            : this(metric, value, tags: Empty)
        {
        }

        public MetricValue(Metric metric, double value, string[] tags)
        {
            Metric = metric;
            Value = value;
            Tags = tags ?? Empty;
        }
    }
}
