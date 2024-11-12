// <copyright file="ConfigurationBuilderTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.ConfigurationSources.Telemetry;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Telemetry;
using FluentAssertions;
using Newtonsoft.Json;
using Xunit;
using Entry = Datadog.Trace.Configuration.Telemetry.ConfigurationTelemetry.ConfigurationTelemetryEntry;

namespace Datadog.Trace.Tests.Configuration;

public class ConfigurationBuilderTests
{
    public interface IConfigurationSourceFactory
    {
        IConfigurationSource GetSource(IDictionary<string, object> collection);
    }

    public class NameValueCollectionTests
    {
        public class Factory : IConfigurationSourceFactory
        {
            public IConfigurationSource GetSource(IDictionary<string, object> values)
            {
                var data = new NameValueCollection();
                foreach (var kvp in values)
                {
                    data.Add(kvp.Key, kvp.Value?.ToString());
                }

                return new NameValueConfigurationSource(data);
            }
        }

        public class StringTests : StringTestsBase
        {
            public StringTests()
                : base(new Factory())
            {
            }
        }

        public class GetAsTests : GetAsTestsBase
        {
            public GetAsTests()
                : base(new Factory())
            {
            }
        }

        public class BoolTests : BoolTestsBase
        {
            public BoolTests()
                : base(new Factory())
            {
            }
        }

        public class Int32Tests : Int32TestsBase
        {
            public Int32Tests()
                : base(new Factory())
            {
            }
        }

        public class DoubleTests : DoubleTestsBase
        {
            public DoubleTests()
                : base(new Factory())
            {
            }
        }

        public class DictionaryTests : DictionaryTestsBase
        {
            public DictionaryTests()
                : base(new Factory())
            {
            }
        }
    }

    public class JsonTests
    {
        private const string Key = "key";

        private static void TestTelemetryHelper<T>(object value, List<Entry> expectedTelemetry, Func<IConfigurationSource, IConfigurationTelemetry, T> runBuilder)
        {
            var telemetry = new ConfigurationTelemetry();
            var source = new Factory().GetSource(new Dictionary<string, object> { { Key, value } });
            try
            {
                var result = runBuilder(source, telemetry);
            }
            catch (Exception)
            {
                // Don't care as we're looking at telemetry
            }

            telemetry
               .GetQueueForTesting()
               .OrderBy(x => x.SeqId)
               .Should()
               .BeEquivalentTo(
                    expectedTelemetry.Select(
                        x => new
                        {
                            x.Error,
                            x.Key,
                            x.Origin,
                            x.Type,
                            x.BoolValue,
                            x.DoubleValue,
                            x.IntValue,
                            x.StringValue
                        })); // Ignore seqId for comparison
        }

        public class Factory : IConfigurationSourceFactory
        {
            private static JsonSerializerSettings _jsonSettings = new JsonSerializerSettings
            {
                Culture = CultureInfo.InvariantCulture
            };

            public IConfigurationSource GetSource(IDictionary<string, object> collection)
                => new JsonConfigurationSource(JsonConvert.SerializeObject(collection, _jsonSettings), ConfigurationOrigins.Code);
        }

        public class StringTests : StringTestsBase
        {
            private const string Default = "some value";

            public StringTests()
                : base(new Factory())
            {
            }

