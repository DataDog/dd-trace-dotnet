// <copyright file="IGrpcCapabilities.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK
#nullable enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.Functions;

/// <summary>
/// Duck type for https://github.com/Azure/azure-functions-host/blob/8ceb05a89a4337f07264d4991545538a3e8b58a0/src/WebJobs.Script.Grpc/Channel/GrpcCapabilities.cs
/// </summary>
internal interface IGrpcCapabilities
{
    string GetCapabilityState(string capability);
}

#endif
