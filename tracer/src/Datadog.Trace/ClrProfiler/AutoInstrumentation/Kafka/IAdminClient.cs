// <copyright file="IAdminClient.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Threading.Tasks;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka;

/// <summary>
/// Duck Type for Confluent.Kafka.IAdminClient
/// Used to access DescribeClusterAsync extension method for cluster_id extraction
/// Duck typing can see extension methods as if they were instance methods
/// </summary>
internal interface IAdminClient : IDisposable
{
    Task<IDescribeClusterResult> DescribeClusterAsync(IDescribeClusterOptions? options = null);
}
