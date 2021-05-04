using System;

namespace Datadog.Trace.ClrProfiler.Helpers
{
    internal static class HttpBypassHelper
    {
        public static bool ShouldSkipResource(Uri requestUri, string[] patternsToSkip)
        {
            var url = requestUri.ToString();

            for (var index = 0; index < patternsToSkip.Length; index++)
            {
                for (var y = 0; y < patternsToSkip.Length; y++)
                {
                    var found = (url.Length - url.Replace(patternsToSkip[y], string.Empty).Length) / patternsToSkip[y].Length > 0;
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
