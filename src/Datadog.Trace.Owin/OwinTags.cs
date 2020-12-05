using System.Linq;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.Owin
{
    internal class OwinTags : WebTags
    {
        private const string ComponentName = "Owin";

        private static readonly IProperty<string>[] AspNetCoreTagsProperties =
            WebTagsProperties.Concat(
                new ReadOnlyProperty<OwinTags, string>(Trace.Tags.InstrumentationName, t => t.InstrumentationName));

        public string InstrumentationName => ComponentName;

        protected override IProperty<string>[] GetAdditionalTags() => AspNetCoreTagsProperties;
    }
}
