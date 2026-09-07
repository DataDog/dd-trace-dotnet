// <copyright file="ActivityTagStorageEmitterTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using System.Reflection;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Activity;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Activity;

/// <summary>
/// Exercises <see cref="ActivityTagStorageEmitter{TTarget}"/> against the real, installed
/// <see cref="System.Diagnostics.Activity"/> for whichever DiagnosticSource is actually loaded by this
/// TFM's runtime — not a fake. On DS 9+ runtimes (net9.0+) this proves the synthesised
/// <c>DiagNode</c>/<c>Enumerator</c> chain really works; on older runtimes (net6.0/net8.0) it proves the
/// "degrade rather than throw" contract the plan requires, since <c>EnumerateTagObjects</c> genuinely
/// doesn't exist there.
/// </summary>
public class ActivityTagStorageEmitterTests
{
    private static bool RuntimeSupportsEnumerateTagObjects =>
        typeof(System.Diagnostics.Activity).GetMethod("EnumerateTagObjects", BindingFlags.Public | BindingFlags.Instance) is not null;

    [Fact]
    public void IsAvailable_MatchesWhetherTheRuntimeActuallyExposesEnumerateTagObjects()
    {
        ActivityTagStorageEmitter<System.Diagnostics.Activity>.IsAvailable.Should().Be(RuntimeSupportsEnumerateTagObjects);
    }

    // Activity.Enumerator<T> only exists as a compile-time-referenceable nested type from DS 9.0 (net9.0+)
    // onward, so these two tests can't even compile pre-net9.0 — RuntimeSupportsEnumerateTagObjects already
    // covers the older-runtime "degrade rather than throw" contract via IsAvailable_... above.
#if NET9_0_OR_GREATER
    [SkippableFact]
    public void TryEnumerate_RoundTripsTagsThroughARealEnumerator()
    {
        Skip.If(!RuntimeSupportsEnumerateTagObjects, "EnumerateTagObjects only exists on DiagnosticSource 9.0+");

        var tags = new List<KeyValuePair<string, object?>>
        {
            new("string.tag", "value"),
            new("numeric.tag", 42d),
            new("bool.tag", "true"),
        };

        var found = ActivityTagStorageEmitter<System.Diagnostics.Activity>.TryEnumerate<System.Diagnostics.Activity.Enumerator<KeyValuePair<string, object?>>>(tags, out var enumerator);
        found.Should().BeTrue();

        var collected = new List<KeyValuePair<string, object?>>();
        while (enumerator.MoveNext())
        {
            collected.Add(enumerator.Current);
        }

        collected.Should().BeEquivalentTo(tags, options => options.WithStrictOrdering());
    }

    [SkippableFact]
    public void TryEnumerate_EmptyList_ProducesAnEnumeratorThatYieldsNothing()
    {
        Skip.If(!RuntimeSupportsEnumerateTagObjects, "EnumerateTagObjects only exists on DiagnosticSource 9.0+");

        var found = ActivityTagStorageEmitter<System.Diagnostics.Activity>.TryEnumerate<System.Diagnostics.Activity.Enumerator<KeyValuePair<string, object?>>>(new List<KeyValuePair<string, object?>>(), out var enumerator);
        found.Should().BeTrue();

        enumerator.MoveNext().Should().BeFalse();
    }
#endif

    [Fact]
    public void TryEnumerate_MismatchedReturnType_DegradesInsteadOfThrowing()
    {
        // TReturn is bound by the CallTarget rewrite in production and can never actually mismatch, but
        // this is the safety check that stops a wrong cast from happening if it ever did.
        var tags = new List<KeyValuePair<string, object?>> { new("a", "b") };

        var found = ActivityTagStorageEmitter<System.Diagnostics.Activity>.TryEnumerate<int>(tags, out var result);

        found.Should().BeFalse();
        result.Should().Be(0);
    }
}
