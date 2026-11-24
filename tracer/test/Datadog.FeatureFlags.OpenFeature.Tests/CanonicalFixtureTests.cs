// <copyright file="CanonicalFixtureTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

extern alias Tracer;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.FeatureFlags;
using Datadog.Trace.FeatureFlags.FlagEvaluation;
using FluentAssertions;
using Moq;
using OpenFeature.Constant;
using OpenFeature.Model;
using Tracer::Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using Xunit;
using Json = Tracer::Datadog.Trace.Vendors.Newtonsoft.Json;
using JsonLinq = Tracer::Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using TracerFlagEvaluation = Tracer::Datadog.Trace.FeatureFlags.FlagEvaluation;
using TracerFlags = Tracer::Datadog.Trace.FeatureFlags;
using TracerModel = Tracer::Datadog.Trace.FeatureFlags.Rcm.Model;

namespace Datadog.FeatureFlags.OpenFeature.Tests;

#pragma warning disable DDFF001 // Exercise the experimental public API with the canonical fixtures.

public class CanonicalFixtureTests
{
    private const string CasesPrefix = "Fixtures.evaluation-cases.";
    private const string DependentCasesPrefix = "Fixtures.evaluation-suites.dependent-flags.cases.";
    private const string DependentConfigResource = "Fixtures.evaluation-suites.dependent-flags.ufc-config.json";
    private static readonly TracerModel.ServerConfiguration Configuration =
        Json.JsonConvert.DeserializeObject<TracerModel.ServerConfiguration>(ReadResource("Fixtures.ufc-config.json"))!;