            [Theory]
            [InlineData(123)]
            [InlineData(true)]
            [InlineData(-12.23)]
            [InlineData("Testing")]
            [InlineData("-23.3")]
            [InlineData("False")]
            [InlineData(null)]
            [InlineData("")]
            [InlineData("  ")]
            public void RecordsTelemetryCorrectly(object value)
            {
                var expectedTelemetry = value switch
                {
                    "" => new List<Entry>
                    {
                        Entry.String(Key, string.Empty, ConfigurationOrigins.Code, error: TelemetryErrorCode.FailedValidation),
                        Entry.String(Key, Default, ConfigurationOrigins.Default, error: null),
                    },
                    string s => new List<Entry>
                    {
                        Entry.String(Key, s, ConfigurationOrigins.Code, error: null),
                    },
                    null => new()
                    {
                        Entry.String(Key, Default, ConfigurationOrigins.Default, error: null),
                    },
                    { } i => new List<Entry>
                    {
                        Entry.String(Key, Convert.ToString(i, CultureInfo.InvariantCulture), ConfigurationOrigins.Code, error: null),
                    },
                };

                TestTelemetryHelper(
                    value,
                    expectedTelemetry,
                    (source, telemetry) => new ConfigurationBuilder(source, telemetry)
                                          .WithKeys(Key)
                                          .AsString(Default, x => !string.IsNullOrEmpty(x)));
            }
        }

        public class GetAsTests : GetAsTestsBase
        {
            public GetAsTests()
                : base(new Factory())
            {
            }
        }

        public class BoolTests : BoolTestsBase
        {
            public BoolTests()
                : base(new Factory())
            {
            }

            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            [InlineData("true")]
            [InlineData("false")]
            [InlineData("True")]
            [InlineData("False")]
            [InlineData(null)]
            [InlineData("")]
            [InlineData("Not a bool")]
            public void RecordsTelemetryCorrectly(object value)
            {
                var expectedTelemetry = value switch
                {
                    true or "True" or "true" => new List<Entry>()
                    {
                        Entry.Bool(Key, true, ConfigurationOrigins.Code, error: null),
                    },
                    false or "False" or "false" => new()
                    {
                        Entry.Bool(Key, false, ConfigurationOrigins.Code, TelemetryErrorCode.FailedValidation),
                        Entry.Bool(Key, true, ConfigurationOrigins.Default, error: null),
                    },
                    null => new()
                    {
                        Entry.Bool(Key, true, ConfigurationOrigins.Default, error: null),
                    },
                    string x1 => new()
                    {
                        Entry.String(Key, x1, ConfigurationOrigins.Code, TelemetryErrorCode.JsonBooleanError),
                    },
                    _ => throw new InvalidOperationException("Unexpected value " + value),
                };

                TestTelemetryHelper(
                    value,
                    expectedTelemetry,
                    (source, telemetry) => new ConfigurationBuilder(source, telemetry)
                                          .WithKeys(Key)
                                          .AsBool(true, x => x));
            }
        }

        public class Int32Tests : Int32TestsBase
        {
            private const int Default = 42;

            public Int32Tests()
                : base(new Factory())
            {
            }

            [Theory]
            [InlineData(123)]
            [InlineData(-123)]
            [InlineData(0)]
            [InlineData(12.23)]
            [InlineData(-12.23)]
            [InlineData("123")]
            [InlineData("23.3")]
            [InlineData("False")]
            [InlineData(null)]
            [InlineData("")]
            public void RecordsTelemetryCorrectly(object value)
            {
                var expectedTelemetry = value switch
                {
                    int i and > 0 => new List<Entry>
                    {
                        Entry.Number(Key, i, ConfigurationOrigins.Code, error: null),
                    },
                    "123" => new List<Entry> // Note the implicit conversion, but not for 23.3!
                    {
                        Entry.Number(Key, 123, ConfigurationOrigins.Code, error: null),
                    },
                    int i => new List<Entry>
                    {
                        Entry.Number(Key, i, ConfigurationOrigins.Code, error: TelemetryErrorCode.FailedValidation),
                        Entry.Number(Key, Default, ConfigurationOrigins.Default, error: null),
                    },
                    double d and > 0 => new List<Entry> // Note the implicit conversion!
                    {
                        Entry.Number(Key, (int)d, ConfigurationOrigins.Code, error: null),
                    },
                    double d => new List<Entry> // Note the implicit conversion!
                    {
                        Entry.Number(Key, (int)d, ConfigurationOrigins.Code, error: TelemetryErrorCode.FailedValidation),
                        Entry.Number(Key, Default, ConfigurationOrigins.Default, error: null),
                    },
                    null => new()
                    {
                        Entry.Number(Key, Default, ConfigurationOrigins.Default, error: null),
                    },
                    string x1 => new()
                    {
                        Entry.String(Key, x1, ConfigurationOrigins.Code, TelemetryErrorCode.JsonInt32Error),
                    },
                    _ => throw new InvalidOperationException("Unexpected value " + value),
                };

                TestTelemetryHelper(
                    value,
                    expectedTelemetry,
                    (source, telemetry) => new ConfigurationBuilder(source, telemetry)
                                          .WithKeys(Key)
                                          .AsInt32(Default, x => x > 0));
            }
        }

