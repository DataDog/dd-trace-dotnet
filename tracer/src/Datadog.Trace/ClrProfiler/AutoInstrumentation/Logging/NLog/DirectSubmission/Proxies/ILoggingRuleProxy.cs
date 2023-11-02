// <copyright file="ILoggingRuleProxy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections;
using Datadog.Trace.DuckTyping;

#nullable enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.NLog.DirectSubmission.Proxies;

internal interface ILoggingRuleProxy : IDuckType
{
    public IList Targets { get; }

    public string LoggerNamePattern { get; set; }

    public bool Final { get; set; }

    [Duck(ParameterTypeNames = new[] { "NLog.LogLevel , NLog", "NLog.LogLevel , NLog" })]
    public void EnableLoggingForLevels(object minLevel, object maxLevel);
}
