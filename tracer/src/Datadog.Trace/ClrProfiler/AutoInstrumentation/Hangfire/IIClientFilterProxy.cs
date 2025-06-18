// <copyright file="IIClientFilterProxy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Hangfire;

/// <summary>
/// DuckTyping interface for Hangfire.Client.IClientFilter
/// </summary>
/// <remarks>
/// https://github.com/HangfireIO/Hangfire/blob/96c5d825ab3ee6f123f9e041ac301881e168e508/src/Hangfire.Core/Client/IClientFilter.cs
/// </remarks>
internal interface IIClientFilterProxy : IDuckType
{
    /// <summary>
    /// Calls method: System.Void Hangfire.Client.IClientFilter::OnCreating(Hangfire.Client.CreatingContext)
    /// </summary>
    void OnCreating(object context);

    /// <summary>
    /// Calls method: System.Void Hangfire.Client.IClientFilter::OnCreated(Hangfire.Client.CreatedContext)
    /// </summary>
    void OnCreated(object context);
}
