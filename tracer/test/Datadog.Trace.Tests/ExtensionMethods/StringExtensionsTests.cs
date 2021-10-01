// <copyright file="StringExtensionsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.ExtensionMethods;
using NUnit.Framework;

namespace Datadog.Trace.Tests.ExtensionMethods
{
    public class StringExtensionsTests
    {
        [TestCase("NameSuffix", "Suffix", "Name")]
        [TestCase("Name", "Suffix", "Name")]
        [TestCase("Suffix", "Suffix", "")]
        [TestCase("NameSuffix", "Name", "NameSuffix")]
        [TestCase("Name", "", "Name")]
        [TestCase("Name", null, "Name")]
        [TestCase("", "Name", "")]
        [TestCase("", "", "")]
        public void TrimEnd(string original, string suffix, string expected)
        {
            string actual = original.TrimEnd(suffix, StringComparison.Ordinal);
            Assert.AreEqual(expected, actual);
        }

        [TestCase("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_:/-.", true, "abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz0123456789_:/-.")]
        [TestCase("Content-Type", true, "content-type")]
        [TestCase(" Content-Type ", true, "content-type")]
        [TestCase("C!!!ont_____ent----tYp!/!e", true, "c___ont_____ent----typ_/_e")]
        [TestCase("Some.Header", true, "some.header")]
        [TestCase("9invalidtagname", false, null)]
        [TestCase("invalid_length_201_______________________________________________________________________________________________________________________________________________________________________________________", false, null)]
        [TestCase("valid_length_200________________________________________________________________________________________________________________________________________________________________________________________", true, "valid_length_200________________________________________________________________________________________________________________________________________________________________________________________")]
        [TestCase(" original_length_201_with_one_leading_whitespace________________________________________________________________________________________________________________________________________________________", true, "original_length_201_with_one_leading_whitespace________________________________________________________________________________________________________________________________________________________")]
        public void TryConvertToNormalizedTagName(string input, bool expectedConversionSuccess, string expectedTagName)
        {
            bool actualConversionSuccess = input.TryConvertToNormalizedTagName(out string actualTagName);
            Assert.AreEqual(expectedConversionSuccess, actualConversionSuccess);

            if (actualConversionSuccess)
            {
                Assert.AreEqual(expectedTagName, actualTagName);
            }
        }

        [TestCase("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_:/-.", true, "abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz0123456789_:/-_")]
        [TestCase("Content-Type", true, "content-type")]
        [TestCase(" Content-Type ", true, "content-type")]
        [TestCase("C!!!ont_____ent----tYp!/!e", true, "c___ont_____ent----typ_/_e")]
        [TestCase("Some.Header", true, "some_header")] // Note: Differs from TryConvertToNormalizedTagName because '.' characters are replaced with '_'
        [TestCase("9invalidtagname", false, null)]
        [TestCase("invalid_length_201_______________________________________________________________________________________________________________________________________________________________________________________", false, null)]
        [TestCase("valid_length_200________________________________________________________________________________________________________________________________________________________________________________________", true, "valid_length_200________________________________________________________________________________________________________________________________________________________________________________________")]
        [TestCase(" original_length_201_with_one_leading_whitespace________________________________________________________________________________________________________________________________________________________", true, "original_length_201_with_one_leading_whitespace________________________________________________________________________________________________________________________________________________________")]
        public void TryConvertToNormalizedHeaderTagName(string input, bool expectedConversionSuccess, string expectedTagName)
        {
            bool actualConversionSuccess = input.TryConvertToNormalizedHeaderTagName(out string actualTagName);
            Assert.AreEqual(expectedConversionSuccess, actualConversionSuccess);

            if (actualConversionSuccess)
            {
                Assert.AreEqual(expectedTagName, actualTagName);
            }
        }
    }
}
