// <copyright file="ProbeLocationType.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable disable

namespace Datadog.Trace.Debugger
{
    internal enum ProbeLocationType
    {
        Line,
        Method,
        Unrecognized
    }
}
