// <copyright file="CompositeConfigurationSourceTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.ConfigurationSources;
using Datadog.Trace.Configuration.ConfigurationSources.Telemetry;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Telemetry;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Configuration;

public class CompositeConfigurationSourceTests
{
    private readonly CompositeConfigurationSource _source;
    private readonly NullConfigurationTelemetry _telemetry = new();

    public CompositeConfigurationSourceTests()
    {
        _source = new CompositeConfigurationSource()
        {
            new NameValueConfigurationSource(new NameValueCollection
            {
                { "string1", "source1_value1" },
                // { "string2", "source1_value2" },
                // { "string3", "source1_value3" },
                // { "string4", "source1_value4" },
                { "int1", "11" },
                // { "int2", "12" },
                // { "int3", "13" },
                // { "int4", "14" },
                { "double1", "1.1" },
                // { "double2", "1.2" },
                // { "double3", "1.3" },
                // { "double4", "1.4" },
                { "bool1", "true" },
                // { "bool2", "true" },
                // { "bool3", "true" },
                // { "bool4", "true" },
                { "dict1", "source1_a:value1,source1_b:value1" },
                // { "dict2", "source1_a:value2,source1_b:value2" },
                // { "dict3", "source1_a:value3,source1_b:value3" },
                // { "dict4", "source1_a:value4,source1_b:value4" },
            }),
            new NameValueConfigurationSource(new NameValueCollection
            {
                { "string1", "source2_value1" },
                { "string2", "source2_value2" },
                // { "string3", "source2_value3" },
                // { "string4", "source2_value4" },
                { "int1", "21" },
                { "int2", "22" },
                // { "int3", "23" },
                // { "int4", "24" },
                { "double1", "2.1" },
                { "double2", "2.2" },
                // { "double3", "2.3" },
                // { "double4", "2.4" },
                { "bool1", "false" },
                { "bool2", "false" },
                // { "bool3", "false" },
                // { "bool4", "false" },
                { "dict1", "source2_a:value1,source2_b:value1" },
                { "dict2", "source2_a:value2,source2_b:value2" },
                // { "dict3", "source2_a:value3,source2_b:value3" },
                // { "dict4", "source2_a:value4,source2_b:value4" },
            }),
            new NameValueConfigurationSource(new NameValueCollection
            {
                { "string1", "source3_value1" },
                { "string2", "source3_value2" },
                { "string3", "source3_value3" },
                // { "string4", "source3_value4" },
                { "int1", "31" },
                { "int2", "32" },
                { "int3", "33" },
                // { "int4", "34" },
                { "double1", "3.1" },
                { "double2", "3.2" },
                { "double3", "3.3" },
                // { "double4", "3.4" },
                { "bool1", "true" },
                { "bool2", "true" },
                { "bool3", "true" },
                // { "bool4", "true" },
                { "dict1", "source3_a:value1,source3_b:value1" },
                { "dict2", "source3_a:value2,source3_b:value2" },
                { "dict3", "source3_a:value3,source3_b:value3" },
                // { "dict4", "source3_a:value4,source3_b:value4" },
            }),
            new NameValueConfigurationSource(new NameValueCollection
            {
                { "string1", "source4_value1" },
                { "string2", "source4_value2" },
                { "string3", "source4_value3" },
                { "string4", "source4_value4" },
                { "int1", "41" },
                { "int2", "42" },
                { "int3", "43" },
                { "int4", "44" },
                { "double1", "4.1" },
                { "double2", "4.2" },
                { "double3", "4.3" },
                { "double4", "4.4" },
                { "bool1", "false" },
                { "bool2", "false" },
                { "bool3", "false" },
                { "bool4", "false" },
                { "dict1", "source4_a:value1,source4_b:value1" },
                { "dict2", "source4_a:value2,source4_b:value2" },
                { "dict3", "source4_a:value3,source4_b:value3" },
                { "dict4", "source4_a:value4,source4_b:value4" },
            }),
        };
    }

    [Theory]
    [InlineData("string1", "source1_value1")]
    [InlineData("string2", "source2_value2")]
    [InlineData("string3", "source3_value3")]
    [InlineData("string4", "source4_value4")]
    public void GetsTheExpectedStringInAllCases(string key, string expected)
    {
        var actual = _source.GetString(key, _telemetry, validator: null, recordValue: true);
        actual.Result.Should().Be(expected);
    }

