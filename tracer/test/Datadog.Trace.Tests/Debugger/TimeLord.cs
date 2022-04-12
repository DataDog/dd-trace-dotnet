// <copyright file="TimeLord.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions.Common;
using IClock = Datadog.Trace.Util.IClock;

namespace Datadog.Trace.Tests.Debugger;

public class TimeLord : IClock
{
    private static DateTime? _now;

    public DateTime UtcNow => _now ?? DateTime.UtcNow;

    public void TravelTo(DateTime dateTime)
    {
        _now = dateTime;
    }

    public void StopTime()
    {
        TravelTo(UtcNow);
    }

    public void BackToNormal()
    {
        _now = null;
    }
}
