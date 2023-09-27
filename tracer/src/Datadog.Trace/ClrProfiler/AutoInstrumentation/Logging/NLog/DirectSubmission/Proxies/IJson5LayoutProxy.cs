// <copyright file="IJson5LayoutProxy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.NLog.DirectSubmission.Proxies
{
    internal interface IJson5LayoutProxy : IDuckType
    {
        [DuckField(Name = "_includeScopeProperties")]
        bool IncludeScopePropertiesField { get; set; } // added in v5
    }
}
