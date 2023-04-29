// <copyright file="StringAssertionsExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using FluentAssertions;
using FluentAssertions.Execution;
using FluentAssertions.Primitives;

namespace Datadog.Trace.TestHelpers.FluentAssertionsExtensions.Json;

internal static class StringAssertionsExtensions
{
    [CustomAssertionAttribute]
    public static AndWhichConstraint<StringAssertions, JToken> BeValidJson(this StringAssertions stringAssertions, string because = "", params object[] becauseArgs)
    {
        JToken json = null;

        try
        {
            json = JToken.Parse(stringAssertions.Subject);
        }
#pragma warning disable CA1031 // Ignore catching general exception
        catch (Exception ex)
#pragma warning restore CA1031 // Ignore catching general exception
        {
            Execute.Assertion.BecauseOf(because, becauseArgs)
                .FailWith("Expected {context:string} to be valid JSON{reason}, but parsing failed with {0}.", ex.Message);
        }

        return new AndWhichConstraint<StringAssertions, JToken>(stringAssertions, json);
    }

    public static AndWhichConstraint<StringAssertions, string> BeJsonEquivalentTo(this StringAssertions stringAssertions, string json, Func<string, string, bool> filterProperty = null, string because = "", params object[] becauseArgs)
    {
        try
        {
            var subjectDeserialized = JToken.Parse(stringAssertions.Subject);
            var jsonDeserialized = JToken.Parse(json);
            subjectDeserialized.Should().BeEquivalentTo(jsonDeserialized, filterProperty, because, becauseArgs);
        }
#pragma warning disable CA1031 // Ignore catching general exception
        catch (Exception ex)
#pragma warning restore CA1031 // Ignore catching general exception
        {
            Execute.Assertion.BecauseOf(because, becauseArgs)
                .FailWith("Expected {context:string} to be equivalent JSON{reason}, but failed with {0}.", ex.Message);
        }

        return new AndWhichConstraint<StringAssertions, string>(stringAssertions, json);
    }

    public static AndWhichConstraint<StringAssertions, string> ContainSubtree(this StringAssertions stringAssertions, string json, string because = "", params object[] becauseArgs)
    {
        try
        {
            var subjectDeserialized = JToken.Parse(stringAssertions.Subject);
            var jsonDeserialized = JToken.Parse(json);
            subjectDeserialized.Should().ContainSubtree(jsonDeserialized, because, becauseArgs);
        }
#pragma warning disable CA1031 // Ignore catching general exception
        catch (Exception ex)
#pragma warning restore CA1031 // Ignore catching general exception
        {
            Execute.Assertion.BecauseOf(because, becauseArgs)
                .FailWith("Expected {context:string} to be equivalent JSON{reason}, but failed with {0}.", ex.Message);
        }

        return new AndWhichConstraint<StringAssertions, string>(stringAssertions, json);
    }
}
