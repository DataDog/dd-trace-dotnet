// <copyright file="OverheadController.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Iast;

internal sealed class OverheadController
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(OverheadController));
    private readonly int _maxConcurrentRequests;
    private readonly int _sampling;
    private int _executedRequests;
    private int _availableRequests;

    internal OverheadController(int maxConcurrentRequest, int samplingParameter)
    {
        _maxConcurrentRequests = maxConcurrentRequest;
        _availableRequests = maxConcurrentRequest;
        _sampling = ComputeSamplingParameter(samplingParameter);
    }

    public bool AcquireRequest()
    {
        var result = true;
        lock (this)
        {
            if (_availableRequests <= 0 || _sampling == 0 || _executedRequests % _sampling != 0)
            {
                Log.Debug<int, int>("Could not acquire request from OverheadController, sampling is {Sampling} and available requests are {AvailableRequests}", _sampling, _availableRequests);
                result = false;
            }
            else
            {
                _availableRequests--;
            }

            _executedRequests++;
        }

        return result;
    }

    public void ReleaseRequest()
    {
        lock (this)
        {
            if (_availableRequests < _maxConcurrentRequests)
            {
                _availableRequests++;
            }
        }
    }

    public void Reset()
    {
        lock (this)
        {
            _availableRequests = _maxConcurrentRequests;
        }
    }

    private static int ComputeSamplingParameter(int samplingPercent) => samplingPercent <= 0 ? 0 : (int)Math.Round(100m / samplingPercent);
}
