using System;
using System.Collections.Generic;

namespace MismatchedTracerVersions.AspNetCore
{
    public class ApiModel
    {
        public string Timestamp { get; set; }

        public IEnumerable<string> Assemblies { get; set; }
    }
}
