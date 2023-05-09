// <copyright file="ConfigurationBuilderTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Specialized;
using System.Linq;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.ConfigurationSources.Telemetry;
using Datadog.Trace.Configuration.Telemetry;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Configuration;

public class ConfigurationBuilderTests
{
    public class StringTests
    {
        private const string Default = "Some default";
        private readonly NameValueCollection _collection;
        private readonly NameValueConfigurationSource _source;
        private readonly NullConfigurationTelemetry _telemetry = new();

        public StringTests()
        {
            _collection = new NameValueCollection()
            {
                { "key", "value" },
                { "key_with_null_value", null },
                { "key_with_empty_value", string.Empty },
            };
            _source = new NameValueConfigurationSource(_collection);
        }

        [Fact]
        public void GetString_WorksTheSameAsNaiveApproachWithDefault()
        {
            var keys = _collection.AllKeys.Concat(new[] { "unknown" });

            foreach (var key in keys)
            {
                var expected = Naive(key);
                var actual = new ConfigurationBuilder(_source, _telemetry)
                            .WithKeys(key)
                            .AsString(Default);
                actual.Should().Be(expected, $"using key '{key}'");
            }

            string Naive(string key) => _source.GetString(key) ?? Default;
        }

        [Fact]
        public void GetString_WorksTheSameAsNaiveApproachWithValidation()
        {
            var keys = _collection.AllKeys.Concat(new[] { "unknown" });

            foreach (var key in keys)
            {
                var expected = Naive(key);
                var actual = new ConfigurationBuilder(_source, _telemetry)
                            .WithKeys(key)
                            .AsString(Default, x => !string.IsNullOrEmpty(x));
                actual.Should().Be(expected, $"using key '{key}'");
            }

            string Naive(string key)
            {
                var value = _source.GetString(key);
                if (!string.IsNullOrEmpty(value))
                {
                    return value;
                }

                return Default;
            }
        }
    }

    public class GetAsTests
    {
        private const string Default = "40CFDD4B-2CB0-4B18-94CC-6A5E9F44A323";
        private readonly Guid _default = Guid.Parse(Default);

        private readonly Func<string, ParsingResult<Guid>> _converter =
            x => Guid.TryParse(x, out var result) ? result : ParsingResult<Guid>.Failure();

        private readonly Func<string, ParsingResult<Guid?>> _nullableConverter =
            x => Guid.TryParse(x, out var result) ? result : ParsingResult<Guid?>.Failure();

        private readonly NullConfigurationTelemetry _telemetry = new();

        [Theory]
        [InlineData("539F5206-2F28-4F34-9E05-AD7FA78B9705", "539F5206-2F28-4F34-9E05-AD7FA78B9705")]
        [InlineData(null, Default)]
        [InlineData("", Default)]
        [InlineData("invalid", Default)]
        public void GetAs_ReturnsTheExpectedValue(string value, string expected)
        {
            var collection = new NameValueCollection { { "key", value } };
            var source = new NameValueConfigurationSource(collection);

            var actual = new ConfigurationBuilder(source, _telemetry)
                        .WithKeys("key")
                        .GetAs<Guid>(
                             getDefaultValue: () => _default,
                             validator: null,
                             converter: _converter);

            actual.Should().Be(Guid.Parse(expected));
        }

        [Theory]
        [InlineData("539F5206-2F28-4F34-9E05-AD7FA78B9705", "539F5206-2F28-4F34-9E05-AD7FA78B9705")]
        [InlineData(null, Default)]
        [InlineData("", Default)]
        [InlineData("invalid", Default)]
        public void GetAs_ReturnsTheExpectedValueWithNullableGuid(string value, string expected)
        {
            var collection = new NameValueCollection { { "key", value } };
            var source = new NameValueConfigurationSource(collection);

            var actual = new ConfigurationBuilder(source, _telemetry)
                        .WithKeys("key")
                        .GetAs<Guid?>(
                             getDefaultValue: () => _default,
                             validator: null,
                             converter: _nullableConverter);

            actual.Should().Be(Guid.Parse(expected));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("invalid")]
        public void GetAs_ReturnsNullWhenCantParseAndNoDefault(string value)
        {
            var collection = new NameValueCollection { { "key", value } };
            var source = new NameValueConfigurationSource(collection);

            var actual = new ConfigurationBuilder(source, _telemetry)
                        .WithKeys("key")
                        .GetAs(
                             getDefaultValue: null,
                             validator: null,
                             converter: _nullableConverter);

            actual.Should().BeNull();
        }