        public class DoubleTests : DoubleTestsBase
        {
            private const double Default = 42.0;

            public DoubleTests()
                : base(new Factory())
            {
            }

            [Theory]
            [InlineData(123)]
            [InlineData(-123)]
            [InlineData(0)]
            [InlineData(0.0)]
            [InlineData(12.23)]
            [InlineData(-12.23)]
            [InlineData("123")]
            [InlineData("23.3")]
            [InlineData("-23.3")]
            [InlineData("False")]
            [InlineData(null)]
            [InlineData("")]
            public void RecordsTelemetryCorrectly(object value)
            {
                var expectedTelemetry = value switch
                {
                    int i and > 0 => new List<Entry>
                    {
                        Entry.Number(Key, (double)i, ConfigurationOrigins.Code, error: null),
                    },
                    int i => new List<Entry>
                    {
                        Entry.Number(Key, (double)i, ConfigurationOrigins.Code, error: TelemetryErrorCode.FailedValidation),
                        Entry.Number(Key, Default, ConfigurationOrigins.Default, error: null),
                    },
                    double d and > 0 => new List<Entry>
                    {
                        Entry.Number(Key, d, ConfigurationOrigins.Code, error: null),
                    },
                    double d => new List<Entry> // Note the implicit conversion!
                    {
                        Entry.Number(Key, d, ConfigurationOrigins.Code, error: TelemetryErrorCode.FailedValidation),
                        Entry.Number(Key, Default, ConfigurationOrigins.Default, error: null),
                    },
                    string s when TryParse(s, out var d) && d > 0 => new List<Entry>
                    {
                        Entry.Number(Key, d, ConfigurationOrigins.Code, error: null),
                    },
                    string s when TryParse(s, out var d) => new List<Entry>
                    {
                        Entry.Number(Key, d, ConfigurationOrigins.Code, error: TelemetryErrorCode.FailedValidation),
                        Entry.Number(Key, Default, ConfigurationOrigins.Default, error: null),
                    },
                    null => new()
                    {
                        Entry.Number(Key, Default, ConfigurationOrigins.Default, error: null),
                    },
                    string x => new()
                    {
                        Entry.String(Key, x, ConfigurationOrigins.Code, TelemetryErrorCode.JsonDoubleError),
                    },
                    _ => throw new InvalidOperationException("Unexpected value " + value),
                };

                TestTelemetryHelper(
                    value,
                    expectedTelemetry,
                    (source, telemetry) => new ConfigurationBuilder(source, telemetry)
                                          .WithKeys(Key)
                                          .AsDouble(Default, x => x > 0));
            }
        }

        public class DictionaryTests : DictionaryTestsBase
        {
            public DictionaryTests()
                : base(new Factory())
            {
            }
        }

        public class DictionaryObjectTests : DictionaryObjectTestsBase
        {
            public DictionaryObjectTests()
                : base(new Factory())
            {
            }
        }
    }

    public abstract class StringTestsBase
    {
        private const string Default = "Some default";
        private readonly IConfigurationSourceFactory _factory;
        private readonly IConfigurationSource _source;
        private readonly Dictionary<string, object> _collection;
        private readonly NullConfigurationTelemetry _telemetry = new();

        private readonly Func<string, ParsingResult<string>> _converter = val
            => val switch
            {
                "none" => "false",
                "on" => "true",
                _ => ParsingResult<string>.Failure(),
            };

