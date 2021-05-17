// <copyright file="HttpMethod.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1602 // Enumeration items should be documented
namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Elasticsearch
{
    /// <summary>
    /// HTTP method
    /// </summary>
    public enum HttpMethod
    {
        GET,
        POST,
        PUT,
        DELETE,
        HEAD,
    }
}
