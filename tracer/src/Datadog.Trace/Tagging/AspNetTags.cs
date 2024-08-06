// <copyright file="AspNetTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Internal.SourceGenerators;

namespace Datadog.Trace.Internal.Tagging
{
    internal partial class AspNetTags : WebTags
    {
        [Tag(Trace.Internal.Tags.AspNetRoute)]
        public string AspNetRoute { get; set; }

        [Tag(Trace.Internal.Tags.AspNetController)]
        public string AspNetController { get; set; }

        [Tag(Trace.Internal.Tags.AspNetAction)]
        public string AspNetAction { get; set; }

        [Tag(Trace.Internal.Tags.AspNetArea)]
        public string AspNetArea { get; set; }

        [Tag(Tags.HttpRoute)]
        public string HttpRoute { get; set; }
    }
}
