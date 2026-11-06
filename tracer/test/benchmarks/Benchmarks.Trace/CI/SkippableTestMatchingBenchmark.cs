// <copyright file="SkippableTestMatchingBenchmark.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Globalization;
using System.Reflection;
using BenchmarkDotNet.Attributes;
using Datadog.Trace.Ci;

namespace Benchmarks.Trace;

[MemoryDiagnoser]
[BenchmarkCategory(Constants.TracerCategory)]
public class SkippableTestMatchingBenchmark
{
    private static readonly ParameterInfo[] Parameters = ((Action<string>)Sample).Method.GetParameters();
    private SkippableTest[] _candidates;
    private SkippableTestCollection _tests;
    private object[] _existingArguments;
    private object[] _newArguments;

    [Params(1, 100, 1000, 10000)]
    public int CandidateCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _candidates = new SkippableTest[CandidateCount];
        for (var i = 0; i < CandidateCount; i++)
        {
            _candidates[i] = new SkippableTest("Sample", "Suite", "{\"arguments\":{\"value\":\"" + i.ToString(CultureInfo.InvariantCulture) + "\"}}", configurations: null);
        }

        _tests = new SkippableTestCollection(_candidates).ForModule("Module");
        _existingArguments = [(CandidateCount - 1).ToString(CultureInfo.InvariantCulture)];
        _newArguments = ["new-parameter"];
    }

    [Benchmark]
    public bool ExistingParameters() => Match(_existingArguments);

    [Benchmark]
    public bool NewParameters() => Match(_newArguments);

    [Benchmark]
    public int BuildIndex() => new SkippableTestCollection(_candidates).Count;

    private static void Sample(string value)
    {
    }

    private bool Match(object[] arguments)
    {
        var query = new SkippableTestCollection.Query(arguments, Parameters, metadataTestName: null, includeMetadataTestNameInFingerprint: true, useParameterIndexForUnnamedParameters: false);
        try
        {
            return _tests.TryMatch(ref query, out _);
        }
        finally
        {
            query.Dispose();
        }
    }
}
