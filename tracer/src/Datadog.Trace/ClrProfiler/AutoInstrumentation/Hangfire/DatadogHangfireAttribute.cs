// <copyright file="DatadogHangfireAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.dnlib.DotNet.Writer;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Hangfire;

/// <summary>
/// The datadog job filter
/// </summary>
public class DatadogHangfireAttribute
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(DatadogHangfireAttribute));

    /// <summary>
    /// Do nothing
    /// </summary>
    /// <param name="context"> does nothing</param>
    [DuckReverseMethod(ParameterTypeNames = new[] { "Hangfire.Server.IServerFilter, Hangfire.Core" })]
    public void OnPerforming(object context)
    {
        Log.Debug("On performing hangfire attribute");
    }

    /// <summary>
    /// not
    /// </summary>
    /// <param name="context"> not thing </param>
    [DuckReverseMethod(ParameterTypeNames = new[] { "Hangfire.Server.IServerFilter, Hangfire.Core" })]
    public void OnPerformed(object context)
    {
        Log.Debug("On performed hangfire attribute");
    }

    // /// <summary>
    // /// Nothing
    // /// </summary>
    // /// <param name="context"> TBD </param>
    // [DuckReverseMethod(ParameterTypeNames = new[] { "Hangfire.Server.IClientFilter, Hangfire.Core" })]
    // public void OnCreating(object context)
    // {
    //     Log.Debug("On creating hangfire attribute");
    // }
    //
    // /// <summary>
    // /// Nothign
    // /// </summary>
    // /// <param name="context"> TBD </param>
    // [DuckReverseMethod(ParameterTypeNames = new[] { "Hangfire.Server.IClientFilter, Hangfire.Core" })]
    // public void OnCreated(object context)
    // {
    //     Log.Debug("On created hangfire attribute");
    // }
}
