// <copyright file="TemplatePartStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
#nullable enable

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore
{
    [DuckCopy]
    internal struct TemplatePartStruct
    {
        public bool IsCatchAll;

        public bool IsOptional;

        public bool IsParameter;

        public string? Name;

        public string? Text;
    }
}
#endif