        public StringTestsBase(IConfigurationSourceFactory factory)
        {
            _factory = factory;
            _collection = new Dictionary<string, object>
            {
                { "key", "value" },
                { "key_with_null_value", null },
                { "key_with_empty_value", string.Empty },
                { "key_integer", 123 },
                { "key_bool", true },
                { "key_double", -12.23 },
            };
            _source = factory.GetSource(_collection);
        }

        [Fact]
        public void GetString_WorksTheSameAsNaiveApproach()
        {
            var keys = _collection.Keys.Concat(new[] { "unknown" });

            foreach (var key in keys)
            {
                var expected = Naive(key);
                var actual = Builder(key);
                actual.Should().Be(expected, $"using key '{key}'");
            }

            string Naive(string key) => _source.GetString(key) ?? Default;

            string Builder(string key)
                => new ConfigurationBuilder(_source, _telemetry)
                  .WithKeys(key)
                  .AsString(Default);
        }

        [Fact]
        public void GetString_WorksTheSameAsNaiveApproachWithValidation()
        {
            var keys = _collection.Keys.Concat(new[] { "unknown" });

            foreach (var key in keys)
            {
                var expected = Naive(key);
                var actual = Builder(key);
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

            string Builder(string key)
                => new ConfigurationBuilder(_source, _telemetry)
                  .WithKeys(key)
                  .AsString(Default, x => !string.IsNullOrEmpty(x));
        }

        [Theory]
        [InlineData("none", "false")]
        [InlineData("on", "true")]
        [InlineData(null, Default)]
        [InlineData("", Default)]
        [InlineData("invalid", Default)]
        public void GetString_ReturnsTheExpectedValueWithConverter(string value, string expected)
        {
            const string key = "key_converter";
            var collection = new Dictionary<string, object> { { key, value } };
            var source = _factory.GetSource(collection);
            var actual = new ConfigurationBuilder(source, _telemetry)
                        .WithKeys(key)
                        .AsString(() => Default, validator: null, converter: _converter);

            actual.Should().Be(expected);
        }
    }

    public abstract class GetAsTestsBase
    {
        private const string Default = "40CFDD4B-2CB0-4B18-94CC-6A5E9F44A323";
        private readonly IConfigurationSourceFactory _factory;
        private readonly Guid _default = Guid.Parse(Default);

        private readonly Func<string, ParsingResult<Guid>> _converter =
            x => Guid.TryParse(x, out var result) ? result : ParsingResult<Guid>.Failure();

        private readonly Func<string, ParsingResult<Guid?>> _nullableConverter =
            x => Guid.TryParse(x, out var result) ? result : ParsingResult<Guid?>.Failure();

        private readonly NullConfigurationTelemetry _telemetry = new();

        protected GetAsTestsBase(IConfigurationSourceFactory factory)
        {
            _factory = factory;
        }

