using Datadog.Trace.ExtensionMethods;

namespace Datadog.Trace.Tagging
{
    internal class AspNetCoreTags : WebTags
    {
        private static new readonly IProperty<string>[] TagsProperties =
            WebTags.TagsProperties.Concat(
                new Property<AspNetCoreTags, string>(Trace.Tags.InstrumentationName, t => t.InstrumentationName, (t, v) => t.InstrumentationName = v));

        public string InstrumentationName { get; set; }

        protected override IProperty<string>[] GetAdditionalTags() => TagsProperties;
    }
}
