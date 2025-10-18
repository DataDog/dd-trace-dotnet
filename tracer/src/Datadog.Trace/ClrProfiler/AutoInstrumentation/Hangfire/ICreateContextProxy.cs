// <copyright file="ICreateContextProxy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.Collections.Generic;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Hangfire;

/// <summary>
/// DuckTyping interface for Hangfire.Client.CreateContext
/// </summary>
/// <remarks>
/// https://github.com/HangfireIO/Hangfire/blob/96c5d825ab3ee6f123f9e041ac301881e168e508/src/Hangfire.Core/Client/CreateContext.cs
/// </remarks>
internal interface ICreateContextProxy : IDuckType
{
    /// <summary>
    /// Gets a value of System.Collections.Generic.IDictionary`2[System.String,System.Object]
    /// </summary>
    IDictionary<string, object?> Items { get; }

    /// <summary>
    /// Gets a value of System.Collections.Generic.IDictionary`2[System.String,System.Object]
    /// </summary>
    IDictionary<string, object?> Parameters { get; }
}
