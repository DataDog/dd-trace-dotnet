// <copyright file="AspNetCoreMvcTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.ExtensionMethods;

namespace Datadog.Trace.Tagging
{
    internal class AspNetCoreMvcTags : AspNetCoreTags
    {
        private static readonly IProperty<string>[] AspNetCoreEndpointTagsProperties =
            AspNetCoreTagsProperties.Concat(
                new Property<AspNetCoreMvcTags, string>(Trace.Tags.AspNetCoreArea, t => t.AspNetCoreArea, (t, v) => t.AspNetCoreArea = v),
                new Property<AspNetCoreMvcTags, string>(Trace.Tags.AspNetCoreController, t => t.AspNetCoreController, (t, v) => t.AspNetCoreController = v),
                new Property<AspNetCoreMvcTags, string>(Trace.Tags.AspNetCorePage, t => t.AspNetCorePage, (t, v) => t.AspNetCorePage = v),
                new Property<AspNetCoreMvcTags, string>(Trace.Tags.AspNetCoreAction, t => t.AspNetCoreAction, (t, v) => t.AspNetCoreAction = v));

        public string AspNetCoreController { get; set; }

        public string AspNetCoreAction { get; set; }

        public string AspNetCoreArea { get; set; }

        public string AspNetCorePage { get; set; }

        protected override IProperty<string>[] GetAdditionalTags() => AspNetCoreEndpointTagsProperties;
    }
}
