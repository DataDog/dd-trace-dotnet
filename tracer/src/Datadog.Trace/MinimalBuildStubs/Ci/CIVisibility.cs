// <copyright file="CIVisibility.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Ci.Configuration;

namespace Datadog.Trace.Ci;

internal class CIVisibility
{
    public static bool IsRunning => false;

    public static CIVisibilitySettings Settings { get; } = new();

    public static bool Enabled => false;

    public static void Initialize()
    {
    }
}
