// <copyright file="IClientHandle.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka;

/// <summary>
/// Duck Type for Confluent.Kafka.IClient to access the Handle property.
/// The Handle object can be passed to DependentAdminClientBuilder to
/// create an AdminClient that reuses the existing broker connection.
/// </summary>
internal interface IClientHandle
{
    object Handle { get; }
}
