// <copyright file="DatabaseMonitoringPropagatorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Data;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.DatabaseMonitoring;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Sampling;
using Datadog.Trace.Tagging;
using Datadog.Trace.TestHelpers.TestTracer;
using FluentAssertions;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Tests.DatabaseMonitoring
{
    public class DatabaseMonitoringPropagatorTests : IClassFixture<ScopedTracerFixture>
    {
        private readonly Tracer _v0Tracer;
        private readonly Tracer _v1Tracer;
        private readonly Mock<IAgentWriter> _writerMock;

        public DatabaseMonitoringPropagatorTests(ITestOutputHelper output, ScopedTracerFixture scopedTracerFixture)
        {
            var v0Settings = new TracerSettings();
            _writerMock = new Mock<IAgentWriter>();
            var samplerMock = new Mock<ITraceSampler>();

            _v0Tracer = scopedTracerFixture.BuildScopedTracer(v0Settings, _writerMock.Object, samplerMock.Object, scopeManager: null, statsd: null);

            var v1Settings = TracerSettings.Create(
                new() { { ConfigurationKeys.MetadataSchemaVersion, SchemaVersion.V1.ToString() } });

            _v1Tracer = scopedTracerFixture.BuildScopedTracer(v1Settings, _writerMock.Object, samplerMock.Object, scopeManager: null, statsd: null);
        }

        [Theory]
        [InlineData("string100", SamplingPriorityValues.UserKeep, "npgsql", "", "", false)]
        [InlineData("full", SamplingPriorityValues.UserKeep, "sqlite", "", "", false)]
        [InlineData("disabled", SamplingPriorityValues.UserKeep, "sqlclient", "", "", false)]
        [InlineData("Service", SamplingPriorityValues.AutoReject, "npgsql", "Test.Service-postgres", "/*dddbs='Test.Service-postgres',ddps='Test.Service',dddb='MyDatabase',ddh='MyHost'*/", false)]
        [InlineData("full", SamplingPriorityValues.UserReject, "sqlclient", "Test.Service-sql-server", "/*dddbs='Test.Service-sql-server',ddps='Test.Service',dddb='MyDatabase',ddh='MyHost'*/", false)]
        [InlineData("full", SamplingPriorityValues.UserReject, "oracle", "Test.Service-oracle", "/*dddbs='Test.Service-oracle',ddps='Test.Service',dddb='MyDatabase',ddh='MyHost'*/", false)]
        [InlineData("fUlL", SamplingPriorityValues.AutoKeep, "mysql", "Test.Service-mysql", "/*dddbs='Test.Service-mysql',ddps='Test.Service',dddb='MyDatabase',ddh='MyHost',traceparent='00-00000000000000006172c1c9a829c71c-05a5f7b5320d6e4d-01'*/", true)]
        public void ExpectedCommentInjected(string propagationMode, int? samplingPriority, string integration, string dbServiceName, string expectedComment, bool traceParentInjected)
        {
            DbmPropagationLevel dbmPropagationLevel;
            Enum.TryParse(propagationMode, true, out dbmPropagationLevel);

            IntegrationId integrationId;
            Enum.TryParse(integration, true, out integrationId);

            var span = _v0Tracer.StartSpan(operationName: "db.query", parent: SpanContext.None, serviceName: dbServiceName, traceId: (TraceId)7021887840877922076, spanId: 407003698947780173);
            span.SetTraceSamplingPriority((SamplingPriority)samplingPriority.Value);

            var initialCommandText = "select * from table";
            var command = CreateCommand(initialCommandText);

            var traceParentInjectedValue = DatabaseMonitoringPropagator.PropagateDataViaComment(dbmPropagationLevel, integrationId, command, "Test.Service", "MyDatabase", "MyHost", span, injectStoredProcedure: true);

            traceParentInjectedValue.Should().Be(traceParentInjected);
            command.CommandText.Should().StartWith(expectedComment);
            command.CommandText.Should().EndWith(initialCommandText);
        }

        [Theory]
        [InlineData("/*dddbs='Test.Service-mysql',dde='testing',ddps='Test.Service',dddb='MyDatabase',ddh='MyHost',ddpv='1.0.0'*/", "testing", "1.0.0")]
        [InlineData("/*dddbs='Test.Service-mysql',ddps='Test.Service',dddb='MyDatabase',ddh='MyHost',ddpv='1.0.0'*/", null, "1.0.0")]
        [InlineData("/*dddbs='Test.Service-mysql',dde='testing',ddps='Test.Service',dddb='MyDatabase',ddh='MyHost'*/", "testing", null)]
        [InlineData("/*dddbs='Test.Service-mysql',ddps='Test.Service',dddb='MyDatabase',ddh='MyHost'*/", null, null)]
        public void ExpectedTagsInjected(string expectedComment, string env = null, string version = null)
        {
            var span = _v0Tracer.StartSpan(operationName: "db.query", parent: SpanContext.None, serviceName: "Test.Service-mysql", traceId: (TraceId)7021887840877922076, spanId: 407003698947780173);
            span.Context.TraceContext.Environment = env;
            span.Context.TraceContext.ServiceVersion = version;
            span.SetTraceSamplingPriority(SamplingPriority.AutoKeep);

            var initialCommandText = "select * from table";
            var command = CreateCommand(initialCommandText);

            var traceParentInjected = DatabaseMonitoringPropagator.PropagateDataViaComment(DbmPropagationLevel.Service, IntegrationId.MySql, command, "Test.Service", "MyDatabase", "MyHost", span, injectStoredProcedure: true);

            // Always false since this test never runs for full mode
            traceParentInjected.Should().Be(false);
            command.CommandText.Should().StartWith(expectedComment);
            command.CommandText.Should().EndWith(initialCommandText);
        }

        [Theory]
        [InlineData("/*dddbs='Test.Service%20%21%23%24%25%26%27%28%29%2A%2B%2C%2F%3A%3B%3D%3F%40%5B%5D-mysql',dde='testing',ddps='Test.Service%20%21%23%24%25%26%27%28%29%2A%2B%2C%2F%3A%3B%3D%3F%40%5B%5D',dddb='My.Database%20%21%23%24%25%26%27%28%29%2A%2B%2C%2F%3A%3B%3D%3F%40%5B%5D',ddh='My.Host%20%21%23%24%25%26%27%28%29%2A%2B%2C%2F%3A%3B%3D%3F%40%5B%5D',ddpv='1.0.0'*/", "Test.Service !#$%&'()*+,/:;=?@[]", "My.Database !#$%&'()*+,/:;=?@[]", "My.Host !#$%&'()*+,/:;=?@[]", "testing", "1.0.0")]
        [InlineData("/*dddbs='Test.Service-mysql',dde='te%23%27sti%2F%2Ang',ddps='Test.Service',ddpv='1.0.0'*/", "Test.Service", null, null, "te#'sti/*ng", "1.0.0")]
        [InlineData("/*dddbs='Test.Service-mysql',dde='testing',ddps='Test.Service',ddpv='1.%2A0.0'*/", "Test.Service", "", "", "testing", "1.*0.0")]
        [InlineData("/*dddbs='Test.Service%20%21%23%24%25%26%27%28%29%2A%2B%2C%2F%3A%3B%3D%3F%40%5B%5D-mysql',dde='te%23%27sti%2F%2Ang',ddps='Test.Service%20%21%23%24%25%26%27%28%29%2A%2B%2C%2F%3A%3B%3D%3F%40%5B%5D',dddb='My_Database',ddh='192.168.0.1',ddpv='1.%2A0.0'*/", "Test.Service !#$%&'()*+,/:;=?@[]", "My_Database", "192.168.0.1", "te#'sti/*ng", "1.*0.0")]
        public void ExpectedTagsEncoded(string expectedComment, string service, string dbName, string host, string env, string version)
        {
            var span = _v0Tracer.StartSpan(operationName: "db.query", parent: SpanContext.None, serviceName: $"{service}-mysql", traceId: (TraceId)7021887840877922076, spanId: 407003698947780173);
            span.Context.TraceContext.Environment = env;
            span.Context.TraceContext.ServiceVersion = version;
            span.SetTraceSamplingPriority(SamplingPriority.AutoKeep);

            var initialCommandText = "select * from table";
            var command = CreateCommand(initialCommandText);

            var traceParentInjected = DatabaseMonitoringPropagator.PropagateDataViaComment(DbmPropagationLevel.Service, IntegrationId.MySql, command, service, dbName, host, span, injectStoredProcedure: true);

            // Always false since this test never runs for full mode
            traceParentInjected.Should().Be(false);
            command.CommandText.Should().StartWith(expectedComment);
            command.CommandText.Should().EndWith(initialCommandText);
        }

        [Fact]
        public void ExpectedCommentInjectedV1()
        {
            var dbmPropagationLevel = DbmPropagationLevel.Service;
            var integrationId = IntegrationId.Npgsql;
            var samplingPriority = SamplingPriority.AutoReject;
            var dbServiceName = "Test.Service-postgres";
            var expectedComment = $"/*dddbs='{dbServiceName}',ddps='Test.Service',dddb='MyDatabase',ddh='MyHost'*/";
            var traceParentInjected = false;
            var dbName = "dbname";
            var initialCommandText = "select * from table";
            var command = CreateCommand(initialCommandText);

            var span = _v1Tracer.StartSpan(tags: new SqlV1Tags() { DbName = dbName }, operationName: "db.query", parent: SpanContext.None, serviceName: dbServiceName, traceId: (TraceId)7021887840877922076, spanId: 407003698947780173);
            span.SetTraceSamplingPriority(samplingPriority);

            var traceParentInjectedValue = DatabaseMonitoringPropagator.PropagateDataViaComment(dbmPropagationLevel, integrationId, command, "Test.Service", "MyDatabase", "MyHost", span, injectStoredProcedure: true);

            traceParentInjectedValue.Should().Be(traceParentInjected);
            command.CommandText.Should().StartWith(expectedComment);
            command.CommandText.Should().EndWith(initialCommandText);
        }

        [Fact]
        public void ExpectedCommentAppendedV1()
        {
            var dbmPropagationLevel = DbmPropagationLevel.Service;
            var integrationId = IntegrationId.Npgsql;
            var samplingPriority = SamplingPriority.AutoReject;
            var dbServiceName = "Test.Service-postgres";
            var expectedComment = $"/*dddbs='{dbServiceName}',ddps='Test.Service',dddb='MyDatabase',ddh='MyHost'*/";
            var traceParentInjected = false;
            var dbName = "dbname";
            var initialCommandText = "/*+ this is a hint */ select * from table";
            var command = CreateCommand(initialCommandText);

            var span = _v1Tracer.StartSpan(tags: new SqlV1Tags() { DbName = dbName }, operationName: "db.query", parent: SpanContext.None, serviceName: dbServiceName, traceId: (TraceId)7021887840877922076, spanId: 407003698947780173);
            span.SetTraceSamplingPriority(samplingPriority);

            var traceParentInjectedValue = DatabaseMonitoringPropagator.PropagateDataViaComment(dbmPropagationLevel, integrationId, command, "Test.Service", "MyDatabase", "MyHost", span, injectStoredProcedure: true);

            traceParentInjectedValue.Should().Be(traceParentInjected);
            command.CommandText.Should().EndWith(expectedComment);
            command.CommandText.Should().StartWith(initialCommandText);
        }

        [Theory]
        [InlineData(SchemaVersion.V0)]
        [InlineData(SchemaVersion.V1)]
        internal void PeerServiceInjected(SchemaVersion version)
        {
            var dbmPropagationLevel = DbmPropagationLevel.Service;
            var traceParentInjected = false;
            var peerService = "myPeerService";
            var initialCommandText = "select * from table";
            var command = CreateCommand(initialCommandText);

            var sqlTags = version == SchemaVersion.V1 ? new SqlV1Tags() : new SqlTags();
            sqlTags.SetTag(Tags.PeerServiceRemappedFrom, "old_value");
            sqlTags.SetTag(Tags.PeerService, peerService);

            var tracer = version == SchemaVersion.V1 ? _v1Tracer : _v0Tracer;
            var span = tracer.StartSpan("db.query", sqlTags, serviceName: "myServiceName");

            var traceParentInjectedValue = DatabaseMonitoringPropagator.PropagateDataViaComment(dbmPropagationLevel, IntegrationId.Npgsql, command, "Test.Service", "MyDatabase", "MyHost", span, injectStoredProcedure: true);

            traceParentInjectedValue.Should().Be(traceParentInjected);
            command.CommandText.Should().StartWith("/*dddbs='myServiceName',ddprs='myPeerService',ddps='Test.Service',dddb='MyDatabase',ddh='MyHost'*/");
            command.CommandText.Should().EndWith(initialCommandText);
        }

        [Theory]
        [InlineData("full", "sqlclient", SamplingPriorityValues.UserKeep, true, "01BEEFBEEFBEEFBEEFBABEBABEBABEBABECAFECAFECAFECAFE")]
        [InlineData("full", "sqlclient", SamplingPriorityValues.UserReject, true, "00BEEFBEEFBEEFBEEFBABEBABEBABEBABECAFECAFECAFECAFE")]
        [InlineData("nope", "sqlclient", SamplingPriorityValues.UserKeep, false, null)]
        // disabled for all db types except sqlclient for now
        [InlineData("full", "npgsql", SamplingPriorityValues.UserKeep, false, null)]
        [InlineData("full", "sqlite", SamplingPriorityValues.UserKeep, false, null)]
        [InlineData("full", "oracle", SamplingPriorityValues.UserKeep, false, null)]
        [InlineData("full", "mysql", SamplingPriorityValues.UserKeep, false, null)]
        internal void ExpectedContextSet(string propagationMode, string integration, int samplingPriority, bool shouldInject, string expectedContext)
        {
            Enum.TryParse(propagationMode, ignoreCase: true, out DbmPropagationLevel dbmPropagationLevel);
            Enum.TryParse(integration, ignoreCase: true, out IntegrationId integrationId);

            // capture command and parameter sent
            string sql = null;
            byte[] context = null;
            var connectionMock = new Mock<IDbConnection>(MockBehavior.Strict);
            var commandMock = new Mock<IDbCommand>();
            var parameterMock = new Mock<IDbDataParameter>();
            connectionMock.Setup(c => c.CreateCommand()).Returns(commandMock.Object);
            connectionMock.SetupGet(c => c.State).Returns(ConnectionState.Open);
            commandMock.SetupSet(c => c.CommandText = It.IsAny<string>())
                       .Callback<string>(value => sql = value);
            commandMock.Setup(c => c.CreateParameter()).Returns(parameterMock.Object);
            commandMock.SetupGet(c => c.Parameters).Returns(Mock.Of<IDataParameterCollection>());
            commandMock.SetupGet(c => c.Connection).Returns(connectionMock.Object);
            parameterMock.SetupSet(p => p.Value = It.IsAny<byte[]>())
                         .Callback<object>(value => context = (byte[])value);

            foreach (var tracer in new[] { _v0Tracer, _v1Tracer })
            {
                var span = tracer.StartSpan("db.query", parent: SpanContext.None, serviceName: "pouet", traceId: new TraceId(Upper: 0xBABEBABEBABEBABE, Lower: 0xCAFECAFECAFECAFE), spanId: 0xBEEFBEEFBEEFBEEF);
                span.Context.TraceContext.SetSamplingPriority(samplingPriority);

                DatabaseMonitoringPropagator.PropagateDataViaContext(dbmPropagationLevel, integrationId, commandMock.Object, span);

                if (shouldInject)
                {
                    sql.Should().StartWith("set context_info ");
                    BitConverter.ToString(context).Replace("-", string.Empty).Should().Be(expectedContext);
                }
                else
                {
                    sql.Should().BeNull();
                    context.Should().BeNull();
                }
            }
        }

        [Theory]
        [InlineData(true, IntegrationId.Npgsql, "/*+ HashJoin(t1 t1) */ EXPLAIN SELECT * FROM s1.t1 JOIN public.t1 ON (s1.t1.id=public.t1.id);")]
        // only for PG, not the others
        [InlineData(false, IntegrationId.MySql, "/*+ HashJoin(t1 t1) */ EXPLAIN SELECT * FROM s1.t1 JOIN public.t1 ON (s1.t1.id=public.t1.id);")]
        [InlineData(false, IntegrationId.Oracle, "/*+ HashJoin(t1 t1) */ EXPLAIN SELECT * FROM s1.t1 JOIN public.t1 ON (s1.t1.id=public.t1.id);")]
        [InlineData(false, IntegrationId.SqlClient, "/*+ HashJoin(t1 t1) */ EXPLAIN SELECT * FROM s1.t1 JOIN public.t1 ON (s1.t1.id=public.t1.id);")]
        // leading whitespace is ignored
        [InlineData(true, IntegrationId.Npgsql, " \n\t \u00A0\r /*+ HashJoin(t1 t1) */ EXPLAIN SELECT * FROM s1.t1 JOIN public.t1 ON (s1.t1.id=public.t1.id);")]
        // some other cases
        [InlineData(false, IntegrationId.Npgsql, "/* HashJoin(t1 t1) */ EXPLAIN SELECT * FROM s1.t1 JOIN public.t1 ON (s1.t1.id=public.t1.id);")]
        [InlineData(false, IntegrationId.Npgsql, "/*")]
        [InlineData(false, IntegrationId.Npgsql, "")]
        internal void ShouldAppendComment(bool expected, IntegrationId integrationId, string commandText)
        {
            DatabaseMonitoringPropagator.ShouldAppend(integrationId, commandText).Should().Be(expected);
        }

        private IDbCommand CreateCommand(string commandText)
        {
            var commandMock = new Mock<IDbCommand>();
            // allow properties to be used "normally" (value set is returned on get)
            commandMock.SetupAllProperties();
            commandMock.Object.CommandText = commandText;
            return commandMock.Object;
        }
    }
}
