// <copyright file="ICreatingContextProxy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Hangfire;

/// <summary>
/// DuckTyping interface for Hangfire.Client.CreatingContext
/// </summary>
internal interface ICreatingContextProxy : IDuckType
{
    /// <summary>
    /// Gets or sets a value indicating whether gets or sets a value of System.Boolean
    /// </summary>
    bool Canceled { get; set; }

    /// <summary>
    /// Calls method: System.Void Hangfire.Client.CreatingContext::SetJobParameter(System.String,System.Object)
    /// </summary>
    void SetJobParameter(string name, object value);
}
