﻿// <copyright file="AspNetCoreMvcTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.Tagging
{
    internal partial class AspNetCoreMvcTags : AspNetCoreTags
    {
        [Tag(Trace.Tags.AspNetCoreController)]
        public string AspNetCoreController { get; set; }

        [Tag(Trace.Tags.AspNetCoreAction)]
        public string AspNetCoreAction { get; set; }

        [Tag(Trace.Tags.AspNetCoreArea)]
        public string AspNetCoreArea { get; set; }

        [Tag(Trace.Tags.AspNetCorePage)]
        public string AspNetCorePage { get; set; }
    }
}
