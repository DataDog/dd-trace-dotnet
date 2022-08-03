// <copyright file="TracerConstants.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace
{
    internal static class TracerConstants
    {
        public const string Language = "dotnet";

        /// <summary>
        /// 2^63-1
        /// </summary>
        public const ulong MaxTraceId = 9_223_372_036_854_775_807;

        public const string AssemblyVersion = "2.14.0.0";
    }
}
