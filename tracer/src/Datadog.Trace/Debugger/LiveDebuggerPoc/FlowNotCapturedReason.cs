// <copyright file="FlowNotCapturedReason.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Debugger.LiveDebuggerPoc
{
    internal enum FlowNotCapturedReason : byte
    {
        None = 0,
        Depth = 1,
        CollectionSize = 2,
        PayloadLimit = 3,
        Unsupported = 4
    }
}
