// <copyright file="JTokenDifferentiator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

// Originally Based on https://github.com/fluentassertions/fluentassertions.json
// License: https://github.com/fluentassertions/fluentassertions.json/blob/master/LICENSE

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using FluentAssertions;
using FluentAssertions.Execution;

namespace Datadog.Trace.TestHelpers.FluentAssertionsExtensions.Json;

internal class JTokenDifferentiator
{
    private readonly bool ignoreExtraProperties;

    private readonly Func<IJsonAssertionOptions<object>, IJsonAssertionOptions<object>> config;

    public JTokenDifferentiator(bool ignoreExtraProperties, Func<IJsonAssertionOptions<object>, IJsonAssertionOptions<object>> config)
    {
        this.ignoreExtraProperties = ignoreExtraProperties;
        this.config = config;
    }

    public Difference FindFirstDifference(JToken actual, JToken expected, Func<string, string, bool> filterProperty)
    {
        var path = new JPath();

        if (actual == expected)
        {
            return null;
        }

        if (actual == null)
        {
            return new Difference(DifferenceKind.ActualIsNull, path);
        }

        if (expected == null)
        {
            return new Difference(DifferenceKind.ExpectedIsNull, path);
        }

        return FindFirstDifference(actual, expected, path, filterProperty);
    }

    private static string Describe(JTokenType jTokenType)
    {
        return jTokenType switch
        {
            JTokenType.None => "type none",
            JTokenType.Object => "an object",
            JTokenType.Array => "an array",
            JTokenType.Constructor => "a constructor",
            JTokenType.Property => "a property",
            JTokenType.Comment => "a comment",
            JTokenType.Integer => "an integer",
            JTokenType.Float => "a float",
            JTokenType.String => "a string",
            JTokenType.Boolean => "a boolean",
            JTokenType.Null => "type null",
            JTokenType.Undefined => "type undefined",
            JTokenType.Date => "a date",
            JTokenType.Raw => "type raw",
            JTokenType.Bytes => "type bytes",
            JTokenType.Guid => "a GUID",
            JTokenType.Uri => "a URI",
            JTokenType.TimeSpan => "a timespan",
            _ => throw new ArgumentOutOfRangeException(nameof(jTokenType), jTokenType, null),
        };
    }

    private Difference FindFirstDifference(JToken actual, JToken expected, JPath path, Func<string, string, bool> filterProperty)
    {
        return actual switch
        {
            JArray actualArray => FindJArrayDifference(actualArray, expected, path, filterProperty),
            JObject actualObject => FindJObjectDifference(actualObject, expected, path, filterProperty),
            JProperty actualProperty => FindJPropertyDifference(actualProperty, expected, path, filterProperty),
            JValue actualValue => FindValueDifference(actualValue, expected, path),
            _ => throw new NotSupportedException(),
        };
    }

    private Difference FindJArrayDifference(JArray actualArray, JToken expected, JPath path, Func<string, string, bool> filterProperty)
    {
        if (expected is not JArray expectedArray)
        {
            return new Difference(DifferenceKind.OtherType, path, Describe(actualArray.Type), Describe(expected.Type));
        }

        if (ignoreExtraProperties)
        {
            return CompareExpectedItems(actualArray, expectedArray, path, filterProperty);
        }
        else
        {
            return CompareItems(actualArray, expectedArray, path, filterProperty);
        }
    }

    private Difference CompareExpectedItems(JArray actual, JArray expected, JPath path, Func<string, string, bool> filterProperty)
    {
        JToken[] actualChildren = actual.Children().ToArray();
        JToken[] expectedChildren = expected.Children().ToArray();

        int matchingIndex = 0;
        for (int expectedIndex = 0; expectedIndex < expectedChildren.Length; expectedIndex++)
        {
            var expectedChild = expectedChildren[expectedIndex];
            bool match = false;
            for (int actualIndex = matchingIndex; actualIndex < actualChildren.Length; actualIndex++)
            {
                var difference = FindFirstDifference(actualChildren[actualIndex], expectedChild, filterProperty);

                if (difference == null)
                {
                    match = true;
                    matchingIndex = actualIndex + 1;
                    break;
                }
            }

            if (!match)
            {
                if (matchingIndex >= actualChildren.Length)
                {
                    if (actualChildren.Any(actualChild => FindFirstDifference(actualChild, expectedChild, filterProperty) == null))
                    {
                        return new Difference(DifferenceKind.WrongOrder, path.AddIndex(expectedIndex));
                    }

                    return new Difference(DifferenceKind.ActualMissesElement, path.AddIndex(expectedIndex));
                }

                return FindFirstDifference(actualChildren[matchingIndex], expectedChild, path.AddIndex(expectedIndex), filterProperty);
            }
        }

        return null;
    }

