// <copyright file="ProbeExpressionsBucket.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Threading;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.Debugger.Expressions;

internal sealed class ProbeExpressionsBucket
{
    private readonly IDatadogLogger _log;
    private readonly object _lock = new();
    private ProbeExpressionsCacheEntry[] _entries = Array.Empty<ProbeExpressionsCacheEntry>();
    private ProbeExpressionsCacheEntry? _last;

    public ProbeExpressionsBucket(IDatadogLogger log)
    {
        _log = log;
    }

    public bool TryGetFirstEntry(out ProbeExpressionsCacheEntry entry)
    {
        var entries = Volatile.Read(ref _entries);
        if (entries.Length > 0)
        {
            entry = entries[0];
            return true;
        }

        entry = null!;
        return false;
    }

    public ProbeExpressionsCacheEntry GetOrAdd(
        MethodScopeMembers scopeMembers,
        int memberCount)
    {
        var members = scopeMembers.Members;

        // Fastest path: monomorphic calls (best-effort hint, must still validate)
        var last = Volatile.Read(ref _last);
        if (last is not null && last.Matches(members, memberCount))
        {
            return last;
        }

        // Fast path: scan without allocating
        var snapshot = Volatile.Read(ref _entries);
        for (int i = 0; i < snapshot.Length; i++)
        {
            if (snapshot[i].Matches(members, memberCount))
            {
                return snapshot[i];
            }
        }

        lock (_lock)
        {
            // Re-check after taking lock
            snapshot = Volatile.Read(ref _entries);
            for (int i = 0; i < snapshot.Length; i++)
            {
                if (snapshot[i].Matches(members, memberCount))
                {
                    Volatile.Write(ref _last, snapshot[i]);
                    return snapshot[i];
                }
            }

            // Slow path: allocate member runtime type array ONCE for the miss, then publish an entry.
            var runtimeTypes = memberCount == 0 ? Array.Empty<Type?>() : new Type?[memberCount];
            for (int i = 0; i < memberCount; i++)
            {
                runtimeTypes[i] = members[i].Value?.GetType() ?? members[i].Type;
            }

            if (_log.IsEnabled(LogEventLevel.Debug))
            {
                _log.Debug(
                    "Probe expression cache MISS. Compiling for ThisType={ThisType}, ReturnType={ReturnType}, MemberCount={MemberCount}, BucketSize={BucketSize}",
                    property0: scopeMembers.InvocationTarget.Value?.GetType().FullName ?? scopeMembers.InvocationTarget.Type?.FullName,
                    property1: scopeMembers.Return.Value?.GetType().FullName ?? scopeMembers.Return.Type?.FullName,
                    property2: memberCount,
                    property3: snapshot.Length);
            }

            var entry = new ProbeExpressionsCacheEntry(runtimeTypes);

            var newEntries = new ProbeExpressionsCacheEntry[snapshot.Length + 1];
            Array.Copy(snapshot, newEntries, snapshot.Length);
            newEntries[snapshot.Length] = entry;
            Volatile.Write(ref _entries, newEntries);
            Volatile.Write(ref _last, entry);
            return entry;
        }
    }
}
