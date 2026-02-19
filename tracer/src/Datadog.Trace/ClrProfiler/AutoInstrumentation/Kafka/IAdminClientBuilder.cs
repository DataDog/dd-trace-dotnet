// <copyright file="IAdminClientBuilder.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka;

/// <summary>
/// Duck Type for Confluent.Kafka.AdminClientBuilder
/// Returns object so we can TryDuckCast the result separately,
/// allowing graceful fallback on older Confluent.Kafka versions
/// that lack DescribeClusterAsync.
/// </summary>
internal interface IAdminClientBuilder
{
    object Build();
}
