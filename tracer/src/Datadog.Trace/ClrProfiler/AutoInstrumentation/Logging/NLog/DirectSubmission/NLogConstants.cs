// <copyright file="NLogConstants.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Configuration;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.NLog.DirectSubmission
{
    internal static class NLogConstants
    {
        internal const string DatadogTargetName = "Datadog";

        internal const string IntegrationName = nameof(IntegrationId.NLog);
    }
}