    private Difference CompareItems(JArray actual, JArray expected, JPath path, Func<string, string, bool> filterProperty)
    {
        JToken[] actualChildren = actual.Children().ToArray();
        JToken[] expectedChildren = expected.Children().ToArray();

        if (actualChildren.Length != expectedChildren.Length)
        {
            return new Difference(DifferenceKind.DifferentLength, path, actualChildren.Length, expectedChildren.Length);
        }

        for (int i = 0; i < actualChildren.Length; i++)
        {
            Difference firstDifference = FindFirstDifference(actualChildren[i], expectedChildren[i], path.AddIndex(i), filterProperty);

            if (firstDifference != null)
            {
                return firstDifference;
            }
        }

        return null;
    }

    private Difference FindJObjectDifference(JObject actual, JToken expected, JPath path, Func<string, string, bool> filterProperty)
    {
        if (expected is not JObject expectedObject)
        {
            return new Difference(DifferenceKind.OtherType, path, Describe(actual.Type), Describe(expected.Type));
        }

        return CompareProperties(actual?.Properties(), expectedObject.Properties(), path, filterProperty);
    }

    private Difference CompareProperties(IEnumerable<JProperty> actual, IEnumerable<JProperty> expected, JPath path, Func<string, string, bool> filterProperty)
    {
        var actualDictionary = actual?.ToDictionary(p => p.Name, p => p.Value) ?? new Dictionary<string, JToken>();
        var expectedDictionary = expected?.ToDictionary(p => p.Name, p => p.Value) ?? new Dictionary<string, JToken>();

        foreach (KeyValuePair<string, JToken> expectedPair in expectedDictionary)
        {
            if (!actualDictionary.ContainsKey(expectedPair.Key))
            {
                return new Difference(DifferenceKind.ActualMissesProperty, path.AddProperty(expectedPair.Key));
            }
        }

        foreach (KeyValuePair<string, JToken> actualPair in actualDictionary)
        {
            if (!ignoreExtraProperties && !expectedDictionary.ContainsKey(actualPair.Key) && !(filterProperty?.Invoke(path.ToString(), actualPair.Key) ?? false))
            {
                return new Difference(DifferenceKind.ExpectedMissesProperty, path.AddProperty(actualPair.Key));
            }
        }

        foreach (KeyValuePair<string, JToken> expectedPair in expectedDictionary)
        {
            JToken actualValue = actualDictionary[expectedPair.Key];

            Difference firstDifference = FindFirstDifference(actualValue, expectedPair.Value, path.AddProperty(expectedPair.Key), filterProperty);

            if (firstDifference != null)
            {
                return firstDifference;
            }
        }

        return null;
    }

    private Difference FindJPropertyDifference(JProperty actualProperty, JToken expected, JPath path, Func<string, string, bool> filterProperty)
    {
        if (expected is not JProperty expectedProperty)
        {
            return new Difference(DifferenceKind.OtherType, path, Describe(actualProperty.Type), Describe(expected.Type));
        }

        if (actualProperty.Name != expectedProperty.Name)
        {
            return new Difference(DifferenceKind.OtherName, path);
        }

        return FindFirstDifference(actualProperty.Value, expectedProperty.Value, path, filterProperty);
    }

    private Difference FindValueDifference(JValue actualValue, JToken expected, JPath path)
    {
        if (expected is not JValue expectedValue)
        {
            return new Difference(DifferenceKind.OtherType, path, Describe(actualValue.Type), Describe(expected.Type));
        }

        return CompareValues(actualValue, expectedValue, path);
    }

    private Difference CompareValues(JValue actual, JValue expected, JPath path)
    {
        if (actual.Type != expected.Type)
        {
            return new Difference(DifferenceKind.OtherType, path, Describe(actual.Type), Describe(expected.Type));
        }

        bool hasMismatches;
        using (var scope = new AssertionScope())
        {
            actual.Value.Should().BeEquivalentTo(expected.Value, options => (JsonAssertionOptions<object>)config.Invoke(new JsonAssertionOptions<object>(options)));
            hasMismatches = scope.Discard().Length > 0;
        }

        if (hasMismatches)
        {
            return new Difference(DifferenceKind.OtherValue, path);
        }

        return null;
    }
}
