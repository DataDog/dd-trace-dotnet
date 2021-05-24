using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Dotnet.WindowsContainer.Example.Pages
{
    public class IndexModel : PageModel
    {
        public IEnumerable<KeyValuePair<string, string>> EnvVars { get; set; }
        public bool IsProfilerAttached { get; set; }
        public string TracerAssemblyLocation { get; set; }
        public string ClrProfilerAssemblyLocation { get; set; }

        public void OnGet()
        {
            var instrumentationType = Type.GetType("Datadog.Trace.ClrProfiler.Instrumentation, Datadog.Trace.ClrProfiler.Managed");
            IsProfilerAttached = (bool?)instrumentationType?.GetProperty("ProfilerAttached", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) ?? false;
            TracerAssemblyLocation = Type.GetType("Datadog.Trace.Tracer, Datadog.Trace")?.Assembly.Location;
            ClrProfilerAssemblyLocation = instrumentationType?.Assembly.Location;

            var prefixes = new[] { "COR_", "CORECLR_", "DD_", "DATADOG_" };

            EnvVars = from envVar in Environment.GetEnvironmentVariables().Cast<DictionaryEntry>()
                      from prefix in prefixes
                      let key = (envVar.Key as string)?.ToUpperInvariant()
                      let value = envVar.Value as string
                      where key.StartsWith(prefix)
                      orderby key
                      select new KeyValuePair<string, string>(key, value);
        }
    }
}
