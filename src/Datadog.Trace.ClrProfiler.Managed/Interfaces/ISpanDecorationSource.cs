using System.Collections.Generic;

namespace Datadog.Trace.ClrProfiler.Interfaces
{
    internal interface ISpanDecorationSource
    {
        IEnumerable<KeyValuePair<string, string>> GetTags();

        bool TryGetResourceName(out string resourceName);

        bool TryGetType(out string spanType);
    }
}
