// <copyright file="ICreatedContextProxy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Hangfire;

/// <summary>
/// DuckTyping interface for Hangfire.Client.CreatedContext
/// </summary>
/// <remarks>
/// https://github.com/HangfireIO/Hangfire/blob/96c5d825ab3ee6f123f9e041ac301881e168e508/src/Hangfire.Core/Client/CreatedContext.cs
/// </remarks>
internal interface ICreatedContextProxy : IDuckType
{
    /// <summary>
    /// Gets a value of System.String
    /// </summary>
    string JobId { get; }

    /// <summary>
    /// Gets a value of System.Collections.Generic.IDictionary`2[System.String,System.Object]
    /// </summary>
    object Parameters { get; }

    /// <summary>
    /// Gets a value of System.Exception
    /// </summary>
    Exception Exception { get; }

    /// <summary>
    /// Gets a value indicating whether it gets a value of System.Boolean
    /// </summary>
    bool Canceled { get; }

    /// <summary>
    /// Gets or sets a value indicating whether it gets or sets a value of System.Boolean
    /// </summary>
    bool ExceptionHandled { get; set; }

    /// <summary>
    /// Calls method: System.Void Hangfire.Client.CreatedContext::SetJobParameter(System.String,System.Object)
    /// </summary>
    void SetJobParameter(string name, object value);
}
