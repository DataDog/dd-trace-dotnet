// <copyright file="AspNetCoreTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.Tagging
{
    internal partial class AspNetCoreTags : WebTags
    {
        public AspNetCoreTags()
            : base("aspnet_core")
        {
        }

        [Tag(Trace.Tags.AspNetCoreRoute)]
        public string AspNetCoreRoute { get; set; }

        [Tag(Tags.HttpRoute)]
        public string HttpRoute { get; set; }
    }
}