        [Theory]
        [InlineData("539F5206-2F28-4F34-9E05-AD7FA78B9705", "539F5206-2F28-4F34-9E05-AD7FA78B9705")]
        [InlineData(null, Default)]
        [InlineData("", Default)]
        [InlineData("invalid", Default)]
        public void GetAs_ReturnsTheExpectedValue(string value, string expected)
        {
            var collection = new Dictionary<string, object> { { "key", value } };
            var source = _factory.GetSource(collection);

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
            var collection = new Dictionary<string, object> { { "key", value } };
            var source = _factory.GetSource(collection);

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
        public void GetAs_RecordsTheDefaultValueInTelemetry(string value)
        {
            const string key = "key";
            var collection = new Dictionary<string, object> { { key, value } };
            var source = _factory.GetSource(collection);

            var telemetry = new ConfigurationTelemetry();
            var actual = new ConfigurationBuilder(source, telemetry)
                        .WithKeys(key)
                        .GetAs<Guid?>(
                             getDefaultValue: () => _default,
                             validator: null,
                             converter: _nullableConverter);

            actual.Should().Be(_default);
            var finalValue = telemetry.GetData()
                                      .Where(x => x.Name == key)
                                      .OrderByDescending(x => x.SeqId)
                                      .FirstOrDefault()
                                      .Value;
            finalValue.Should().Be(_default.ToString());
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("invalid")]
        public void GetAs_RecordsTheProvidedDefaultValueInTelemetry(string value)
        {
            const string key = "key";
            const string stringifiedDefault = "TheDefault";
            var collection = new Dictionary<string, object> { { key, value } };
            var source = _factory.GetSource(collection);

            var telemetry = new ConfigurationTelemetry();
            var actual = new ConfigurationBuilder(source, telemetry)
                        .WithKeys(key)
                        .GetAs<Guid?>(
                             getDefaultValue: () => new DefaultResult<Guid?>(_default, stringifiedDefault),
                             validator: null,
                             converter: _nullableConverter);

            actual.Should().Be(_default);
            var finalValue = telemetry.GetData()
                                      .Where(x => x.Name == key)
                                      .OrderByDescending(x => x.SeqId)
                                      .FirstOrDefault()
                                      .Value;
            finalValue.Should().Be(stringifiedDefault);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("invalid")]
        public void GetAs_ReturnsNullWhenCantParseAndNoDefault(string value)
        {
            var collection = new Dictionary<string, object> { { "key", value } };
            var source = _factory.GetSource(collection);

            var actual = new ConfigurationBuilder(source, _telemetry)
                        .WithKeys("key")
                        .GetAsStruct(
                             validator: null,
                             converter: _converter);

            actual.Should().BeNull();
        }

        [Theory]
        [InlineData("539F5206-2F28-4F34-9E05-AD7FA78B9705", Default)]
        [InlineData("D2681B55-0E58-4192-B4D9-09584163E949", "D2681B55-0E58-4192-B4D9-09584163E949")]
        [InlineData("55911B24-EE69-45FC-AB84-D912F31DEEB9", Default)]
        [InlineData("C3B639AB-28F8-401E-B9C7-D0D36C3B55FF", "C3B639AB-28F8-401E-B9C7-D0D36C3B55FF")]
        public void GetAs_ReturnsDefaultWhenValidationFails(string value, string expected)
        {
            var collection = new Dictionary<string, object> { { "key", value } };
            var source = _factory.GetSource(collection);

            var actual = new ConfigurationBuilder(source, _telemetry)
                        .WithKeys("key")
                        .GetAs<Guid>(
                             getDefaultValue: () => _default,
                             validator: x => x.ToString()[0] != '5',
                             converter: _converter);

            actual.Should().Be(expected);
        }
    }

    public abstract class BoolTestsBase
    {
        private const bool Default = true;
        private readonly IConfigurationSourceFactory _factory;
        private readonly Dictionary<string, object> _collection;
        private readonly IConfigurationSource _source;
        private readonly NullConfigurationTelemetry _telemetry = new();

        private readonly Func<string, ParsingResult<bool>> _converter = val
            => val switch
            {
                "on" => true,
                "off" => false,
                _ => ParsingResult<bool>.Failure(),
            };

        public BoolTestsBase(IConfigurationSourceFactory factory)
        {
            _factory = factory;
            _collection = new Dictionary<string, object>
            {
                { "key_True", true },
                { "key_False", false },
                { "key_with_null_value", null },
                { "key_with_empty_value", string.Empty },
            };
            _source = factory.GetSource(_collection);
        }

        [Fact]
        public void GetBool_WorksTheSameAsNaiveApproachWithDefault()
        {
            var keys = _collection.Keys.Concat(new[] { "unknown" });

            foreach (var key in keys)
            {
                var expected = Result<bool>.Try(() => Naive(key));
                var actual = Result<bool>.Try(() => Builder(key));
                actual.Should().Be(expected, $"using key '{key}'");
            }

            bool Naive(string key) => _source.GetBool(key) ?? Default;

            bool Builder(string key) => new ConfigurationBuilder(_source, _telemetry)
                                       .WithKeys(key)
                                       .AsBool(Default);
        }

