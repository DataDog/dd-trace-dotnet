using System;
using Datadog.Trace.ExtensionMethods;
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
        [InlineData("Content-Type", false, true, "content-type")]
        [InlineData(" Content-Type ", false, true, "content-type")]
        [InlineData("C!!!ont_____ent----tYp!/!e", false, true, "c___ont_____ent----typ_/_e")]
        [InlineData("Some.Header", false, true, "some.header")]
        [InlineData("Some.Header", true, true, "some_header")]
        [InlineData("9invalidtagname", false, false, null)]
        [InlineData("invalid_length_201_______________________________________________________________________________________________________________________________________________________________________________________", false, false, null)]
        [InlineData("valid_length_200________________________________________________________________________________________________________________________________________________________________________________________", false, true, "valid_length_200________________________________________________________________________________________________________________________________________________________________________________________")]
        [InlineData(" original_length_201_with_one_leading_whitespace________________________________________________________________________________________________________________________________________________________", false, true, "original_length_201_with_one_leading_whitespace________________________________________________________________________________________________________________________________________________________")]
        public void TryConvertToNormalizedTagName(string input, bool convertPeriodsToUnderscores, bool expectedConversionSuccess, string expectedTagName)
        {
            bool actualConversionSuccess = input.TryConvertToNormalizedTagName(out string actualTagName, convertPeriodsToUnderscores);
            Assert.Equal(expectedConversionSuccess, actualConversionSuccess);

            if (actualConversionSuccess)
            {
                Assert.Equal(expectedTagName, actualTagName);
            }
        }

        [Theory]
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
            bool actualConversionSuccess = input.TryConvertToNormalizedHeaderTagName(out string actualTagName);
            Assert.Equal(expectedConversionSuccess, actualConversionSuccess);

            if (actualConversionSuccess)
            {
                Assert.Equal(expectedTagName, actualTagName);
            }
        }
    }
}
