// <copyright file="DebuggerSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Debugger;

internal class DebuggerSettings
{
    private static readonly DebuggerSettings Instance = new();

    public bool Enabled => false;

    public bool CodeOriginForSpansEnabled => false;

    public static DebuggerSettings FromDefaultSource() => Instance;
}
