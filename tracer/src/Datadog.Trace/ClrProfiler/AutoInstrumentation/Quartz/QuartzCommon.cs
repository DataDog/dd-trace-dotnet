// <copyright file="QuartzCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Quartz
{
    internal static class QuartzCommon
    {
        internal static string CreateResourceName(string operationName, string jobName)
        {
            return operationName switch
            {
                var name when name.Contains("Execute") => "execute " + jobName,
                var name when name.Contains("Veto") => "veto " + jobName,
                _ => operationName
            };
        }
    }
}
