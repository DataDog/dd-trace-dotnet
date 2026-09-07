// <copyright file="FirstChanceExceptionCounter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;

namespace Datadog.Trace.DuckTyping.Tests;

/// <summary>
/// Records first-chance exceptions raised on the thread that created it.
/// </summary>
internal sealed class FirstChanceExceptionCounter : IDisposable
{
    private readonly int _threadId;
    private readonly List<Exception> _exceptions = [];

    public FirstChanceExceptionCounter()
    {
        _threadId = Environment.CurrentManagedThreadId;
        AppDomain.CurrentDomain.FirstChanceException += OnFirstChanceException;
    }

    public List<Exception> Exceptions
    {
        get
        {
            lock (_exceptions)
            {
                return [.._exceptions];
            }
        }
    }

    public void Dispose()
    {
        AppDomain.CurrentDomain.FirstChanceException -= OnFirstChanceException;
    }

    private void OnFirstChanceException(object sender, FirstChanceExceptionEventArgs e)
    {
        // Keep this handler as simple as possible: it runs during the first pass of SEH for every
        // exception in the AppDomain, and anything that throws in here would be a nightmare to debug.
        if (Environment.CurrentManagedThreadId != _threadId || e?.Exception is null)
        {
            return;
        }

        lock (_exceptions)
        {
            _exceptions.Add(e.Exception);
        }
    }
}
