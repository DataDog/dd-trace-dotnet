// <copyright file="ILogFactoryPre43Proxy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.DuckTyping;

#nullable enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.NLog.DirectSubmission.Proxies.Pre43
{
    internal interface ILogFactoryPre43Proxy : IDuckType
    {
        // Note - when this is get/set it will do a _lot_ of re-configuration of NLog
        object? Configuration { get; set; }
    }
}
