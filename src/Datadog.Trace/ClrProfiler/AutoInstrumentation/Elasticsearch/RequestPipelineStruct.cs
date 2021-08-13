// <copyright file="RequestPipelineStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented

using System.ComponentModel;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Elasticsearch
{
    /// <summary>
    /// Duck-copy struct for RequestPipeline
    /// </summary>
    [DuckCopy]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct RequestPipelineStruct
    {
        public object RequestParameters;
    }
}
