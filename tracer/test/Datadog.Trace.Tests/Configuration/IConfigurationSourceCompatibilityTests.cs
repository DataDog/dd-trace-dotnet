// <copyright file="IConfigurationSourceCompatibilityTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.ConfigurationSources.Telemetry;
using Datadog.Trace.Configuration.Telemetry;
using Xunit;

namespace Datadog.Trace.Tests.Configuration;

public class IConfigurationSourceCompatibilityTests
{
    [Fact]
    public void ExistingContractRemainsImplementable()
    {
        IConfigurationSource source = new PreExistingConfigurationSource();

        var result = source.GetString("missing", NullConfigurationTelemetry.Instance, validator: null, recordValue: true);

        Assert.False(result.IsPresent);
    }

    private sealed class PreExistingConfigurationSource : IConfigurationSource
    {
        public ConfigurationOrigins Origin => ConfigurationOrigins.Code;

        public ConfigurationResult<string> GetString(string key, IConfigurationTelemetry telemetry, Func<string, bool>? validator, bool recordValue)
            => ConfigurationResult<string>.NotFound();

        public ConfigurationResult<int> GetInt32(string key, IConfigurationTelemetry telemetry, Func<int, bool>? validator)
            => ConfigurationResult<int>.NotFound();

        public ConfigurationResult<double> GetDouble(string key, IConfigurationTelemetry telemetry, Func<double, bool>? validator)
            => ConfigurationResult<double>.NotFound();

        public ConfigurationResult<bool> GetBool(string key, IConfigurationTelemetry telemetry, Func<bool, bool>? validator)
            => ConfigurationResult<bool>.NotFound();

        public ConfigurationResult<IDictionary<string, string>> GetDictionary(string key, IConfigurationTelemetry telemetry, Func<IDictionary<string, string>, bool>? validator)
            => ConfigurationResult<IDictionary<string, string>>.NotFound();

        public ConfigurationResult<IDictionary<string, string>> GetDictionary(string key, IConfigurationTelemetry telemetry, Func<IDictionary<string, string>, bool>? validator, bool allowOptionalMappings, char separator)
            => ConfigurationResult<IDictionary<string, string>>.NotFound();

        public ConfigurationResult<IDictionary<string, string>> GetDictionary(string key, IConfigurationTelemetry telemetry, Func<IDictionary<string, string>, bool>? validator, Func<string, IDictionary<string, string>> parser)
            => ConfigurationResult<IDictionary<string, string>>.NotFound();

        public ConfigurationResult<T> GetAs<T>(string key, IConfigurationTelemetry telemetry, Func<string, ParsingResult<T>> converter, Func<T, bool>? validator, bool recordValue)
            => ConfigurationResult<T>.NotFound();
    }
}
