// <copyright file="GrpcCoreApiVersionHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

#nullable enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Grpc
{
    internal static class GrpcCoreApiVersionHelper
    {
        static GrpcCoreApiVersionHelper()
        {
            var status = Type.GetType("Grpc.Core.Status, Grpc.Core.Api, Version=2.0.0.0, Culture=neutral, PublicKeyToken=d754f35622e28bad", throwOnError: false);
            IsSupported = status?.GetProperty("DebugException") is not null;
        }

        public static bool IsSupported { get; }
    }
}
