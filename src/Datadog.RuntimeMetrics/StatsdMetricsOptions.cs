using System.Collections.Generic;
using System.Linq;

namespace Datadog.RuntimeMetrics
{
    public class StatsdMetricsOptions
    {
        public IEnumerable<string> Tags { get; set; } = Enumerable.Empty<string>();

        public double? SampleRate { get; set; } = 1d;
    }
}
