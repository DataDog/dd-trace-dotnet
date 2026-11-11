// <copyright file="FlagSelectionTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.FeatureFlags.Agentless;
using Datadog.Trace.Logging;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using FluentAssertions;
using Moq;
using Xunit;

using DroppedReason = Datadog.Trace.Telemetry.Metrics.MetricTags.FlagSelectionDroppedReason;
using IgnoredReason = Datadog.Trace.Telemetry.Metrics.MetricTags.FlagSelectionIgnoredReason;

namespace Datadog.Trace.Tests.FeatureFlags;

public class FlagSelectionTests
{
    private static readonly JArray VectorCases = (JArray)JObject.Parse(
        ResourceHelper.ReadAllText<FlagSelectionTests>("ffe_system_test_data.flag_selection.selection-vectors.json"))["cases"]!;

    // The rawQuery cases exercise the server's percent-decoding of a hand-written query, which an
    // SDK never produces, so only the cases with an sdk expectation run here.
    public static IEnumerable<object[]> SdkVectorCases()
        => VectorCases.Where(c => c["sdk"] is not null).Select(c => new object[] { (string)c["name"]! });

    [Fact]
    public void RunsEverySdkVectorAndSkipsOnlyTheRawQueryCases()
    {
        // The snapshot at e8777633 has 38 sdk cases; a later refresh can only add to them.
        VectorCases.Count(c => c["sdk"] is not null).Should().BeGreaterThanOrEqualTo(38);
        VectorCases.Where(c => c["sdk"] is null).Should().OnlyContain(c => c["rawQuery"] != null);
    }

    [Theory]
    [MemberData(nameof(SdkVectorCases))]
    public void MatchesTheSharedVector(string name)
    {
        var vector = VectorCases.Single(c => (string)c["name"]! == name);
        var query = vector["query"] as JObject;
        var selection = FlagSelection.Create(
            query?["selected_flag_tags"]?.ToObject<string[]>(),
            query?["selected_flag_key_patterns"]?.ToObject<string[]>());

        var sdk = vector["sdk"]!;
        if (sdk["dropped"] is { } dropped)
        {
            selection.DropReason.Should().Be(ToDroppedReason((string)dropped!));
            selection.HasValues.Should().BeFalse();
            return;
        }

        var expectedTags = sdk["selection"]!["tags"]!.ToObject<string[]>()!;
        var expectedKeyPatterns = sdk["selection"]!["keyPatterns"]!.ToObject<string[]>()!;
        selection.DropReason.Should().BeNull();
        selection.Tags.Should().Equal(expectedTags);
        selection.KeyPatterns.Should().Equal(expectedKeyPatterns);
        selection.HasValues.Should().Be(expectedTags.Length + expectedKeyPatterns.Length > 0);
    }

    [Theory]
    [CombinatorialData]
    public void ANullElementIsMalformed(bool inTags)
    {
        string?[] values = ["mobile", null];
        var selection = inTags ? FlagSelection.Create(values, null) : FlagSelection.Create(null, values);

        selection.DropReason.Should().Be(DroppedReason.MalformedSelection);
        selection.HasValues.Should().BeFalse();
        selection.IsSet.Should().BeTrue();
    }

    [Theory]
    // Code units rather than strings: test discovery serializes string data as UTF-8, which replaces
    // an unpaired surrogate with U+FFFD.
    [InlineData(new[] { 0xD83D })] // a high surrogate alone
    [InlineData(new[] { 0xDE00 })] // a low surrogate alone
    [InlineData(new[] { 0x61, 0xDE00, 0xD83D, 0x62 })] // a pair in the wrong order
    [InlineData(new[] { 0xD83D, 0x78 })] // a high surrogate followed by an ordinary character
    public void AnUnpairedSurrogateIsMalformed(int[] codeUnits)
    {
        var value = new string(codeUnits.Select(c => (char)c).ToArray());

        FlagSelection.Create(null, [value]).DropReason.Should().Be(DroppedReason.MalformedSelection);
        FlagSelection.Create([value], null).DropReason.Should().Be(DroppedReason.MalformedSelection);
    }

    [Fact]
    public void MalformedTakesPrecedenceOverTheLimits()
    {
        var tooMany = Enumerable.Range(0, 40).Select(i => $"tag-{i}").ToList<string?>();
        tooMany.Add(null);

        FlagSelection.Create(tooMany.ToArray(), [new string('x', 300)]).DropReason.Should().Be(DroppedReason.MalformedSelection);
    }