    [Theory]
    [InlineData("string1", 4)]
    [InlineData("string2", 3)]
    [InlineData("string3", 2)]
    [InlineData("string4", 1)]
    public void AttemptsToGrabStringFromEverySourceAndRecordsAllOccurrences(string key, int occurrences)
    {
        var telemetry = new StubTelemetry();
        var actual = _source.GetString(key, telemetry, validator: null, recordValue: true);
        telemetry.GetInstanceCount(key).Should().Be(occurrences);
    }

    [Theory]
    [InlineData("int1", 11)]
    [InlineData("int2", 22)]
    [InlineData("int3", 33)]
    [InlineData("int4", 44)]
    public void GetsTheExpectedIntInAllCases(string key, int expected)
    {
        var actual = _source.GetInt32(key, _telemetry, validator: null);
        actual.Result.Should().Be(expected);
    }

    [Theory]
    [InlineData("int1", 4)]
    [InlineData("int2", 3)]
    [InlineData("int3", 2)]
    [InlineData("int4", 1)]
    public void AttemptsToGrabIntFromEverySourceAndRecordsAllOccurrences(string key, int occurrences)
    {
        var telemetry = new StubTelemetry();
        var actual = _source.GetInt32(key, telemetry, validator: null);
        telemetry.GetInstanceCount(key).Should().Be(occurrences);
    }

    [Theory]
    [InlineData("double1", 1.1)]
    [InlineData("double2", 2.2)]
    [InlineData("double3", 3.3)]
    [InlineData("double4", 4.4)]
    public void GetsTheExpectedDoubleInAllCases(string key, double expected)
    {
        var actual = _source.GetDouble(key, _telemetry, validator: null);
        actual.Result.Should().Be(expected);
    }

    [Theory]
    [InlineData("double1", 4)]
    [InlineData("double2", 3)]
    [InlineData("double3", 2)]
    [InlineData("double4", 1)]
    public void AttemptsToGrabDoubleFromEverySourceAndRecordsAllOccurrences(string key, int occurrences)
    {
        var telemetry = new StubTelemetry();
        var actual = _source.GetDouble(key, telemetry, validator: null);
        telemetry.GetInstanceCount(key).Should().Be(occurrences);
    }

    [Theory]
    [InlineData("bool1", true)]
    [InlineData("bool2", false)]
    [InlineData("bool3", true)]
    [InlineData("bool4", false)]
    public void GetsTheExpectedBoolInAllCases(string key, bool expected)
    {
        var actual = _source.GetBool(key, _telemetry, validator: null);
        actual.Result.Should().Be(expected);
    }

    [Theory]
    [InlineData("bool1", 4)]
    [InlineData("bool2", 3)]
    [InlineData("bool3", 2)]
    [InlineData("bool4", 1)]
    public void AttemptsToGrabBoolFromEverySourceAndRecordsAllOccurrences(string key, int occurrences)
    {
        var telemetry = new StubTelemetry();
        var actual = _source.GetBool(key, telemetry, validator: null);
        telemetry.GetInstanceCount(key).Should().Be(occurrences);
    }

    [Theory]
    [InlineData("dict1", "source1_a", "source1_b")]
    [InlineData("dict2", "source2_a", "source2_b")]
    [InlineData("dict3", "source3_a", "source3_b")]
    [InlineData("dict4", "source4_a", "source4_b")]
    public void GetsTheExpectedDictionaryInAllCases(string key, params string[] expectedKeys)
    {
        var actual = _source.GetDictionary(key, _telemetry, validator: null);
        actual.Result.Should().ContainKeys(expectedKeys);
    }

    [Theory]
    [InlineData("dict1", 4)]
    [InlineData("dict2", 3)]
    [InlineData("dict3", 2)]
    [InlineData("dict4", 1)]
    public void AttemptsToGrabDictionaryFromEverySourceAndRecordsAllOccurrences(string key, int occurrences)
    {
        var telemetry = new StubTelemetry();
        var actual = _source.GetDictionary(key, telemetry, validator: null);
        telemetry.GetInstanceCount(key).Should().Be(occurrences);
    }

