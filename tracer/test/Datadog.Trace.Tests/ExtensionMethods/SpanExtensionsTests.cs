// <copyright file="SpanExtensionsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Data;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Util;
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
            Assert.Equal(expectedDbName, commandTags.DbName);
            Assert.Equal(expectedUserId, commandTags.DbUser);
            Assert.Equal(expectedHost, commandTags.OutHost);
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

                Assert.True(DbCommandCache.Cache.IsCaching);
                Assert.Equal("myServerName" + i, commandTags.OutHost);
            }

            // Test the logic with cache disabled
            for (int i = 0; i <= 10; i++)
            {
                var connectionString = string.Format(connectionStringTemplate, "NoCache" + i);

                var commandTags = DbCommandCache.GetTagsFromDbCommand(CreateDbCommand(connectionString));

                Assert.False(DbCommandCache.Cache.IsCaching);
                Assert.Equal("myServerName" + "NoCache" + i, commandTags.OutHost);
            }
        }

        [Theory]
        [InlineData(true, 500, "500")]
        [InlineData(true, 200, null)]
        [InlineData(false, 500, null)]
        [InlineData(false, 200, null)]
        public void SetHttpStatusCode_SetsErrorTypeWhenOtelSemanticsEnabledForErrorStatusCode(
            bool otelSemanticsEnabled,
            int statusCode,
            string expectedErrorType)
        {
            var span = CreateSpan(openTelemetrySemanticsEnabled: otelSemanticsEnabled);
            var settings = CreateMutableSettings();

            span.SetHttpStatusCode(statusCode, isServer: true, settings);

            Assert.Equal(expectedErrorType, span.GetTag(Tags.ErrorType));
        }

        [Fact]
        public void SetHttpStatusCode_ForServerSpan_DoesNotOverwriteExistingErrorType()
        {
            const string existingErrorType = "System.InvalidOperationException";
            var span = CreateSpan(openTelemetrySemanticsEnabled: true);
            var settings = CreateMutableSettings();
            span.SetTag(Tags.ErrorType, existingErrorType);

            span.SetHttpStatusCode(500, isServer: true, settings);

            Assert.Equal(existingErrorType, span.GetTag(Tags.ErrorType));
        }

        [Fact]
        public void SetHttpStatusCode_ForClientSpan_DoesNotOverwriteExistingErrorType()
        {
            const string existingErrorType = "System.InvalidOperationException";
            var span = CreateSpan(openTelemetrySemanticsEnabled: true);
            var settings = CreateMutableSettings();
            span.SetTag(Tags.ErrorType, existingErrorType);

            span.SetHttpStatusCode(500, isServer: false, settings);

            Assert.Equal(existingErrorType, span.GetTag(Tags.ErrorType));
        }

        private static MutableSettings CreateMutableSettings()
            => new TracerSettings().Manager.InitialMutableSettings;

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
