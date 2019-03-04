using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.ClrProfiler.Interfaces;

namespace Datadog.Trace.ClrProfiler.Models
{
    internal abstract class BaseSpanDecorationSource : ISpanDecorationSource
    {
        private static readonly IEnumerable<KeyValuePair<string, string>> EmptyTags = Enumerable.Empty<KeyValuePair<string, string>>();

        public virtual IEnumerable<KeyValuePair<string, string>> GetTags() => EmptyTags;

        public virtual bool TryGetResourceName(out string resourceName)
        {
            resourceName = null;

            return false;
        }

        public virtual bool TryGetType(out string spanType)
        {
            spanType = null;

            return false;
        }
    }
}
