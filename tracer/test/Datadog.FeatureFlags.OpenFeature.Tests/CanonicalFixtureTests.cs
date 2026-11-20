// <copyright file="CanonicalFixtureTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

extern alias Tracer;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.FeatureFlags;
using OpenFeature.Constant;
using OpenFeature.Model;
using Tracer::Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using Xunit;
using Json = Tracer::Datadog.Trace.Vendors.Newtonsoft.Json;
using JsonLinq = Tracer::Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using TracerFlags = Tracer::Datadog.Trace.FeatureFlags;
using TracerModel = Tracer::Datadog.Trace.FeatureFlags.Rcm.Model;

namespace Datadog.FeatureFlags.OpenFeature.Tests;

#pragma warning disable DDFF001 // Exercise the experimental public API with the canonical fixtures.

public class CanonicalFixtureTests
{
    private const string CasesPrefix = "Fixtures.evaluation-cases.";
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
            var cases = JsonLinq.JArray.Parse(ReadResource(resource));
            Assert.NotEmpty(cases);
            for (var index = 0; index < cases.Count; index++)
            {
                var description = $"{resource.Substring(CasesPrefix.Length)}[{index}]";
                var json = cases[index].ToString(Json.Formatting.None);
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
        var testCase = JsonLinq.JObject.Parse(json);
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

    private static void AssertResolution<T>(JsonLinq.JObject testCase, ResolutionDetails<T> actual)
    {
        var expected = testCase["result"]!;
        var actualValue = actual.Value is Value value ? ToJson(value) : JsonLinq.JToken.FromObject(actual.Value!);
        var expectedValue = NormalizeNumbers(expected["value"]!);
        actualValue = NormalizeNumbers(actualValue);
        Assert.True(JsonLinq.JToken.DeepEquals(expectedValue, actualValue), $"Expected {expectedValue}, got {actualValue}");
        Assert.Equal(testCase["flag"]!.Value<string>(), actual.FlagKey);
        Assert.Equal(expected["reason"]!.Value<string>()!.ToLowerInvariant(), actual.Reason);

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
