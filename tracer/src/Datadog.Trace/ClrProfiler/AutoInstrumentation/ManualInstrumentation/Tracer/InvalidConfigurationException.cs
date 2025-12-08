// <copyright file="InvalidConfigurationException.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Diagnostics.CodeAnalysis;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Tracer;

/// <summary>
/// An exception indicating that an invalid value was specified for a setting in the public API.
/// </summary>
internal class InvalidConfigurationException(string message) : CallTargetBubbleUpException(message)
{
    [DoesNotReturn]
    public static void Throw(string message) => throw new InvalidConfigurationException(message);
}
