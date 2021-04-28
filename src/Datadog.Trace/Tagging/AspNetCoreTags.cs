using Datadog.Trace.ExtensionMethods;

namespace Datadog.Trace.Tagging
{
    internal class AspNetCoreTags : WebTags
    {
        private protected static readonly IProperty<string>[] AspNetCoreTagsProperties =
            WebTagsProperties.Concat(
                new ReadOnlyProperty<AspNetCoreTags, string>(Trace.Tags.InstrumentationName, t => t.InstrumentationName));

        private const string ComponentName = "aspnet_core";

        public string InstrumentationName => ComponentName;

        protected override IProperty<string>[] GetAdditionalTags() => AspNetCoreTagsProperties;
    }
}