    [Fact]
    public void ASurrogatePairIsNotMalformed()
        => FlagSelection.Create(["\U0001F600"], null).Tags.Should().Equal("\U0001F600");

    [Theory]
    [InlineData('\u0009')]
    [InlineData('\u000A')]
    [InlineData('\u000B')]
    [InlineData('\u000C')]
    [InlineData('\u000D')]
    [InlineData(' ')]
    [InlineData('\u0085')]
    [InlineData('\u00A0')]
    [InlineData('\u1680')]
    [InlineData('\u2000')]
    [InlineData('\u2005')]
    [InlineData('\u200A')]
    [InlineData('\u2028')]
    [InlineData('\u2029')]
    [InlineData('\u202F')]
    [InlineData('\u205F')]
    [InlineData('\u3000')]
    public void TrimsEveryUnicodeWhiteSpaceCharacter(char whiteSpace)
        => FlagSelection.Create([$"{whiteSpace}mobile{whiteSpace}"], null).Tags.Should().Equal("mobile");

    [Theory]
    // U+180E was White_Space before Unicode 6.3, and char.IsWhiteSpace still follows the runtime's own
    // Unicode tables, so a runtime-dependent trim would disagree with the server on it.
    [InlineData('\u180E')]
    [InlineData('\u200B')]
    [InlineData('\uFEFF')]
    public void KeepsCharactersOutsideUnicodeWhiteSpace(char character)
        => FlagSelection.Create([$"{character}mobile{character}"], null).Tags.Should().Equal($"{character}mobile{character}");

    [Fact]
    public void SortsByCodePointRatherThanUtf16CodeUnit()
    {
        // string.CompareOrdinal puts U+1F600 (a surrogate pair, 0xD83D...) before U+E000 and U+FF5E,
        // while their UTF-8 bytes order it last.
        var selection = FlagSelection.Create(["\U0001F600", "\uFF5E", "\uE000", "z", "A"], null);

        selection.Tags.Should().Equal("A", "z", "\uE000", "\uFF5E", "\U0001F600");
    }

    [Fact]
    public void SortsEachListIndependently()
    {
        var selection = FlagSelection.Create(["b", "a"], ["d", "c"]);

        selection.Tags.Should().Equal("a", "b");
        selection.KeyPatterns.Should().Equal("c", "d");
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(new string[0], new string[0])]
    [InlineData(new[] { "", " \u3000 " }, new[] { "\t" })]
    public void AnEmptySelectionMeansEveryFlag(string[]? tags, string[]? keyPatterns)
    {
        var selection = FlagSelection.Create(tags, keyPatterns);

        selection.IsSet.Should().BeFalse();
        selection.HasValues.Should().BeFalse();
        selection.DropReason.Should().BeNull();
        selection.Should().Be(FlagSelection.AllFlags);
    }

    [Fact]
    public void ComparesSelectionsInCanonicalForm()
    {
        FlagSelection.Create([" b", "a", "a", string.Empty], ["k*"]).Should().Be(FlagSelection.Create(["a", "b"], ["k*"]));
        FlagSelection.Create(["a"], null).Should().NotBe(FlagSelection.Create(null, ["a"]));
        FlagSelection.Create(["a"], null).Should().NotBe(FlagSelection.AllFlags);

        var tooMany = Enumerable.Range(0, 32).Select(i => $"t{i}").ToArray();
        FlagSelection.Create(tooMany, null).Should().Be(FlagSelection.Create(Enumerable.Reverse(tooMany).ToArray(), null));
        FlagSelection.Create(tooMany, null).Should().NotBe(FlagSelection.Create(tooMany.Take(31).ToArray(), null));
    }

    [Theory]
    [InlineData("mobile", "mobile")]
    [InlineData("-._~", "-._~")]
    [InlineData("AZaz09", "AZaz09")]
    [InlineData("region:eu", "region%3Aeu")]
    [InlineData("a b", "a%20b")]
    [InlineData("100%", "100%25")]
    [InlineData("a+b", "a%2Bb")]
    [InlineData("a&b=c", "a%26b%3Dc")]
    [InlineData("team/a?b#c", "team%2Fa%3Fb%23c")]
    // Encoded although RFC 2396 and some runtime escapers treat them as unreserved.
    [InlineData("!'()*", "%21%27%28%29%2A")]
    [InlineData("caf\u00E9", "caf%C3%A9")]
    [InlineData("\U0001F600", "%F0%9F%98%80")]
    [InlineData("\uFEFF", "%EF%BB%BF")]
    public void PercentEncodesEveryByteOutsideTheUnreservedSetWithUppercaseHex(string value, string expected)
        => FlagSelection.Create([value], null).ToQuery().Should().Be("selected_flag_tags=" + expected);

