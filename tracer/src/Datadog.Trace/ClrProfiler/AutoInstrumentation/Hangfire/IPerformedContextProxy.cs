// <copyright file="IPerformedContextProxy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Hangfire;

/// <summary>
/// DuckTyping interface for Hangfire.Server.PerformedContext
/// </summary>
/// <remarks>
/// https://github.com/HangfireIO/Hangfire/blob/96c5d825ab3ee6f123f9e041ac301881e168e508/src/Hangfire.Core/Server/PerformedContext.cs
/// </remarks>
public interface IPerformedContextProxy : IPerformContextProxy
{
    /// <summary>
    /// Gets a value of System.Object
    /// </summary>
    object Result { get; }

    /// <summary>
    /// Gets a value indicating whether it gets a value of System.Boolean
    /// </summary>
    bool Canceled { get; }

    /// <summary>
    /// Gets a value of System.Exception
    /// </summary>
    Exception? Exception { get; }

    /// <summary>
    /// Gets or sets a value indicating whether it gets or sets a value of System.Boolean
    /// </summary>
    bool ExceptionHandled { get; set; }
}
