// <copyright file="IHttpMethodActionConstraint.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#if !NETFRAMEWORK

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore.Mvc;

internal interface IHttpMethodActionConstraint
{
    public System.Collections.Generic.IEnumerable<string> HttpMethods { get; }
}

#endif
