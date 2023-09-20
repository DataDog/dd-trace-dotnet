// <copyright file="NLogVersion.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.NLog
{
    internal enum NLogVersion
    {
        NLog50,
        NLog45,
        NLog43To45,
        NLogPre43,
    }
}
