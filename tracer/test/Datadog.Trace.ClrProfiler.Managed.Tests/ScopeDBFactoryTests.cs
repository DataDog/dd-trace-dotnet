// <copyright file="ScopeDBFactoryTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
using System.Data.Common;
using Datadog.Trace.Configuration;
using MySql.Data.MySqlClient;
using Npgsql;
using NUnit.Framework;

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

        [TestCaseSource(nameof(GetDbCommandScopeData))]
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

        [TestCaseSource(nameof(GetDbCommandScopeData))]
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
                Assert.AreEqual("my-custom-type", outerScope.Span.ServiceName);
            }
        }

        [TestCaseSource(nameof(GetDbCommandScopeData))]
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
                Assert.AreNotEqual("my-custom-type", outerScope.Span.ServiceName);
            }
        }

        [TestCase("System.Data.SqlClient", "SqlCommand", "sql-server")]
        [TestCase("MySql.Data.MySqlClient", "MySqlCommand", "mysql")]
        [TestCase("Npgsql", "NpgsqlCommand", "postgres")]
        [TestCase("", "ProfiledDbCommand", null)]
        [TestCase("", "ExampleCommand", "example")]
        [TestCase("", "Example", "example")]
        [TestCase("", "Command", "command")]
        [TestCase("Custom.DB", "Command", "db")]
        public void GetDbType_CorrectNameGenerated(string namespaceName, string commandTypeName, string expected)
        {
            var dbType = ScopeFactory.GetDbType(namespaceName, commandTypeName);
            Assert.AreEqual(expected, dbType);
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