    [Fact]
    public void Telemetry_WhenMissingDoesNotRecordTelemetry()
    {
        var telemetry = new StubTelemetry();
        const string key = "int_value";
        var source = new CompositeConfigurationSource()
        {
            new NameValueConfigurationSource(new(), ConfigurationOrigins.Calculated),
            new NameValueConfigurationSource(new() { { "something_else", "456" } }, ConfigurationOrigins.EnvVars),
            new NameValueConfigurationSource(new(), ConfigurationOrigins.AppConfig),
        };

        // not present
        var actual = source.GetInt32(key, telemetry, validator: null);
        actual.Should().Be(ConfigurationResult<int>.NotFound());

        // final telemetry value should be the "real" value
        telemetry.Telemetry.Should().BeEmpty();
    }

    [Fact]
    public void Telemetry_WhenErrorRecordsTelemetry()
    {
        var telemetry = new StubTelemetry();
        const string key = "int_value";
        var source = new CompositeConfigurationSource()
        {
            new NameValueConfigurationSource(new(), ConfigurationOrigins.Calculated),
            new NameValueConfigurationSource(new() { { key, "not_an_int" } }, ConfigurationOrigins.DdConfig),
            new NameValueConfigurationSource(new(), ConfigurationOrigins.AppConfig),
        };

        // no valid value
        var actual = source.GetInt32(key, telemetry, validator: null);
        actual.Should().Be(ConfigurationResult<int>.NotFound());

        // only telemetry value should be the error
        telemetry.Telemetry.Should()
                 .ContainSingle()
                 .Which.Should()
                 .BeEquivalentTo(new ConfigurationTelemetryTests.ConfigDto(key, "not_an_int", ConfigurationOrigins.DdConfig, true, TelemetryErrorCode.ParsingInt32Error));
    }

    [Fact]
    public void RecordsTelemetry_WhenPresentInMultipleSources()
    {
        var telemetry = new StubTelemetry();
        const string key = "int_value";
        var source = new CompositeConfigurationSource()
        {
            new NameValueConfigurationSource(new(), ConfigurationOrigins.Calculated),
            new NameValueConfigurationSource(new() { { key, "not_an_int" } }, ConfigurationOrigins.DdConfig),
            new NameValueConfigurationSource(new() { { key, "123" } }, ConfigurationOrigins.Code),
            new NameValueConfigurationSource(new(), ConfigurationOrigins.AppConfig),
            new NameValueConfigurationSource(new() { { key, "not_an_int" } }, ConfigurationOrigins.RemoteConfig),
            new NameValueConfigurationSource(new() { { key, "456" } }, ConfigurationOrigins.EnvVars),
        };

        // first wins
        var expected = 123;
        var actual = source.GetInt32(key, telemetry, validator: null);
        actual.Should().Be(ConfigurationResult<int>.Valid(expected));

        // final telemetry value should be the "real" value
        telemetry.Telemetry.Last().Value.Should().Be(expected);

        // telemetry records everything where a value was found. The last value is the "current" value
        telemetry.Telemetry.Should()
                 .BeEquivalentTo(
                  [
                      new ConfigurationTelemetryTests.ConfigDto(key, 456, ConfigurationOrigins.EnvVars, true, null),
                      new ConfigurationTelemetryTests.ConfigDto(key, "not_an_int", ConfigurationOrigins.RemoteConfig, true, TelemetryErrorCode.ParsingInt32Error),
                      new ConfigurationTelemetryTests.ConfigDto(key, 123, ConfigurationOrigins.Code, true, null),
                      new ConfigurationTelemetryTests.ConfigDto(key, "not_an_int", ConfigurationOrigins.DdConfig, true, TelemetryErrorCode.ParsingInt32Error),
                      new ConfigurationTelemetryTests.ConfigDto(key, 123, ConfigurationOrigins.Code, true, null),
                  ]);
    }

