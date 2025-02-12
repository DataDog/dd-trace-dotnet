// <copyright file="IDogStatsd.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Vendors.StatsdClient;

internal interface IDogStatsd
{
    void Counter(string statName, double value, double sampleRate = 1, string[] tags = null);

    void Increment(string statName, int value = 1, double sampleRate = 1, string[] tags = null);

    void Gauge(string statName, double value, double sampleRate = 1, string[] tags = null);

    public void Dispose();
}
