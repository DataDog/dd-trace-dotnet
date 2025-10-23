// <copyright file="IAdminClient.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka;

/// <summary>
/// Duck Type for Confluent.Kafka.IAdminClient
/// </summary>
internal interface IAdminClient : IDuckType, IDisposable
{
    /// <summary>
    /// Describes the cluster metadata including the cluster ID
    /// </summary>
    /// <param name="options">Optional configuration options</param>
    /// <returns>Duck typed task containing the cluster description result</returns>
    IDuckTypeTask<IDescribeClusterResult> DescribeClusterAsync(IDescribeClusterOptions? options = null);
}