        [Fact]
        public void GetBool_WorksTheSameAsNaiveApproachWithValidation()
        {
            var keys = _collection.Keys.Concat(new[] { "unknown" });

            foreach (var key in keys)
            {
                var expected = Result<bool>.Try(() => Naive(key));
                var actual = Result<bool>.Try(() => Builder(key));
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

            bool Builder(string key)
            {
                return new ConfigurationBuilder(_source, _telemetry)
                      .WithKeys(key)
                      .AsBool(Default, x => x);
            }
        }

        [Theory]
        [InlineData("off", false)]
        [InlineData("on", true)]
        [InlineData(null, Default)]
        [InlineData("", Default)]
        [InlineData("invalid", Default)]
        public void GetBool_ReturnsTheExpectedValueWithConverter(string value, bool expected)
        {
            const string key = "key_converter";
            var collection = new Dictionary<string, object> { { key, value } };
            var source = _factory.GetSource(collection);
            var actual = new ConfigurationBuilder(source, _telemetry)
                        .WithKeys(key)
                        .AsBool(() => Default, validator: null, converter: _converter);

            actual.Should().Be(expected);
        }
    }

    public abstract class Int32TestsBase
    {
        private const int Default = 42;
        private readonly IConfigurationSourceFactory _factory;
        private readonly Dictionary<string, object> _collection;
        private readonly IConfigurationSource _source;
        private readonly NullConfigurationTelemetry _telemetry = new();

        private readonly Func<string, ParsingResult<int>> _absConverter = val
            => val switch
            {
                { } x when int.TryParse(x, out var value) && value >= 0 => value,
                { } x when int.TryParse(x, out var value) && value < 0 => -value,
                _ => ParsingResult<int>.Failure(),
            };

        public Int32TestsBase(IConfigurationSourceFactory factory)
        {
            _factory = factory;
            _collection = new Dictionary<string, object>()
            {
                { "key", 123 },
                { "negative", -123 },
                { "key_double", 12.3 },
                { "negative_double", -12.3 },
                { "key_string", "123" },
                { "negative_string", "-123" },
                { "key_string_double", "12.3" },
                { "negative_string_double", "-12.3" },
                { "zero", 0 },
                { "invalid", "fdsfds" },
                { "key_with_null_value", null },
                { "key_with_empty_value", string.Empty },
            };
            _source = factory.GetSource(_collection);
        }

        [Fact]
        public void GetInt32_WorksTheSameAsNaiveApproachWithDefault()
        {
            var keys = _collection.Keys.Concat(new[] { "unknown" });

            foreach (var key in keys)
            {
                var expected = Result<int>.Try(() => Naive(key));
                var actual = Result<int>.Try(() => Builder(key));
                actual.Should().Be(expected, $"using key '{key}'");
            }

            int Naive(string key) => _source.GetInt32(key) ?? Default;

            int Builder(string key) => new ConfigurationBuilder(_source, _telemetry)
                                      .WithKeys(key)
                                      .AsInt32(Default);
        }

        [Fact]
        public void GetInt32_WorksTheSameAsNaiveApproachWithValidation()
        {
            var keys = _collection.Keys.Concat(new[] { "unknown" });

            foreach (var key in keys)
            {
                var expected = Result<int>.Try(() => Naive(key));
                var actual = Result<int>.Try(() => Builder(key).Value);
                actual.Should().Be(expected, $"using key '{key}'");
            }

            int Naive(string key)
            {
                var value = _source.GetInt32(key);
                return value is > 0 ? value.Value : Default;
            }

            int? Builder(string key)
            {
                return new ConfigurationBuilder(_source, _telemetry)
                      .WithKeys(key)
                      .AsInt32(Default, x => x > 0);
            }
        }