    [Fact]
    public void RepeatsEachParameterWithTagsFirst()
        => FlagSelection.Create(["mobile", "beta"], ["checkout.*"]).ToQuery()
                        .Should().Be("selected_flag_tags=beta&selected_flag_tags=mobile&selected_flag_key_patterns=checkout.%2A");

    [Theory]
    [InlineData("malformed_selection", "malformed_selection")]
    [InlineData("too_many_values", "too_many_values")]
    [InlineData("invalid_key_pattern", "invalid_key_pattern")]
    [InlineData("invalid_tag", "invalid_tag")]
    [InlineData("tag_load_failed", "tag_load_failed")]
    [InlineData("unexpected", "unknown")]
    [InlineData("too_many_values ", "unknown")]
    [InlineData(" too_many_values", "unknown")]
    [InlineData("TOO_MANY_VALUES", "unknown")]
    [InlineData("too_many_values, invalid_tag", "unknown")]
    [InlineData("too_many_values_and_more_than_32_bytes", "unknown")]
    public void KeepsAnIgnoredReasonOnlyWhenItIsKnownByteForByte(string header, string expected)
        => FlagSelectionDiagnostics.ParseIgnoredReason(header).Should().Be(ToIgnoredReason(expected));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void AnEmptyIgnoredHeaderIsAbsent(string? header)
        => FlagSelectionDiagnostics.ParseIgnoredReason(header).Should().BeNull();

    [Theory]
    [InlineData("malformed_selection", "FlagSelection was not sent (malformed_selection: DatadogProviderOptions.FlagSelection must be an object with optional Tags and KeyPatterns string arrays). Loading the full flag configuration.")]
    [InlineData("too_many_values", "FlagSelection was not sent (too_many_values: each list accepts at most 31 values). Loading the full flag configuration.")]
    [InlineData("invalid_key_pattern", "FlagSelection was not sent (invalid_key_pattern: each key pattern is at most 200 UTF-8 bytes). Loading the full flag configuration.")]
    [InlineData("invalid_tag", "FlagSelection was not sent (invalid_tag: each tag is at most 200 UTF-8 bytes). Loading the full flag configuration.")]
    public void NamesTheReasonAndTheLimitWhenASelectionIsNotSent(string reason, string expected)
    {
        var log = new Mock<IDatadogLogger>();

        NewDiagnostics().ReportDropped(log.Object, ToDroppedReason(reason));

        Errors(log).Should().Equal(expected);
    }

    [Fact]
    public void NamesTheUrlLimitWhenTheRequestWouldBeTooLong()
    {
        var log = new Mock<IDatadogLogger>();

        NewDiagnostics().ReportDroppedForUrlLimit(log.Object);

        Errors(log).Should().Equal("FlagSelection was not sent (too_many_values: the request URL is at most 8192 bytes). Loading the full flag configuration.");
    }

    [Theory]
    [InlineData("malformed_selection")]
    [InlineData("too_many_values")]
    [InlineData("invalid_key_pattern")]
    [InlineData("invalid_tag")]
    [InlineData("tag_load_failed")]
    [InlineData("unknown")]
    public void NamesTheReasonWhenTheServerIgnoresASelection(string name)
    {
        var log = new Mock<IDatadogLogger>();

        NewDiagnostics().ReportIgnored(log.Object, ToIgnoredReason(name));

        Errors(log).Should().Equal($"FlagSelection was ignored by the server ({name}). Loaded the full flag configuration.");
    }