    [Fact]
    public void Telemetry_RecordsDictionary()
    {
        var telemetry = new StubTelemetry();
        const string key = "some_value";
        var source = new CompositeConfigurationSource()
        {
            new NameValueConfigurationSource(new() { { key, "foo:bar,x:baz" } }, ConfigurationOrigins.EnvVars),
            new NameValueConfigurationSource(new(), ConfigurationOrigins.Calculated),
            new NameValueConfigurationSource(new() { { key, string.Empty } }, ConfigurationOrigins.DdConfig),
            new NameValueConfigurationSource(new() { { key, "not_a_dict" } }, ConfigurationOrigins.Code),
            new NameValueConfigurationSource(new() { { key, null } }, ConfigurationOrigins.AppConfig),
            new NameValueConfigurationSource(new() { { key, "x:y" } }, ConfigurationOrigins.RemoteConfig),
        };

        // first wins
        var config = new ConfigurationBuilder(source, telemetry);
        var actual = config.WithKeys(key).AsDictionary();
        actual.Should().NotBeNullOrEmpty().And.BeEquivalentTo(new Dictionary<string, string> { { "foo", "bar" }, { "x", "baz" } });

        // final telemetry value should be the "real" value
        var telemetryData = telemetry.Telemetry;
        telemetryData?.Last().Value.Should().Be("foo:bar,x:baz");

        // telemetry records everything where a value was found
        telemetryData.Should()
                 .BeEquivalentTo(
                  [
                      new ConfigurationTelemetryTests.ConfigDto(key, "x:y", ConfigurationOrigins.RemoteConfig, true, null),
                      new ConfigurationTelemetryTests.ConfigDto(key, "not_a_dict", ConfigurationOrigins.Code, true, null),
                      new ConfigurationTelemetryTests.ConfigDto(key, string.Empty, ConfigurationOrigins.DdConfig, true, null),
                      new ConfigurationTelemetryTests.ConfigDto(key, "foo:bar,x:baz", ConfigurationOrigins.EnvVars, true, null),
                  ]);
    }

    [Fact]
    public void Telemetry_RecordsDictionaryManualConfig()
    {
        var dynamicSource = NullConfigurationSource.Instance;
        var tags = new Dictionary<string, string> { { "x", "y" } };
        var manualSource = new ManualInstrumentationConfigurationSource(
            new Dictionary<string, object>
            {
                { TracerSettingKeyConstants.GlobalTagsKey, tags },
            },
            useDefaultSources: true);
        var telemetry = new StubTelemetry();
        var config = new ConfigurationBuilder(new CompositeConfigurationSource([dynamicSource, manualSource]), telemetry);

        var result = config.WithKeys(ConfigurationKeys.GlobalTags).AsDictionaryResult();
        result.ConfigurationResult.Result.Should().BeSameAs(tags);
        telemetry.Telemetry
                 .Should()
                 .NotBeNull()
                 .And.BeEquivalentTo(
                  [
                      new ConfigurationTelemetryTests.ConfigDto("DD_TAGS", "x:y", ConfigurationOrigins.Code, true, null),
                  ]);
    }

    internal class StubTelemetry : IConfigurationTelemetry
    {
        public List<ConfigurationTelemetryTests.ConfigDto> Telemetry { get; } = new();

        public void Record(string key, string value, bool recordValue, ConfigurationOrigins origin, TelemetryErrorCode? error = null)
            => Telemetry.Add(new(key, value, origin, recordValue, error));

        public void Record(string key, bool value, ConfigurationOrigins origin, TelemetryErrorCode? error = null)
            => Telemetry.Add(new(key, value, origin, recordValue: true, error));

        public void Record(string key, double value, ConfigurationOrigins origin, TelemetryErrorCode? error = null)
            => Telemetry.Add(new(key, value, origin, recordValue: true, error));

        public void Record(string key, int value, ConfigurationOrigins origin, TelemetryErrorCode? error = null)
            => Telemetry.Add(new(key, value, origin, recordValue: true, error));

        public void Record(string key, double? value, ConfigurationOrigins origin, TelemetryErrorCode? error = null)
            => Telemetry.Add(new(key, value, origin, recordValue: true, error));

        public void Record(string key, int? value, ConfigurationOrigins origin, TelemetryErrorCode? error = null)
            => Telemetry.Add(new(key, value, origin, recordValue: true, error));

        public ICollection<ConfigurationKeyValue> GetData() => null;

        public void CopyTo(IConfigurationTelemetry destination)
        {
        }

        public void SetErrorOnCurrentEntry(string key, TelemetryErrorCode error)
        {
        }

        public int GetInstanceCount(string key)
            => Telemetry.Count(x => x.Name == key);
    }
}
