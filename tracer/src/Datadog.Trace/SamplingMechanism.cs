// <copyright file="SamplingMechanism.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace;

/// <summary>
/// Sampling decision mechanisms still used to mark manually retained and IAST vulnerability traces.
/// </summary>
internal static class SamplingMechanism
{
    public const string Manual = "-4";

    public const string Asm = "-5";
}
