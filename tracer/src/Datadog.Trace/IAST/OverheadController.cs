// <copyright file="OverheadController.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Iast.Settings;

namespace Datadog.Trace.Iast;

internal class OverheadController
{
    private int sampling;
    private int executedRequests = 0;
    private IastSettings iastSettings = Iast.Instance.Settings;
    private int availableRequests;

    /// <summary>
    /// Initializes a new instance of the <see cref="OverheadController"/> class.
    /// For testing only.
    /// Note that this API does NOT replace the global OverheadController instance.
    /// </summary>
    internal OverheadController()
    {
        availableRequests = iastSettings.MaxConcurrentRequests;
        sampling = ComputeSamplingParameter(Iast.Instance.Settings.RequestSampling);
    }

    public static OverheadController Instance { get; } = new();

    public bool AcquireRequest()
    {
        lock (Instance)
        {
            if (((executedRequests++) % sampling != 0) || (availableRequests <= 0))
            {
                return false;
            }

            availableRequests--;
        }

        return true;
    }

    public void ReleaseRequest()
    {
        if (availableRequests < iastSettings.MaxConcurrentRequests)
        {
            lock (Instance)
            {
                availableRequests++;
            }
        }
    }

    public void Reset()
    {
        // Periodic reset of maximum concurrent requests. This guards us against exhausting concurrent
        // requests if some bug led us to lose a request end event. This will lead to periodically
        // going above the max concurrent requests. But overall, it should be self-stabilizing. So for
        // practical purposes, the max concurrent requests is a hint.
        lock (Instance)
        {
            availableRequests = iastSettings.MaxConcurrentRequests;
        }
    }

    private static int ComputeSamplingParameter(decimal pct)
    {
        // We don't support disabling IAST by setting the sampling to 0.
        if ((pct >= 100) || (pct <= 0))
        {
            return 1;
        }

        return (int)Math.Round(100 / pct);
    }
}