        [Theory]
        [InlineData("23", 23)]
        [InlineData("35", 35)]
        [InlineData("-27", 27)]
        [InlineData("0", 0)]
        [InlineData("-0", 0)]
        [InlineData("1.23", Default)]
        [InlineData(null, Default)]
        [InlineData("", Default)]
        [InlineData("invalid", Default)]
        public void GetInt32_ReturnsTheExpectedValueWithConverter(string value, int expected)
        {
            const string key = "key_converter";
            var collection = new Dictionary<string, object> { { key, value } };
            var source = _factory.GetSource(collection);
            var actual = new ConfigurationBuilder(source, _telemetry)
                        .WithKeys(key)
                        .AsInt32(Default, validator: null, converter: _absConverter);

            actual.Should().Be(expected);
        }
    }

    public abstract class DoubleTestsBase
    {
        private const double Default = 42.0;
        private readonly IConfigurationSourceFactory _factory;
        private readonly Dictionary<string, object> _collection;
        private readonly IConfigurationSource _source;
        private readonly NullConfigurationTelemetry _telemetry = new();

        private readonly Func<string, ParsingResult<double>> _absConverter = val
            => val switch
            {
                { } x when TryParse(x, out var value) && value >= 0 => value,
                { } x when TryParse(x, out var value) && value < 0 => -value,
                _ => ParsingResult<double>.Failure(),
            };

        public DoubleTestsBase(IConfigurationSourceFactory factory)
        {
            _factory = factory;
            _collection = new Dictionary<string, object>()
            {
                { "key", 1.23 },
                { "integer", 1 },
                { "negative", -12.3 },
                { "negative_integer", -12 },
                { "zero", 0.0 },
                { "zero_integer", 0 },
                { "key_string", "1.23" },
                { "integer_string", "1" },
                { "negative_string", "-12.3" },
                { "negative_integer_string", "-12" },
                { "zero_string", "0.0" },
                { "zero_integer_string", "0" },
                { "invalid", "fdsfds" },
                { "key_with_null_value", null },
                { "key_with_empty_value", string.Empty },
            };
            _source = factory.GetSource(_collection);
        }

        [Fact]
        public void GetDouble_WorksTheSameAsNaiveApproachWithDefault()
        {
            var keys = _collection.Keys.Concat(new[] { "unknown" });

            foreach (var key in keys)
            {
                var expected = Result<double>.Try(() => Naive(key));
                var actual = Result<double>.Try(() => Builder(key));
                actual.Should().Be(expected, $"using key '{key}'");
            }

            double Naive(string key) => _source.GetDouble(key) ?? Default;

            double Builder(string key)
            {
                return new ConfigurationBuilder(_source, _telemetry)
                      .WithKeys(key)
                      .AsDouble(Default);
            }
        }

        [Fact]
        public void GetDouble_WorksTheSameAsNaiveApproachWithValidation()
        {
            var keys = _collection.Keys.Concat(new[] { "unknown" });

            foreach (var key in keys)
            {
                var expected = Result<double>.Try(() => Naive(key));
                var actual = Result<double>.Try(() => Builder(key).Value);
                actual.Should().Be(expected, $"using key '{key}'");
            }

            double Naive(string key)
            {
                var value = _source.GetDouble(key);
                return value is > 0 ? value.Value : Default;
            }

            double? Builder(string key)
            {
                return new ConfigurationBuilder(_source, _telemetry)
                      .WithKeys(key)
                      .AsDouble(Default, x => x > 0);
            }
        }

        [Theory]
        [InlineData("23.1", 23.1)]
        [InlineData("23", 23.0)]
        [InlineData("35.5", 35.5)]
        [InlineData("-27.1", 27.1)]
        [InlineData("-27", 27.0)]
        [InlineData("0", 0)]
        [InlineData("-0", 0)]
        [InlineData(null, Default)]
        [InlineData("", Default)]
        [InlineData("invalid", Default)]
        public void GetDouble_ReturnsTheExpectedValueWithConverter(string value, double expected)
        {
            const string key = "key_converter";
            var collection = new Dictionary<string, object> { { key, value } };
            var source = _factory.GetSource(collection);
            var actual = new ConfigurationBuilder(source, _telemetry)
                        .WithKeys(key)
                        .AsDouble(Default, validator: null, converter: _absConverter);

            actual.Should().Be(expected);
        }

