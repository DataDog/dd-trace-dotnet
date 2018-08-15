using System;
using Newtonsoft.Json;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    internal static class NewtonsoftJsonIntegration
    {
        [TraceMethod("Newtonsoft.Json", "Newtonsoft.Json.JsonConvert", "SerializeObject")]
        public static string SerializeObject(object value)
        {
            using (var scope = Tracer.Instance.StartActive("SerializeObject"))
            {
                try
                {
                    return JsonConvert.SerializeObject(value);
                }
                catch (Exception e)
                {
                    scope.Span.SetException(e);
                    throw;
                }
            }
        }
    }
}
