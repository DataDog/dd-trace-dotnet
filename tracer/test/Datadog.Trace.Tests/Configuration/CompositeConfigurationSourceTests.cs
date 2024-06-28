// <copyright file="CompositeConfigurationSourceTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Collections.Specialized;
using Datadog.Trace.Configuration;
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
        var actual = ((ITelemeteredConfigurationSource)_source).GetString(key, _telemetry, validator: null, recordValue: true);
        actual.Result.Should().Be(expected);
    }

    [Theory]
    [InlineData("string1")]
    [InlineData("string2")]
    [InlineData("string3")]
    [InlineData("string4")]
    public void GetsTheSameStringInAllCases(string key)
    {
        var expected = ((IConfigurationSource)_source).GetString(key);
        var actual = ((ITelemeteredConfigurationSource)_source).GetString(key, _telemetry, validator: null, recordValue: true);
        actual.Result.Should().Be(expected);
    }

    [Theory]
    [InlineData("string1")]
    [InlineData("string2")]
    [InlineData("string3")]
    [InlineData("string4")]
    public void AttemptsToGrabStringFromEverySource(string key)
    {
        var telemetry = new StubTelemetry();
        var actual = ((ITelemeteredConfigurationSource)_source).GetString(key, telemetry, validator: null, recordValue: true);
        telemetry.Accesses[key].Should().Be(1);
    }

    [Theory]
    [InlineData("int1", 11)]
    [InlineData("int2", 22)]
    [InlineData("int3", 33)]
    [InlineData("int4", 44)]
    public void GetsTheExpectedIntInAllCases(string key, int expected)
    {
        var actual = ((ITelemeteredConfigurationSource)_source).GetInt32(key, _telemetry, validator: null);
        actual.Result.Should().Be(expected);
    }

    [Theory]
    [InlineData("int1")]
    [InlineData("int2")]
    [InlineData("int3")]
    [InlineData("int4")]
    public void GetsTheSameIntInAllCases(string key)
    {
        var expected = ((IConfigurationSource)_source).GetInt32(key);
        var actual = ((ITelemeteredConfigurationSource)_source).GetInt32(key, _telemetry, validator: null);
        actual.Result.Should().Be(expected);
    }

    [Theory]
    [InlineData("int1")]
    [InlineData("int2")]
    [InlineData("int3")]
    [InlineData("int4")]
    public void AttemptsToGrabIntFromEverySource(string key)
    {
        var telemetry = new StubTelemetry();
        var actual = ((ITelemeteredConfigurationSource)_source).GetInt32(key, telemetry, validator: null);
        telemetry.Accesses[key].Should().Be(1);
    }

    [Theory]
    [InlineData("double1", 1.1)]
    [InlineData("double2", 2.2)]
    [InlineData("double3", 3.3)]
    [InlineData("double4", 4.4)]
    public void GetsTheExpectedDoubleInAllCases(string key, double expected)
    {
        var actual = ((ITelemeteredConfigurationSource)_source).GetDouble(key, _telemetry, validator: null);
        actual.Result.Should().Be(expected);
    }

    [Theory]
    [InlineData("double1")]
    [InlineData("double2")]
    [InlineData("double3")]
    [InlineData("double4")]
    public void GetsTheSameDoubleInAllCases(string key)
    {
        var expected = ((IConfigurationSource)_source).GetDouble(key);
        var actual = ((ITelemeteredConfigurationSource)_source).GetDouble(key, _telemetry, validator: null);
        actual.Result.Should().Be(expected);
    }

    [Theory]
    [InlineData("double1")]
    [InlineData("double2")]
    [InlineData("double3")]
    [InlineData("double4")]
    public void AttemptsToGrabDoubleFromEverySource(string key)
    {
        var telemetry = new StubTelemetry();
        var actual = ((ITelemeteredConfigurationSource)_source).GetDouble(key, telemetry, validator: null);
        telemetry.Accesses[key].Should().Be(1);
    }

    [Theory]
    [InlineData("bool1", true)]
    [InlineData("bool2", false)]
    [InlineData("bool3", true)]
    [InlineData("bool4", false)]
    public void GetsTheExpectedBoolInAllCases(string key, bool expected)
    {
        var actual = ((ITelemeteredConfigurationSource)_source).GetBool(key, _telemetry, validator: null);
        actual.Result.Should().Be(expected);
    }

    [Theory]
    [InlineData("bool1")]
    [InlineData("bool2")]
    [InlineData("bool3")]
    [InlineData("bool4")]
    public void GetsTheSameBoolInAllCases(string key)
    {
        var expected = ((IConfigurationSource)_source).GetBool(key);
        var actual = ((ITelemeteredConfigurationSource)_source).GetBool(key, _telemetry, validator: null);
        ((bool?)actual.Result).Should().Be(expected);
    }

    [Theory]
    [InlineData("bool1")]
    [InlineData("bool2")]
    [InlineData("bool3")]
    [InlineData("bool4")]
    public void AttemptsToGrabBoolFromEverySource(string key)
    {
        var telemetry = new StubTelemetry();
        var actual = ((ITelemeteredConfigurationSource)_source).GetBool(key, telemetry, validator: null);
        telemetry.Accesses[key].Should().Be(1);
    }

    [Theory]
    [InlineData("dict1", "source1_a", "source1_b")]
    [InlineData("dict2", "source2_a", "source2_b")]
    [InlineData("dict3", "source3_a", "source3_b")]
    [InlineData("dict4", "source4_a", "source4_b")]
    public void GetsTheExpectedDictionaryInAllCases(string key, params string[] expectedKeys)
    {
        var actual = ((ITelemeteredConfigurationSource)_source).GetDictionary(key, _telemetry, validator: null);
        actual.Result.Should().ContainKeys(expectedKeys);
    }

    [Theory]
    [InlineData("dict1")]
    [InlineData("dict2")]
    [InlineData("dict3")]
    [InlineData("dict4")]
    public void GetsTheSameDictionaryInAllCases(string key)
    {
        var expected = ((IConfigurationSource)_source).GetDictionary(key);
        var actual = ((ITelemeteredConfigurationSource)_source).GetDictionary(key, _telemetry, validator: null);
        actual.Result.Should().Equal(expected);
    }

    [Theory]
    [InlineData("dict1")]
    [InlineData("dict2")]
    [InlineData("dict3")]
    [InlineData("dict4")]
    public void AttemptsToGrabDictionaryFromEverySource(string key)
    {
        var telemetry = new StubTelemetry();
        var actual = ((ITelemeteredConfigurationSource)_source).GetDictionary(key, telemetry, validator: null);
        telemetry.Accesses[key].Should().Be(1);
    }

    internal class StubTelemetry : IConfigurationTelemetry
    {
        public Dictionary<string, int> Accesses { get; } = new();

        public void Record(string key, string value, bool recordValue, ConfigurationOrigins origin, TelemetryErrorCode? error = null)
            => IncrementAccess(key);

        public void Record(string key, bool value, ConfigurationOrigins origin, TelemetryErrorCode? error = null)
            => IncrementAccess(key);

        public void Record(string key, double value, ConfigurationOrigins origin, TelemetryErrorCode? error = null)
            => IncrementAccess(key);

        public void Record(string key, int value, ConfigurationOrigins origin, TelemetryErrorCode? error = null)
            => IncrementAccess(key);

        public void Record(string key, double? value, ConfigurationOrigins origin, TelemetryErrorCode? error = null)
            => IncrementAccess(key);

        public void Record(string key, int? value, ConfigurationOrigins origin, TelemetryErrorCode? error = null)
            => IncrementAccess(key);

        public ICollection<ConfigurationKeyValue> GetData() => null;

        public void CopyTo(IConfigurationTelemetry destination)
        {
        }

        public void SetErrorOnCurrentEntry(string key, TelemetryErrorCode error)
        {
        }

        private void IncrementAccess(string key)
        {
            if (Accesses.TryGetValue(key, out var i))
            {
                Accesses[key] = i + 1;
            }
            else
            {
                Accesses[key] = 1;
            }
        }
    }
}
