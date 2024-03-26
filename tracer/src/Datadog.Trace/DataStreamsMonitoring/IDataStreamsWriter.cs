// <copyright file="IDataStreamsWriter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Threading.Tasks;
using Datadog.Trace.DataStreamsMonitoring.Aggregation;

namespace Datadog.Trace.DataStreamsMonitoring;

internal interface IDataStreamsWriter
{
    void Add(in StatsPoint point);

    void AddBacklog(in BacklogPoint point);

    Task DisposeAsync();
}
