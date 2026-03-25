// <copyright file="IDescribeClusterTask.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Threading.Tasks;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka;

/// <summary>
/// Duck type for Task&lt;DescribeClusterResult&gt; that avoids using
/// IsCompletedSuccessfully, which does not exist on .NET Framework.
/// </summary>
internal interface IDescribeClusterTask : IDuckType
{
    TaskStatus Status { get; }

    IDescribeClusterResult? Result { get; }

    IDuckTypeAwaiter<IDescribeClusterResult> GetAwaiter();
}
