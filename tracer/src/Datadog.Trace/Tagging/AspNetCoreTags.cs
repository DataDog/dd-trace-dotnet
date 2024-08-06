// <copyright file="AspNetCoreTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Internal.SourceGenerators;

namespace Datadog.Trace.Internal.Tagging
{
    internal partial class AspNetCoreTags : WebTags
    {
        private const string ComponentName = "aspnet_core";

        // Read/write instead of readonly as AzureFunctions updates the component name
        [Tag(Trace.Internal.Tags.InstrumentationName)]
        public string InstrumentationName { get; set; } = ComponentName;

        [Tag(Trace.Internal.Tags.AspNetCoreRoute)]
        public string AspNetCoreRoute { get; set; }

        [Tag(Tags.HttpRoute)]
        public string HttpRoute { get; set; }
    }
}
