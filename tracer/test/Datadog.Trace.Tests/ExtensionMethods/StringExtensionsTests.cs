// <copyright file="StringExtensionsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.ExtensionMethods;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.ExtensionMethods
{
    public class StringExtensionsTests
    {
        [Theory]
        [InlineData("NameSuffix", "Suffix", "Name")]
        [InlineData("Name", "Suffix", "Name")]
        [InlineData("Suffix", "Suffix", "")]
        [InlineData("NameSuffix", "Name", "NameSuffix")]
        [InlineData("Name", "", "Name")]
        [InlineData("Name", null, "Name")]
        [InlineData("", "Name", "")]
        [InlineData("", "", "")]
        public void TrimEnd(string original, string suffix, string expected)
        {
            original.TrimEnd(suffix, StringComparison.Ordinal).Should().Be(expected);
        }

        [Theory]
        [InlineData("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_:/-.", true, "abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz0123456789_:/-_")]
        [InlineData("Content-Type", true, "content-type")]
        [InlineData(" Content-Type ", true, "content-type")]
        [InlineData("C!!!ont_____ent----tYp!/!e", true, "c___ont_____ent----typ_/_e")]
        [InlineData("9invalidtagname", false, null)]
        [InlineData("invalid_length_201_______________________________________________________________________________________________________________________________________________________________________________________", false, null)]
        [InlineData("valid_length_200________________________________________________________________________________________________________________________________________________________________________________________", true, "valid_length_200________________________________________________________________________________________________________________________________________________________________________________________")]
        [InlineData(" original_length_201_with_one_leading_whitespace________________________________________________________________________________________________________________________________________________________", true, "original_length_201_with_one_leading_whitespace________________________________________________________________________________________________________________________________________________________")]
        public void TryConvertToNormalizedTagName(string input, bool expectedConversionSuccess, string expectedTagName)
        {
            var actualConversionSuccess = input.TryConvertToNormalizedTagName(normalizeSpaces: false, out var actualTagName);
            actualConversionSuccess.Should().Be(expectedConversionSuccess);

            if (actualConversionSuccess)
            {
                actualTagName.Should().Be(expectedTagName);
            }
        }

        [Theory]
        [InlineData("Some.Header", true, "some_header")]    // always replace periods
        [InlineData("Some.Header", false, "some_header")]   // always replace periods
        [InlineData("Some Header", true, "some_header")]    // optionally replace spaces
        [InlineData("Some Header", false, "some header")]   // optionally replace spaces
        [InlineData(" Some Header ", true, "some_header")]  // always trim whitespace
        [InlineData(" Some Header ", false, "some header")] // always trim whitespace
        public void TryConvertToNormalizedTagName_NormalizePeriodsAndSpaces(string input, bool normalizeSpaces, string expectedTagName)
        {
            input.TryConvertToNormalizedTagName(normalizeSpaces, out var actualTagName).Should().BeTrue();
            actualTagName.Should().Be(expectedTagName);
        }
    }
}
