// <copyright file="MethodCallMetadata.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.ClrProfiler.Managed.Tests
{
    public class MethodCallMetadata
    {
        public string MethodString { get; set; }

        public int MetadataToken { get; set; }

        public object[] Parameters { get; set; }
    }
}
