using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
using System.Data.Common;
using Datadog.Trace.Configuration;
using MySql.Data.MySqlClient;
using Npgsql;
using Xunit;

namespace Datadog.Trace.ClrProfiler.Managed.Tests
{
    public class ScopeDBFactoryTests
    {
        public static IEnumerable<object[]> GetDbCommandScopeData()
        {
            yield return new object[] { new System.Data.SqlClient.SqlCommand() };
            yield return new object[] { new MySqlCommand() };
            yield return new object[] { new NpgsqlCommand() };
#if !NET452
            yield return new object[] { new Microsoft.Data.SqlClient.SqlCommand() };
#endif
        }

        [Theory]
        [MemberData(nameof(GetDbCommandScopeData))]
        public void CreateDbCommandScope_ReturnsNullForExcludedAdoNetTypes(IDbCommand command)
        {
            // Set up tracer
            var collection = new NameValueCollection
            {
                { ConfigurationKeys.AdoNetExcludedTypes, command.GetType().FullName }
            };
            IConfigurationSource source = new NameValueConfigurationSource(collection);
            var tracerSettings = new TracerSettings(source);
            var tracer = new Tracer(tracerSettings);

            // Create scope
            using (var outerScope = CreateDbCommandScope(tracer, new CustomDbCommand()))
            {
                using (var innerScope = CreateDbCommandScope(tracer, command))
                {
                    Assert.Null(innerScope);
                }

                Assert.NotNull(outerScope);
            }
        }

        [Theory]
        [MemberData(nameof(GetDbCommandScopeData))]
        public void CreateDbCommandScope_UsesReplacementServiceNameWhenProvided(IDbCommand command)
        {
            // Set up tracer
            var t = command.GetType();
            var dbType = ScopeFactory.GetDbType(t.Namespace, t.Name);
            var collection = new NameValueCollection
            {
                { ConfigurationKeys.ServiceNameMappings, $"{dbType}:my-custom-type" }
            };
            IConfigurationSource source = new NameValueConfigurationSource(collection);
            var tracerSettings = new TracerSettings(source);
            var tracer = new Tracer(tracerSettings);

            // Create scope
            using (var outerScope = CreateDbCommandScope(tracer, command))
            {
                Assert.Equal("my-custom-type", outerScope.Span.ServiceName);
            }
        }

        [Theory]
        [MemberData(nameof(GetDbCommandScopeData))]
        public void CreateDbCommandScope_IgnoresReplacementServiceNameWhenNotProvided(IDbCommand command)
        {
            // Set up tracer
            var collection = new NameValueCollection
            {
                { ConfigurationKeys.ServiceNameMappings, $"something:my-custom-type" }
            };
            IConfigurationSource source = new NameValueConfigurationSource(collection);
            var tracerSettings = new TracerSettings(source);
            var tracer = new Tracer(tracerSettings);

            // Create scope
            using (var outerScope = CreateDbCommandScope(tracer, command))
            {
                Assert.NotEqual("my-custom-type", outerScope.Span.ServiceName);
            }
        }

        [Theory]
        [InlineData("System.Data.SqlClient", "SqlCommand", "sql-server")]
        [InlineData("MySql.Data.MySqlClient", "MySqlCommand", "mysql")]
        [InlineData("Npgsql", "NpgsqlCommand", "postgres")]
        [InlineData("", "ProfiledDbCommand", null)]
        [InlineData("", "ExampleCommand", "example")]
        [InlineData("", "Example", "example")]
        [InlineData("", "Command", "command")]
        [InlineData("Custom.DB", "Command", "db")]
        public void GetDbType_CorrectNameGenerated(string namespaceName, string commandTypeName, string expected)
        {
            var dbType = ScopeFactory.GetDbType(namespaceName, commandTypeName);
            Assert.Equal(expected, dbType);
        }

        private static Scope CreateDbCommandScope(Tracer tracer, IDbCommand command)
        {
            return (Scope)typeof(ScopeDBFactory<>)
                .MakeGenericType(command.GetType())
                .GetMethod(nameof(CreateDbCommandScope))
                .Invoke(null, new object[] { tracer, command });
        }

        private class CustomDbCommand : DbCommand
        {
            public override string CommandText { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

            public override int CommandTimeout { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

            public override CommandType CommandType { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

            public override bool DesignTimeVisible { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

            public override UpdateRowSource UpdatedRowSource { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

            protected override DbConnection DbConnection { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

            protected override DbParameterCollection DbParameterCollection => throw new NotImplementedException();

            protected override DbTransaction DbTransaction { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

            public override void Cancel()
            {
                throw new NotImplementedException();
            }

            public override int ExecuteNonQuery()
            {
                throw new NotImplementedException();
            }

            public override object ExecuteScalar()
            {
                throw new NotImplementedException();
            }

            public override void Prepare()
            {
                throw new NotImplementedException();
            }

            protected override DbParameter CreateDbParameter()
            {
                throw new NotImplementedException();
            }

            protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
            {
                throw new NotImplementedException();
            }
        }
    }
}
