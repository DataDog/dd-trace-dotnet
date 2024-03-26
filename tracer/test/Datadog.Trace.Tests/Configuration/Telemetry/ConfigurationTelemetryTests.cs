// <copyright file="ConfigurationTelemetryTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Telemetry;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Configuration;

public class ConfigurationTelemetryTests
{
    [Fact]
    public void Record_RecordsTelemetryValues()
    {
        var stringValues = new List<ConfigDto>
        {
            new("string1", null, ConfigurationOrigins.Code),
            new("string2", "value", ConfigurationOrigins.AppConfig),
            new("string3", "value", ConfigurationOrigins.DdConfig, error: TelemetryErrorCode.FailedValidation),
            new("string4", "overridden", ConfigurationOrigins.RemoteConfig),
            new("string4", "newvalue", ConfigurationOrigins.EnvVars),
            new("redacted1", null, ConfigurationOrigins.Code, recordValue: false),
            new("redacted2", "value", ConfigurationOrigins.AppConfig, recordValue: false),
            new("redacted3", "value", ConfigurationOrigins.DdConfig, recordValue: false, TelemetryErrorCode.FailedValidation),
            new("redacted4", "overridden", ConfigurationOrigins.RemoteConfig, recordValue: false),
            new("redacted4", "newvalue", ConfigurationOrigins.EnvVars, recordValue: false),
        };

        var boolValues = new List<ConfigDto>
        {
            new("bool", false, ConfigurationOrigins.EnvVars),
            new("bool", true, ConfigurationOrigins.Code),
        };

        var intValues = new List<ConfigDto>
        {
            new("int", 123, ConfigurationOrigins.EnvVars),
            new("int", 42, ConfigurationOrigins.Code),
        };

        var doubleValues = new List<ConfigDto>
        {
            new("double", 123.0, ConfigurationOrigins.EnvVars),
            new("double", 42.0, ConfigurationOrigins.Code),
        };

        var telemetry = new ConfigurationTelemetry();
        foreach (var val in stringValues)
        {
            telemetry.Record(val.Name, (string)val.Value, recordValue: val.RecordValue, val.Origin, val.Error);
        }

        foreach (var val in boolValues)
        {
            telemetry.Record(val.Name, (bool)val.Value, val.Origin, val.Error);
        }

        foreach (var val in intValues)
        {
            telemetry.Record(val.Name, (int)val.Value, val.Origin, val.Error);
        }

        foreach (var val in doubleValues)
        {
            telemetry.Record(val.Name, (double)val.Value, val.Origin, val.Error);
        }

        var expected = stringValues
                      .Select(
                           x =>
                           {
                               if (!x.RecordValue)
                               {
                                   x.Value = "<redacted>";
                               }

                               return x;
                           })
                      .Concat(boolValues)
                      .Concat(intValues)
                      .Concat(doubleValues)
                      .ToList();

        var actual = telemetry.GetQueueForTesting()
                              .OrderBy(x => x.SeqId)
                              .Select(x => new ConfigDto(x))
                              .ToList();

        actual.Should().BeEquivalentTo(expected);
    }

    internal class ConfigDto
    {
        public ConfigDto(ConfigurationTelemetry.ConfigurationTelemetryEntry entry)
        {
            Name = entry.Key;
            Value = entry.Type switch
            {
                ConfigurationTelemetry.ConfigurationTelemetryEntryType.String => entry.StringValue,
                ConfigurationTelemetry.ConfigurationTelemetryEntryType.Redacted => "<redacted>",
                ConfigurationTelemetry.ConfigurationTelemetryEntryType.Bool => entry.BoolValue,
                ConfigurationTelemetry.ConfigurationTelemetryEntryType.Int => entry.IntValue,
                ConfigurationTelemetry.ConfigurationTelemetryEntryType.Double => entry.DoubleValue,
                _ => new InvalidOperationException("Unknown entry type" + entry.Type),
            };
            RecordValue = entry.Type != ConfigurationTelemetry.ConfigurationTelemetryEntryType.Redacted;
            Origin = entry.Origin;
            Error = entry.Error;
        }

        public ConfigDto(string name, object value, ConfigurationOrigins origin, bool recordValue = true, TelemetryErrorCode? error = null)
        {
            Name = name;
            Value = value;
            RecordValue = recordValue;
            Origin = origin;
            Error = error;
        }

        public string Name { get; set; }

        public object Value { get; set; }

        public bool RecordValue { get; }

        public ConfigurationOrigins Origin { get; set; }

        public TelemetryErrorCode? Error { get; set; }
    }
}