        [Theory]
        [InlineData("539F5206-2F28-4F34-9E05-AD7FA78B9705", Default)]
        [InlineData("D2681B55-0E58-4192-B4D9-09584163E949", "D2681B55-0E58-4192-B4D9-09584163E949")]
        [InlineData("55911B24-EE69-45FC-AB84-D912F31DEEB9", Default)]
        [InlineData("C3B639AB-28F8-401E-B9C7-D0D36C3B55FF", "C3B639AB-28F8-401E-B9C7-D0D36C3B55FF")]
        public void GetAs_ReturnsDefaultWhenValidationFails(string value, string expected)
        {
            var collection = new NameValueCollection { { "key", value } };
            var source = new NameValueConfigurationSource(collection);

            var actual = new ConfigurationBuilder(source, _telemetry)
                        .WithKeys("key")
                        .GetAs<Guid>(
                             getDefaultValue: () => _default,
                             validator: x => x.ToString()[0] != '5',
                             converter: _converter);

            actual.Should().Be(expected);
        }
    }

    public class BoolTests
    {
        private const bool Default = true;
        private readonly NameValueCollection _collection;
        private readonly NameValueConfigurationSource _source;
        private readonly NullConfigurationTelemetry _telemetry = new();

        public BoolTests()
        {
            _collection = new NameValueCollection()
            {
                { "key_True", "true" },
                { "key_False", "false" },
                { "key_with_null_value", null },
                { "key_with_empty_value", string.Empty },
            };
            _source = new NameValueConfigurationSource(_collection);
        }

        [Fact]
        public void GetBool_WorksTheSameAsNaiveApproachWithDefault()
        {
            var keys = _collection.AllKeys.Concat(new[] { "unknown" });

            foreach (var key in keys)
            {
                var expected = Naive(key);
                var actual = new ConfigurationBuilder(_source, _telemetry)
                            .WithKeys(key)
                            .AsBool(Default);
                actual.Should().Be(expected, $"using key '{key}'");
            }

            bool Naive(string key) => _source.GetBool(key) ?? Default;
        }

        [Fact]
        public void GetBool_WorksTheSameAsNaiveApproachWithValidation()
        {
            var keys = _collection.AllKeys.Concat(new[] { "unknown" });

            foreach (var key in keys)
            {
                var expected = Naive(key);
                var actual = new ConfigurationBuilder(_source, _telemetry)
                            .WithKeys(key)
                            .AsBool(Default, x => x);
                actual.Should().Be(expected, $"using key '{key}'");
            }

            bool Naive(string key)
            {
                var value = _source.GetBool(key);
                // not much validation we can do here!
                if (value is true)
                {
                    return true;
                }

                return Default;
            }
        }
    }

    public class Int32Tests
    {
        private const int Default = 42;
        private readonly NameValueCollection _collection;
        private readonly NameValueConfigurationSource _source;
        private readonly NullConfigurationTelemetry _telemetry = new();

        public Int32Tests()
        {
            _collection = new NameValueCollection()
            {
                { "key", "123" },
                { "negative", "-123" },
                { "zero", "0" },
                { "invalid", "fdsfds" },
                { "key_with_null_value", null },
                { "key_with_empty_value", string.Empty },
            };
            _source = new NameValueConfigurationSource(_collection);
        }

