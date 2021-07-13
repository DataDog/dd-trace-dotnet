using System;
using System.Collections.Generic;

namespace WeatherServiceDotNetCore
{
    public class ApiModel
    {
        public string Timestamp { get; set; }

        public IEnumerable<string> Assemblies { get; set; }
    }
}
