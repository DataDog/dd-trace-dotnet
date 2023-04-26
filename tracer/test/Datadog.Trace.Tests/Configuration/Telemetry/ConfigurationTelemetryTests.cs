// <copyright file="ConfigurationTelemetryTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Datadog.Trace.Configuration.Telemetry;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Configuration;

public class ConfigurationTelemetryTests
{
    [Fact]
    public void Record_RecordsTelemetryValues()
    {
        var i = 0;
        var stringValues = new List<ConfigDto>
        {
            new("string1", null, ConfigurationOrigins.Code, Interlocked.Increment(ref i)),
            new("string2", "value", ConfigurationOrigins.AppConfig, Interlocked.Increment(ref i)),
            new("string3", "value", ConfigurationOrigins.DdConfig, Interlocked.Increment(ref i), error: ConfigurationTelemetryErrorCode.FailedValidation),
            new("string4", "overridden", ConfigurationOrigins.RemoteConfig, Interlocked.Increment(ref i)),
            new("string4", "newvalue", ConfigurationOrigins.EnvVars, Interlocked.Increment(ref i)),
            new("redacted1", null, ConfigurationOrigins.Code, Interlocked.Increment(ref i), recordValue: false),
            new("redacted2", "value", ConfigurationOrigins.AppConfig, Interlocked.Increment(ref i), recordValue: false),
            new("redacted3", "value", ConfigurationOrigins.DdConfig, Interlocked.Increment(ref i), recordValue: false, ConfigurationTelemetryErrorCode.FailedValidation),
            new("redacted4", "overridden", ConfigurationOrigins.RemoteConfig, Interlocked.Increment(ref i), recordValue: false),
            new("redacted4", "newvalue", ConfigurationOrigins.EnvVars, Interlocked.Increment(ref i), recordValue: false),
        };

        var boolValues = new List<ConfigDto>
        {
            new("bool", false, ConfigurationOrigins.EnvVars, Interlocked.Increment(ref i)),
            new("bool", true, ConfigurationOrigins.Code, Interlocked.Increment(ref i)),
        };

        var intValues = new List<ConfigDto>
        {
            new("int", 123, ConfigurationOrigins.EnvVars, Interlocked.Increment(ref i)),
            new("int", 42, ConfigurationOrigins.Code, Interlocked.Increment(ref i)),
        };

        var doubleValues = new List<ConfigDto>
        {
            new("double", 123.0, ConfigurationOrigins.EnvVars, Interlocked.Increment(ref i)),
            new("double", 42.0, ConfigurationOrigins.Code, Interlocked.Increment(ref i)),
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
                      .OrderBy(x => x.SeqId)
                      .ToList();

        var actual = telemetry.GetLatest()
                              .Select(x => new ConfigDto(x))
                              .OrderBy(x => x.SeqId)
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
            SeqId = entry.SeqId;
        }

        public ConfigDto(string name, object value, ConfigurationOrigins origin, long seqId, bool recordValue = true, ConfigurationTelemetryErrorCode? error = null)
        {
            Name = name;
            Value = value;
            RecordValue = recordValue;
            Origin = origin;
            Error = error;
            SeqId = seqId;
        }

        public string Name { get; set; }

        public object Value { get; set; }

        public bool RecordValue { get; }

        public ConfigurationOrigins Origin { get; set; }

        public ConfigurationTelemetryErrorCode? Error { get; set; }

        public long SeqId { get; set; }
    }
}
