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

        // private static readonly Dictionary<string, string> HeaderTagsWithOptionalMappings = new Dictionary<string, string> { { "header1", "tag1" }, { "header2", "content-type" }, { "header3", "content-type" }, { "header4", "c___ont_____ent----typ_/_e" }, { "header5", "some_header" }, { "validheaderonly", string.Empty }, { "validheaderwithoutcolon", string.Empty } };

        [Theory]
        [InlineData("Content-Type", false, true, "content-type")]
        [InlineData(" Content-Type ", false, true, "content-type")]
        [InlineData("C!!!ont_____ent----tYp!/!e", false, true, "")]
        [InlineData("Some.Header", false, true, "Some.Header")]
        [InlineData("Some.Header", true, true, "Some_Header")]
        [InlineData("9invalidtagname", false, false, null)]
        [InlineData("invalid_length_201_______________________________________________________________________________________________________________________________________________________________________________________", false, false, null)]
        [InlineData("valid_length_200________________________________________________________________________________________________________________________________________________________________________________________", true, false, "valid_length_200________________________________________________________________________________________________________________________________________________________________________________________")]
        [InlineData(" original_length_201_with_one_leading_whitespace________________________________________________________________________________________________________________________________________________________", true, false, "original_length_201_with_one_leading_whitespace________________________________________________________________________________________________________________________________________________________")]
        public void TryConvertToNormalizedTagName(string input, bool convertPeriodsToUnderscores, bool expectedConversionSuccess, string expectedTagName)
        {
            bool actualConversionSuccess = input.TryConvertToNormalizedTagName(out string actualTagName, convertPeriodsToUnderscores);
            Assert.Equal(expectedConversionSuccess, actualConversionSuccess);

            if (actualConversionSuccess)
            {
                Assert.Equal(expectedTagName, actualTagName);
            }
        }
    }
}
