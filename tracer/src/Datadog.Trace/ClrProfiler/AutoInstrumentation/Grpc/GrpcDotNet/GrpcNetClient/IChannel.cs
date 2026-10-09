// <copyright file="IChannel.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using Datadog.Trace.DuckTyping;

#if !NET461

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Grpc.GrpcDotNet.GrpcNetClient;

[DuckType("Grpc.Net.Client.GrpcChannel", "Grpc.Net.Client")]
internal interface IChannel
{
    Uri Address { get; }
}
#endif
