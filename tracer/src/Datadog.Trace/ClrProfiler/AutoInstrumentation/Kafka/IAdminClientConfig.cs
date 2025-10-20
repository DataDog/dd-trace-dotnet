// <copyright file="IAdminClientConfig.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka;

/// <summary>
/// Duck Type for Confluent.Kafka.AdminClientConfig
/// Used to create an AdminClient for cluster_id extraction
/// </summary>
internal interface IAdminClientConfig
{
    string BootstrapServers { get; set; }
}
