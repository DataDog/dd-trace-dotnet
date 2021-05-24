// <copyright file="SpanExtensionsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
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
            DbCommandCache.Cache = new ConcurrentDictionary<string, KeyValuePair<string, string>[]>();
        }

        [Theory]
        [InlineData("Server=myServerName,myPortNumber;Database=myDataBase;User Id=myUsername;Password=myPassword;", "myDataBase", "myUsername", "myServerName,myPortNumber")]
        [InlineData(@"Server=myServerName\myInstanceName;Database=myDataBase;User Id=myUsername;Password=myPassword;", "myDataBase", "myUsername", @"myServerName\myInstanceName")]
        [InlineData(@"Server=.\SQLExpress;AttachDbFilename=|DataDirectory|mydbfile.mdf;Database=dbname;Trusted_Connection=Yes;", "dbname", null, @".\SQLExpress")]
        public void ExtractProperTagsFromConnectionString(
            string connectionString,
            string expectedDbName,
            string expectedUserId,
            string expectedHost)
        {
            var spanContext = new SpanContext(Mock.Of<ISpanContext>(), Mock.Of<ITraceContext>(), "test");
            var span = new Span(spanContext, null);

            span.AddTagsFromDbCommand(CreateDbCommand(connectionString));

            Assert.Equal(expectedDbName, span.GetTag(Tags.DbName));
            Assert.Equal(expectedUserId, span.GetTag(Tags.DbUser));
            Assert.Equal(expectedHost, span.GetTag(Tags.OutHost));
        }

        [Fact]
        public void SetSpanTypeToSql()
        {
            const string connectionString = "Server=myServerName;Database=myDataBase;User Id=myUsername;Password=myPassword;";
            const string commandText = "SELECT * FROM Table ORDER BY id";

            var spanContext = new SpanContext(Mock.Of<ISpanContext>(), Mock.Of<ITraceContext>(), "test");
            var span = new Span(spanContext, null);

            span.AddTagsFromDbCommand(CreateDbCommand(connectionString, commandText));

            Assert.Equal(SpanTypes.Sql, span.Type);
            Assert.Equal(commandText, span.ResourceName);
        }

        [Fact]
        public void ShouldDisableCacheIfTooManyConnectionStrings()
        {
            const string connectionStringTemplate = "Server=myServerName{0};Database=myDataBase;User Id=myUsername;Password=myPassword;";

            var spanContext = new SpanContext(Mock.Of<ISpanContext>(), Mock.Of<ITraceContext>(), "test");
            var span = new Span(spanContext, null);

            // Fill-up the cache and test the logic with cache enabled
            for (int i = 0; i <= DbCommandCache.MaxConnectionStrings; i++)
            {
                var connectionString = string.Format(connectionStringTemplate, i);

                span.AddTagsFromDbCommand(CreateDbCommand(connectionString));

                Assert.NotNull(DbCommandCache.Cache);
                Assert.Equal("myServerName" + i, span.GetTag(Tags.OutHost));
            }

            // Test the logic with cache disabled
            for (int i = 0; i <= 10; i++)
            {
                var connectionString = string.Format(connectionStringTemplate, "NoCache" + i);

                span.AddTagsFromDbCommand(CreateDbCommand(connectionString));

                Assert.Null(DbCommandCache.Cache);
                Assert.Equal("myServerName" + "NoCache" + i, span.GetTag(Tags.OutHost));
            }
        }

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
