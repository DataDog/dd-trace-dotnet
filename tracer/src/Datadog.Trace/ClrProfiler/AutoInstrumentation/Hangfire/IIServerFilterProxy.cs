// <copyright file="IIServerFilterProxy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Hangfire;

/// <summary>
/// DuckTyping interface for Hangfire.Server.IServerFilter
/// </summary>
/// <remarks>
/// https://github.com/HangfireIO/Hangfire/blob/96c5d825ab3ee6f123f9e041ac301881e168e508/src/Hangfire.Core/Server/IServerFilter.cs
/// </remarks>
internal interface IIServerFilterProxy : IDuckType
{
    /// <summary>
    /// Calls method: System.Void Hangfire.Server.IServerFilter::OnPerforming(Hangfire.Server.PerformingContext)
    /// </summary>
    void OnPerforming(object context);

    /// <summary>
    /// Calls method: System.Void Hangfire.Server.IServerFilter::OnPerformed(Hangfire.Server.PerformedContext)
    /// </summary>
    void OnPerformed(object context);
}
