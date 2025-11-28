// <copyright file="ImmutableDynamicDebuggerSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Debugger.Configurations
{
    internal sealed record ImmutableDynamicDebuggerSettings
    {
        public bool? DynamicInstrumentationEnabled { get; init; }

        public bool? ExceptionReplayEnabled { get; init; }

        public bool? CodeOriginEnabled { get; init; }
    }
}
