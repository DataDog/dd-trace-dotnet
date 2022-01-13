﻿// <copyright file="LogPropertyRenderingDetails.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

namespace Datadog.Trace.Logging.DirectSubmission.Formatting
{
    internal readonly struct LogPropertyRenderingDetails
    {
        public readonly bool HasRenderedSource;
        public readonly bool HasRenderedService;
        public readonly bool HasRenderedHost;
        public readonly bool HasRenderedTags;
        public readonly bool HasRenderedEnv;
        public readonly bool HasRenderedVersion;
        public readonly string? MessageTemplate;

        public LogPropertyRenderingDetails(
            bool hasRenderedSource,
            bool hasRenderedService,
            bool hasRenderedHost,
            bool hasRenderedTags,
            bool hasRenderedEnv,
            bool hasRenderedVersion,
            string? messageTemplate)
        {
            HasRenderedSource = hasRenderedSource;
            HasRenderedService = hasRenderedService;
            HasRenderedHost = hasRenderedHost;
            HasRenderedTags = hasRenderedTags;
            HasRenderedEnv = hasRenderedEnv;
            HasRenderedVersion = hasRenderedVersion;
            MessageTemplate = messageTemplate;
        }
    }
}
