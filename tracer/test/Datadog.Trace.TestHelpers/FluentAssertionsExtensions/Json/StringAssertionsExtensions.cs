// <copyright file="StringAssertionsExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
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
}
