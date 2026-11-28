// <copyright file="RequestDeadline.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading;

namespace Datadog.Trace.Agent.Transports;

internal static class RequestDeadline
{
    internal static CancellationTokenSource Create(TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (!cancellationToken.CanBeCanceled)
        {
            return new CancellationTokenSource(timeout);
        }

        var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, CancellationToken.None);
        deadline.CancelAfter(timeout);
        return deadline;
    }
}
