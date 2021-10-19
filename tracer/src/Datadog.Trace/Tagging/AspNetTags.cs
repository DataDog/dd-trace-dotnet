// <copyright file="AspNetTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.Tagging
{
    internal partial class AspNetTags : WebTags
    {
        [TagName(Trace.Tags.AspNetRoute)]
        public string AspNetRoute { get; set; }

        [TagName(Trace.Tags.AspNetController)]
        public string AspNetController { get; set; }

        [TagName(Trace.Tags.AspNetAction)]
        public string AspNetAction { get; set; }

        [TagName(Trace.Tags.AspNetArea)]
        public string AspNetArea { get; set; }
    }
}
