// <copyright file="DuckTypeMappings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.DuckTyping;

[assembly: DuckTypeMapping("Datadog.Trace.DuckTyping.IDuckTypeAwaiter`1[[Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka.IDescribeClusterResult, Datadog.Trace]]", "Datadog.Trace", "System.Runtime.CompilerServices.TaskAwaiter`1[[Confluent.Kafka.Admin.DescribeClusterResult, Confluent.Kafka]]", "System.Private.CoreLib")]
[assembly: DuckTypeMapping("Datadog.Trace.DuckTyping.IDuckTypeTask`1[[Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka.IDescribeClusterResult, Datadog.Trace]]", "Datadog.Trace", "System.Threading.Tasks.Task`1[[Confluent.Kafka.Admin.DescribeClusterResult, Confluent.Kafka]]", "System.Private.CoreLib")]
