// <copyright file="ILibrdkafkaHandle.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Runtime.InteropServices;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka;

/// <summary>
/// Duck Type for Confluent.Kafka.Handle to access the internal LibrdkafkaHandle (SafeKafkaHandle) property.
/// SafeKafkaHandle extends SafeHandleZeroIsInvalid which extends SafeHandle,
/// so we can access DangerousGetHandle() to get the native rd_kafka_t* pointer.
/// </summary>
internal interface ILibrdkafkaHandle
{
    SafeHandle? LibrdkafkaHandle { get; }
}
