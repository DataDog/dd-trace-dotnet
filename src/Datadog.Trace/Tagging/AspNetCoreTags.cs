using Datadog.Trace.ExtensionMethods;

namespace Datadog.Trace.Tagging
{
    internal class AspNetCoreTags : WebTags
    {
        private const string ComponentName = "aspnet_core";

        private static readonly IProperty<string>[] AspNetCoreTagsProperties =
            WebTagsProperties.Concat(
                new ReadOnlyProperty<AspNetCoreTags, string>(Trace.Tags.InstrumentationName, t => t.InstrumentationName),
                new Property<AspNetCoreTags, string>(Trace.Tags.AspNetRoute, t => t.AspNetRoute, (t, v) => t.AspNetRoute = v),
                new Property<AspNetCoreTags, string>(Trace.Tags.AspNetArea, t => t.AspNetArea, (t, v) => t.AspNetArea = v),
                new Property<AspNetCoreTags, string>(Trace.Tags.AspNetController, t => t.AspNetController, (t, v) => t.AspNetController = v),
                new Property<AspNetCoreTags, string>(Trace.Tags.AspNetAction, t => t.AspNetAction, (t, v) => t.AspNetAction = v));

        public string InstrumentationName => ComponentName;

        public string AspNetRoute { get; set; }

        public string AspNetController { get; set; }

        public string AspNetAction { get; set; }

        public string AspNetArea { get; set; }

        protected override IProperty<string>[] GetAdditionalTags() => AspNetCoreTagsProperties;
    }
}
