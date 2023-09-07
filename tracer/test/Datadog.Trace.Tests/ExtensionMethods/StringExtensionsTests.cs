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
            string actual = original.TrimEnd(suffix, StringComparison.Ordinal);
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_:/-.", true, "abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz0123456789_:/-.")]
        [InlineData("Content-Type", true, "content-type")]
        [InlineData(" Content-Type ", true, "content-type")]
        [InlineData("C!!!ont_____ent----tYp!/!e", true, "c___ont_____ent----typ_/_e")]
        [InlineData("Some.Header", true, "some.header")]
        [InlineData("9invalidtagname", false, null)]
        [InlineData("invalid_length_201_______________________________________________________________________________________________________________________________________________________________________________________", false, null)]
        [InlineData("valid_length_200________________________________________________________________________________________________________________________________________________________________________________________", true, "valid_length_200________________________________________________________________________________________________________________________________________________________________________________________")]
        [InlineData(" original_length_201_with_one_leading_whitespace________________________________________________________________________________________________________________________________________________________", true, "original_length_201_with_one_leading_whitespace________________________________________________________________________________________________________________________________________________________")]
        public void TryConvertToNormalizedTagName(string input, bool expectedConversionSuccess, string expectedTagName)
        {
            bool actualConversionSuccess = input.TryConvertToNormalizedTagName(normalizePeriodsAndSpaces: false, out string actualTagName);
            Assert.Equal(expectedConversionSuccess, actualConversionSuccess);

            if (actualConversionSuccess)
            {
                Assert.Equal(expectedTagName, actualTagName);
            }
        }

        [Theory]
        [InlineData("Some.Header", true, "some_header")]
        [InlineData("Some Header", true, "some_header")]
        [InlineData("Some.Header", false, "some.header")]
        [InlineData("Some Header", false, "some header")]
        [InlineData(" Some Header ", true, "some_header")]  // always trim whitespace
        [InlineData(" Some Header ", false, "some header")] // always trim whitespace
        public void TryConvertToNormalizedTagName_NormalizePeriodsAndSpaces(string input, bool normalizePeriodsAndSpaces, string expectedTagName)
        {
            input.TryConvertToNormalizedTagName(normalizePeriodsAndSpaces: normalizePeriodsAndSpaces, out var actualTagName).Should().BeTrue();

            Assert.Equal(expectedTagName, actualTagName);
        }

        [Theory]
        [InlineData("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_:/-.", true, "abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz0123456789_:/-_")]
        [InlineData("Content-Type", true, "content-type")]
        [InlineData(" Content-Type ", true, "content-type")]
        [InlineData("C!!!ont_____ent----tYp!/!e", true, "c___ont_____ent----typ_/_e")]
        [InlineData("Some.Header", true, "some_header")]
        [InlineData("9invalidtagname", false, null)]
        [InlineData("invalid_length_201_______________________________________________________________________________________________________________________________________________________________________________________", false, null)]
        [InlineData("valid_length_200________________________________________________________________________________________________________________________________________________________________________________________", true, "valid_length_200________________________________________________________________________________________________________________________________________________________________________________________")]
        [InlineData(" original_length_201_with_one_leading_whitespace________________________________________________________________________________________________________________________________________________________", true, "original_length_201_with_one_leading_whitespace________________________________________________________________________________________________________________________________________________________")]
        public void TryConvertToNormalizedHeaderTagName(string input, bool expectedConversionSuccess, string expectedTagName)
        {
            bool actualConversionSuccess = input.TryConvertToNormalizedTagName(normalizePeriodsAndSpaces: true, out string actualTagName);
            Assert.Equal(expectedConversionSuccess, actualConversionSuccess);

            if (actualConversionSuccess)
            {
                Assert.Equal(expectedTagName, actualTagName);
            }
        }
    }
}