        [Fact]
        public void GetInt32_WorksTheSameAsNaiveApproachWithDefault()
        {
            var keys = _collection.AllKeys.Concat(new[] { "unknown" });

            foreach (var key in keys)
            {
                var expected = Naive(key);
                var actual = new ConfigurationBuilder(_source, _telemetry)
                            .WithKeys(key)
                            .AsInt32(Default);
                actual.Should().Be(expected, $"using key '{key}'");
            }

            int Naive(string key) => _source.GetInt32(key) ?? Default;
        }

        [Fact]
        public void GetInt32_WorksTheSameAsNaiveApproachWithValidation()
        {
            var keys = _collection.AllKeys.Concat(new[] { "unknown" });

            foreach (var key in keys)
            {
                var expected = Naive(key);
                var actual = new ConfigurationBuilder(_source, _telemetry)
                            .WithKeys(key)
                            .AsInt32(Default, x => x > 0);
                actual.Should().Be(expected, $"using key '{key}'");
            }

            int Naive(string key)
            {
                var value = _source.GetInt32(key);
                return value is > 0 ? value.Value : Default;
            }
        }
    }

    public class DoubleTests
    {
        private const double Default = 42.0;
        private readonly NameValueCollection _collection;
        private readonly NameValueConfigurationSource _source;
        private readonly NullConfigurationTelemetry _telemetry = new();

        public DoubleTests()
        {
            _collection = new NameValueCollection()
            {
                { "key", "1.23" },
                { "negative", "-12.3" },
                { "zero", "0" },
                { "invalid", "fdsfds" },
                { "key_with_null_value", null },
                { "key_with_empty_value", string.Empty },
            };
            _source = new NameValueConfigurationSource(_collection);
        }

        [Fact]
        public void GetInt32_WorksTheSameAsNaiveApproachWithDefault()
        {
            var keys = _collection.AllKeys.Concat(new[] { "unknown" });

            foreach (var key in keys)
            {
                var expected = Naive(key);
                var actual = new ConfigurationBuilder(_source, _telemetry)
                            .WithKeys(key)
                            .AsDouble(Default);
                actual.Should().Be(expected, $"using key '{key}'");
            }

            double Naive(string key) => _source.GetDouble(key) ?? Default;
        }

        [Fact]
        public void GetInt32_WorksTheSameAsNaiveApproachWithValidation()
        {
            var keys = _collection.AllKeys.Concat(new[] { "unknown" });

            foreach (var key in keys)
            {
                var expected = Naive(key);
                var actual = new ConfigurationBuilder(_source, _telemetry)
                            .WithKeys(key)
                            .AsDouble(Default, x => x > 0);
                actual.Should().Be(expected, $"using key '{key}'");
            }

            double Naive(string key)
            {
                var value = _source.GetDouble(key);
                return value is > 0 ? value.Value : Default;
            }
        }
    }

    public class DictionaryTests
    {
        private readonly NameValueCollection _collection;
        private readonly NameValueConfigurationSource _source;
        private readonly NullConfigurationTelemetry _telemetry = new();

        public DictionaryTests()
        {
            _collection = new NameValueCollection()
            {
                { "key_no_spaces", "key1:value1,key2:value2,key3:value3" },
                { "key_with_spaces", "key1:value1, key2:value2, key3:value3" },
                { "trailing_semicolon", "key1:value1,key2:value2, key3:value3," },
                { "optional_mappings", "key1:,key2:, key3:value3," },
                { "single_value", "key1" },
                { "key_with_null_value", null },
                { "key_with_empty_value", string.Empty },
            };
            _source = new NameValueConfigurationSource(_collection);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void GetDictionary_WorksTheSameAsNaiveApproachWithDefault(bool allowOptionalMappings)
        {
            var keys = _collection.AllKeys.Concat(new[] { "unknown" });

            foreach (var key in keys)
            {
                var expected = _source.GetDictionary(key, allowOptionalMappings);
                var actual = new ConfigurationBuilder(_source, _telemetry)
                            .WithKeys(key)
                            .AsDictionary(allowOptionalMappings);
                if (expected is null)
                {
                    actual.Should().BeNull();
                }
                else
                {
                    actual.Should().Equal(expected, $"using key '{key}'");
                }
            }
        }
    }
}
