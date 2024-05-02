// <copyright file="StringConfigurationSourceTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.Configuration;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Configuration;

public class StringConfigurationSourceTests
{
    [Theory]
    [InlineData("")]        // empty
    [InlineData(" ")]       // 1 space
    [InlineData("  ")]      // 2 spaces
    [InlineData("\t")]      // tab
    [InlineData(":")]       // lonely pair separator
    [InlineData(",")]       // lonely key/value separator
    [InlineData(" : , : ")] // mix of separators and whitespace
    public void ParseCustomKeyValues_WhitespaceOnly_ValueOptional(string entry)
    {
        var dictionary = StringConfigurationSource.ParseCustomKeyValues(entry, allowOptionalMappings: true);
        dictionary.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]        // empty
    [InlineData(" ")]       // 1 space
    [InlineData("  ")]      // 2 spaces
    [InlineData("\t")]      // tab
    [InlineData(":")]       // lonely pair separator
    [InlineData(",")]       // lonely key/value separator
    [InlineData(" : , : ")] // mix of separators and whitespace
    public void ParseCustomKeyValues_WhitespaceOnly_ValueRequired(string entry)
    {
        var dictionary = StringConfigurationSource.ParseCustomKeyValues(entry, allowOptionalMappings: false);
        dictionary.Should().BeEmpty();
    }

    [Theory]
    [InlineData("key")]      // no space
    [InlineData("key ")]     // space after key
    [InlineData(" key")]     // space before key
    [InlineData(" key ")]    // 1 space around key
    [InlineData("  key  ")]  // 2 spaces around key
    [InlineData("key:")]     // no space
    [InlineData("key :")]    // space after key
    [InlineData(" key:")]    // space before key
    [InlineData(" key :")]   // 1 space around key
    [InlineData("  key  :")] // 2 spaces around key
    [InlineData("  key  : ")] // 2 spaces around key and space at end
    public void ParseCustomKeyValues_WhitespaceAroundKey_NoValue_ValueOptional(string entry)
    {
        var dictionary = StringConfigurationSource.ParseCustomKeyValues(entry, allowOptionalMappings: true);

        if (entry.TrimEnd().EndsWith(":"))
        {
            dictionary.Should().HaveCount(0);
        }
        else
        {
            dictionary.Should().HaveCount(1);
            dictionary.Should().Contain(new KeyValuePair<string, string>("key", string.Empty));
        }
    }

    [Theory]
    [InlineData("key")]      // no space
    [InlineData("key ")]     // space after key
    [InlineData(" key")]     // space before key
    [InlineData(" key ")]    // 1 space around key
    [InlineData("  key  ")]  // 2 spaces around key
    [InlineData("key:")]     // no space
    [InlineData("key :")]    // space after key
    [InlineData(" key:")]    // space before key
    [InlineData(" key :")]   // 1 space around key
    [InlineData("  key  :")] // 2 spaces around key
    [InlineData("  key  : ")] // 2 spaces around key and space at end
    public void ParseCustomKeyValues_WhitespaceAroundKey_NoValue_ValueRequired_WithColon(string entry)
    {
        var dictionary = StringConfigurationSource.ParseCustomKeyValues(entry, allowOptionalMappings: false);

        if (entry.Contains(":"))
        {
            dictionary.Should().HaveCount(1);
            dictionary.Should().Contain(new KeyValuePair<string, string>("key", string.Empty));
        }
        else
        {
            dictionary.Should().HaveCount(0);
        }
    }

    [Theory]
    [InlineData("key")]   // no space
    [InlineData("key ")]  // space after key
    [InlineData(" key")]  // space before key
    [InlineData(" key ")] // space before and after
    public void ParseCustomKeyValues_WhitespaceAroundKey_NoValue_ValueRequired_WithoutColon(string entry)
    {
        var dictionary = StringConfigurationSource.ParseCustomKeyValues(entry, allowOptionalMappings: false);

        dictionary.Should().HaveCount(0);
    }

    [Theory]
    [InlineData("key:value")]     // no space
    [InlineData("key :value")]    // space after key
    [InlineData(" key:value")]    // space before key
    [InlineData(" key :value")]   // 1 space around key
    [InlineData("  key  :value")] // 2 spaces around key
    public void ParseCustomKeyValues_WhitespaceAroundKey_WithValue(string entry)
    {
        var dictionary = StringConfigurationSource.ParseCustomKeyValues(entry);

        dictionary.Should().HaveCount(1);
        dictionary.Should().Contain(new KeyValuePair<string, string>("key", "value"));
    }

    [Theory]
    [InlineData("key:value")]     // no space
    [InlineData("key:value ")]    // space after value
    [InlineData("key: value")]    // space before value
    [InlineData("key: value ")]   // 1 space around value
    [InlineData("key:  value  ")] // 2 spaces around value
    public void ParseCustomKeyValues_WhitespaceAroundValue(string entry)
    {
        var dictionary = StringConfigurationSource.ParseCustomKeyValues(entry);

        dictionary.Should().HaveCount(1);
        dictionary.Should().Contain(new KeyValuePair<string, string>("key", "value"));
    }

    [Theory]
    [InlineData("key:value", "value")]                   // none
    [InlineData("key::value", ":value")]                 // leading
    [InlineData("key:value:", "value:")]                 // trailing
    [InlineData("key:value:1", "value:1")]               // middle
    [InlineData("key: : value : 1 : ", ": value : 1 :")] // mix in some spaces
    public void ParseCustomKeyValues_ColonsInValue(string entry, string expectedValue)
    {
        var dictionary = StringConfigurationSource.ParseCustomKeyValues(entry);

        dictionary.Should().HaveCount(1);
        dictionary.Should().Contain(new KeyValuePair<string, string>("key", expectedValue));
    }
}
