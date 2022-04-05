// <copyright file="TemporaryHeaders.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Grpc.GrpcLegacy.Client
{
    internal class TemporaryHeaders
    {
        public const string Service = "x-datadog-temp-service";
        public const string MethodName = "x-datadog-temp-method";
        public const string StartTime = "x-datadog-temp-starttime";
        public const string MethodKind = "x-datadog-temp-kind";
        public const string ParentId = "x-datadog-temp-parent-id";
        public const string ParentService = "x-datadog-temp-parent-service";
    }
}
