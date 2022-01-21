// <copyright file="ICommonTracer2.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.ClrProfiler;

internal interface ICommonTracer2 : ICommonTracer
{
    bool GetSamplingDecision(out int priority, out int mechanism, out double? rate);

    void SetSamplingDecision(int priority, int mechanism, double? rate);

    void ClearSamplingDecision();
}
