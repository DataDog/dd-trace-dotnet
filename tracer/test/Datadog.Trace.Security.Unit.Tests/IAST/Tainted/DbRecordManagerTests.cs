// <copyright file="DbRecordManagerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Iast;
using Datadog.Trace.Iast.Settings;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests.Iast.Tainted;

public class DbRecordManagerTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    public void GivenDataReader_WhenRowRead_RightAmountOfRowsAreTainted(int rowLimit)
    {
        var settings = new CustomSettingsForTests(new Dictionary<string, object>()
        {
            { ConfigurationKeys.Iast.Enabled, true },
            { ConfigurationKeys.Iast.DataBaseRowsToTaint, rowLimit }
        });
        var recordsManager = new IastModule.DbRecordManager(new IastSettings(settings, NullConfigurationTelemetry.Instance));

        object instance = new object();
        for (int row = 0; row < 10; row++)
        {
            recordsManager.RegisterDbRecord(instance);
            bool shouldTaint = row < rowLimit;
            for (int field = 0; field < 10; field++)
            {
                var value = $"value{field}";
                recordsManager.AddDbValue(instance, field.ToString(), value).Should().Be(shouldTaint, $"Row: {row} Field: {field}");
            }
        }

        IastModule.UnregisterDbRecord(instance);
    }
}