    [Fact]
    public void LogsEveryDropButCountsEachReasonOncePerProcess()
    {
        var metrics = new Mock<IMetricsTelemetryCollector>();
        var diagnostics = new FlagSelectionDiagnostics(metrics.Object);
        var log = new Mock<IDatadogLogger>();

        diagnostics.ReportDropped(log.Object, DroppedReason.InvalidTag);
        diagnostics.ReportDropped(log.Object, DroppedReason.InvalidTag);
        diagnostics.ReportDroppedForUrlLimit(log.Object);
        diagnostics.ReportDropped(log.Object, DroppedReason.TooManyValues);

        Errors(log).Should().Equal(
            FlagSelectionDiagnostics.InvalidTagNotSent,
            FlagSelectionDiagnostics.InvalidTagNotSent,
            FlagSelectionDiagnostics.UrlLimitNotSent,
            FlagSelectionDiagnostics.TooManyValuesNotSent);
        metrics.Verify(m => m.RecordCountFlagSelectionDropped(DroppedReason.InvalidTag, 1), Times.Once);
        metrics.Verify(m => m.RecordCountFlagSelectionDropped(DroppedReason.TooManyValues, 1), Times.Once);
        metrics.Verify(m => m.RecordCountFlagSelectionDropped(It.IsAny<DroppedReason>(), It.IsAny<int>()), Times.Exactly(2));
    }

    [Fact]
    public void ReportsEachIgnoredReasonOncePerProcess()
    {
        var metrics = new Mock<IMetricsTelemetryCollector>();
        var diagnostics = new FlagSelectionDiagnostics(metrics.Object);
        var log = new Mock<IDatadogLogger>();

        diagnostics.ReportIgnored(log.Object, IgnoredReason.TagLoadFailed);
        diagnostics.ReportIgnored(log.Object, IgnoredReason.TagLoadFailed);
        diagnostics.ReportIgnored(log.Object, IgnoredReason.Unknown);
        diagnostics.ReportIgnored(new Mock<IDatadogLogger>().Object, IgnoredReason.Unknown);

        Errors(log).Should().Equal(FlagSelectionDiagnostics.TagLoadFailedIgnored, FlagSelectionDiagnostics.UnknownIgnored);
        metrics.Verify(m => m.RecordCountFlagSelectionIgnored(IgnoredReason.TagLoadFailed, 1), Times.Once);
        metrics.Verify(m => m.RecordCountFlagSelectionIgnored(IgnoredReason.Unknown, 1), Times.Once);
    }

    [Fact]
    public void WarnsOncePerProcess()
    {
        var diagnostics = new FlagSelectionDiagnostics(new Mock<IMetricsTelemetryCollector>().Object);
        var log = new Mock<IDatadogLogger>();

        diagnostics.WarnRemoteConfig(log.Object);
        diagnostics.WarnRemoteConfig(log.Object);
        diagnostics.WarnDifferentSelection(log.Object);
        diagnostics.WarnDifferentSelection(log.Object);

        Warnings(log).Should().Equal(
            "FlagSelection is not supported when flags are delivered through Remote Config. Loading the full flag configuration.",
            "FlagSelection differs from the flag selection already in use in this process. Using the first selection.");
    }

    internal static List<string> Errors(Mock<IDatadogLogger> log) => Messages(log, nameof(IDatadogLogger.ErrorSkipTelemetry));

    internal static List<string> Warnings(Mock<IDatadogLogger> log) => Messages(log, nameof(IDatadogLogger.Warning));

    private static FlagSelectionDiagnostics NewDiagnostics() => new(new Mock<IMetricsTelemetryCollector>().Object);

    private static List<string> Messages(Mock<IDatadogLogger> log, string method)
        => log.Invocations
              .Where(i => i.Method.Name == method)
              .Select(i => (string)i.Arguments[i.Method.GetParameters().ToList().FindIndex(p => p.Name == "messageTemplate")])
              .Where(m => m.StartsWith("FlagSelection ", StringComparison.Ordinal))
              .ToList();

    private static DroppedReason ToDroppedReason(string reason) => reason switch
    {
        "malformed_selection" => DroppedReason.MalformedSelection,
        "too_many_values" => DroppedReason.TooManyValues,
        "invalid_key_pattern" => DroppedReason.InvalidKeyPattern,
        "invalid_tag" => DroppedReason.InvalidTag,
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "not a reason an SDK drops a selection for"),
    };

    private static IgnoredReason ToIgnoredReason(string reason) => reason switch
    {
        "malformed_selection" => IgnoredReason.MalformedSelection,
        "too_many_values" => IgnoredReason.TooManyValues,
        "invalid_key_pattern" => IgnoredReason.InvalidKeyPattern,
        "invalid_tag" => IgnoredReason.InvalidTag,
        "tag_load_failed" => IgnoredReason.TagLoadFailed,
        "unknown" => IgnoredReason.Unknown,
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "not a sanitized ignored reason"),
    };
}
