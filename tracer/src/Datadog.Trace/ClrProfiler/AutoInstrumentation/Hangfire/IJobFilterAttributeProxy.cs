// <copyright file="IJobFilterAttributeProxy.cs" company="Datadog">
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
/// DuckTyping interface for Hangfire.Common.JobFilterAttribute
/// </summary>
internal interface IJobFilterAttributeProxy : IDuckType
{
    /// <summary>
    /// Gets or sets a value of System.Collections.Concurrent.ConcurrentDictionary`2[System.Type,System.Boolean]
    /// </summary>
    [DuckField(Name = "MultiuseAttributeCache")]
    object MultiuseAttributeCacheField { get; set; }

    /// <summary>
    /// Gets a value of System.Int32
    /// </summary>
    [DuckField(Name = "_order")]
    int OrderField { get; }

    /// <summary>
    /// Gets a value indicating whether gets a value of System.Boolean
    /// </summary>
    bool AllowMultiple { get; }

    /// <summary>
    /// Gets or sets a value of System.Int32
    /// </summary>
    int Order { get; set; }

    /// <summary>
    /// Gets a value of System.Object
    /// </summary>
    object TypeId { get; }

    /// <summary>
    /// Calls method: System.Boolean Hangfire.Common.JobFilterAttribute::AllowsMultiple(System.Type)
    /// </summary>
    bool AllowsMultiple(Type attributeType);
}
