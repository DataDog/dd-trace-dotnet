// <copyright file="SpanExtensionsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Data;
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
            DbCommandCache.Cache = new();
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

                Assert.NotNull(DbCommandCache.Cache);
                Assert.Equal("myServerName" + i, commandTags.OutHost);
            }

            // Test the logic with cache disabled
            for (int i = 0; i <= 10; i++)
            {
                var connectionString = string.Format(connectionStringTemplate, "NoCache" + i);

                var commandTags = DbCommandCache.GetTagsFromDbCommand(CreateDbCommand(connectionString));

                Assert.Null(DbCommandCache.Cache);
                Assert.Equal("myServerName" + "NoCache" + i, commandTags.OutHost);
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
