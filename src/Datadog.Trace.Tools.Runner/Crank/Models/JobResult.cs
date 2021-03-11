using System;
using System.Collections.Generic;

namespace Datadog.Trace.Tools.Runner.Crank.Models
{
    internal class JobResult
    {
        public Dictionary<string, object> Results { get; set; } = new();

        public ResultMetadata[] Metadata { get; set; } = Array.Empty<ResultMetadata>();

        public List<Measurement[]> Measurements { get; set; } = new();

        public Dictionary<string, object> Environment { get; set; } = new();
    }
}
