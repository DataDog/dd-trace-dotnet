using Datadog.Trace.ExtensionMethods;

namespace Datadog.Trace.Tagging
{
    internal class AspNetTags : WebTags
    {
        private static readonly IProperty<string>[] AspNetTagsProperties =
            WebTagsProperties.Concat(
                new Property<AspNetTags, string>(Trace.Tags.AspNetRoute, t => t.AspNetRoute, (t, v) => t.AspNetRoute = v),
                new Property<AspNetTags, string>(Trace.Tags.AspNetArea, t => t.AspNetArea, (t, v) => t.AspNetArea = v),
                new Property<AspNetTags, string>(Trace.Tags.AspNetController, t => t.AspNetController, (t, v) => t.AspNetController = v),
                new Property<AspNetTags, string>(Trace.Tags.AspNetAction, t => t.AspNetAction, (t, v) => t.AspNetAction = v));

        public string AspNetRoute { get; set; }

        public string AspNetController { get; set; }

        public string AspNetAction { get; set; }

        public string AspNetArea { get; set; }

        protected override IProperty<string>[] GetAdditionalTags() => AspNetTagsProperties;
    }
}
