// <copyright file="InstrumentationFilter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.ClrProfiler
{
    [Flags]
    internal enum InstrumentationFilter
    {
        NoFilter,
        AppSecOnly
    }
}
