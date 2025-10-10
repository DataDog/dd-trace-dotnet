﻿// <copyright file="DeprecationConstants.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.Configuration
{
    internal static class DeprecationConstants
    {
        public const string AppAnalytics = "App Analytics has been replaced by Tracing without Limits. For more information see https://docs.datadoghq.com/tracing/legacy_app_analytics/";
        public const string ProfilerLogPathObsoleteMessage = "DD_TRACE_LOG_PATH is deprecated. Use DD_TRACE_LOG_DIRECTORY instead";

        [Obsolete(ProfilerLogPathObsoleteMessage)]
        public const string ProfilerLogPath = "DD_TRACE_LOG_PATH";
    }
}
