// <copyright file="AsyncCountdownEvent.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable
using System.Threading;
using System.Threading.Tasks;

namespace Datadog.Trace.Util;

// Based on: https://github.com/StephenCleary/AsyncEx/blob/master/src/Nito.AsyncEx.Coordination/AsyncCountdownEvent.cs

internal class AsyncCountdownEvent
{
    private readonly AsyncManualResetEvent _mre;
    private long _count;

    public AsyncCountdownEvent(long count)
    {
        _mre = new AsyncManualResetEvent(count == 0);
        _count = count;
    }

    public long CurrentCount
    {
        get
        {
            lock (_mre)
            {
                return _count;
            }
        }
    }

    public Task WaitAsync()
    {
        return _mre.WaitAsync();
    }

    public Task WaitAsync(CancellationToken cancellationToken)
    {
        return _mre.WaitAsync(cancellationToken);
    }

    private void ModifyCount(long difference, bool add)
    {
        if (difference == 0)
        {
            return;
        }

        lock (_mre)
        {
            var oldCount = _count;
            checked
            {
                if (add)
                {
                    _count += difference;
                }
                else
                {
                    _count -= difference;
                }
            }

            if (oldCount == 0)
            {
                _mre.Reset();
            }
            else if (_count == 0)
            {
                _mre.Set();
            }
            else if ((oldCount < 0 && _count > 0) || (oldCount > 0 && _count < 0))
            {
                _mre.Set();
                _mre.Reset();
            }
        }
    }

    public void AddCount(long addCount)
    {
        ModifyCount(addCount, true);
    }

    public void AddCount()
    {
        AddCount(1);
    }

    public void Signal(long signalCount)
    {
        ModifyCount(signalCount, false);
    }

    public void Signal()
    {
        Signal(1);
    }
}
