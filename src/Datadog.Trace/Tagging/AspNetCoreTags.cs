using Datadog.Trace.ExtensionMethods;

namespace Datadog.Trace.Tagging
{
    internal class AspNetCoreTags : WebTags
    {
        private const string ComponentName = "aspnet_core";

        private static readonly IProperty<string>[] AspNetCoreTagsProperties =
            WebTagsProperties.Concat(
                new ReadOnlyProperty<AspNetCoreTags, string>(Trace.Tags.InstrumentationName, t => t.InstrumentationName),
                new Property<AspNetCoreTags, string>(Trace.Tags.AspNetCoreRoute, t => t.AspNetCoreRoute, (t, v) => t.AspNetCoreRoute = v),
                new Property<AspNetCoreTags, string>(Trace.Tags.AspNetCoreArea, t => t.AspNetCoreArea, (t, v) => t.AspNetCoreArea = v),
                new Property<AspNetCoreTags, string>(Trace.Tags.AspNetCoreController, t => t.AspNetCoreController, (t, v) => t.AspNetCoreController = v),
                new Property<AspNetCoreTags, string>(Trace.Tags.AspNetCoreEndpoint, t => t.AspNetCoreEndpoint, (t, v) => t.AspNetCoreEndpoint = v),
                new Property<AspNetCoreTags, string>(Trace.Tags.AspNetCorePage, t => t.AspNetCorePage, (t, v) => t.AspNetCorePage = v),
                new Property<AspNetCoreTags, string>(Trace.Tags.AspNetCoreAction, t => t.AspNetCoreAction, (t, v) => t.AspNetCoreAction = v));

        public string InstrumentationName => ComponentName;

        public string AspNetCoreRoute { get; set; }

        public string AspNetCoreController { get; set; }

        public string AspNetCoreAction { get; set; }

        public string AspNetCoreArea { get; set; }

        public string AspNetCorePage { get; set; }

        public string AspNetCoreEndpoint { get; set; }

        protected override IProperty<string>[] GetAdditionalTags() => AspNetCoreTagsProperties;
    }
}
