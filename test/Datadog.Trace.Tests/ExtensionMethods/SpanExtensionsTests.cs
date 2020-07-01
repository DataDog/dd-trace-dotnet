using System.Data;
using Datadog.Trace.ExtensionMethods;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.ExtensionMethods
{
    public class SpanExtensionsTests
    {
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

            var dbConnection = new Mock<IDbConnection>();
            dbConnection.SetupGet(c => c.ConnectionString).Returns(connectionString);

            var dbCommand = new Mock<IDbCommand>();
            dbCommand.SetupGet(c => c.Connection).Returns(dbConnection.Object);

            span.AddTagsFromDbCommand(dbCommand.Object);

            Assert.Equal(span.GetTag(Tags.DbName), expectedDbName);
            Assert.Equal(span.GetTag(Tags.DbUser), expectedUserId);
            Assert.Equal(span.GetTag(Tags.OutHost), expectedHost);
        }

        [Fact]
        public void SetSpanTypeToSql()
        {
            const string connectionString = "Server=myServerName,myPortNumber;Database=myDataBase;User Id=myUsername;Password=myPassword;";
            const string commandText = "SELECT * FROM Table ORDER BY id";

            var spanContext = new SpanContext(Mock.Of<ISpanContext>(), Mock.Of<ITraceContext>(), "test");
            var span = new Span(spanContext, null);

            var dbConnection = new Mock<IDbConnection>();
            dbConnection.SetupGet(c => c.ConnectionString).Returns(connectionString);

            var dbCommand = new Mock<IDbCommand>();
            dbCommand.SetupGet(c => c.Connection).Returns(dbConnection.Object);
            dbCommand.SetupGet(c => c.CommandText).Returns(commandText);

            span.AddTagsFromDbCommand(dbCommand.Object);

            Assert.Equal(SpanTypes.Sql, span.Type);
            Assert.Equal(commandText, span.ResourceName);
        }
    }
}
