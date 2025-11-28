// <copyright file="DebuggerSettingsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Debugger;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Debugger
{
    public class DebuggerSettingsTests
    {
        [Theory]
        [InlineData("-1")]
        [InlineData("0")]
        [InlineData("")]
        [InlineData(null)]
        public void InvalidMaxDepthToSerialize_DefaultUsed(string value)
        {
            var settings = new DebuggerSettings(
                new NameValueConfigurationSource(new() { { ConfigurationKeys.Debugger.MaxDepthToSerialize, value }, }),
                NullConfigurationTelemetry.Instance);

            settings.MaximumDepthOfMembersToCopy.Should().Be(3);
        }

        [Theory]
        [InlineData("-1")]
        [InlineData("0")]
        [InlineData("")]
        [InlineData(null)]
        public void InvalidSerializationTimeThreshold_DefaultUsed(string value)
        {
            var settings = new DebuggerSettings(
                new NameValueConfigurationSource(new() { { ConfigurationKeys.Debugger.MaxTimeToSerialize, value }, }),
                NullConfigurationTelemetry.Instance);

            settings.MaxSerializationTimeInMilliseconds.Should().Be(200);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("false")]
        public void DynamicInstrumentationDisabled(string enabled)
        {
            var settings = new DebuggerSettings(
                new NameValueConfigurationSource(new() { { ConfigurationKeys.Debugger.DynamicInstrumentationEnabled, enabled }, }),
                NullConfigurationTelemetry.Instance);

            settings.DynamicInstrumentationEnabled.Should().BeFalse();
        }

        [Theory]
        [InlineData("false")]
        [InlineData("0")]
        public void SymbolsDisabled(string enabled)
        {
            var settings = new DebuggerSettings(
                new NameValueConfigurationSource(new() { { ConfigurationKeys.Debugger.SymbolDatabaseUploadEnabled, enabled }, }),
                NullConfigurationTelemetry.Instance);

            settings.SymbolDatabaseUploadEnabled.Should().BeFalse();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("1")]
        [InlineData("true")]
        public void SymbolsEnabled(string enabled)
        {
            var settings = new DebuggerSettings(
                new NameValueConfigurationSource(new() { { ConfigurationKeys.Debugger.SymbolDatabaseUploadEnabled, enabled }, }),
                NullConfigurationTelemetry.Instance);

            settings.SymbolDatabaseUploadEnabled.Should().BeTrue();
        }

        [Fact]
        public void DebuggerSettings_UseSettings()
        {
            var settings = new DebuggerSettings(
                new NameValueConfigurationSource(new()
                {
                    { ConfigurationKeys.Debugger.DynamicInstrumentationEnabled, "true" },
                    { ConfigurationKeys.Debugger.SymbolDatabaseUploadEnabled, "true" },
                    { ConfigurationKeys.Debugger.MaxDepthToSerialize, "100" },
                    { ConfigurationKeys.Debugger.MaxTimeToSerialize, "1000" },
                }),
                NullConfigurationTelemetry.Instance);

            settings.DynamicInstrumentationEnabled.Should().BeTrue();
            settings.SymbolDatabaseCompressionEnabled.Should().BeTrue();
            settings.SymbolDatabaseUploadEnabled.Should().BeTrue();
            settings.MaximumDepthOfMembersToCopy.Should().Be(100);
            settings.MaxSerializationTimeInMilliseconds.Should().Be(1000);
        }

        [Theory]
        [InlineData("-1")]
        [InlineData("0")]
        [InlineData("")]
        [InlineData(null)]
        public void InvalidUploadBatchSize_DefaultUsed(string value)
        {
            var settings = new DebuggerSettings(
                new NameValueConfigurationSource(new() { { ConfigurationKeys.Debugger.UploadBatchSize, value }, }),
                NullConfigurationTelemetry.Instance);

            settings.UploadBatchSize.Should().Be(100);
        }

        [Theory]
        [InlineData("-1")]
        [InlineData("0")]
        [InlineData("")]
        [InlineData(null)]
        public void InvalidMaxSymbolSizeToUpload_DefaultUsed(string value)
        {
            var settings = new DebuggerSettings(
                new NameValueConfigurationSource(new() { { ConfigurationKeys.Debugger.SymbolDatabaseBatchSizeInBytes, value }, }),
                NullConfigurationTelemetry.Instance);

            settings.SymbolDatabaseBatchSizeInBytes.Should().Be(100000);
        }

        [Theory]
        [InlineData("-1")]
        [InlineData("0")]
        [InlineData("")]
        [InlineData(null)]
        public void InvalidDiagnosticsInterval_DefaultUsed(string value)
        {
            var settings = new DebuggerSettings(
                new NameValueConfigurationSource(new() { { ConfigurationKeys.Debugger.DiagnosticsInterval, value }, }),
                NullConfigurationTelemetry.Instance);

            settings.DiagnosticsIntervalSeconds.Should().Be(3600);
        }

        [Theory]
        [InlineData("-1")]
        [InlineData("0")]
        [InlineData("")]
        [InlineData(null)]
        public void InvalidUploadFlushInterval_DefaultUsed(string value)
        {
            var settings = new DebuggerSettings(
                new NameValueConfigurationSource(new() { { ConfigurationKeys.Debugger.UploadFlushInterval, value }, }),
                NullConfigurationTelemetry.Instance);

            settings.UploadFlushIntervalMilliseconds.Should().Be(0);
        }

        [Theory]
        [InlineData("/path/to/probes.json")]
        [InlineData("C:\\probes\\config.json")]
        [InlineData("probes.json")]
        public void ProbeFile_ParsesCorrectly(string probeFilePath)
        {
            var settings = new DebuggerSettings(
                new NameValueConfigurationSource(new()
                {
                    { ConfigurationKeys.Debugger.DynamicInstrumentationProbeFile, probeFilePath }
                }),
                NullConfigurationTelemetry.Instance);

            settings.ProbeFile.Should().Be(probeFilePath);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void ProbeFile_EmptyOrNull(string probeFilePath)
        {
            var settings = new DebuggerSettings(
                new NameValueConfigurationSource(new()
                {
                    { ConfigurationKeys.Debugger.DynamicInstrumentationProbeFile, probeFilePath }
                }),
                NullConfigurationTelemetry.Instance);

            settings.ProbeFile.Should().BeEmpty();
        }

        public class DebuggerSettingsCodeOriginTests
        {
            [Theory]
            [InlineData("")]
            [InlineData("False")]
            [InlineData("false")]
            [InlineData("0")]
            [InlineData("2")]
            [InlineData(null)]
            public void CodeOriginEnabled_False(string value)
            {
                var settings = new DebuggerSettings(
                    new NameValueConfigurationSource(new() { { ConfigurationKeys.Debugger.CodeOriginForSpansEnabled, value }, }),
                    NullConfigurationTelemetry.Instance);

                settings.CodeOriginForSpansEnabled.Should().BeFalse();
            }

            [Theory]
            [InlineData("True")]
            [InlineData("true")]
            [InlineData("1")]
            public void CodeOriginEnabled_True(string value)
            {
                var settings = new DebuggerSettings(
                    new NameValueConfigurationSource(new() { { ConfigurationKeys.Debugger.CodeOriginForSpansEnabled, value }, }),
                    NullConfigurationTelemetry.Instance);

                settings.CodeOriginForSpansEnabled.Should().BeTrue();
            }

            [Theory]
            [InlineData("8")]
            [InlineData("1")]
            [InlineData("1000")]
            public void CodeOriginMaxUserFrames(string value)
            {
                var settings = new DebuggerSettings(
                    new NameValueConfigurationSource(new() { { ConfigurationKeys.Debugger.CodeOriginMaxUserFrames, value }, }),
                    NullConfigurationTelemetry.Instance);

                settings.CodeOriginMaxUserFrames.Should().Be(int.Parse(value));
            }

            [Theory]
            [InlineData("-1")]
            [InlineData("0")]
            [InlineData("")]
            [InlineData(null)]
            public void InvalidCodeOriginMaxUserFrames_DefaultUsed(string value)
            {
                var settings = new DebuggerSettings(
                    new NameValueConfigurationSource(new() { { ConfigurationKeys.Debugger.CodeOriginMaxUserFrames, value }, }),
                    NullConfigurationTelemetry.Instance);

                settings.CodeOriginMaxUserFrames.Should().Be(8);
            }
        }

        public class DebuggerSettingsRedactionTests
        {
            [Theory]
            [InlineData("id,name,email", new[] { "id", "name", "email" })]
            [InlineData("id,ID,Id", new[] { "id" })] // Tests case insensitive uniqueness
            [InlineData("id,,name", new[] { "id", "name" })] // Tests empty entries removal
            [InlineData(" id , name ", new[] { "id", "name" })] // Tests trimming
            [InlineData("", new string[0])]
            [InlineData(null, new string[0])]
            public void RedactedIdentifiers_ParsesCorrectly(string value, string[] expected)
            {
                var settings = new DebuggerSettings(
                    new NameValueConfigurationSource(new()
                    {
                        { ConfigurationKeys.Debugger.RedactedIdentifiers, value }
                    }),
                    NullConfigurationTelemetry.Instance);

                settings.RedactedIdentifiers.Should().BeEquivalentTo(
                    expected,
                    options => options.Using<string>(
                                           ctx =>
                                               string.Equals(ctx.Subject, ctx.Expectation, StringComparison.OrdinalIgnoreCase))
                                      .WhenTypeIs<string>());
            }

            [Theory]
            [InlineData("password", "token", new[] { "password", "token" })]
            [InlineData("password", "", new[] { "password" })]
            [InlineData("", "token", new[] { "token" })]
            [InlineData(null, null, new string[0])]
            public void RedactedExcludedIdentifiers_CombinesBothSources(
                string excludedIds,
                string redactionExcludedIds,
                string[] expected)
            {
                var settings = new DebuggerSettings(
                    new NameValueConfigurationSource(new()
                    {
                { ConfigurationKeys.Debugger.RedactedExcludedIdentifiers, excludedIds },
                { ConfigurationKeys.Debugger.RedactionExcludedIdentifiers, redactionExcludedIds }
                    }),
                    NullConfigurationTelemetry.Instance);

                settings.RedactedExcludedIdentifiers.Should().BeEquivalentTo(expected);
            }

            [Theory]
            [InlineData("System.String,System.Int32", new[] { "System.String", "System.Int32" })]
            [InlineData("System.String,SYSTEM.STRING,system.string", new[] { "System.String" })] // Tests case insensitive uniqueness
            [InlineData("System.String,,System.Int32", new[] { "System.String", "System.Int32" })] // Tests empty entries removal
            [InlineData(" System.String , System.Int32 ", new[] { "System.String", "System.Int32" })] // Tests trimming
            [InlineData("", new string[0])]
            [InlineData(null, new string[0])]
            public void RedactedTypes_ParsesCorrectly(string value, string[] expected)
            {
                var settings = new DebuggerSettings(
                    new NameValueConfigurationSource(new()
                    {
                        { ConfigurationKeys.Debugger.RedactedTypes, value }
                    }),
                    NullConfigurationTelemetry.Instance);

                settings.RedactedTypes.Should().BeEquivalentTo(
                    expected,
                    options => options.Using<string>(
                                           ctx =>
                                               string.Equals(ctx.Subject, ctx.Expectation, StringComparison.OrdinalIgnoreCase))
                                      .WhenTypeIs<string>());
            }

            [Fact]
            public void RedactedExcludedIdentifiers_RemovesDuplicates()
            {
                var settings = new DebuggerSettings(
                    new NameValueConfigurationSource(new()
                    {
                { ConfigurationKeys.Debugger.RedactedExcludedIdentifiers, "token,TOKEN" },
                { ConfigurationKeys.Debugger.RedactionExcludedIdentifiers, "Token,secret" }
                    }),
                    NullConfigurationTelemetry.Instance);

                settings.RedactedExcludedIdentifiers.Should().BeEquivalentTo(new[] { "token", "secret" });
            }

            [Fact]
            public void AllRedactionCollections_AreNotNull_WhenConfigIsNull()
            {
                var settings = new DebuggerSettings(
                    new NameValueConfigurationSource(new()),
                    NullConfigurationTelemetry.Instance);

                settings.RedactedIdentifiers.Should().NotBeNull();
                settings.RedactedExcludedIdentifiers.Should().NotBeNull();
                settings.RedactedTypes.Should().NotBeNull();
            }
        }
    }
}
