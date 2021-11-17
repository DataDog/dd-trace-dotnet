// <copyright file="AspNetCoreTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.ExtensionMethods;

namespace Datadog.Trace.Tagging
{
    internal class AspNetCoreTags : WebTags
    {
        private protected static readonly IProperty<string>[] AspNetCoreTagsProperties =
            WebTagsProperties.Concat(
                new ReadOnlyProperty<AspNetCoreTags, string>(Trace.Tags.InstrumentationName, t => t.InstrumentationName),
                new Property<AspNetCoreTags, string>(Trace.Tags.AspNetCoreRoute, t => t.AspNetCoreRoute, (t, v) => t.AspNetCoreRoute = v));

        private const string ComponentName = "aspnet_core";

        public string InstrumentationName => ComponentName;

        public string AspNetCoreRoute { get; set; }

        protected override IProperty<string>[] GetAdditionalTags() => AspNetCoreTagsProperties;
    }
}
