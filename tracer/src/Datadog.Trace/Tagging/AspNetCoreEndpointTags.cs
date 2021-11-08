// <copyright file="AspNetCoreEndpointTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.ExtensionMethods;

namespace Datadog.Trace.Tagging
{
    internal class AspNetCoreEndpointTags : AspNetCoreTags
    {
        private static readonly IProperty<string>[] AspNetCoreEndpointTagsProperties =
            AspNetCoreTagsProperties.Concat(new Property<AspNetCoreEndpointTags, string>(Trace.Tags.AspNetCoreEndpoint, t => t.AspNetCoreEndpoint, (t, v) => t.AspNetCoreEndpoint = v));

        public string AspNetCoreEndpoint { get; set; }

        protected override IProperty<string>[] GetAdditionalTags() => AspNetCoreEndpointTagsProperties;
    }
}
