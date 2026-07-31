// <copyright file="SpanExtensionsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Data;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Util;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.ExtensionMethods
{
    public class SpanExtensionsTests
    {
        public SpanExtensionsTests()
        {
            // Reset the cache
            DbCommandCache.Cache.ResetForTests();
        }

        [Theory]
        [InlineData("Server=myServerName,myPortNumber;Database=myDataBase;User Id=myUsername;Password=myPassword;", "myDataBase", "myUsername", "myServerName,myPortNumber")]
        [InlineData("Server=myServerName,myPortNumber;Database=myDataBase;UserID=myUsername;Password=myPassword;", "myDataBase", "myUsername", "myServerName,myPortNumber")]
        [InlineData("Server=myServerName,myPortNumber;Database=myDataBase;User=myUsername;Password=myPassword;", "myDataBase", "myUsername", "myServerName,myPortNumber")]
        [InlineData("Server=myServerName,myPortNumber;Database=myDataBase;Uid=myUsername;Password=myPassword;", "myDataBase", "myUsername", "myServerName,myPortNumber")]
        [InlineData("Host Name=127.0.0.1;Port=5432;Database=myDataBase;User Id=myUsername;Password=myPassword;", "myDataBase", "myUsername", "127.0.0.1")]
        [InlineData("Hostname=127.0.0.1;Port=5432;Database=myDataBase;User Id=myUsername;Password=myPassword;", "myDataBase", "myUsername", "127.0.0.1")]
        [InlineData("Host=myServerName;Database=myDataBase;Username=myUsername;Password=myPassword;", "myDataBase", "myUsername", "myServerName")]
        [InlineData("Data Source=myServerName;Initial Catalog=myDataBase;User Name=myUsername;Password=myPassword;", "myDataBase", "myUsername", "myServerName")]
        [InlineData(@"Server=myServerName\myInstanceName;Database=myDataBase;User Id=myUsername;Password=myPassword;", "myDataBase", "myUsername", @"myServerName\myInstanceName")]
        [InlineData(@"Server=.\SQLExpress;AttachDbFilename=|DataDirectory|mydbfile.mdf;Database=dbname;Trusted_Connection=Yes;", "dbname", null, @".\SQLExpress")]
        public void ExtractProperTagsFromConnectionString(
            string connectionString,
            string expectedDbName,
            string expectedUserId,
            string expectedHost)
        {
            var commandTags = DbCommandCache.GetTagsFromDbCommand(CreateDbCommand(connectionString));
            commandTags.DbName.Should().Be(expectedDbName);
            commandTags.DbUser.Should().Be(expectedUserId);
            commandTags.OutHost.Should().Be(expectedHost);
        }

        [Fact]
        public void ShouldDisableCacheIfTooManyConnectionStrings()
        {
            const string connectionStringTemplate = "Server=myServerName{0};Database=myDataBase;User Id=myUsername;Password=myPassword;";

            // Fill-up the cache and test the logic with cache enabled
            for (int i = 0; i <= DbCommandCache.MaxConnectionStrings; i++)
            {
                var connectionString = string.Format(connectionStringTemplate, i);

                var commandTags = DbCommandCache.GetTagsFromDbCommand(CreateDbCommand(connectionString));

                DbCommandCache.Cache.IsCaching.Should().BeTrue();
                commandTags.OutHost.Should().Be("myServerName" + i);
            }

            // Test the logic with cache disabled
            for (int i = 0; i <= 10; i++)
            {
                var connectionString = string.Format(connectionStringTemplate, "NoCache" + i);

                var commandTags = DbCommandCache.GetTagsFromDbCommand(CreateDbCommand(connectionString));

                DbCommandCache.Cache.IsCaching.Should().BeFalse();
                commandTags.OutHost.Should().Be("myServerName" + "NoCache" + i);
            }
        }

        // With OTel semantics enabled the error status codes default to 500-599 for server
        // spans and 400-599 for client spans; otherwise client spans default to 400-499.
        // Under OTel semantics the error is described by error.type rather than error.msg.
        [Theory]
        // Server spans: the 500-599 default is the same either way
        [InlineData(true, true, 500, true, "500", null)]
        [InlineData(false, true, 500, true, null, "The HTTP response has status code 500.")]
        [InlineData(true, true, 404, false, null, null)]
        [InlineData(false, true, 404, false, null, null)]
        // Client spans: 5xx is only an error under OTel semantics
        [InlineData(true, false, 500, true, "500", null)]
        [InlineData(false, false, 500, false, null, null)]
        [InlineData(true, false, 404, true, "404", null)]
        [InlineData(false, false, 404, true, null, "The HTTP response has status code 404.")]
        // Success status codes are never errors
        [InlineData(true, true, 200, false, null, null)]
        [InlineData(false, true, 200, false, null, null)]
        public void SetHttpStatusCode_SetsErrorTagsForErrorStatusCodes(
            bool otelSemanticsEnabled,
            bool isServer,
            int statusCode,
            bool expectedError,
            string expectedErrorType,
            string expectedErrorMsg)
        {
            var span = CreateSpan(openTelemetrySemanticsEnabled: otelSemanticsEnabled);
            var settings = CreateMutableSettings(otelSemanticsEnabled);

            span.SetHttpStatusCode(statusCode, isServer, settings);

            span.Error.Should().Be(expectedError);
            span.GetTag(Tags.ErrorType).Should().Be(expectedErrorType);
            span.GetTag(Tags.ErrorMsg).Should().Be(expectedErrorMsg);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void SetHttpStatusCode_DoesNotOverwriteExistingErrorType(bool isServer)
        {
            const string existingErrorType = "System.InvalidOperationException";
            var span = CreateSpan(openTelemetrySemanticsEnabled: true);
            var settings = CreateMutableSettings(otelSemanticsEnabled: true);
            span.SetTag(Tags.ErrorType, existingErrorType);

            span.SetHttpStatusCode(500, isServer, settings);

            // Guards against the assertion below passing only because the status code was
            // never treated as an error in the first place.
            span.Error.Should().BeTrue();
            span.GetTag(Tags.ErrorType).Should().Be(existingErrorType);
        }

        private static MutableSettings CreateMutableSettings(bool otelSemanticsEnabled = false)
        {
            // Keep the settings in lockstep with the span's own flag: Tracer always passes
            // TracerSettings.OtelSemanticsEnabled into the Span constructor, so the two can
            // never disagree in production.
            var source = new NameValueConfigurationSource(new()
            {
                { ConfigurationKeys.OpenTelemetry.OtelSemanticsEnabled, otelSemanticsEnabled ? "true" : "false" },
            });

            return new TracerSettings(source).Manager.InitialMutableSettings;
        }

        private static Span CreateSpan(bool openTelemetrySemanticsEnabled = false)
            => new(
                new SpanContext(traceId: 1, spanId: 1),
                DateTimeOffset.UtcNow,
                tags: null,
                links: null,
                openTelemetrySemanticsEnabled);

        private static IDbCommand CreateDbCommand(string connectionString, string commandText = null)
        {
            var dbConnection = new Mock<IDbConnection>();
            dbConnection.SetupGet(c => c.ConnectionString).Returns(connectionString);

            var dbCommand = new Mock<IDbCommand>();
            dbCommand.SetupGet(c => c.Connection).Returns(dbConnection.Object);
            dbCommand.SetupGet(c => c.CommandText).Returns(commandText);

            return dbCommand.Object;
        }
    }
}
