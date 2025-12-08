// <copyright file="WebTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.Tagging
{
    internal partial class WebTags : WebTagsWithoutIpTracking
    {
        [Tag(Trace.Tags.NetworkClientIp)]
        public string NetworkClientIp { get; set; }

        [Tag(Trace.Tags.HttpClientIp)]
        public string HttpClientIp { get; set; }
    }
}
