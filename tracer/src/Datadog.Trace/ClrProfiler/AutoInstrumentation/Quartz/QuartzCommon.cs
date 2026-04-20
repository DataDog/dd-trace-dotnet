// <copyright file="QuartzCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Quartz
{
    internal static class QuartzCommon
    {
        internal const string ComponentName = "quartz";

        internal static string CreateResourceName(string operationName, string jobName)
        {
            return operationName switch
            {
                _ when operationName.Contains("Execute") => "execute " + jobName,
                _ when operationName.Contains("Veto") => "veto " + jobName,
                _ => operationName
            };
        }
    }
}
