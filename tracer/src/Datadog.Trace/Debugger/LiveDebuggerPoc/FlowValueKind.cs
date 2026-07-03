// <copyright file="FlowValueKind.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Debugger.LiveDebuggerPoc
{
    internal enum FlowValueKind : byte
    {
        This = 1,
        Argument = 2,
        Local = 3,
        Return = 4,
        Exception = 5
    }
}
