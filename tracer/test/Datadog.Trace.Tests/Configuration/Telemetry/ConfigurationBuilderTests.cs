// <copyright file="ConfigurationBuilderTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Specialized;
using System.Linq;
using Datadog.Trace.Configuration;
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
