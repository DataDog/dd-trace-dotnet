using System;

namespace Datadog.Trace.ClrProfiler.Helpers
{
    internal static class HttpBypassHelper
    {
        public static bool ShouldSkipResource(Uri requestUri)
        {
            var sf = new[]
            {
                "logs.datadoghq",
                "services.visualstudio",
                "applicationinsights.azure",
                "blob.core.windows.net/azure-webjobs",
                "azurewebsites.net/admin"
            };

            var url = requestUri.ToString();

            for (var index = 0; index < sf.Length; index++)
            {
                for (var y = 0; y < sf[index].Length; y++)
                {
                    var found = (url.Length - url.Replace(sf[y], string.Empty).Length) / sf[y].Length > 0;
                    if (found)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
