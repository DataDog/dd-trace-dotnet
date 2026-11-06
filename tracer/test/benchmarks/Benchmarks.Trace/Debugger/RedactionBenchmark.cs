// <copyright file="RedactionBenchmark.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Security;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Datadog.Trace.Debugger.Snapshots;

namespace Benchmarks.Trace.Debugger;

/// <summary>
/// Measures <see cref="Redaction"/> lookups, which run for every captured variable, field and dictionary key.
/// Local only: not part of the CI benchmark list.
/// </summary>
[MemoryDiagnoser]
public class RedactionBenchmark
{
    private const int NamesCount = 8;
    private const int ParallelWorkers = 8;
    private const int CallsPerWorker = 1000;

    // More distinct keys than any redaction cache holds, so every lookup misses and the cache keeps resetting.
    private const int ChurnKeysCount = 4096;

    private Redaction? _redaction;
    private string[] _names = [];
    private Type[] _types = [];
    private string[] _churnKeys = [];

    [GlobalSetup]
    public void GlobalSetup()
    {
        _redaction = Redaction.Instance;
        _names = ["password", "userName", "Order_Id", "x-api-key", "count", "customerEmail", "_internalState", "SessionId"];
        _types = [typeof(string), typeof(int), typeof(List<string>), typeof(Guid?), typeof(DateTime), typeof(SecureString), typeof(Dictionary<string, object>), typeof(long)];
        if (_names.Length != NamesCount || _types.Length != NamesCount)
        {
            throw new InvalidOperationException($"Expected {NamesCount} names and types");
        }

        // Mixed case and separators, like typical data keys, so each miss also normalizes.
        _churnKeys = new string[ChurnKeysCount];
        for (var i = 0; i < ChurnKeysCount; i++)
        {
            _churnKeys[i] = "Order_" + i;
        }
    }

    [Benchmark(OperationsPerInvoke = NamesCount)]
    public int ShouldRedactHit()
    {
        var redaction = _redaction!;
        var names = _names;
        var types = _types;
        var redacted = 0;
        for (var i = 0; i < names.Length; i++)
        {
            if (redaction.ShouldRedact(names[i], types[i], out _))
            {
                redacted++;
            }
        }

        return redacted;
    }

    [Benchmark(OperationsPerInvoke = ChurnKeysCount)]
    public int ShouldRedactDataKeyMiss()
    {
        var redaction = _redaction!;
        var keys = _churnKeys;
        var redacted = 0;
        for (var i = 0; i < keys.Length; i++)
        {
            if (redaction.ShouldRedactDataKey(keys[i], typeof(string), out _))
            {
                redacted++;
            }
        }

        return redacted;
    }

    [Benchmark(OperationsPerInvoke = ParallelWorkers * CallsPerWorker * NamesCount)]
    public void ShouldRedactHitParallel()
    {
        Parallel.For(
            0,
            ParallelWorkers,
            _ =>
            {
                for (var i = 0; i < CallsPerWorker; i++)
                {
                    ShouldRedactHit();
                }
            });
    }
}
