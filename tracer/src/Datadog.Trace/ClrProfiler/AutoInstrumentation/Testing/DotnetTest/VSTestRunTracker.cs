// <copyright file="VSTestRunTracker.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Runtime.CompilerServices;
using Datadog.Trace.Ci;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.DotnetTest;

internal static class VSTestRunTracker
{
    private static readonly ConditionalWeakTable<TestSession, SessionState> Sessions = new();
    private static readonly ConditionalWeakTable<object, SessionState> Requests = new();

    internal static void Start(object request, TestSession? session)
    {
        if (session is null)
        {
            return;
        }

        lock (Requests)
        {
            if (!Requests.TryGetValue(request, out _))
            {
                var state = Sessions.GetValue(session, static _ => new SessionState());
                Requests.Add(request, state);
                state.Pending++;
            }
        }
    }

    internal static void Complete(object request, bool empty)
    {
        lock (Requests)
        {
            if (Requests.TryGetValue(request, out var state))
            {
                Requests.Remove(request);
                state.Pending--;
                state.Completed++;
                state.AllEmpty &= empty;
            }
        }
    }

    internal static bool IsEmpty(TestSession session)
    {
        lock (Requests)
        {
            return Sessions.TryGetValue(session, out var state)
                && state.Completed > 0
                && state.Pending == 0
                && state.AllEmpty;
        }
    }

    private sealed class SessionState
    {
        public int Pending { get; set; }

        public int Completed { get; set; }

        public bool AllEmpty { get; set; } = true;
    }
}