        protected static bool TryParse(string txt, out double value)
        {
            return double.TryParse(txt, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
        }
    }

    public abstract class DictionaryTestsBase
    {
        private readonly Dictionary<string, object> _collection;
        private readonly IConfigurationSource _source;
        private readonly NullConfigurationTelemetry _telemetry = new();

        public DictionaryTestsBase(IConfigurationSourceFactory factory)
        {
            _collection = new Dictionary<string, object>()
            {
                { "key_no_spaces", "key1:value1,key2:value2,key3:value3" },
                { "key_with_spaces", "key1:value1, key2:value2, key3:value3" },
                { "trailing_semicolon", "key1:value1,key2:value2, key3:value3," },
                { "optional_mappings", "key1:,key2:, key3:value3," },
                { "single_value", "key1" },
                { "key_with_null_value", null },
                { "key_with_empty_value", string.Empty },
            };
            _source = factory.GetSource(_collection);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void GetDictionary_WorksTheSameAsNaiveApproachWithDefault(bool allowOptionalMappings)
        {
            var keys = _collection.Keys.Concat(new[] { "unknown" });

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

    public abstract class DictionaryObjectTestsBase
    {
        private readonly Dictionary<string, object> _collection;
        private readonly IConfigurationSource _source;
        private readonly NullConfigurationTelemetry _telemetry = new();

        public DictionaryObjectTestsBase(IConfigurationSourceFactory factory)
        {
            _collection = new Dictionary<string, object>()
            {
                { "key", new Dictionary<string, object> { { "key1", "value1" }, { "key2", "value2" }, { "key3", "value3" } } },
                { "single_value", new Dictionary<string, object> { { "key1", "value1" } } },
                { "empty", new Dictionary<string, object>() },
                { "missing_values", new Dictionary<string, object> { { "key1", null }, { "key2", string.Empty }, { "key3", "value3" } } },
                { "key_with_null_value", null },
            };
            _source = factory.GetSource(_collection);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void GetDictionary_WorksTheSameAsNaiveApproachWithDefault(bool allowOptionalMappings)
        {
            var keys = _collection.Keys.Concat(new[] { "unknown" });

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

    public class Result<T>
    {
        public Result(T value)
        {
            Value = value;
            IsSuccess = true;
            Error = null;
        }

        public Result(Exception error)
        {
            Error = error;
            IsSuccess = false;
        }

        public bool IsSuccess { get; }

        public T Value { get; }

        public Exception Error { get; }

        public static implicit operator Result<T>(T result) => Success(result);

        public static Result<T> Success(T value) => new(value);

        public static Result<T> Failure(Exception ex) => new(ex);

        public static Result<T> Try(Func<T> function)
        {
            try
            {
                return function();
            }
            catch (Exception ex)
            {
                return Failure(ex);
            }
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj.GetType() != this.GetType())
            {
                return false;
            }

            return Equals((Result<T>)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = IsSuccess.GetHashCode();
                hashCode = (hashCode * 397) ^ EqualityComparer<T>.Default.GetHashCode(Value);
                hashCode = (hashCode * 397) ^ (Error?.GetType() != null ? Error.GetType().GetHashCode() : 0);
                return hashCode;
            }
        }

        protected bool Equals(Result<T> other)
        {
            return IsSuccess == other.IsSuccess
                && ((Value is null && other.Value is null)
                 || (Value?.Equals(other.Value) ?? false))
                && ((Error is null && other.Error is null)
                 || (Error?.GetType() == other.Error?.GetType()));
        }
    }
}
