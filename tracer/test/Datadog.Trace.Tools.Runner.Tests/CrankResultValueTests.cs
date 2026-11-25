// <copyright file="CrankResultValueTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.Collections.Generic;
using System.Globalization;
using Datadog.Trace.Tools.Runner.Crank;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tools.Runner.Tests;

public class CrankResultValueTests
{
    public static IEnumerable<object?[]> Values()
    {
        // Newtonsoft deserializes JSON numbers into object as boxed doubles and longs
        yield return [1.5d, true, 1.5d];
        yield return [12345L, true, 12345d];
        yield return [-42, true, -42d];
        yield return [1.5f, true, 1.5d];
        yield return [1.5m, true, 1.5d];
        yield return [new JValue(1.5d), true, 1.5d];
        yield return [new JValue(12345L), true, 12345d];
        yield return [JValue.CreateNull(), false, 0d];
        yield return ["1.5", true, 1.5d];
        yield return ["not a number", false, 0d];
        yield return [null, false, 0d];
    }

    [Theory]
    [MemberData(nameof(Values))]
    public void TryGetDouble_IsCultureInvariant(object? value, bool expectedSuccess, double expected)
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            // Build the culture explicitly rather than using e.g. de-DE, because without ICU (globalization-invariant
            // mode, e.g. on Alpine) every predefined culture formats like the invariant culture
            var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            culture.NumberFormat.NumberDecimalSeparator = ",";
            culture.NumberFormat.NumberGroupSeparator = ".";
            CultureInfo.CurrentCulture = culture;

            // Make sure the current culture really would format these differently
            1.5d.ToString(CultureInfo.CurrentCulture).Should().Be("1,5");

            ResultValue.TryGetDouble(value, out var result).Should().Be(expectedSuccess);
            result.Should().Be(expected);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
