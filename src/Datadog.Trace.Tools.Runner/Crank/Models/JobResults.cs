using System.Collections.Generic;

namespace Datadog.Trace.Tools.Runner.Crank.Models
{
    internal class JobResults
    {
        public Dictionary<string, JobResult> Jobs { get; set; } = new();

        public Dictionary<string, string> Properties { get; set; } = new();
    }
}
