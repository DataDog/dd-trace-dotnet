// <copyright file="RequestContextStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.DuckTyping;

#if NETFRAMEWORK
namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNet
{
    /// <summary>
    /// Duck type for https://github.com/aspnet/AspNetWebStack/blob/main/src/System.Web.Http/Controllers/HttpRequestContext.cs
    /// </summary>
    [DuckCopy]
    internal struct RequestContextStruct
    {
        public string VirtualPathRoot;
    }
}
#endif
