using Datadog.Trace.ExtensionMethods;

namespace Datadog.Trace.Tagging
{
    internal class AspNetCoreEndpointTags : AspNetCoreTags
    {
        private static readonly IProperty<string>[] AspNetCoreEndpointTagsProperties =
            AspNetCoreTagsProperties.Concat(
                new Property<AspNetCoreEndpointTags, string>(Trace.Tags.AspNetCoreRoute, t => t.AspNetCoreRoute, (t, v) => t.AspNetCoreRoute = v),
                new Property<AspNetCoreEndpointTags, string>(Trace.Tags.AspNetCoreArea, t => t.AspNetCoreArea, (t, v) => t.AspNetCoreArea = v),
                new Property<AspNetCoreEndpointTags, string>(Trace.Tags.AspNetCoreController, t => t.AspNetCoreController, (t, v) => t.AspNetCoreController = v),
                new Property<AspNetCoreEndpointTags, string>(Trace.Tags.AspNetCoreEndpoint, t => t.AspNetCoreEndpoint, (t, v) => t.AspNetCoreEndpoint = v),
                new Property<AspNetCoreEndpointTags, string>(Trace.Tags.AspNetCorePage, t => t.AspNetCorePage, (t, v) => t.AspNetCorePage = v),
                new Property<AspNetCoreEndpointTags, string>(Trace.Tags.AspNetCoreAction, t => t.AspNetCoreAction, (t, v) => t.AspNetCoreAction = v));

        public string AspNetCoreRoute { get; set; }

        public string AspNetCoreController { get; set; }

        public string AspNetCoreAction { get; set; }

        public string AspNetCoreArea { get; set; }

        public string AspNetCorePage { get; set; }

        public string AspNetCoreEndpoint { get; set; }

        protected override IProperty<string>[] GetAdditionalTags() => AspNetCoreEndpointTagsProperties;
    }
}
