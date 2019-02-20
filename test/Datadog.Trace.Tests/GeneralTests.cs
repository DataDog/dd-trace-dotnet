using System;
using System.Data;
using System.Linq;
using Datadog.Trace.Enums;
using Datadog.Trace.ExtensionMethods;
using Xunit;

namespace Datadog.Trace.Tests
{
    public class GeneralTests
    {
        [Fact]
        public void AllDbTypesAreMappedToTagNames()
        {
            var dbTypeValues = Enum.GetValues(typeof(DbProviderType)).Cast<DbProviderType>();

            foreach (var dbType in dbTypeValues)
            {
                string dbTypeTagName = null;

                try
                {
                    dbTypeTagName = dbType.ToTagName();
                }
                catch (ArgumentOutOfRangeException)
                {
                    dbTypeTagName = null;
                }

                Assert.True(!string.IsNullOrEmpty(dbTypeTagName), $"dbType [{dbType.ToString()}] does not return a valid tag name and/or is not mapped.");
            }
        }

        [Fact]
        public void UnknownDbProviderTypesReturnsNullTagName()
        {
            var unknownTagName = DbProviderType.Unknown.ToTagName();
            Assert.Null(unknownTagName);
        }

        [Fact]
        public void UnmappedIDbCommandReturnsCorrectDefaultTagName()
        {
            var testCommand = new GeneralTestDbCommand();

            var tagName = testCommand.ToTagName();

            Assert.Equal("generaltestdb", tagName);
        }

        [Fact]
        public void UnmappedIDbCommandReturnsUnknownType()
        {
            var testCommand = new GeneralTestDbCommand();

            var dbType = testCommand.ToDbProviderType();

            Assert.Equal(DbProviderType.Unknown, dbType);
        }

        private class GeneralTestDbCommand : IDbCommand
        {
            public IDbConnection Connection { get; set; }

            public IDbTransaction Transaction { get; set; }

            public string CommandText { get; set; }

            public int CommandTimeout { get; set; }

            public CommandType CommandType { get; set; }

            public IDataParameterCollection Parameters { get; }

            public UpdateRowSource UpdatedRowSource { get; set; }

            public void Dispose()
            {
            }

            public void Prepare()
            {
                throw new NotImplementedException();
            }

            public void Cancel()
            {
                throw new NotImplementedException();
            }

            public IDbDataParameter CreateParameter() => throw new NotImplementedException();

            public int ExecuteNonQuery() => throw new NotImplementedException();

            public IDataReader ExecuteReader() => throw new NotImplementedException();

            public IDataReader ExecuteReader(CommandBehavior behavior) => throw new NotImplementedException();

            public object ExecuteScalar() => throw new NotImplementedException();
        }
    }
}
