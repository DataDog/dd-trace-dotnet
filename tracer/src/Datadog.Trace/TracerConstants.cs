// <copyright file="TracerConstants.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Text;

namespace Datadog.Trace
{
    internal static class TracerConstants
    {
        public const string TelemetrySdkName = "datadog";
        public const string Language = "dotnet";
        public static readonly string AssemblyVersion = typeof(TracerConstants).Assembly.GetName().Version?.ToString() ?? "0.0.0.0";
        public const string ThreePartVersion = "3.46.0";

        private static readonly byte[] AssemblyVersionBytesArray = Encoding.UTF8.GetBytes(AssemblyVersion);

        public static ReadOnlySpan<byte> AssemblyVersionBytes => AssemblyVersionBytesArray;
    }
}