    public static IEnumerable<object[]> Cases()
    {
        var resources = typeof(CanonicalFixtureTests).Assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(CasesPrefix, StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        Assert.NotEmpty(resources);

        foreach (var resource in resources)
        {
            var cases = (JsonLinq.JArray)ParseFixture(ReadResource(resource));
            Assert.NotEmpty(cases);
            for (var index = 0; index < cases.Count; index++)
            {
                if (cases[index] is not JsonLinq.JObject testCase || !RunsOnDotnet(testCase))
                {
                    continue;
                }

                var description = $"{resource.Substring(CasesPrefix.Length)}[{index}]";
                var json = testCase.ToString(Json.Formatting.None);
                yield return [description, json, false];
                yield return [description, json, true];
            }
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task PublicProviderMethodsMatchCanonicalFixtures(string description, string json, bool useAsync)
    {
        Assert.False(string.IsNullOrEmpty(description));
        var testCase = (JsonLinq.JObject)ParseFixture(json);
        var evaluator = new TracerFlags.FeatureFlagsEvaluator(null, Configuration);
        using var provider = new DatadogProvider((key, type, fallback, targetingKey, attributes) =>
            new EvaluationAdapter(evaluator.Evaluate(
                key,
                (TracerFlags.ValueType)type,
                fallback,
                new TracerFlags.EvaluationContext(targetingKey, attributes))));

        var builder = EvaluationContext.Builder();
        foreach (var attribute in ((JsonLinq.JObject)testCase["attributes"]!).Properties())
        {
            builder.Set(attribute.Name, ToOpenFeatureValue(attribute.Value));
        }

        // An absent targeting key remains absent; do not replace it with an empty string.
        if (testCase["targetingKey"]?.Type == JsonLinq.JTokenType.String)
        {
            builder.SetTargetingKey(testCase["targetingKey"]!.Value<string>()!);
        }

        var context = builder.Build();
        var flag = testCase["flag"]!.Value<string>()!;
        var fallbackValue = testCase["defaultValue"]!;

        switch (testCase["variationType"]!.Value<string>())
        {
            case "BOOLEAN":
                var boolean = useAsync
                    ? await provider.ResolveBooleanValueAsync(flag, fallbackValue.Value<bool>(), context)
                    : provider.ResolveBooleanValue(flag, fallbackValue.Value<bool>(), context);
                AssertResolution(testCase, boolean);
                break;
            case "INTEGER":
                var integer = useAsync
                    ? await provider.ResolveIntegerValueAsync(flag, fallbackValue.Value<int>(), context)
                    : provider.ResolveIntegerValue(flag, fallbackValue.Value<int>(), context);
                AssertResolution(testCase, integer);
                break;
            case "NUMERIC":
                var numeric = useAsync
                    ? await provider.ResolveDoubleValueAsync(flag, fallbackValue.Value<double>(), context)
                    : provider.ResolveDoubleValue(flag, fallbackValue.Value<double>(), context);
                AssertResolution(testCase, numeric);
                break;
            case "STRING":
                var text = useAsync
                    ? await provider.ResolveStringValueAsync(flag, fallbackValue.Value<string>()!, context)
                    : provider.ResolveStringValue(flag, fallbackValue.Value<string>()!, context);
                AssertResolution(testCase, text);
                break;
            case "JSON":
                var structure = useAsync
                    ? await provider.ResolveStructureValueAsync(flag, ToOpenFeatureValue(fallbackValue), context)
                    : provider.ResolveStructureValue(flag, ToOpenFeatureValue(fallbackValue), context);
                AssertResolution(testCase, structure);
                break;
            default:
                throw new InvalidOperationException($"Unsupported fixture variationType: {testCase["variationType"]}");
        }
    }

    [Fact]
    public async Task PublicProviderMatchesDependentFlagCanonicalSuite()
    {
        var configuration = Json.JsonConvert.DeserializeObject<TracerModel.ServerConfiguration>(ReadResource(DependentConfigResource))!;
        var resources = typeof(CanonicalFixtureTests).Assembly.GetManifestResourceNames()
                                                        .Where(name => name.StartsWith(DependentCasesPrefix, StringComparison.Ordinal))
                                                        .OrderBy(name => name, StringComparer.Ordinal)
                                                        .ToArray();
        resources.Should().NotBeEmpty();
        var cases = resources.SelectMany(resource => ((JsonLinq.JArray)ParseFixture(ReadResource(resource))).Children())
                             .OfType<JsonLinq.JObject>()
                             .Where(RunsOnDotnet)
                             .ToArray();
        cases.Should().NotBeEmpty();
        foreach (var testCase in cases)
        {
            var domain = $"dependent-fixture-{Guid.NewGuid():N}";
            var payloads = new ConcurrentQueue<byte[]>();
            var writer = new TracerFlagEvaluation.FlagEvaluationWriter(
                bytes =>
                {
                    payloads.Enqueue(Decompress(bytes));
                    return Task.CompletedTask;
                },
                new Dictionary<string, string>());
            try
            {
                var evaluationHook = new FlagEvalEVPHook(
                    writer.CanAdmit,
                    (flagKey, variant, allocationKey, targetingKey, evaluationTimeMs, errorCode, attributes, flags) =>
                        EnqueueRoot(flagKey, variant, allocationKey, targetingKey, evaluationTimeMs, errorCode, attributes, features: null, flags),
                    () => { },
                    FlagEvaluationContextSnapshot.Capture,
                    writer.CanAdmit,
                    (flagKey, variant, allocationKey, targetingKey, evaluationTimeMs, errorCode, attributes, features, flags) =>
                        EnqueueRoot(flagKey, variant, allocationKey, targetingKey, evaluationTimeMs, errorCode, attributes, features, flags));
                using var module = new TracerFlags.FeatureFlagsModule(
                    new Tracer::Datadog.Trace.Configuration.TracerSettings(
                        new Tracer::Datadog.Trace.Configuration.NameValueConfigurationSource(new NameValueCollection())),
                    new Mock<Tracer::Datadog.Trace.RemoteConfigurationManagement.IRcmSubscriptionManager>().Object);
                module.SetEvaluationWriterForTesting(writer);
                module.ApplyConfiguration(configuration).Should().BeTrue();
                var context = EvaluationContext.Builder().SetTargetingKey(testCase["targetingKey"]!.Value<string>()!);
                foreach (var attribute in ((JsonLinq.JObject)testCase["attributes"]!).Properties())
                {
                    context.Set(attribute.Name, ToOpenFeatureValue(attribute.Value));
                }

                var commitCalls = 0;
                using var provider = new DatadogProvider(
                    (key, type, fallback, targetingKey, attributes) => throw new InvalidOperationException("the provider must use deferred graph evaluation"),
                    (key, type, fallback, targetingKey, attributes) => new EvaluationAdapter(
                        module.Evaluate(key, (TracerFlags.ValueType)type, fallback, targetingKey, attributes, deferExposure: true, reportRootEvaluation: false)),
                    () => true,
                    metadata =>
                    {
                        commitCalls++;
                        return module.CommitExposure(metadata);
                    },
                    metadata => (metadata as TracerFlags.DeferredExposureMetadata)?.DependencyTelemetry,
                    evaluationHook);
                await global::OpenFeature.Api.Instance.SetProviderAsync(domain, provider);
                var exposures = new List<JsonLinq.JObject>();
                var client = global::OpenFeature.Api.Instance.GetClient(domain);
                client.AddHooks(
                    new ExposureHook(evaluation =>
                    {
                        if (evaluation.IsExposure)
                        {
                            exposures.Add(
                                new JsonLinq.JObject
                                {
                                    ["flag"] = evaluation.FlagKey,
                                    ["allocation"] = evaluation.AllocationKey,
                                    ["variant"] = evaluation.Variant,
                                });
                        }
                    }));
                var resolution = await client.GetStringDetailsAsync(
                    testCase["flag"]!.Value<string>()!,
                    testCase["defaultValue"]!.Value<string>()!,
                    context.Build());

                AssertResolution<string>(testCase, resolution);
                await writer.FlushAsync();
                var evaluations = EvaluationEvents(payloads);
                commitCalls.Should().Be(
                    resolution.ErrorType == ErrorType.None && exposures.Count > 0 ? 1 : 0,
                    "only a successful complete graph with exposure candidates is committed");
                if (testCase["expectations"] is not JsonLinq.JObject expectations)
                {
                    continue;
                }

                AssertExpectedTelemetry(expectations, exposures, evaluations);
            }
            finally
            {
                await writer.CloseAsync(TimeSpan.FromSeconds(2));
            }

            void EnqueueRoot(
                string flagKey,
                string? variant,
                string? allocationKey,
                string? targetingKey,
                long evaluationTimeMs,
                string? errorCode,
                IReadOnlyDictionary<string, object?>? attributes,
                IReadOnlyDictionary<string, object?>? features,
                int flags)
            {
                var consent = (flags & FlagEvaluationBridge.ObserveFullEvaluationData) != 0;
                writer.TryEnqueue(
                    new TracerFlagEvaluation.FlagEvalEvent(
                        flagKey,
                        variant,
                        allocationKey,
                        targetingKey,
                        evaluationTimeMs,
                        attributes,
                        errorCode,
                        consent,
                        features),
                    flags & ~FlagEvaluationBridge.ObserveFullEvaluationData);
            }
        }
    }

    [Theory]
    [InlineData("{ '_skip': [{ 'platform': 'dotnet', 'reason': 'unsupported' }] }", false)]
    [InlineData("{ '_skip': [{ 'platform': 'java', 'reason': 'unsupported' }] }", true)]
    [InlineData("{ '_include': [{ 'platform': 'dotnet', 'reason': 'covered' }] }", true)]
    [InlineData("{ '_include': [{ 'platform': 'java', 'reason': 'covered' }] }", false)]
    [InlineData("{}", true)]
    public void PlatformFiltersUseSharedCorpusMetadata(string json, bool expected)
        => RunsOnDotnet((JsonLinq.JObject)ParseFixture(json.Replace('\'', '"'))).Should().Be(expected);

    [Theory]
    [InlineData("exposures")]
    [InlineData("evaluationEvents")]
    public void NoUnmatchedEventsTreatsAMissingCategoryAsEmpty(string missingCategory)
    {
        var expectations = new JsonLinq.JObject
        {
            [missingCategory == "exposures" ? "evaluationEvents" : "exposures"] = new JsonLinq.JArray(),
            ["noUnmatchedEvents"] = true,
        };
        JsonLinq.JObject[] exposures = missingCategory == "exposures" ? [Event("flag")] : [];
        JsonLinq.JObject[] evaluations = missingCategory == "evaluationEvents" ? [Event("flag")] : [];

        Action assertion = () => AssertExpectedTelemetry(expectations, exposures, evaluations);

        assertion.Should().Throw<Exception>();

        static JsonLinq.JObject Event(string flag) => new() { ["flag"] = flag };
    }

    private static bool RunsOnDotnet(JsonLinq.JObject testCase)
    {
        var skipped = testCase["_skip"] is JsonLinq.JArray skip
                   && skip.OfType<JsonLinq.JObject>().Any(item => item["platform"]?.Value<string>() == "dotnet");
        if (skipped)
        {
            return false;
        }

        return testCase["_include"] is not JsonLinq.JArray include
            || include.OfType<JsonLinq.JObject>().Any(item => item["platform"]?.Value<string>() == "dotnet");
    }

    private static void AssertExpectedTelemetry(JsonLinq.JObject expectations, IEnumerable<JsonLinq.JObject> exposures, IEnumerable<JsonLinq.JObject> evaluations)
    {
        var noUnmatchedEvents = expectations["noUnmatchedEvents"]?.Value<bool>() == true;
        AssertExpectedEvents((JsonLinq.JArray?)expectations["exposures"], exposures, noUnmatchedEvents);
        AssertExpectedEvents((JsonLinq.JArray?)expectations["evaluationEvents"], evaluations, noUnmatchedEvents);
    }

    private static void AssertExpectedEvents(JsonLinq.JArray? expectedEvents, IEnumerable<JsonLinq.JObject> actualEvents, bool noUnmatchedEvents)
    {
        if (expectedEvents is null && !noUnmatchedEvents)
        {
            return;
        }

        var remaining = actualEvents.ToList();
        foreach (var expectedToken in expectedEvents ?? new JsonLinq.JArray())
        {
            var expected = (JsonLinq.JObject)expectedToken;
            var count = expected["_count"]?.Value<int>() ?? 1;
            for (var occurrence = 0; occurrence < count; occurrence++)
            {
                var index = remaining.FindIndex(actual => expected.Properties()
                    .Where(property => property.Name != "_count")
                    .All(property => JsonLinq.JToken.DeepEquals(actual[property.Name], property.Value)));
                index.Should().BeGreaterThanOrEqualTo(0);
                remaining.RemoveAt(index);
            }
        }

        if (noUnmatchedEvents)
        {
            remaining.Should().BeEmpty();
        }
    }

    private static List<JsonLinq.JObject> EvaluationEvents(IEnumerable<byte[]> payloads)
    {
        var events = new List<JsonLinq.JObject>();
        foreach (var row in payloads.SelectMany(payload => (JsonLinq.JArray)JsonLinq.JObject.Parse(Encoding.UTF8.GetString(payload))["flagEvaluations"]!))
        {
            var count = row["evaluation_count"]!.Value<int>();
            for (var i = 0; i < count; i++)
            {
                events.Add(
                    new JsonLinq.JObject
                    {
                        ["flag"] = row["flag"]!["key"]!.Value<string>(),
                        ["errorCode"] = row["error"]?["message"]?.DeepClone() ?? JsonLinq.JValue.CreateNull(),
                    });
            }
        }

        return events;
    }

    private static byte[] Decompress(ArraySegment<byte> compressed)
    {
        using var input = new MemoryStream(compressed.Array!, compressed.Offset, compressed.Count);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        return output.ToArray();
    }

#pragma warning disable SA1202 // This pre-existing public fixture test remains grouped with public tests.
    [Fact]
    public async Task ProviderTargetingPreservesScalarsAfterLargeAndBudgetHeavyAttributes()
    {
        var flags = new TracerModel.FlagCollection
        {
            ["flag"] = new TracerModel.Flag
            {
                Key = "flag",
                Enabled = true,
                VariationType = TracerFlags.ValueType.String,
                Variations = new Dictionary<string, TracerModel.Variant>
                {
                    ["matched"] = new("matched", "matched"),
                    ["fallback"] = new("fallback", "fallback"),
                },
                Allocations =
                [
                    new TracerModel.Allocation
                    {
                        Key = "targeted",
                        Rules =
                        [
                            new TracerModel.Rule(
                            [
                                new TracerModel.ConditionConfiguration
                                {
                                    Attribute = "match",
                                    Operator = TracerModel.ConditionOperator.ONE_OF,
                                    Value = new[] { "paid" },
                                },
                            ]),
                        ],
                        Splits = [new TracerModel.Split { VariationKey = "matched", Shards = [] }],
                    },
                    new TracerModel.Allocation
                    {
                        Key = "default",
                        Splits = [new TracerModel.Split { VariationKey = "fallback", Shards = [] }],
                    },
                ],
            },
        };
        var evaluator = new TracerFlags.FeatureFlagsEvaluator(null, new TracerModel.ServerConfiguration { Flags = flags });
        IDictionary<string, object?>? captured = null;
        using var provider = new DatadogProvider((key, type, fallback, targetingKey, attributes) =>
        {
            captured = attributes;
            return new EvaluationAdapter(evaluator.Evaluate(key, (TracerFlags.ValueType)type, fallback, new TracerFlags.EvaluationContext(targetingKey, attributes)));
        });

        (EvaluationContext Context, int ExpectedCount)[] contexts =
        [
            (CreateContext(includeBudgetHeavyNestedValue: false), 301),
            (CreateContext(includeBudgetHeavyNestedValue: true), 302),
        ];
        foreach (var (context, expectedCount) in contexts)
        {
            var resolution = await provider.ResolveStringValueAsync("flag", "caller-default", context);

            resolution.Value.Should().Be("matched");
            captured!["match"].Should().Be("paid");
            captured.Count.Should().Be(expectedCount);
        }

        static EvaluationContext CreateContext(bool includeBudgetHeavyNestedValue)
        {
            var builder = EvaluationContext.Builder();
            for (var i = 0; i < 300; i++)
            {
                builder.Set("field" + i, i);
            }

            if (includeBudgetHeavyNestedValue)
            {
                var branches = Enumerable.Range(0, 256).ToDictionary(
                    i => i.ToString(),
                    _ => new Value(Enumerable.Repeat(new Value("item"), 256).ToList()));
                builder.Set("nested", new Value(new Structure(branches)));
            }

            return builder.Set("match", "paid").Build();
        }
    }
#pragma warning restore SA1202

    private static void AssertResolution<T>(JsonLinq.JObject testCase, ResolutionDetails<T> actual)
    {
        var expected = testCase["result"]!;
        var actualValue = actual.Value is Value value ? ToJson(value) : JsonLinq.JToken.FromObject(actual.Value!);
        var expectedValue = NormalizeNumbers(expected["value"]!);
        actualValue = NormalizeNumbers(actualValue);
        Assert.True(JsonLinq.JToken.DeepEquals(expectedValue, actualValue), $"Expected {expectedValue}, got {actualValue}");
        Assert.Equal(testCase["flag"]!.Value<string>(), actual.FlagKey);
        actual.Reason.Should().Be(expected["reason"]!.Value<string>()!.ToLowerInvariant());

        var errorCode = expected["errorCode"]?.Value<string>();
        if (errorCode is null && expected["reason"]!.Value<string>() == "ERROR")
        {
            // Older canonical error cases specify the reason without an errorCode. Do not invent
            // a successful error status for them; require a reported error without guessing its code.
            Assert.NotEqual(ErrorType.None, actual.ErrorType);
            Assert.False(string.IsNullOrEmpty(actual.ErrorMessage));
        }
        else
        {
            AssertError(errorCode, actual);
        }

        if (expected["variant"] is { } variant)
        {
            Assert.Equal(variant.Value<string>(), actual.Variant);
        }
    }

    private static void AssertResolution<T>(JsonLinq.JObject testCase, FlagEvaluationDetails<T> actual)
        => AssertResolution(
            testCase,
            new ResolutionDetails<T>(actual.FlagKey, actual.Value, actual.ErrorType, actual.Reason, actual.Variant, actual.ErrorMessage, actual.FlagMetadata));

    private static void AssertError<T>(string? errorCode, ResolutionDetails<T> actual)
    {
        var expectedError = errorCode switch
        {
            null => ErrorType.None,
            "PARSE_ERROR" => ErrorType.ParseError,
            "FLAG_NOT_FOUND" => ErrorType.FlagNotFound,
            "TYPE_MISMATCH" => ErrorType.TypeMismatch,
            "TARGETING_KEY_MISSING" => ErrorType.TargetingKeyMissing,
            "INVALID_CONTEXT" => ErrorType.InvalidContext,
            "PROVIDER_NOT_READY" => ErrorType.ProviderNotReady,
            "PROVIDER_FATAL" => ErrorType.ProviderFatal,
            "GENERAL" => ErrorType.General,
            _ => throw new InvalidOperationException($"Unsupported fixture errorCode: {errorCode}"),
        };
        Assert.Equal(expectedError, actual.ErrorType);
        Assert.Equal(errorCode, actual.ErrorMessage);
    }

    // Timestamps in fixture attributes are strings, as the other SDKs read them, not dates.
    private static JsonLinq.JToken ParseFixture(string json)
    {
        using var reader = new Json.JsonTextReader(new StringReader(json)) { DateParseHandling = Json.DateParseHandling.None };
        return JsonLinq.JToken.Load(reader);
    }

    private static string ReadResource(string name)
    {
        using var stream = typeof(CanonicalFixtureTests).Assembly.GetManifestResourceStream(name)
                        ?? throw new InvalidOperationException($"Missing canonical fixture: {name}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static Value ToOpenFeatureValue(JsonLinq.JToken token) => token.Type switch
    {
        JsonLinq.JTokenType.Null => new Value(),
        JsonLinq.JTokenType.Boolean => new Value(token.Value<bool>()),
        JsonLinq.JTokenType.Integer or JsonLinq.JTokenType.Float => new Value(token.Value<double>()),
        JsonLinq.JTokenType.String => new Value(token.Value<string>()!),
        JsonLinq.JTokenType.Object => new Value(new Structure(((JsonLinq.JObject)token).Properties().ToDictionary(p => p.Name, p => ToOpenFeatureValue(p.Value)))),
        JsonLinq.JTokenType.Array => new Value(token.Children().Select(ToOpenFeatureValue).ToList()),
        _ => throw new InvalidOperationException($"Unsupported fixture token: {token.Type}"),
    };

    private static JsonLinq.JToken ToJson(Value value) => value switch
    {
        { IsNull: true } => JsonLinq.JValue.CreateNull(),
        { IsBoolean: true } => new JsonLinq.JValue(value.AsBoolean),
        { IsNumber: true } => new JsonLinq.JValue(value.AsDouble),
        { IsString: true } => new JsonLinq.JValue(value.AsString),
        { IsList: true } => new JsonLinq.JArray(value.AsList!.Select(ToJson)),
        { IsStructure: true } => new JsonLinq.JObject(value.AsStructure!.AsDictionary().Select(p => new JsonLinq.JProperty(p.Key, ToJson(p.Value)))),
        _ => throw new InvalidOperationException("Unexpected OpenFeature value type"),
    };

    // JSON does not distinguish integer and floating-point representations of the same number.
    private static JsonLinq.JToken NormalizeNumbers(JsonLinq.JToken token) => token.Type switch
    {
        JsonLinq.JTokenType.Integer or JsonLinq.JTokenType.Float => new JsonLinq.JValue(token.Value<double>()),
        JsonLinq.JTokenType.Object => new JsonLinq.JObject(((JsonLinq.JObject)token).Properties().Select(p => new JsonLinq.JProperty(p.Name, NormalizeNumbers(p.Value)))),
        JsonLinq.JTokenType.Array => new JsonLinq.JArray(token.Children().Select(NormalizeNumbers)),
        _ => token.DeepClone(),
    };

    // The provider and tracer have separate copies of the evaluation interface. Adapt only that
    // assembly boundary; values, reasons and errors all come from the real production evaluator.
    private sealed class EvaluationAdapter(TracerFlags.Evaluation evaluation) : IEvaluation
    {
        public string FlagKey => evaluation.FlagKey;

        public object? Value => evaluation.Value;

        public EvaluationReason Reason => (EvaluationReason)evaluation.Reason;

        public string? Variant => evaluation.Variant;

        public string? Error => evaluation.Error;

        public IDictionary<string, string>? FlagMetadata => evaluation.FlagMetadata;
    }
}

#pragma warning restore DDFF001
