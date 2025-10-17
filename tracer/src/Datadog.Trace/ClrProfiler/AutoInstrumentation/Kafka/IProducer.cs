// <copyright file="IProducer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka;

/// <summary>
/// Duck Type for IProducer[TKey, TValue]
/// Used to access GetMetadata method for cluster_id extraction
/// </summary>
internal interface IProducer
{
    IMetadata GetMetadata(TimeSpan timeout);
}
