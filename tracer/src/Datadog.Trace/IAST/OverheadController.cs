// <copyright file="OverheadController.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Iast.Settings;

namespace Datadog.Trace.Iast;

internal class OverheadController
{
    private int _sampling;
    private int _executedRequests = 0;
    private IastSettings _iastSettings;
    private int _availableRequests;

    /// <summary>
    /// Initializes a new instance of the <see cref="OverheadController"/> class.
    /// For testing only.
    /// Note that this API does NOT replace the global OverheadController instance.
    /// </summary>
    internal OverheadController(IastSettings settings = null)
    {
        _iastSettings = settings ?? Iast.Instance.Settings;
        _availableRequests = _iastSettings.MaxConcurrentRequests;
        _sampling = ComputeSamplingParameter(_iastSettings.RequestSampling);
    }

    public static OverheadController Instance { get; } = new();

    public bool AcquireRequest()
    {
        lock (Instance)
        {
            if ((_executedRequests++ % _sampling != 0) || (_availableRequests <= 0))
            {
                return false;
            }

            _availableRequests--;
        }

        return true;
    }

    public void ReleaseRequest()
    {
        lock (Instance)
        {
            if (_availableRequests < _iastSettings.MaxConcurrentRequests)
            {
                _availableRequests++;
            }
        }
    }

    public void Reset()
    {
        lock (Instance)
        {
            _availableRequests = _iastSettings.MaxConcurrentRequests;
        }
    }

    private static int ComputeSamplingParameter(int pct)
    {
        return (int)Math.Round(100m / pct);
    }
}
