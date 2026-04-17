// <copyright file="IDebuggerGlobalRateLimiter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Debugger.Expressions;

namespace Datadog.Trace.Debugger.RateLimiting
{
    internal interface IDebuggerGlobalRateLimiter : IDisposable
    {
        bool ShouldSample(ProbeType probeType, string probeId);

        void SetRate(double? samplesPerSecond);

        void ResetRate();
    }
}
